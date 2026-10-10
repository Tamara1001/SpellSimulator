using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.World;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Formas solidas del hechizo de piedra (tierra, no cristal), segun los signos del sello (canon: «Sand Bridge», «Sand Cage», muros):
    /// columna = muro (dibujado en el piso) o puente (dibujado en una pared); dispersion = abanico de estacas. Son piezas de roca
    /// fijas (cinematicas, con collider macizo) que emergen del suelo y se hunden solas tras <see cref="RockMath.SolidLifeSeconds"/>;
    /// con un lente de convergencia (compacta la materia) quedan fijas hasta reiniciar el mundo. Topes: ver <see cref="RockMath"/>.
    /// </summary>
    public static class EarthShapes
    {
        private const float RiseSeconds = 0.8f;
        private const float GroundProbe = 2.5f;

        private static readonly List<ExpiringBody> temporary = new List<ExpiringBody>();
        private static readonly List<ExpiringBody> permanent = new List<ExpiringBody>();

        /// <summary>Una pieza de roca de una forma solida: de donde emerge, donde queda, su pose y escala.</summary>
        public struct PieceData
        {
            public Vector3 start, final, scale;
            public Quaternion rot;
            public byte variant;
            public bool fixedShape;
        }

        private struct LivePiece
        {
            public PieceData data;
            public float bornAt;
            public GameObject go;
        }

        /// <summary>(piezas) cada vez que se crea una forma en esta maquina: la red las repite en los clientes.</summary>
        public static event System.Action<List<PieceData>> Spawned;

        private static readonly List<LivePiece> live = new List<LivePiece>();
        private static readonly List<PieceData> batch = new List<PieceData>(16);

        /// <summary>Formas vivas con su edad (s): para quien entra tarde a una sala.</summary>
        public static void CollectLive(List<PieceData> pieces, List<float> ages)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (live[i].go == null) { live.RemoveAt(i); continue; }
            }
            for (int i = 0; i < live.Count; i++)
            {
                pieces.Add(live[i].data);
                ages.Add(Time.time - live[i].bornAt);
            }
        }

        /// <summary>True si el plan pide una forma solida (columna o abanico) en vez de una roca.</summary>
        public static bool IsShape(SpellPlan plan) => plan != null && (plan.Has(PlanPrimitive.Jet) || plan.Has(PlanPrimitive.Fan));

        /// <summary>Crea la forma. Devuelve la cantidad de piezas.</summary>
        public static int Spawn(SpellIR spell, SealFrame frame, ElementLook look)
        {
            SpellPlan plan = spell.plan;
            bool fixedShape = RockMath.IsPermanent(plan.Focus);
            float ink = Mathf.Max(spell.inkPower, 0.0001f);
            batch.Clear();
            int count;
            if (plan.Has(PlanPrimitive.Jet))
            {
                float cone = frame.Scale * (4f + 10f * spell.range);
                count = frame.Normal.y > 0.5f
                    ? Wall(frame, look, RockMath.WallWidth(frame.Radius, ink), RockMath.WallHeight(cone), fixedShape)
                    : Bridge(frame, look, RockMath.BridgeLength(cone), Mathf.Clamp(frame.Radius * 6f, 1.2f, 3f), fixedShape);
            }
            else count = Spikes(frame, look, fixedShape);
            if (batch.Count > 0) Spawned?.Invoke(batch);
            return count;
        }

        /// <summary>Copia de una pieza para un cliente de red (o para quien entra tarde, con la edad que ya lleva): mismo aspecto, colision y duracion.</summary>
        public static GameObject BuildReplica(PieceData d, float age, ElementLook look)
        {
            if (!d.fixedShape && age >= RockMath.SolidLifeSeconds) return null;
            return Create(d, look, age, record: false);
        }

        // ---------------------------------------------------------------- muro (piso)

        private static int Wall(SealFrame frame, ElementLook look, float width, float height, bool fixedShape)
        {
            Vector3 right = Flat(frame.Right, Vector3.right);
            Vector3 forward = Flat(frame.Up, Vector3.forward); // en el piso, el borde de arriba del dibujo se aleja de quien mira
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            int n = RockMath.WallRocks(width);
            Vector3 center = frame.Origin + forward * 0.9f;
            for (int i = 0; i < n; i++)
            {
                Vector3 at = center + right * ((i - (n - 1) * 0.5f) * width / n);
                float ground = GroundY(at, frame.Origin.y);
                float h = height * Random.Range(0.85f, 1.1f);
                Vector3 final = new Vector3(at.x, ground + h * 0.45f, at.z);
                Piece(final, rotation * Quaternion.Euler(0f, Random.Range(-10f, 10f), Random.Range(-4f, 4f)),
                    new Vector3(width / n * 0.62f, h * 0.5f, 0.42f), look, fixedShape, final - Vector3.up * h);
            }
            return n;
        }

        // ---------------------------------------------------------------- puente (pared)

        private static int Bridge(SealFrame frame, ElementLook look, float length, float width, bool fixedShape)
        {
            Vector3 dir = Flat(frame.Normal, Vector3.forward);
            Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);
            int n = RockMath.WallRocks(length);
            float step = length / n;
            for (int i = 0; i < n; i++)
            {
                Vector3 at = frame.Origin + dir * (0.35f + step * (i + 0.5f));
                Vector3 final = new Vector3(at.x, frame.Origin.y - 0.22f, at.z);
                // Crece desde el sello hacia afuera: nace pegada al muro (sin atravesar el vacio).
                Vector3 start = frame.Origin + dir * 0.3f + Vector3.down * 0.22f;
                Piece(final, rotation * Quaternion.Euler(0f, 0f, Random.Range(-3f, 3f)),
                    new Vector3(width * 0.5f, 0.24f, step * 0.62f), look, fixedShape, start);
            }
            return n;
        }

        // ---------------------------------------------------------------- estacas (dispersion)

        private static int Spikes(SealFrame frame, ElementLook look, bool fixedShape)
        {
            int n = RockMath.SpikeCount(frame.Radius);
            float ring = Mathf.Clamp(frame.Scale * 2.2f, 0.7f, 3f);
            Vector3 axis = frame.Normal;
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 u = Vector3.Cross(axis, reference).normalized, v = Vector3.Cross(axis, u);
            float offset = Random.value * 6.28f;
            for (int i = 0; i < n; i++)
            {
                float a = offset + i * Mathf.PI * 2f / n;
                Vector3 radial = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                float length = Random.Range(0.8f, 1.4f) * Mathf.Clamp(frame.Scale * 3f, 0.8f, 2f);
                Vector3 up = (axis + radial * 0.35f).normalized;
                Vector3 baseAt = frame.Origin + radial * ring;
                Vector3 final = baseAt + up * length * 0.4f;
                Piece(final, Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, Random.value * 360f, 0f),
                    new Vector3(Random.Range(0.14f, 0.24f), length * 0.55f, Random.Range(0.14f, 0.24f)), look, fixedShape, baseAt - up * length * 0.5f);
            }
            return n;
        }

        // ---------------------------------------------------------------- pieza

        private static void Piece(Vector3 finalPosition, Quaternion rotation, Vector3 scale, ElementLook look, bool fixedShape, Vector3 startPosition)
        {
            var data = new PieceData
            {
                start = startPosition, final = finalPosition, rot = rotation, scale = scale,
                variant = (byte)Random.Range(0, RockSpell.VariantCount), fixedShape = fixedShape
            };
            batch.Add(data);
            Create(data, look, 0f, record: true);
        }

        private static GameObject Create(PieceData d, ElementLook look, float age, bool record)
        {
            var go = new GameObject("Piedra");
            float rise = Mathf.Clamp01(age / RiseSeconds);
            go.transform.SetPositionAndRotation(Vector3.Lerp(d.start, d.final, Mathf.SmoothStep(0f, 1f, rise)), d.rot);
            go.transform.localScale = d.scale;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = RockSpell.MeshFor(Mathf.Clamp(d.variant, 0, RockSpell.VariantCount - 1));
            var meshRenderer = go.AddComponent<MeshRenderer>();
            if (look != null && look.material != null) meshRenderer.sharedMaterial = look.material;
            go.AddComponent<SolidPiece>();
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var col = go.AddComponent<MeshCollider>();
            col.convex = true;
            col.sharedMesh = filter.sharedMesh;
            col.enabled = rise >= 1f; // mientras emerge no empuja ni se pisa
            if (rise < 1f) go.AddComponent<SolidRise>().Init(go.transform.position, d.final, RiseSeconds * (1f - rise), col);
            Track(go, d.fixedShape, age);
            if (record)
            {
                live.Add(new LivePiece { data = d, bornAt = Time.time, go = go });
                for (int i = live.Count - 1; i >= 0; i--) if (live[i].go == null) live.RemoveAt(i);
            }
            return go;
        }

        private static void Track(GameObject go, bool fixedShape, float age = 0f)
        {
            var expiring = go.AddComponent<ExpiringBody>();
            expiring.Init(fixedShape ? float.MaxValue : Mathf.Max(0.1f, RockMath.SolidLifeSeconds - age), RockMath.SolidShrinkSeconds);
            List<ExpiringBody> list = fixedShape ? permanent : temporary;
            int cap = fixedShape ? RockMath.MaxPermanentPieces : RockMath.MaxSolidPieces;
            for (int i = list.Count - 1; i >= 0; i--) if (list[i] == null) list.RemoveAt(i);
            list.Add(expiring);
            while (list.Count > cap)
            {
                list[0].BeginExpire();
                list.RemoveAt(0);
            }
        }

        private static float GroundY(Vector3 at, float fallback)
        {
            if (Physics.Raycast(at + Vector3.up * GroundProbe, Vector3.down, out RaycastHit hit, GroundProbe * 2f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallback;
        }

        private static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? fallback : v.normalized;
        }
    }

    /// <summary>Marca de pieza solida viva (cuenta para el harness y los diagnosticos).</summary>
    public sealed class SolidPiece : MonoBehaviour
    {
        public static int Count { get; private set; }

        private void OnEnable() => Count++;

        private void OnDisable() => Count--;
    }

    /// <summary>Hace emerger una pieza de roca de una pose a otra y, al llegar, activa su collider.</summary>
    public sealed class SolidRise : MonoBehaviour
    {
        private Vector3 from, to;
        private float seconds, t;
        private Collider col;

        public void Init(Vector3 start, Vector3 end, float riseSeconds, Collider collider)
        {
            from = start;
            to = end;
            seconds = Mathf.Max(0.05f, riseSeconds);
            col = collider;
        }

        private void Update()
        {
            t += Time.deltaTime / seconds;
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            if (t < 1f) return;
            if (col != null) col.enabled = true;
            Destroy(this);
        }
    }
}
