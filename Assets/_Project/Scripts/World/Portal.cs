using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>Estados de un portal. Dormant -> Charging -> Open -> Cooldown -> Dormant.</summary>
    public enum PortalState { Dormant, Charging, Open, Cooldown }

    /// <summary>
    /// Portal de runas muy complejas. Tiene una placa (un DrawingCanvas fijo) donde el jugador dibuja la receta del portal
    /// (portals.json); si lo leido coincide, el portal carga, se abre y quien este cerca de su ancla viaja al portal PAR.
    ///
    /// La placa lleva un PortalPlate (ISealTrigger): cerrar el anillo deja el sello Armado y NO lo lanza. El portal lo lee con
    /// SpellCaster.ReadSeal; si coincide abre, y si no, dispara el hechizo normal (SpellCaster.Trigger) como cualquier lienzo.
    ///
    /// Pensado para multijugador: el estado es una maquina simple (<see cref="State"/>) y sus cambios y viajes son eventos
    /// estaticos publicos. Hoy viaja el jugador local; un servidor reemplazaria TryTravel por la lista de jugadores.
    /// </summary>
    public class Portal : MonoBehaviour
    {
        [SerializeField] private string id;
        [SerializeField] private DrawingCanvas plate;
        [SerializeField] private Transform arrival;
        [SerializeField] private Transform effectAnchor;
        [SerializeField, Tooltip("portals.json. Si falta, el editor lo busca por ruta; en un build hay que asignarlo.")]
        private TextAsset recipesAsset;

        [Header("Tiempos (s)")]
        [SerializeField] private float chargeSeconds = 2f;
        [SerializeField] private float openSeconds = 10f;
        [SerializeField] private float cooldownSeconds = 5f;

        [Header("Viaje")]
        [SerializeField, Tooltip("Distancia al ancla (m) a la que el jugador viaja.")] private float travelRadius = 2.5f;
        [SerializeField, Tooltip("Segundos sin poder volver a viajar tras un viaje.")] private float travelerCooldown = 3f;
        [SerializeField, Tooltip("Elevacion (m) sobre el punto de llegada para no quedar clavado en el piso.")] private float arrivalLift = 0.1f;
        [SerializeField] private float minConfidence = PortalMath.DefaultMinConfidence;

        private static readonly List<Portal> all = new List<Portal>(8);
        private static PortalRecipes sharedRecipes;

        /// <summary>Portales activos en la escena (solo lectura, para pruebas y VFX).</summary>
        public static IReadOnlyList<Portal> All => all;

        /// <summary>Un jugador viajo: (origen, destino).</summary>
        public static event Action<Portal, Portal> Traveled;

        /// <summary>Un portal cambio de estado (para animacion, luz, sonido y red).</summary>
        public static event Action<Portal, PortalState> StateChanged;

        public string Id => id;
        public string PairId => Definition?.PairId;
        public string DisplayName => Definition != null && !string.IsNullOrEmpty(Definition.DisplayName) ? Definition.DisplayName : id;
        public PortalState State { get; private set; } = PortalState.Dormant;
        public bool IsOpen => State == PortalState.Open;
        public DrawingCanvas Plate => plate;
        public Transform Arrival => arrival;
        public Transform EffectAnchor => effectAnchor != null ? effectAnchor : transform;
        public PortalRecipe Recipe => Definition?.Recipe;

        /// <summary>
        /// Avance del estado actual para la animacion: Charging 0 a 1; Open 1 a 0 (tiempo restante); Cooldown 0 a 1; Dormant 0.
        /// </summary>
        public float Progress01
        {
            get
            {
                float elapsed = Time.time - stateStart;
                switch (State)
                {
                    case PortalState.Charging: return Mathf.Clamp01(elapsed / Mathf.Max(chargeSeconds, 1e-3f));
                    case PortalState.Open: return 1f - Mathf.Clamp01(elapsed / Mathf.Max(openSeconds, 1e-3f));
                    case PortalState.Cooldown: return Mathf.Clamp01(elapsed / Mathf.Max(cooldownSeconds, 1e-3f));
                    default: return 0f;
                }
            }
        }

        private SpellCaster caster;
        private PortalDefinition definition;
        private float stateStart;
        private bool evaluatePending;
        private bool suppressUntilExit;
        private float travelerReadyAt;
        private PlayerMovement player;
        private Rigidbody playerBody;

        private PortalDefinition Definition
        {
            get
            {
                if (definition == null && !string.IsNullOrEmpty(id))
                    definition = EnsureRecipes(recipesAsset).Find(id);
                return definition;
            }
        }

        /// <summary>Reemplaza el catalogo compartido (pruebas y harness).</summary>
        public static void SetRecipes(PortalRecipes recipes) => sharedRecipes = recipes;

        /// <summary>Portal con ese id, o null.</summary>
        public static Portal Find(string portalId)
        {
            if (string.IsNullOrEmpty(portalId)) return null;
            for (int i = 0; i < all.Count; i++)
                if (all[i].id == portalId) return all[i];
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            sharedRecipes = null;
            Traveled = null;
            StateChanged = null;
        }

        private static PortalRecipes EnsureRecipes(TextAsset asset)
        {
            if (sharedRecipes != null) return sharedRecipes;
#if UNITY_EDITOR
            if (asset == null) asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(PortalRecipes.AssetPath);
#endif
            if (asset == null) return PortalRecipes.Empty; // sin cachear: puede asignarse despues
            sharedRecipes = PortalRecipes.Load(asset.text);
            return sharedRecipes;
        }

        /// <summary>Lo llama el builder: id, placa de dibujo, punto de llegada (mira hacia afuera) y ancla del efecto.</summary>
        public void Configure(string portalId, DrawingCanvas portalPlate, Transform arrivalPoint, Transform anchor)
        {
            id = portalId;
            plate = portalPlate;
            arrival = arrivalPoint;
            effectAnchor = anchor;
            definition = null;
#if UNITY_EDITOR
            if (recipesAsset == null) recipesAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(PortalRecipes.AssetPath);
#endif
            if (Application.isPlaying && isActiveAndEnabled) Hook();
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            Hook();
        }

        private void OnDisable()
        {
            all.Remove(this);
            if (caster != null) caster.SealPhaseChanged -= OnSealPhaseChanged;
            caster = null;
        }

        /// <summary>Marca la placa como disparada y se suscribe al caster (idempotente).</summary>
        private void Hook()
        {
            if (plate != null && plate.GetComponentInParent<ISealTrigger>() == null)
                plate.gameObject.AddComponent<PortalPlate>();
            if (caster == null) caster = FindFirstObjectByType<SpellCaster>();
            if (caster == null) return;
            caster.SealPhaseChanged -= OnSealPhaseChanged;
            caster.SealPhaseChanged += OnSealPhaseChanged;
        }

        private void OnSealPhaseChanged(DrawingCanvas canvas, SealPhase phase)
        {
            // No se resuelve aca: SpellCaster esta a mitad de su cambio de fase (y otros oyentes aun no la vieron).
            if (canvas == plate && phase == SealPhase.Armed) evaluatePending = true;
        }

        private void Update()
        {
            if (caster == null && plate != null) Hook();
            float elapsed = Time.time - stateStart;
            switch (State)
            {
                case PortalState.Dormant:
                    if (evaluatePending) Evaluate();
                    break;
                case PortalState.Charging:
                    if (elapsed >= chargeSeconds) Enter(PortalState.Open);
                    break;
                case PortalState.Open:
                    if (elapsed >= openSeconds) Enter(PortalState.Cooldown);
                    else TryTravel();
                    break;
                case PortalState.Cooldown:
                    if (elapsed >= cooldownSeconds) Enter(PortalState.Dormant);
                    break;
            }
        }

        private void Evaluate()
        {
            evaluatePending = false;
            if (plate == null || caster == null || caster.GetPhase(plate) != SealPhase.Armed) return;

            PortalDefinition def = Definition;
            if (def != null && caster.ReadSeal(plate, out PortalReading reading) && PortalMath.Matches(def.Recipe, reading, minConfidence))
            {
                Enter(PortalState.Charging);
                return;
            }
            // No es la receta: la placa se comporta como un lienzo cualquiera.
            caster.Trigger(plate);
        }

        private void Enter(PortalState next)
        {
            State = next;
            stateStart = Time.time;
            Debug.Log($"[Portal] {id}: {next}", this);

            if (next == PortalState.Open)
            {
                // La receta ya se consumio: la placa queda limpia para el proximo uso.
                if (plate != null)
                {
                    plate.Clear();
                    if (caster != null) caster.OnSealEdited(plate);
                }
                evaluatePending = false;
            }
            else if (next == PortalState.Dormant && plate != null && caster != null && caster.GetPhase(plate) == SealPhase.Armed)
            {
                evaluatePending = true; // alguien dibujo mientras estaba ocupado
            }
            StateChanged?.Invoke(this, next);
        }

        private void TryTravel()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerMovement>();
                playerBody = player != null ? player.GetComponent<Rigidbody>() : null;
                if (player == null) return;
            }

            bool inside = PortalMath.InRange(player.transform.position, EffectAnchor.position, travelRadius);
            if (!inside) { suppressUntilExit = false; return; }
            if (suppressUntilExit || Time.time < travelerReadyAt) return;

            Portal pair = Find(PairId);
            if (pair == null || pair.arrival == null)
            {
                Hud.Notify("portal", "El portal no responde", 3f);
                travelerReadyAt = Time.time + travelerCooldown;
                return;
            }

            Teleport(pair.arrival);
            travelerReadyAt = Time.time + travelerCooldown;
            pair.suppressUntilExit = true; // si el par tambien esta abierto, no rebota hasta que salga de su radio
            pair.travelerReadyAt = travelerReadyAt;
            Debug.Log($"[Portal] {id} -> {pair.id}: viaje del jugador", this);
            Traveled?.Invoke(this, pair);
        }

        private void Teleport(Transform destination)
        {
            Vector3 position = destination.position + Vector3.up * arrivalLift;
            Vector3 forward = Vector3.ProjectOnPlane(destination.forward, Vector3.up);
            Quaternion rotation = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : player.transform.rotation;

            if (playerBody != null)
            {
                playerBody.linearVelocity = Vector3.zero;
                playerBody.angularVelocity = Vector3.zero;
                playerBody.position = position;
                playerBody.rotation = rotation;
            }
            player.transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
        }
    }
}
