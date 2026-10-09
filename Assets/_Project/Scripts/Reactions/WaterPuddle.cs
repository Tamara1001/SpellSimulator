using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Un charco del pool de <see cref="WaterPuddleManager"/>: disco de agua translucido cuyo tamano sale del volumen
    /// (WaterMath.PuddleRadius). Crece con cada gota, escurre cuesta abajo y se evapora hasta desaparecer; todo eso lo
    /// avanza el manager (un solo Update para todos). Congelado (agua + cristal) es una placa de hielo quieta y resbaladiza
    /// que dura unos segundos y vuelve a ser charco.
    /// Raiz: Rigidbody cinematico + BoxCollider trigger (capa Ignore Raycast) para que el cristal lo encuentre;
    /// hijo "Hielo": collider solido que solo se prende congelado.
    /// </summary>
    public class WaterPuddle : MonoBehaviour, IFreezable
    {
        private const float SurfaceLift = 0.012f;
        private const float IceThickness = 0.06f;
        private const float RescaleThreshold = 0.02f;

        public float Liters { get; private set; }
        public float Radius { get; private set; }
        /// <summary>Litros por segundo que pierde (segun la tinta del hechizo que lo creo).</summary>
        public float EvaporationRate { get; set; }
        public bool IsFrozen => frozenUntil > Time.time;
        public bool IsActive => gameObject.activeSelf;

        /// <summary>Velocidad de deslizamiento actual (plano XZ, m/s); la recalcula el manager a baja frecuencia.</summary>
        internal Vector2 Slide;
        internal float GroundY;

        private float frozenUntil;
        private bool frozenVisual;
        private float shownRadius = -1f;
        private MeshRenderer surface;
        private GameObject iceGo;
        private BoxCollider trigger;

        /// <summary>Arma la jerarquia una sola vez (lo llama el manager al crear el pool).</summary>
        internal void Build(Material waterMaterial)
        {
            gameObject.layer = 2; // Ignore Raycast: ni el puntero ni las particulas lo ven; el OverlapSphere del cristal si.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2f, 0.1f, 2f);
            trigger.center = new Vector3(0f, 0.05f, 0f);

            surface = WaterMeshes.CreateDisc("Superficie", transform, waterMaterial);
            surface.transform.localPosition = new Vector3(0f, SurfaceLift, 0f);
            surface.gameObject.layer = 2;

            var ice = WaterMeshes.CreateIce("Hielo", transform);
            iceGo = ice.gameObject;
            ice.transform.localPosition = new Vector3(0f, IceThickness, 0f);
            ice.transform.localScale = new Vector3(1f, IceThickness, 1f);
            var col = iceGo.AddComponent<BoxCollider>();
            // El collider vive en el espacio de la malla (radio 1, alto 1, tapa en y = 0).
            col.center = new Vector3(0f, -0.5f, 0f);
            col.size = new Vector3(1.84f, 1f, 1.84f);
            col.sharedMaterial = WaterMeshes.IcePhysics;
            iceGo.SetActive(false);
        }

        internal void SetMaterial(Material waterMaterial)
        {
            if (surface != null && waterMaterial != null) surface.sharedMaterial = waterMaterial;
        }

        /// <summary>Activa el charco en un punto del suelo con un volumen inicial.</summary>
        internal void Activate(Vector3 groundPoint, float liters, float evaporation)
        {
            transform.position = groundPoint;
            GroundY = groundPoint.y;
            Liters = 0f;
            Slide = Vector2.zero;
            frozenUntil = 0f;
            EvaporationRate = evaporation;
            shownRadius = -1f;
            SetFrozenVisual(false);
            gameObject.SetActive(true);
            AddLiters(liters);
        }

        internal void Deactivate()
        {
            Liters = 0f;
            Radius = 0f;
            frozenUntil = 0f;
            SetFrozenVisual(false);
            gameObject.SetActive(false);
        }

        /// <summary>Suma (o resta) agua. Congelado no recibe agua nueva.</summary>
        internal void AddLiters(float liters)
        {
            if (IsFrozen && liters > 0f) return;
            Liters = WaterMath.AddLiters(Liters, liters);
            Radius = WaterMath.PuddleRadius(Liters);
            RefreshScale();
        }

        /// <summary>Evapora y vuelve del hielo. Devuelve false si se seco.</summary>
        internal bool Tick(float dt)
        {
            if (IsFrozen) return true;
            if (frozenVisual) SetFrozenVisual(false); // se derritio: vuelve a charco con el mismo volumen
            AddLiters(-EvaporationRate * dt);
            return Liters > 0f;
        }

        internal void MoveTo(Vector3 position)
        {
            transform.position = position;
        }

        public bool Freeze(Vector3 center, float radius, float seconds)
        {
            if (!IsActive || Liters <= 0f || seconds <= 0f) return false;
            Vector3 p = transform.position;
            float dx = p.x - center.x, dz = p.z - center.z;
            float reach = radius + Radius;
            if (dx * dx + dz * dz > reach * reach || Mathf.Abs(p.y - center.y) > radius + 0.5f) return false;
            frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);
            Slide = Vector2.zero;
            SetFrozenVisual(true);
            return true;
        }

        private void SetFrozenVisual(bool frozen)
        {
            frozenVisual = frozen;
            if (iceGo != null) iceGo.SetActive(frozen);
            if (surface != null) surface.enabled = !frozen;
        }

        private void RefreshScale()
        {
            if (Mathf.Abs(Radius - shownRadius) < RescaleThreshold * Mathf.Max(shownRadius, 0.1f)) return;
            shownRadius = Radius;
            float r = Mathf.Max(Radius, 0.01f);
            transform.localScale = new Vector3(r, 1f, r);
        }
    }
}
