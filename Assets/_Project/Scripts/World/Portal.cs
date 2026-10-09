using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>Estados de un portal. Dormant -> Charging -> Open -> Cooldown -> Dormant.</summary>
    public enum PortalState { Dormant, Charging, Open, Cooldown }

    /// <summary>
    /// Portal de runas. Cada portal tiene una IDENTIDAD (1 sigilo + 2 signos, portals.json) grabada en el propio portal. Para
    /// viajar se escribe en la placa del portal en el que se esta la identidad del portal DESTINO: el portal de origen carga y se
    /// abre hacia el destino (y el destino se abre de vuelta) durante <c>openSeconds</c>. Para viajar hay que ATRAVESAR el portal
    /// abierto (volumen de cruce, <see cref="PortalGate"/>), no basta con estar cerca.
    ///
    /// La placa lleva un PortalPlate (ISealTrigger): cerrar el anillo deja el sello Armado y NO lo lanza. El portal lo lee con
    /// SpellCaster.ReadSeal; si coincide con una identidad abre la ruta, y si no, dispara el hechizo normal (SpellCaster.Trigger).
    ///
    /// Una ruta es un par mutuo (origen, destino): ambos se abren y se cierran juntos. Abrir otra ruta hacia el mismo destino, o
    /// reabrir el origen, cancela la anterior. Pensado para multijugador: el estado es una maquina simple (<see cref="State"/>) y
    /// sus cambios, rutas y viajes son eventos estaticos publicos. Hoy viaja el jugador local; un servidor reemplazaria OnCross.
    /// </summary>
    public class Portal : MonoBehaviour
    {
        [SerializeField] private string id;
        [SerializeField] private DrawingCanvas plate;
        [SerializeField] private Transform arrival;
        [SerializeField] private Transform effectAnchor;
        [SerializeField] private Collider crossTrigger;
        [SerializeField, Tooltip("portals.json. Si falta, el editor lo busca por ruta; en un build hay que asignarlo.")]
        private TextAsset recipesAsset;

        [Header("Tiempos (s)")]
        [SerializeField] private float chargeSeconds = 1.5f;
        [SerializeField] private float openSeconds = 30f;
        [SerializeField] private float cooldownSeconds = 3f;

        [Header("Viaje")]
        [SerializeField, Tooltip("Segundos sin poder volver a viajar tras un cruce (evita rebotar).")] private float travelerCooldown = 2f;
        [SerializeField, Tooltip("Elevacion (m) sobre el punto de llegada para no quedar clavado en el piso.")] private float arrivalLift = 0.1f;
        [SerializeField] private float minConfidence = PortalMath.DefaultMinConfidence;

        private static readonly List<Portal> all = new List<Portal>(8);
        private static PortalRecipes sharedRecipes;

        /// <summary>Si es mayor que 0, reemplaza el tiempo abierto de todos los portales (pruebas del harness).</summary>
        public static float OpenSecondsOverride;

        /// <summary>Portales activos en la escena (solo lectura, para pruebas y VFX).</summary>
        public static IReadOnlyList<Portal> All => all;

        /// <summary>Un jugador cruzo: (origen, destino).</summary>
        public static event Action<Portal, Portal> Traveled;

        /// <summary>Un portal cambio de estado (para animacion, luz, sonido y red).</summary>
        public static event Action<Portal> StateChanged;

        /// <summary>Una ruta quedo abierta: (portal de origen, portal de destino).</summary>
        public static event Action<Portal, Portal> RouteOpened;

        public string Id => id;
        public string DisplayName => Definition != null && !string.IsNullOrEmpty(Definition.DisplayName) ? Definition.DisplayName : id;
        public PortalState State { get; private set; } = PortalState.Dormant;
        public bool IsOpen => State == PortalState.Open;
        public DrawingCanvas Plate => plate;
        public Transform Arrival => arrival;
        public Collider CrossTrigger => crossTrigger;
        public Transform EffectAnchor => effectAnchor != null ? effectAnchor : transform;

        /// <summary>Hacia donde lleva ahora (null si esta cerrado). En el portal de vuelta es el portal que abrio la ruta.</summary>
        public Portal Destination { get; private set; }
        public string DestinationId => Destination != null ? Destination.id : null;

        /// <summary>Identidad de este portal leida de los datos por id (vacia si no esta en portals.json).</summary>
        public PortalIdentity Identity => Definition != null ? Definition.Identity : default;

        private float EffectiveOpenSeconds => OpenSecondsOverride > 0f ? OpenSecondsOverride : openSeconds;

        /// <summary>Segundos que le quedan abierto (0 si no esta Open).</summary>
        public float OpenSecondsLeft => State == PortalState.Open ? Mathf.Max(0f, EffectiveOpenSeconds - (GameClock.Time - stateStart)) : 0f;

        /// <summary>
        /// Avance del estado actual para la animacion: Charging 0 a 1; Open 1 a 0 (tiempo restante); Cooldown 0 a 1; Dormant 0.
        /// </summary>
        public float Progress01
        {
            get
            {
                float elapsed = GameClock.Time - stateStart;
                switch (State)
                {
                    case PortalState.Charging: return Mathf.Clamp01(elapsed / Mathf.Max(chargeSeconds, 1e-3f));
                    case PortalState.Open: return 1f - Mathf.Clamp01(elapsed / Mathf.Max(EffectiveOpenSeconds, 1e-3f));
                    case PortalState.Cooldown: return Mathf.Clamp01(elapsed / Mathf.Max(cooldownSeconds, 1e-3f));
                    default: return 0f;
                }
            }
        }

        private SpellCaster caster;
        private PortalDefinition definition;
        private float stateStart;
        private bool evaluatePending;
        private bool isOrigin;
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
            RouteOpened = null;
            OpenSecondsOverride = 0f;
            PortalTravelerCooldowns.Clear();
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

        /// <summary>Compatibilidad con el builder anterior (sin volumen de cruce: ese portal no se puede atravesar).</summary>
        [Obsolete("Usar la sobrecarga con crossTrigger.")]
        public void Configure(string portalId, DrawingCanvas portalPlate, Transform arrivalPoint, Transform anchor)
            => Configure(portalId, portalPlate, arrivalPoint, anchor, null);

        /// <summary>
        /// Lo llama el builder: id, placa de dibujo, punto de llegada (mira hacia afuera), ancla del efecto y el volumen de cruce
        /// (un Collider en el disco del portal; se fuerza a trigger y se le agrega un PortalGate).
        /// </summary>
        public void Configure(string portalId, DrawingCanvas portalPlate, Transform arrivalPoint, Transform anchor, Collider crossVolume)
        {
            id = portalId;
            plate = portalPlate;
            arrival = arrivalPoint;
            effectAnchor = anchor;
            crossTrigger = crossVolume;
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
            if (State != PortalState.Dormant && State != PortalState.Cooldown) Release(this);
            all.Remove(this);
            if (caster != null) caster.SealPhaseChanged -= OnSealPhaseChanged;
            caster = null;
        }

        /// <summary>Marca la placa como disparada, prepara el volumen de cruce y se suscribe al caster (idempotente).</summary>
        private void Hook()
        {
            if (plate != null && plate.GetComponentInParent<ISealTrigger>() == null)
                plate.gameObject.AddComponent<PortalPlate>();
            if (crossTrigger != null)
            {
                crossTrigger.isTrigger = true;
                PortalGate gate = crossTrigger.GetComponent<PortalGate>();
                if (gate == null) gate = crossTrigger.gameObject.AddComponent<PortalGate>();
                gate.Bind(this);
            }
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
            float elapsed = GameClock.Time - stateStart;
            switch (State)
            {
                case PortalState.Dormant:
                    if (evaluatePending) Evaluate();
                    break;
                case PortalState.Charging:
                    if (elapsed >= chargeSeconds)
                    {
                        Enter(PortalState.Open);
                        if (isOrigin && Destination != null) RouteOpened?.Invoke(this, Destination);
                    }
                    break;
                case PortalState.Open:
                    if (elapsed >= EffectiveOpenSeconds) Release(this);
                    else if (evaluatePending) Evaluate(); // abierto a otro destino: se puede reabrir con uno nuevo
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

            PortalRecipes recipes = EnsureRecipes(recipesAsset);
            if (caster.ReadSeal(plate, out PortalReading reading))
            {
                int index = PortalMath.BestMatch(recipes.Identities, reading, minConfidence);
                if (index >= 0)
                {
                    string targetId = recipes.Portals[index].Id;
                    if (targetId == id)
                    {
                        Hud.Notify("portal", "Ya estas en ese portal", 2f);
                        ConsumePlate();
                        return;
                    }
                    Portal target = Find(targetId);
                    if (target == null || target.arrival == null)
                    {
                        Hud.Notify("portal", "El portal no responde", 3f);
                        ConsumePlate();
                        return;
                    }
                    ConsumePlate(); // la identidad se consumio: la placa queda limpia para el proximo uso
                    OpenRoute(this, target);
                    return;
                }
            }
            // No es la identidad de ningun portal: la placa se comporta como un lienzo cualquiera.
            caster.Trigger(plate);
        }

        private void ConsumePlate()
        {
            evaluatePending = false;
            if (plate == null) return;
            plate.Clear();
            if (caster != null) caster.OnSealEdited(plate);
        }

        /// <summary>
        /// Abre la ruta origen -> destino (y destino -> origen). Cancela antes: la ruta del propio origen, cualquier otra ruta que
        /// ya llevara al destino y la ruta en la que estuviera el destino.
        /// </summary>
        private static void OpenRoute(Portal origin, Portal dest)
        {
            Release(origin);
            for (int i = 0; i < all.Count; i++)
            {
                Portal other = all[i];
                if (other != origin && other != dest && other.Destination == dest) Release(other);
            }
            Release(dest);

            origin.Destination = dest;
            dest.Destination = origin;
            origin.isOrigin = true;
            dest.isOrigin = false;
            origin.Enter(PortalState.Charging);
            dest.Enter(PortalState.Charging);
            Debug.Log($"[Portal] ruta {origin.id} -> {dest.id}");
        }

        /// <summary>Cierra el portal y, si la ruta era mutua, tambien su pareja. No hace nada si ya estaba cerrado.</summary>
        private static void Release(Portal portal)
        {
            if (portal == null || (portal.State != PortalState.Charging && portal.State != PortalState.Open)) return;
            Portal partner = portal.Destination;
            portal.Close();
            if (partner != null && partner.Destination == portal) partner.Close();
        }

        private void Close()
        {
            Destination = null;
            isOrigin = false;
            if (State == PortalState.Charging || State == PortalState.Open) Enter(PortalState.Cooldown);
        }

        private void Enter(PortalState next)
        {
            State = next;
            stateStart = GameClock.Time;
            Debug.Log($"[Portal] {id}: {next}", this);

            if (next == PortalState.Dormant && plate != null && caster != null && caster.GetPhase(plate) == SealPhase.Armed)
                evaluatePending = true; // alguien dibujo mientras estaba ocupado
            StateChanged?.Invoke(this);
        }

        /// <summary>Lo llama PortalGate cuando algo entra al volumen de cruce. Viaja si el portal esta Open y es el jugador.</summary>
        internal void OnCross(Collider other)
        {
            if (State != PortalState.Open || Destination == null || Destination.arrival == null) return;

            // El viajero se identifica por su contexto de jugador (PlayerRegistry); el enfriamiento es por jugador.
            PlayerMovement mover = other.GetComponentInParent<PlayerMovement>();
            if (mover == null) return;
            int ownerId = mover.TryGetComponent(out IPlayerContext traveler) ? traveler.OwnerId : 0;
            if (!PortalTravelerCooldowns.Ready(ownerId, GameClock.Time)) return;
            player = mover;
            playerBody = mover.GetComponent<Rigidbody>();

            Portal dest = Destination;
            Teleport(dest.arrival);
            PortalTravelerCooldowns.Mark(ownerId, GameClock.Time, travelerCooldown);
            Debug.Log($"[Portal] {id} -> {dest.id}: cruce del jugador", this);
            Traveled?.Invoke(this, dest);
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
