using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Net;
using SpellSimulator.Player;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.Lab
{
    /// <summary>Que puede materializar la mesa del laboratorio.</summary>
    public enum LabSpawnKind
    {
        Box = 0,
        Wind = 1,
        Fire = 2,
        Fountain = 3
    }

    /// <summary>
    /// Materializa en anclas fijas los objetos de los experimentos del laboratorio con los valores actuales de
    /// <see cref="LabParameters"/>: caja (masa), zona de viento (direccion y velocidad), parche de fuego (radio de semilla) y,
    /// en la fase 3, la fuente. Topes propios (4 cajas, 2 vientos, 1 fuego activo, 1 fuente; al pasarse se retira el mas viejo),
    /// enfriamiento de 1 s por boton, nada en red (LABORATORIO_PLAN riesgo 11). Todo cuelga de la raiz de runtime «LabSpawned»
    /// y se destruye en <see cref="MagicWorld.WorldReset"/>; los valores de la mesa no se tocan.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LabSpawner : MonoBehaviour
    {
        private const int KindCount = 4;
        private const int MaxBoxes = 4, MaxWinds = 2;
        /// <summary>Enfriamiento por boton (s), LABORATORIO_PLAN riesgo 4.</summary>
        private const float Cooldown = 1f;
        /// <summary>Lado de la caja (m): tamaño fijo para aislar la variable masa (riesgo 3).</summary>
        private const float BoxSize = 0.6f;
        /// <summary>Desde esta masa (kg) la caja usa deteccion continua (riesgo 3).</summary>
        private const float ContinuousMass = 100f;
        /// <summary>Iteraciones del solver y tope de despenetracion, como RockSpell (Output/Bodies/RockSpell.cs).</summary>
        private const int SolverIterations = 12;
        private const float MaxDepenetration = 2f;
        /// <summary>Altura desde la que se busca el piso bajo el ancla y largo del rayo (m).</summary>
        private const float GroundProbeUp = 1.5f, GroundProbeLength = 6f;
        /// <summary>Separacion de los lugares alternativos alrededor del ancla si el centro esta ocupado (m).</summary>
        private const float BoxSpread = 0.85f;
        /// <summary>Achique al retirar la caja mas vieja (s).</summary>
        private const float EvictShrinkSeconds = 0.5f;
        /// <summary>Radio (m) alrededor del ancla en el que se busca si el fuego anterior sigue ardiendo.</summary>
        private const float FireActiveRadius = 12f;
        private const string BoxSupplyId = "lab_box";
        private const string NoticeId = "lab";

        private static readonly Vector2[] BoxOffsets =
        {
            Vector2.zero, new Vector2(BoxSpread, 0f), new Vector2(-BoxSpread, 0f), new Vector2(0f, BoxSpread), new Vector2(0f, -BoxSpread)
        };
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color LightBox = new Color(0.93f, 0.8f, 0.55f);
        private static readonly Color HeavyBox = new Color(0.35f, 0.22f, 0.14f);

        /// <summary>Mesa activa (null si no hay laboratorio en la escena).</summary>
        public static LabSpawner Current { get; private set; }

        /// <summary>Se materializo algo: (que, objeto). Para el fuego el objeto es un marcador vacio en el punto de ignicion.</summary>
        public event Action<LabSpawnKind, GameObject> Materialized;

        [Header("Anclas (las asigna el generador)")]
        [SerializeField, Tooltip("Donde aparecen las cajas (se apoyan en el piso bajo el ancla).")]
        private Transform boxAnchor;
        [SerializeField, Tooltip("Base de la zona de viento (4×3×4 m, la base en el ancla).")]
        private Transform windAnchor;
        [SerializeField, Tooltip("Punto de ignicion del parche de fuego (a ≥ 8 m de la mesa, sobre pasto).")]
        private Transform fireAnchor;
        [SerializeField, Tooltip("Fase 3: lugar de la fuente.")]
        private Transform fountainAnchor;

        [Header("Look (opcional)")]
        [SerializeField, Tooltip("Material de las cajas (vacio = URP/Lit creado en runtime).")]
        private Material boxMaterial;
        [SerializeField, Tooltip("Material de las rayas del viento (vacio = URP Particles/Unlit creado en runtime).")]
        private Material windParticleMaterial;

        private readonly float[] lastPress = { float.MinValue, float.MinValue, float.MinValue, float.MinValue };
        private readonly List<GameObject> boxes = new List<GameObject>(MaxBoxes + 1);
        private readonly List<GameObject> winds = new List<GameObject>(MaxWinds + 1);
        private readonly Vector3[] firePos = new Vector3[1];
        private readonly byte[] fireGen = new byte[1];
        private GameObject fireMarker;
        private Transform root;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            Current = this;
            block = new MaterialPropertyBlock();
        }

        private void OnEnable() => MagicWorld.WorldReset += ClearAll;

        private void OnDisable() => MagicWorld.WorldReset -= ClearAll;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (root != null) Destroy(root.gameObject);
        }

        // ---------------------------------------------------------------- API

        /// <summary>
        /// Materializa un objeto con los valores actuales. Devuelve null si no se pudo: sesion de red, enfriamiento, ancla
        /// invalida, sin lugar, fuego anterior todavia activo o fuente (fase 3, sin implementar).
        /// </summary>
        public GameObject Materialize(LabSpawnKind kind)
        {
            if (NetSession.Exists)
            {
                Notify("Laboratorio: solo en modo Libre");
                return null;
            }
            int k = (int)kind;
            if (k < 0 || k >= KindCount) return null;
            if (Time.time - lastPress[k] < Cooldown) return null;
            lastPress[k] = Time.time;

            GameObject go = kind switch
            {
                LabSpawnKind.Box => SpawnBox(),
                LabSpawnKind.Wind => SpawnWind(),
                LabSpawnKind.Fire => SpawnFire(),
                // TODO (fase 3, LABORATORIO_PLAN seccion 4): LabFountain con WaterSpell.ConfigureDrops(ps, inkPower,
                // caudal fijo, ignore) en fountainAnchor, tope 1 (retirar la mas vieja). Hasta entonces no hace nada.
                LabSpawnKind.Fountain => null,
                _ => null
            };
            if (go != null) Materialized?.Invoke(kind, go);
            return go;
        }

        /// <summary>Destruye todo lo materializado (lo llama el reinicio del mundo). El fuego del pasto lo reinicia GrassFire.</summary>
        public void ClearAll()
        {
            if (root != null)
            {
                for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            }
            boxes.Clear();
            winds.Clear();
            fireMarker = null;
        }

        // ---------------------------------------------------------------- caja

        private GameObject SpawnBox()
        {
            if (!ValidAnchor(boxAnchor, "cajas")) return null;
            Prune(boxes);
            // Al llegar al tope se retira la mas vieja (se achica y desaparece).
            while (boxes.Count >= MaxBoxes)
            {
                Retire(boxes[0]);
                boxes.RemoveAt(0);
            }

            if (!FindBoxSpot(out Vector3 position))
            {
                Notify("Laboratorio: no hay lugar libre para la caja");
                return null;
            }

            float mass = Param(LabVariableId.Mass);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "LabBox";
            go.transform.SetParent(Root(), false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, boxAnchor.eulerAngles.y, 0f));
            go.transform.localScale = Vector3.one * BoxSize;

            var renderer = go.GetComponent<Renderer>();
            Material material = boxMaterial != null ? boxMaterial : BoxFallbackMaterial();
            if (material != null) renderer.sharedMaterial = material;
            // Color por masa (claro = liviano, oscuro = pesado); el overlay rotula el numero.
            block.Clear();
            block.SetColor(BaseColorId, Color.Lerp(LightBox, HeavyBox, LabParameterMath.Normalized(mass, LabCatalog.Get(LabVariableId.Mass))));
            renderer.SetPropertyBlock(block);

            // Rigidbody antes que Movable: Movable cachea collider/renderers/rigidbody en su Awake.
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.solverIterations = SolverIterations;
            rb.maxDepenetrationVelocity = MaxDepenetration;
            rb.collisionDetectionMode = mass >= ContinuousMass ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;

            var movable = go.AddComponent<Movable>();
            movable.SetName("caja del laboratorio");

            boxes.Add(go);
            // Tambien cuenta para el tope global de objetos sueltos (riesgo 4).
            WorldObjectBudget.Register(go, BoxSupplyId);
            return go;
        }

        /// <summary>Busca, alrededor del ancla, un lugar con piso que no sea otra caja y sin nada solapado.</summary>
        private bool FindBoxSpot(out Vector3 position)
        {
            Vector3 anchor = boxAnchor.position;
            Vector3 right = boxAnchor.right, forward = boxAnchor.forward;
            right.y = 0f;
            forward.y = 0f;
            right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 half = Vector3.one * (BoxSize * 0.5f * 0.95f);
            Transform spawned = Root();

            for (int i = 0; i < BoxOffsets.Length; i++)
            {
                Vector3 p = anchor + right * BoxOffsets[i].x + forward * BoxOffsets[i].y;
                if (!Physics.Raycast(p + Vector3.up * GroundProbeUp, Vector3.down, out RaycastHit hit, GroundProbeLength, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                if (hit.transform.IsChildOf(spawned)) continue; // nunca encima de otra caja
                Vector3 candidate = hit.point + Vector3.up * (BoxSize * 0.5f + 0.01f);
                if (Physics.CheckBox(candidate, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                position = candidate;
                return true;
            }
            position = default;
            return false;
        }

        private static void Retire(GameObject go)
        {
            if (go == null) return;
            if (!go.TryGetComponent(out ExpiringBody body)) body = go.AddComponent<ExpiringBody>();
            body.Init(float.MaxValue, EvictShrinkSeconds);
            body.BeginExpire();
        }

        // ---------------------------------------------------------------- viento

        private GameObject SpawnWind()
        {
            if (!ValidAnchor(windAnchor, "viento")) return null;
            Prune(winds);
            while (winds.Count >= MaxWinds)
            {
                if (winds[0] != null) Destroy(winds[0]);
                winds.RemoveAt(0);
            }

            var go = new GameObject("LabWindZone");
            go.transform.SetParent(Root(), false);
            go.transform.SetPositionAndRotation(windAnchor.position, Quaternion.identity);
            var zone = go.AddComponent<LabWindZone>();
            if (windParticleMaterial != null) zone.SetParticleMaterial(windParticleMaterial);
            zone.Configure(Param(LabVariableId.WindYaw), Param(LabVariableId.WindSpeed));
            winds.Add(go);
            return go;
        }

        // ---------------------------------------------------------------- fuego

        private GameObject SpawnFire()
        {
            if (!ValidAnchor(fireAnchor, "fuego")) return null;
            GrassFire grass = GrassFire.Current;
            if (grass == null)
            {
                Notify("Laboratorio: no hay pasto que encender");
                return null;
            }
            Vector3 point = fireAnchor.position;
            if (GrassFire.IsFireproofAt(point))
            {
                Notify("Laboratorio: el ancla del fuego no esta sobre pasto");
                return null;
            }
            // Un solo parche activo: el fuego del pasto no se puede borrar, asi que se espera a que el anterior se apague.
            if (fireMarker != null && grass.SampleBurning(fireMarker.transform.position, FireActiveRadius, firePos, fireGen) > 0)
            {
                Notify("Laboratorio: el fuego anterior sigue ardiendo");
                return null;
            }
            if (fireMarker != null) Destroy(fireMarker);

            grass.Ignite(point, Param(LabVariableId.FireSeedRadius));
            fireMarker = new GameObject("LabFirePatch");
            fireMarker.transform.SetParent(Root(), false);
            fireMarker.transform.position = point;
            return fireMarker;
        }

        // ---------------------------------------------------------------- utilidades

        private static float Param(LabVariableId id)
        {
            LabParameters parameters = LabParameters.Current;
            return parameters != null ? parameters.Get(id) : LabCatalog.Get(id).Default;
        }

        private Transform Root()
        {
            if (root == null) root = new GameObject("LabSpawned").transform;
            return root;
        }

        /// <summary>Saca de la lista lo destruido o ya retirandose (tope global, reinicio).</summary>
        private static void Prune(List<GameObject> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                GameObject go = list[i];
                if (go == null || (go.TryGetComponent(out ExpiringBody body) && body.IsExpiring)) list.RemoveAt(i);
            }
        }

        private static bool ValidAnchor(Transform anchor, string what)
        {
            if (anchor != null) return true;
            Debug.LogWarning($"[Laboratorio] Falta el ancla de {what} en LabSpawner.");
            return false;
        }

        private static void Notify(string text)
        {
            Hud.Notify(NoticeId, text, 2.5f);
            Debug.Log("[Laboratorio] " + text);
        }

        private static Material boxFallback;

        private static Material BoxFallbackMaterial()
        {
            if (boxFallback != null) return boxFallback;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            boxFallback = new Material(shader) { name = "LabBox" };
            return boxFallback;
        }
    }
}
