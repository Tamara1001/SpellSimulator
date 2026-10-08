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
    /// cerca del piso y con los objetos que arden; se propaga a los 8 vecinos cada 0.5 s con probabilidad
    /// spreadChance * generationDecay^g, donde g es la generacion de la celda que propaga (las encendidas por fuente
    /// directa son g = 0 y cada salto suma 1; desde maxGenerations en adelante ya no propagan). Asi el incendio es local
    /// y se apaga solo, salvo que se lo alimente. Cada celda arde burnSeconds y queda quemada. El agua apaga las celdas
    /// que arden (no revive las quemadas).
    /// Logica a pasos fijos de 0.25 s (sin Coroutines). Dibujo: un mapa de quemado global (_GrassBurnTex, 1 texel por
    /// celda: quemado, calor de brasas y altura del suelo) que StylizedLit lee para pintar ceniza organica directamente
    /// sobre el terreno y el pasto (Materials/BurntGround.hlsl; keyword global _GRASS_BURN solo mientras hay algo
    /// quemado). El mapa se sube solo cuando cambia (a lo sumo una vez por frame). Llamas: pool de ParticleSystem bajos
    /// (BurnEffect.CreateGround) que se reasigna cada 0.5 s a las celdas ardiendo mas cercanas a la camara.
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
        private const float IgniteMarkRadius = 0.9f;
        /// <summary>Radio de apagado de una marca de agua (m).</summary>
        private const float WaterMarkRadius = 3f;
        /// <summary>Un hechizo de fuego enciende el pasto solo si su origen esta a menos de esta altura del suelo (m).</summary>
        private const float FireSpellHeight = 2.5f;
        /// <summary>Maximo de celdas que enciende cada hechizo de fuego por revision.</summary>
        private const int FieldFireBudget = 12;
        /// <summary>Radio (fraccion del ReachRadius) dentro del cual un hechizo de fuego enciende celdas.</summary>
        private const float FieldFireRadiusFactor = 0.35f;
        /// <summary>Un objeto ardiendo enciende el pasto si esta a menos de esta altura del suelo (m).</summary>
        private const float ReactiveFireHeight = 1.5f;
        /// <summary>
        /// Un objeto ardiendo enciende la celda bajo el solo si lleva ardiendo mas de esto. Se usa ElementReactive.State.BurnProgress
        /// (avance = tiempo / burnSeconds del perfil): 0.15 equivale a ~2 s cuando el perfil arde ~13 s; con perfiles de otra
        /// duracion el umbral es proporcional, no fijo en segundos.
        /// </summary>
        private const float ReactiveBurnThreshold = 0.15f;
        /// <summary>Tabla precalculada de potencias generationDecay^g para g = 0..DecayTableSize-1 (sin Pow por frame).</summary>
        private const int DecayTableSize = 33;

        // Defaults. Los valores serializados de la escena pisan estos; ApplyDefaults() los restablece.
        private const float DefaultSpreadChance = 0.07f;
        private const float DefaultBurnSeconds = 5f;
        private const float DefaultHeatPerSecond = 5f;
        private const float DefaultGenerationDecay = 0.78f;
        private const int DefaultMaxGenerations = 9;
        /// <summary>Celdas ardiendo que dañan objetos por pasada (round-robin).</summary>
        private const int DamageCellsPerPass = 16;
        private const float DamageRadius = 1.4f;
        /// <summary>Altura del centro del chequeo de dano sobre el suelo (m).</summary>
        private const float DamageHeight = 0.5f;
        private const int MaxOverlaps = 32;
        private const float FlameLift = 0.05f;
        /// <summary>Quemado visible de una celda al encenderse; crece hasta 1 mientras arde.</summary>
        private const float BurningScorchStart = 0.35f;
        /// <summary>Segundos que tardan en apagarse las brasas de una celda ya quemada.</summary>
        private const float EmberSeconds = 7f;

        [SerializeField, Tooltip("Probabilidad base, por vecino y cada 0.5 s, de que una celda ardiendo encienda el pasto de al lado. Se multiplica por generationDecay^g.")]
        private float spreadChance = DefaultSpreadChance;
        [SerializeField, Tooltip("Segundos que arde una celda antes de quedar quemada.")]
        private float burnSeconds = DefaultBurnSeconds;
        [SerializeField, Tooltip("Calor por segundo que recibe lo que esta sobre el fuego. ElementReactive lo consume en el siguiente tick de MagicWorld (15 Hz), asi que cada pasada aporta poco: valor chico a proposito.")]
        private float heatPerSecond = DefaultHeatPerSecond;
        [SerializeField, Range(0.01f, 1f), Tooltip("Factor por generacion: la probabilidad de propagar desde una celda de generacion g es spreadChance * generationDecay^g. Menor = el incendio se apaga antes.")]
        private float generationDecay = DefaultGenerationDecay;
        [SerializeField, Min(0), Tooltip("Generacion maxima que propaga: una celda de generacion mayor no enciende vecinas (la generacion se cuenta desde la fuente directa).")]
        private int maxGenerations = DefaultMaxGenerations;
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
        /// <summary>Generacion de cada celda encendida: 0 = fuente directa; el propagado suma 1 al de su origen.</summary>
        private byte[] generation;
        /// <summary>Potencias generationDecay^g precalculadas (indice g, con tope en DecayTableSize-1).</summary>
        private float[] decayPowers;
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

        // Mapa de quemado (RGBAHalf, GridSize x GridSize): r quemado, g calor de brasas, b altura del suelo.
        private Texture2D burnMap;
        private ushort[] burnPixels;
        private float[] heat;
        private int hotCells;
        private bool burntDirty;

        private static readonly int BurnTexId = Shader.PropertyToID("_GrassBurnTex");
        private static readonly int BurnParamsId = Shader.PropertyToID("_GrassBurnParams");
        private const string BurnKeyword = "_GRASS_BURN";
        private bool burnKeywordOn;

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
            generation = new byte[Cells];
            decayPowers = new float[DecayTableSize];
            RebuildDecayTable();
            groundY = new float[Cells];
            inList = new bool[Cells];
            burning = new int[Cells];
            cellFlame = new int[Cells];
            candCell = new int[Cells];
            candDist = new float[Cells];

            flameSystems = new ParticleSystem[Mathf.Max(maxFlames, 0)];
            flameCell = new int[flameSystems.Length];
            heat = new float[Cells];
            burnPixels = new ushort[Cells * 4];

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

            CreateBurnMap();
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
            SetBurnKeyword(false);
            if (burnMap != null) Destroy(burnMap);
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

            // El mapa cambia con el estado (burntDirty), mientras algo arde (el quemado crece) o quedan brasas.
            int done = Mathf.Min(steps, MaxStepsPerFrame);
            if (burntDirty || (done > 0 && (burningCells > 0 || hotCells > 0))) UpdateBurnMap(done * LogicStep);
        }

        // ---------------------------------------------------------------- API publica

        /// <summary>Enciende la celda que contiene el punto y las celdas cuyo centro esta dentro del radio (m).</summary>
        public void Ignite(Vector3 worldPoint, float radius)
        {
            IgniteArea(worldPoint, radius, int.MaxValue);
        }

        /// <summary>
        /// Restablece spreadChance, burnSeconds, heatPerSecond, generationDecay y maxGenerations a los defaults del codigo.
        /// Pensado para que un script de editor pise los valores serializados viejos de la escena.
        /// </summary>
        public void ApplyDefaults()
        {
            spreadChance = DefaultSpreadChance;
            burnSeconds = DefaultBurnSeconds;
            heatPerSecond = DefaultHeatPerSecond;
            generationDecay = DefaultGenerationDecay;
            maxGenerations = DefaultMaxGenerations;
            RebuildDecayTable();
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
                        IgniteArea(origin, field.ReachRadius * FieldFireRadiusFactor, FieldFireBudget);
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
                // Solo la celda bajo el objeto (sin vecinas), y solo tras arder mas de ReactiveBurnThreshold de su avance.
                if (reactive.State.BurnProgress <= ReactiveBurnThreshold) continue;
                if (InGrid(p) && p.y - ValleyShape.Height(p.x, p.z) < ReactiveFireHeight) TryIgnite(CellOf(p), 0);
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

        /// <summary>
        /// Cada celda ardiendo intenta encender a sus 8 vecinos con pasto, con probabilidad spreadChance * generationDecay^g
        /// (g = su generacion). Las de generacion mayor que maxGenerations no propagan. Solo recorre la lista de ardientes.
        /// </summary>
        private void SpreadStep()
        {
            // Snapshot: las celdas nuevas se agregan al final y arden desde la proxima pasada.
            int count = listCount;
            for (int r = 0; r < count; r++)
            {
                int c = burning[r];
                if (state[c] != Burning) continue;
                int g = generation[c];
                if (g > maxGenerations) continue;
                float chance = spreadChance * decayPowers[Mathf.Min(g, DecayTableSize - 1)];
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
                        if (NextUnit() < chance) TryIgnite(n, (byte)(g + 1));
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

        /// <summary>Enciende una celda de pasto con la generacion dada (0 = fuente directa, g+1 = propagada desde g).</summary>
        private bool TryIgnite(int c, byte gen)
        {
            if (state[c] != Grass) return false;
            state[c] = Burning;
            generation[c] = gen;
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
                    if (TryIgnite(c, 0) && ++done >= budget) return done;
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

        /// <summary>Recalcula la tabla de potencias generationDecay^g (g = 0..DecayTableSize-1). Se llama en Awake y ApplyDefaults.</summary>
        private void RebuildDecayTable()
        {
            if (decayPowers == null) decayPowers = new float[DecayTableSize];
            float p = 1f;
            for (int g = 0; g < DecayTableSize; g++)
            {
                decayPowers[g] = p;
                p *= generationDecay;
            }
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
                flameSystems[slot] = BurnEffect.CreateGround(transform, CellSize, fireLook);
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

        /// <summary>Crea el mapa de quemado (bilineal, sin mips) y lo publica como global; b = altura del suelo.</summary>
        private void CreateBurnMap()
        {
            burnMap = new Texture2D(GridSize, GridSize, TextureFormat.RGBAHalf, false, true)
            {
                name = "GrassBurnMap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int c = 0; c < Cells; c++)
            {
                burnPixels[c * 4] = 0;
                burnPixels[c * 4 + 1] = 0;
                burnPixels[c * 4 + 2] = Mathf.FloatToHalf(groundY[c]);
                burnPixels[c * 4 + 3] = 0;
            }
            burnMap.SetPixelData(burnPixels, 0);
            burnMap.Apply(false, false);
            Shader.SetGlobalTexture(BurnTexId, burnMap);
            Shader.SetGlobalVector(BurnParamsId, new Vector4(GridOrigin, GridOrigin, 1f / (GridSize * CellSize), 0f));
            burnKeywordOn = true;
            SetBurnKeyword(false);
        }

        /// <summary>Keyword global de StylizedLit: sin nada quemado el terreno no paga la muestra del mapa.</summary>
        private void SetBurnKeyword(bool on)
        {
            if (on == burnKeywordOn) return;
            burnKeywordOn = on;
            if (on) Shader.EnableKeyword(BurnKeyword);
            else Shader.DisableKeyword(BurnKeyword);
        }

        /// <summary>
        /// Recalcula quemado y calor de cada celda y sube el mapa (4900 texeles; solo si algo cambio). Ardiendo: el
        /// quemado crece de BurningScorchStart a 1 y el calor es 1. Quemada: quemado 1 y las brasas se apagan en
        /// EmberSeconds. Pasto o no inflamable: 0. La keyword global se enciende solo si hay alguna celda marcada.
        /// </summary>
        private void UpdateBurnMap(float dt)
        {
            burntDirty = false;
            int marked = 0;
            hotCells = 0;
            float invBurn = burnSeconds > 0f ? 1f / burnSeconds : 1f;
            for (int c = 0; c < Cells; c++)
            {
                float scorch, h;
                byte st = state[c];
                if (st == Burning)
                {
                    scorch = Mathf.Lerp(BurningScorchStart, 1f, 1f - Mathf.Clamp01(timers[c] * invBurn));
                    h = 1f;
                }
                else if (st == Burnt)
                {
                    scorch = 1f;
                    h = Mathf.Max(0f, heat[c] - dt / EmberSeconds);
                }
                else
                {
                    scorch = 0f;
                    h = 0f;
                }
                heat[c] = h;
                if (h > 0f) hotCells++;
                if (scorch > 0f) marked++;
                burnPixels[c * 4] = Mathf.FloatToHalf(scorch);
                burnPixels[c * 4 + 1] = Mathf.FloatToHalf(h);
            }
            burnMap.SetPixelData(burnPixels, 0);
            burnMap.Apply(false, false);
            SetBurnKeyword(marked > 0);
        }

    }
}
