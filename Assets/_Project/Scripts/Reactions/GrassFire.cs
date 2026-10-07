using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Output;
using SpellSimulator.World.Map;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Incendio del pasto (SYSTEMS_DESIGN 5, "Suelo"). El campo es una grilla de celdas de 2 m sobre [-70,70]^2
    /// (70x70 = 4900 celdas). Cada celda es pasto (0), ardiendo (1), quemada (2) o no inflamable (3: lago, rio, camino
    /// o fuera de PlayableRadius+4). El fuego se enciende con las marcas de suelo de fuego, con los hechizos de fuego
    /// cerca del piso y con los objetos que arden; se propaga a los 8 vecinos con probabilidad spreadChance cada 0.5 s;
    /// cada celda arde burnSeconds y queda quemada. El agua apaga las celdas que arden (no revive las quemadas).
    /// Logica a pasos fijos de 0.25 s (sin Coroutines). Dibujo: quads quemados con DrawMeshInstanced (las tandas se
    /// reconstruyen solo cuando cambia el conjunto) y un pool de llamas (ParticleSystem de BurnEffect) que se reasigna
    /// cada 0.5 s a las celdas ardiendo mas cercanas a la camara.
    /// Sin allocations por frame: buffers preasignados en Awake y RNG xorshift propio.
    /// </summary>
    public class GrassFire : MonoBehaviour
    {
        // ---------------------------------------------------------------- grilla

        private const int GridSize = 70;
        private const float CellSize = 2f;
        /// <summary>Semilado de la grilla (70 m): la grilla cubre [-70,70] en X y Z.</summary>
        private const float HalfSpan = GridSize * CellSize * 0.5f;
        private const float GridOrigin = -HalfSpan;
        private const int Cells = GridSize * GridSize;

        // Estados de celda (byte para que el arreglo sea compacto).
        private const byte Grass = 0;
        private const byte Burning = 1;
        private const byte Burnt = 2;
        private const byte Fireproof = 3;

        // ---------------------------------------------------------------- tiempos y limites

        /// <summary>Paso fijo de la logica (s).</summary>
        private const float LogicStep = 0.25f;
        /// <summary>Pasos maximos por frame: si el juego va lento, el fuego avanza mas lento (no en rafagas).</summary>
        private const int MaxStepsPerFrame = 4;
        /// <summary>Radio de ignicion de una marca de fuego (m).</summary>
        private const float IgniteMarkRadius = 1.5f;
        /// <summary>Radio de apagado de una marca de agua (m).</summary>
        private const float WaterMarkRadius = 3f;
        /// <summary>Un hechizo de fuego enciende el pasto solo si su origen esta a menos de esta altura del suelo (m).</summary>
        private const float FireSpellHeight = 2.5f;
        /// <summary>Maximo de celdas que enciende cada hechizo de fuego por revision.</summary>
        private const int FieldFireBudget = 25;
        /// <summary>Un objeto ardiendo enciende el pasto si esta a menos de esta altura del suelo (m).</summary>
        private const float ReactiveFireHeight = 1.5f;
        /// <summary>Celdas ardiendo que dañan objetos por pasada (round-robin).</summary>
        private const int DamageCellsPerPass = 16;
        private const float DamageRadius = 1.4f;
        /// <summary>Altura del centro del chequeo de dano sobre el suelo (m).</summary>
        private const float DamageHeight = 0.5f;
        private const int MaxOverlaps = 32;
        private const int BatchSize = 1023;
        private const float BurnLift = 0.05f;
        private const float FlameLift = 0.05f;

        [SerializeField, Tooltip("Probabilidad, por vecino y cada 0.5 s, de que una celda ardiendo encienda el pasto de al lado.")]
        private float spreadChance = 0.03f;
        [SerializeField, Tooltip("Segundos que arde una celda antes de quedar quemada.")]
        private float burnSeconds = 7f;
        [SerializeField, Tooltip("Calor por segundo que recibe lo que esta sobre el fuego. ElementReactive lo consume en el siguiente tick de MagicWorld (15 Hz), asi que cada pasada aporta poco: valor chico a proposito.")]
        private float heatPerSecond = 9f;
        [SerializeField, Tooltip("Material de las celdas quemadas (debe tener enableInstancing).")]
        private Material burnMaterial;
        [SerializeField, Tooltip("Look de las llamas (de aca salen material y gravedad).")]
        private ElementLook fireLook;
        [SerializeField, Tooltip("Cantidad maxima de llamas simultaneas (el pool crece bajo demanda hasta este numero).")]
        private int maxFlames = 36;

        // ---------------------------------------------------------------- estado (preasignado en Awake)

        private byte[] state;
        /// <summary>Estado inicial de cada celda (pasto o no inflamable): lo que vuelve en el reinicio.</summary>
        private byte[] baseState;
        /// <summary>Segundos de combustion que le quedan a cada celda ardiendo.</summary>
        private float[] timers;
        /// <summary>Altura del suelo en el centro de cada celda.</summary>
        private float[] groundY;

        /// <summary>Lista de celdas ardiendo (con entradas viejas hasta la siguiente compactacion).</summary>
        private int[] burning;
        private int listCount;
        /// <summary>La celda esta en la lista (evita duplicados si se apaga y se vuelve a encender antes de compactar).</summary>
        private bool[] inList;
        /// <summary>Contador real de celdas que arden (la lista puede tener entradas viejas).</summary>
        private int burningCells;
        private int burntCells;
        private int damageCursor;
        private int stepCount;
        private float accumulator;
        private uint rng = 0x2545F491u;

        // Llamas: pool de ParticleSystem; cellFlame[c] = slot o -1; flameCell[slot] = celda o -1.
        private ParticleSystem[] flameSystems;
        private int[] flameCell;
        private int[] cellFlame;
        private int[] candCell;
        private float[] candDist;
        private Camera mainCamera;

        // Dibujo instanciado de celdas quemadas.
        private Mesh quad;
        private Matrix4x4[][] burntBatches;
        private int burntTotal;
        private bool burntDirty;

        private static readonly Collider[] overlaps = new Collider[MaxOverlaps];
        private static readonly ElementReactive[] hitReactives = new ElementReactive[MaxOverlaps];

        /// <summary>Celdas que estan ardiendo ahora.</summary>
        public int BurningCells => burningCells;
        /// <summary>Celdas ya quemadas (no vuelven a arder hasta el reinicio).</summary>
        public int BurntCells => burntCells;

        // ---------------------------------------------------------------- ciclo de vida

        private void Awake()
        {
            state = new byte[Cells];
            baseState = new byte[Cells];
            timers = new float[Cells];
            groundY = new float[Cells];
            inList = new bool[Cells];
            burning = new int[Cells];
            cellFlame = new int[Cells];
            candCell = new int[Cells];
            candDist = new float[Cells];

            flameSystems = new ParticleSystem[Mathf.Max(maxFlames, 0)];
            flameCell = new int[flameSystems.Length];
            burntBatches = new Matrix4x4[(Cells + BatchSize - 1) / BatchSize][];
            for (int b = 0; b < burntBatches.Length; b++) burntBatches[b] = new Matrix4x4[BatchSize];

            for (int c = 0; c < Cells; c++)
            {
                float x = CellCenterX(c % GridSize);
                float z = CellCenterZ(c / GridSize);
                groundY[c] = ValleyShape.Height(x, z);
                baseState[c] = IsFireproof(x, z) ? Fireproof : Grass;
                state[c] = baseState[c];
                cellFlame[c] = -1;
            }
            for (int s = 0; s < flameCell.Length; s++) flameCell[s] = -1;

            quad = BuildQuad();
            if (burnMaterial != null) burnMaterial.enableInstancing = true;
            burntDirty = true;
        }

        private void OnEnable()
        {
            GroundMarks.Marked += OnMarked;
            MagicWorld.WorldReset += ResetAll;
        }

        private void OnDisable()
        {
            GroundMarks.Marked -= OnMarked;
            MagicWorld.WorldReset -= ResetAll;
        }

        private void OnDestroy()
        {
            if (quad != null) Destroy(quad);
        }

        private void Update()
        {
            accumulator += Time.deltaTime;
            int steps = 0;
            while (accumulator >= LogicStep)
            {
                if (steps++ >= MaxStepsPerFrame) { accumulator = 0f; break; }
                accumulator -= LogicStep;
                Step();
            }

            if (burntDirty) RebuildBurnt();
            if (burntTotal > 0 && burnMaterial != null) DrawBurnt();
        }

        // ---------------------------------------------------------------- API publica

        /// <summary>Enciende la celda que contiene el punto y las celdas cuyo centro esta dentro del radio (m).</summary>
        public void Ignite(Vector3 worldPoint, float radius)
        {
            IgniteArea(worldPoint, radius, int.MaxValue);
        }

        /// <summary>Reinicio: el pasto vuelve a estar sano (las no inflamables siguen asi), sin llamas.</summary>
        public void ResetAll()
        {
            for (int r = 0; r < listCount; r++) inList[burning[r]] = false;
            listCount = 0;
            for (int c = 0; c < Cells; c++)
            {
                state[c] = baseState[c];
                timers[c] = 0f;
                cellFlame[c] = -1;
            }
            for (int slot = 0; slot < flameSystems.Length; slot++)
            {
                flameCell[slot] = -1;
                if (flameSystems[slot] != null) flameSystems[slot].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            burningCells = 0;
            burntCells = 0;
            burntDirty = true;
            damageCursor = 0;
            stepCount = 0;
            accumulator = 0f;
        }

        // ---------------------------------------------------------------- eventos

        private void OnMarked(ElementType element, Vector3 point)
        {
            if (element == ElementType.Fire) IgniteArea(point, IgniteMarkRadius, int.MaxValue);
            else if (element == ElementType.Water) ExtinguishArea(point, WaterMarkRadius);
        }

        // ---------------------------------------------------------------- logica

        private void Step()
        {
            SampleIgnitionSources();
            BurnStep();
            // Pares: propagacion y llamas (0.5 s). Impares: dano a objetos (0.5 s), desfasado.
            if ((stepCount++ & 1) == 0)
            {
                SpreadStep();
                AssignFlames();
            }
            else
            {
                DamageStep();
            }
        }

        /// <summary>Hechizos de fuego/agua y objetos ardiendo que tocan el suelo (0.25 s).</summary>
        private void SampleIgnitionSources()
        {
            var fields = MagicField.Active;
            for (int f = 0; f < fields.Count; f++)
            {
                var field = fields[f];
                Vector3 origin = field.Frame.Origin;
                if (field.Element == ElementType.Fire)
                {
                    if (origin.y - ValleyShape.Height(origin.x, origin.z) < FireSpellHeight)
                        IgniteArea(origin, field.ReachRadius * 0.5f, FieldFireBudget);
                }
                else if (field.Element == ElementType.Water)
                {
                    ExtinguishArea(origin, field.ReachRadius);
                }
            }

            var all = ElementReactive.All;
            for (int i = 0; i < all.Count; i++)
            {
                var reactive = all[i];
                if (reactive == null || !reactive.isActiveAndEnabled || !reactive.State.Burning) continue;
                Vector3 p = reactive.WorldBounds.center;
                if (InGrid(p) && p.y - ValleyShape.Height(p.x, p.z) < ReactiveFireHeight) TryIgnite(CellOf(p));
            }
        }

        /// <summary>
        /// Avanza el tiempo de las celdas ardiendo, quema las que se agotan y compacta la lista quitando las apagadas
        /// por agua o quemadas (la compactacion se hace al recorrerla, sin buffers extra).
        /// </summary>
        private void BurnStep()
        {
            int kept = 0;
            for (int r = 0; r < listCount; r++)
            {
                int c = burning[r];
                if (state[c] != Burning)
                {
                    inList[c] = false;
                    continue;
                }
                timers[c] -= LogicStep;
                if (timers[c] > 0f)
                {
                    burning[kept++] = c;
                    continue;
                }
                state[c] = Burnt;
                timers[c] = 0f;
                inList[c] = false;
                burningCells--;
                burntCells++;
                burntDirty = true;
                ReleaseFlame(c);
            }
            listCount = kept;
        }

        /// <summary>Cada celda ardiendo intenta encender a sus 8 vecinos con pasto. Solo recorre la lista de ardientes.</summary>
        private void SpreadStep()
        {
            // Snapshot: las celdas nuevas se agregan al final y arden desde la proxima pasada.
            int count = listCount;
            for (int r = 0; r < count; r++)
            {
                int c = burning[r];
                if (state[c] != Burning) continue;
                int cx = c % GridSize;
                int cz = c / GridSize;
                for (int dz = -1; dz <= 1; dz++)
                {
                    int nz = cz + dz;
                    if (nz < 0 || nz >= GridSize) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx;
                        if ((dx == 0 && dz == 0) || nx < 0 || nx >= GridSize) continue;
                        int n = nz * GridSize + nx;
                        if (state[n] != Grass) continue;
                        if (NextUnit() < spreadChance) TryIgnite(n);
                    }
                }
            }
        }

        /// <summary>Hace dano a los objetos cerca de hasta DamageCellsPerPass celdas ardiendo, en round-robin.</summary>
        private void DamageStep()
        {
            if (listCount == 0) return;
            int count = Mathf.Min(DamageCellsPerPass, listCount);
            for (int k = 0; k < count; k++)
            {
                int c = burning[(damageCursor + k) % listCount];
                if (state[c] == Burning) DamageAround(c);
            }
            damageCursor = (damageCursor + count) % listCount;
        }

        private void DamageAround(int c)
        {
            var center = new Vector3(CellCenterX(c % GridSize), groundY[c] + DamageHeight, CellCenterZ(c / GridSize));
            int n = Physics.OverlapSphereNonAlloc(center, DamageRadius, overlaps, ~0, QueryTriggerInteraction.Collide);
            int unique = 0;
            for (int i = 0; i < n; i++)
            {
                var reactive = overlaps[i].GetComponentInParent<ElementReactive>();
                if (reactive == null || !reactive.isActiveAndEnabled) continue;
                bool seen = false;
                for (int j = 0; j < unique; j++) if (hitReactives[j] == reactive) { seen = true; break; }
                if (!seen) hitReactives[unique++] = reactive;
            }
            // Heat por segundo: ElementReactive lo aplica una vez, en el proximo tick de MagicWorld.
            var exposure = new Exposure { Heat = heatPerSecond };
            for (int j = 0; j < unique; j++) hitReactives[j].AddExposure(exposure, 1f);
        }

        // ---------------------------------------------------------------- celdas

        private bool TryIgnite(int c)
        {
            if (state[c] != Grass) return false;
            state[c] = Burning;
            timers[c] = burnSeconds;
            burningCells++;
            if (!inList[c])
            {
                inList[c] = true;
                burning[listCount++] = c;
            }
            return true;
        }

        private bool Extinguish(int c)
        {
            if (state[c] != Burning) return false;
            // Vuelve a pasto (no a quemada). La entrada en la lista queda para la compactacion.
            state[c] = Grass;
            timers[c] = 0f;
            burningCells--;
            ReleaseFlame(c);
            return true;
        }

        /// <summary>Enciende hasta budget celdas dentro del radio (la del punto siempre que este en la grilla).</summary>
        private int IgniteArea(Vector3 p, float radius, int budget)
        {
            int own = InGrid(p) ? CellOf(p) : -1;
            CellRange(p, radius, out int x0, out int x1, out int z0, out int z1);
            float r2 = radius * radius;
            int done = 0;
            for (int cz = z0; cz <= z1; cz++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int c = cz * GridSize + cx;
                    if (c != own && CenterDistanceSq(cx, cz, p) > r2) continue;
                    if (TryIgnite(c) && ++done >= budget) return done;
                }
            }
            return done;
        }

        /// <summary>Apaga las celdas ardiendo dentro del radio: vuelven a pasto, no a quemadas.</summary>
        private void ExtinguishArea(Vector3 p, float radius)
        {
            int own = InGrid(p) ? CellOf(p) : -1;
            CellRange(p, radius, out int x0, out int x1, out int z0, out int z1);
            float r2 = radius * radius;
            for (int cz = z0; cz <= z1; cz++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int c = cz * GridSize + cx;
                    if (c != own && CenterDistanceSq(cx, cz, p) > r2) continue;
                    Extinguish(c);
                }
            }
        }

        private static void CellRange(Vector3 p, float radius, out int x0, out int x1, out int z0, out int z1)
        {
            x0 = CellX(p.x - radius);
            x1 = CellX(p.x + radius);
            z0 = CellZ(p.z - radius);
            z1 = CellZ(p.z + radius);
        }

        private static float CenterDistanceSq(int cx, int cz, Vector3 p)
        {
            float dx = CellCenterX(cx) - p.x;
            float dz = CellCenterZ(cz) - p.z;
            return dx * dx + dz * dz;
        }

        private static bool InGrid(Vector3 p) => Mathf.Abs(p.x) <= HalfSpan && Mathf.Abs(p.z) <= HalfSpan;

        private static int CellOf(Vector3 p) => CellZ(p.z) * GridSize + CellX(p.x);

        private static int CellX(float x) => Mathf.Clamp(Mathf.FloorToInt((x - GridOrigin) / CellSize), 0, GridSize - 1);

        private static int CellZ(float z) => Mathf.Clamp(Mathf.FloorToInt((z - GridOrigin) / CellSize), 0, GridSize - 1);

        private static float CellCenterX(int cx) => GridOrigin + (cx + 0.5f) * CellSize;

        private static float CellCenterZ(int cz) => GridOrigin + (cz + 0.5f) * CellSize;

        /// <summary>No inflamable: lago, rio, camino o fuera del area jugable (+4 m).</summary>
        private static bool IsFireproof(float x, float z)
        {
            if (new Vector2(x, z).magnitude > ValleyShape.PlayableRadius + 4f) return true;
            if (ValleyShape.LakeFactor(x, z) < 1f) return true;
            if (ValleyShape.RiverDistance(x, z) < ValleyShape.RiverHalfWidth) return true;
            return ValleyShape.PathDistance(x, z) < ValleyShape.PathWidth;
        }

        /// <summary>Xorshift32 propio: sin allocations y reproducible.</summary>
        private float NextUnit()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return (rng & 0xFFFFFFu) * (1f / 16777216f);
        }

        // ---------------------------------------------------------------- llamas

        /// <summary>Asigna las llamas libres a las celdas ardiendo mas cercanas a la camara principal.</summary>
        private void AssignFlames()
        {
            if (fireLook == null || burningCells == 0) return;
            if (mainCamera == null) mainCamera = Camera.main;
            Vector3 eye = mainCamera != null ? mainCamera.transform.position : transform.position;

            // Candidatos: celdas ardiendo sin llama, con su distancia (XZ) a la camara.
            int n = 0;
            for (int r = 0; r < listCount; r++)
            {
                int c = burning[r];
                if (state[c] != Burning || cellFlame[c] >= 0) continue;
                float dx = CellCenterX(c % GridSize) - eye.x;
                float dz = CellCenterZ(c / GridSize) - eye.z;
                candCell[n] = c;
                candDist[n] = dx * dx + dz * dz;
                n++;
            }

            for (int slot = 0; slot < flameSystems.Length && n > 0; slot++)
            {
                if (flameCell[slot] >= 0) continue;
                int best = 0;
                for (int k = 1; k < n; k++) if (candDist[k] < candDist[best]) best = k;
                int chosen = candCell[best];
                // Quita el elegido con swap-remove (sin alocar).
                n--;
                candCell[best] = candCell[n];
                candDist[best] = candDist[n];
                PlaceFlame(slot, chosen);
            }
        }

        /// <summary>Crea el ParticleSystem del slot la primera vez (BurnEffect); despues solo lo mueve y lo prende.</summary>
        private void PlaceFlame(int slot, int c)
        {
            if (flameSystems[slot] == null)
                flameSystems[slot] = BurnEffect.Create(transform, new Bounds(transform.position, Vector3.one * CellSize), fireLook);
            var ps = flameSystems[slot];
            ps.Clear(true);
            ps.transform.position = new Vector3(CellCenterX(c % GridSize), groundY[c] + FlameLift, CellCenterZ(c / GridSize));
            ps.Play();
            flameCell[slot] = c;
            cellFlame[c] = slot;
        }

        /// <summary>Detiene la llama de la celda (StopEmitting) y devuelve su slot al pool.</summary>
        private void ReleaseFlame(int c)
        {
            int slot = cellFlame[c];
            if (slot < 0) return;
            cellFlame[c] = -1;
            flameCell[slot] = -1;
            if (flameSystems[slot] != null) flameSystems[slot].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ---------------------------------------------------------------- dibujo

        /// <summary>Reconstruye las tandas de matrices de las celdas quemadas. Solo cuando cambia el conjunto.</summary>
        private void RebuildBurnt()
        {
            burntDirty = false;
            int n = 0;
            for (int c = 0; c < Cells; c++)
            {
                if (state[c] != Burnt) continue;
                var pos = new Vector3(CellCenterX(c % GridSize), groundY[c] + BurnLift, CellCenterZ(c / GridSize));
                burntBatches[n / BatchSize][n % BatchSize] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
                n++;
            }
            burntTotal = n;
        }

        private void DrawBurnt()
        {
            int batches = (burntTotal + BatchSize - 1) / BatchSize;
            for (int b = 0; b < batches; b++)
            {
                int count = Mathf.Min(BatchSize, burntTotal - b * BatchSize);
                Graphics.DrawMeshInstanced(quad, 0, burnMaterial, burntBatches[b], count, null,
                    UnityEngine.Rendering.ShadowCastingMode.Off, false);
            }
        }

        /// <summary>Quad de 2x2 m en el plano XZ (normal hacia arriba), con el origen en el centro de la celda.</summary>
        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "GrassBurntQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-1.6f, 0f, -1.6f), new Vector3(-1.6f, 0f, 1.6f), new Vector3(1.6f, 0f, 1.6f), new Vector3(1.6f, 0f, -1.6f)
            };
            // El shader GroundMark dibuja un disco procedural a partir de las UV.
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
