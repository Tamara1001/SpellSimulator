using System;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;
using SpellSimulator.World.Map;

namespace SpellSimulator.World
{
    /// <summary>
    /// Torre-tutorial (Docs/MAPA_FLUJO_PLAN.md, §17): mientras el tutorial esta activo, una barrera magica encierra la torre;
    /// los faroles de nivel se encienden al pasar el nivel (el del piso 1 y el de la planta baja, al agarrar una pluma en su sala) y al cruzar la puerta la barrera se apaga. Vive en el pie de la torre (su transform
    /// es el origen de las reglas de <see cref="TutorialRules"/>). Lo arranca y lo corta <see cref="PlayerSpawn.Begin"/>.
    /// </summary>
    public class TowerTutorial : MonoBehaviour
    {
        // Revision barata de la posicion del jugador (sin allocations).
        private const float CheckPeriod = 0.2f;

        [SerializeField, Tooltip("Lo que solo existe durante el tutorial: barrera magica y palanca de salteo.")]
        private GameObject activeOnly;
        [SerializeField, Tooltip("Luz de cada farol de nivel (se enciende al pasar el nivel).")]
        private GameObject[] glows = Array.Empty<GameObject>();
        [SerializeField, Tooltip("Padre de los objetos de las trabas (cajas, barril...): vuelven a su lugar al empezar el tutorial.")]
        private Transform props;

        private static readonly System.Collections.Generic.List<Respawnable> rearm = new System.Collections.Generic.List<Respawnable>(16);

        [SerializeField, Tooltip("Puerta de la torre y lo que la destraba (padre de pestillo y contrapeso, piedra rompible).")]
        private TowerDoor door;
        [SerializeField] private Transform doorLatches;
        [SerializeField] private Breakable doorStone;

        private static readonly System.Collections.Generic.List<TutorialCloth> cloths = new System.Collections.Generic.List<TutorialCloth>(2);

        [SerializeField, Tooltip("Reja magica que tapa la rampa de la terraza y los testigos de agacharse y saltar (su luz se enciende al hacerlo).")]
        private GameObject terraceGate, crouchGlow, jumpGlow;

        private readonly TutorialProgress progress = new TutorialProgress();
        private float checkTimer;
        private bool crouched, jumped;
        private Transform movementOwner;
        private SpellSimulator.Player.PlayerMovement movement;

        /// <summary>Ya se agacho bajo la viga / ya salto el muro (la reja de la terraza se apaga con las dos).</summary>
        public bool Crouched => crouched;
        public bool JumpedWall => jumped;
        public bool TerraceGateUp => terraceGate != null && terraceGate.activeSelf;

        /// <summary>Lo llama el generador del mapa (MapTutorial): reja y testigos de la terraza.</summary>
        public void ConfigureTerrace(GameObject gate, GameObject crouchLight, GameObject jumpLight)
        {
            terraceGate = gate;
            crouchGlow = crouchLight;
            jumpGlow = jumpLight;
        }

        /// <summary>Marca las dos acciones de la terraza como hechas (para el smoke y el salteo).</summary>
        public void MarkTerraceDone(bool crouch, bool jump)
        {
            crouched |= crouch;
            jumped |= jump;
            Apply();
        }

        public static TowerTutorial Instance { get; private set; }

        /// <summary>Paso un nivel (indice 0..3).</summary>
        public static event Action<int> LevelPassed;
        /// <summary>El jugador salio de la torre: tutorial terminado.</summary>
        public static event Action Completed;

        public bool IsActive => progress.Active;
        public int Passed => progress.Passed;
        public bool BarrierUp => activeOnly != null && activeOnly.activeSelf;

        /// <summary>Lo llama el generador del mapa (MapTutorial).</summary>
        public void Configure(GameObject activeOnlyRoot, GameObject[] levelGlows, Transform propsRoot)
        {
            activeOnly = activeOnlyRoot;
            glows = levelGlows;
            props = propsRoot;
        }

        /// <summary>Lo llama el generador del mapa (MapTutorial): la puerta y sus tres trabas.</summary>
        public void ConfigureDoor(TowerDoor towerDoor, Transform latches, Breakable stone)
        {
            door = towerDoor;
            doorLatches = latches;
            doorStone = stone;
        }

        /// <summary>True si alguna de las tres trabas de la puerta cedio (pestillo suelto, contrapeso subido o piedra rota).</summary>
        public bool DoorLatchReleased()
        {
            if (doorStone != null && doorStone.IsBroken) return true;
            if (doorLatches == null) return false;
            doorLatches.GetComponentsInChildren(true, cloths);
            bool released = false;
            for (int i = 0; i < cloths.Count; i++) if (cloths[i].IsOpen) released = true;
            cloths.Clear();
            return released;
        }

        private void Awake()
        {
            Instance = this;
            Apply();
        }

        private void OnEnable() => PhysicalTool.PickedUp += OnToolPickedUp;

        private void OnDisable() => PhysicalTool.PickedUp -= OnToolPickedUp;

        /// <summary>Agarrar una pluma enciende el farol de la sala (piso 1 y planta baja); llegar a la sala ya no basta.</summary>
        private void OnToolPickedUp(PhysicalTool tool)
        {
            if (tool != null) ToolPickedUpAt(tool.transform.position);
        }

        /// <summary>Se agarro una pluma en ese punto del mundo (lo usa el evento de PhysicalTool y el smoke). True si encendio un farol.</summary>
        public bool ToolPickedUpAt(Vector3 world)
        {
            int lamp = progress.ToolPickedUp(transform.InverseTransformPoint(world));
            if (lamp < 0) return false;
            Debug.Log($"[Tutorial] pluma agarrada: se enciende el farol {lamp + 1}", this);
            Apply();
            return true;
        }

        /// <summary>¿Esta encendido el farol de nivel i (0 piso 2, 1 piso 1, 2 planta baja, 3 puerta)?</summary>
        public bool LevelLampLit(int i) => i >= 0 && i < glows.Length && glows[i] != null && glows[i].activeSelf;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private bool bypassBuilt;
        private const float BypassWidth = 1.3f;

        /// <summary>
        /// VR: abre un pasillo en el medio de la viga (parte su collider en dos) con una zona que da por hecho «agacharse».
        /// La reja de la bajada sigue cerrada hasta cumplir ambas cosas.
        /// </summary>
        private void BuildVrBypass()
        {
            if (bypassBuilt) return;
            GameObject beam = GameObject.Find("Tutorial_Beam");
            if (beam == null || !beam.TryGetComponent(out BoxCollider box)) return;
            bypassBuilt = true;
            float span = box.size.x;
            if (span <= BypassWidth + 0.4f) return;
            float half = (span - BypassWidth) * 0.5f;
            Vector3 size = box.size;
            Vector3 center = box.center;
            box.size = new Vector3(half, size.y, size.z);
            box.center = center + new Vector3(-(BypassWidth + half) * 0.5f, 0f, 0f);
            var other = beam.AddComponent<BoxCollider>();
            other.size = box.size;
            other.center = center + new Vector3((BypassWidth + half) * 0.5f, 0f, 0f);

            var zone = new GameObject("Tutorial_BeamBypass");
            zone.transform.SetParent(beam.transform, false);
            zone.transform.localPosition = center + new Vector3(0f, -0.9f, 0f);
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(BypassWidth, 2.4f, 0.9f);
            zone.AddComponent<TutorialBypassZone>().Init(this);
        }

        /// <summary>Empieza (o rearma) el tutorial: barrera puesta, faroles apagados y trabas en su lugar.</summary>
        public void Begin()
        {
            progress.Begin();
            VrTutorialTexts.Apply();
            TutorialExercise.ResetAll();
            crouched = false;
            jumped = false;
            if (Player.PlatformDetector.IsVR) BuildVrBypass();
            if (props != null)
            {
                props.GetComponentsInChildren(rearm);
                for (int i = 0; i < rearm.Count; i++) rearm[i].Respawn("se rearma el tutorial");
                rearm.Clear();
            }
            if (props != null)
            {
                props.GetComponentsInChildren(true, cloths);
                for (int i = 0; i < cloths.Count; i++) cloths[i].Rearm();
                cloths.Clear();
            }
            if (doorStone != null) doorStone.Restore();
            if (doorLatches != null)
            {
                var latchColliders = doorLatches.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < latchColliders.Length; i++) latchColliders[i].enabled = true;
            }
            if (door != null) door.Lock();
            checkTimer = 0f;
            Apply();
        }

        /// <summary>Lo corta (salteo): la barrera se apaga.</summary>
        public void Skip()
        {
            progress.Skip();
            if (door != null && door.Locked) door.Unlock(true, true);
            // El sensor invisible de viento (DoorLatch_Bar) y demas trabas quedaban como colliders en el vano: pared invisible al entrar.
            if (doorLatches != null)
            {
                var colliders = doorLatches.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            }
            Apply();
        }

        private void Update()
        {
            if (!progress.Active) return;
            if (!(crouched && jumped)) WatchTerrace();
            checkTimer += Time.deltaTime;
            if (checkTimer < CheckPeriod) return;
            checkTimer = 0f;
            if (door != null && door.Locked && DoorLatchReleased()) door.Unlock(true, false);
            IPlayerContext local = PlayerRegistry.Local;
            if (local == null || local.Root == null) return;
            int before = progress.Passed;
            if (progress.Advance(transform.InverseTransformPoint(local.Root.position)) == 0) return;
            Apply();
            for (int level = before; level < progress.Passed; level++)
            {
                Debug.Log($"[Tutorial] nivel {level + 1} de {TutorialRules.Levels} pasado", this);
                LevelPassed?.Invoke(level);
            }
            if (!progress.Completed) return;
            PlayerSpawn.TutorialSeen = true;
            Debug.Log("[Tutorial] el jugador salio de la torre: la barrera se apaga", this);
            Completed?.Invoke();
        }

        /// <summary>Cada cuadro, mientras falte alguna: ¿se agacho bajo la viga o salto el muro? Cuentas baratas, sin allocations.</summary>
        private void WatchTerrace()
        {
            IPlayerContext local = PlayerRegistry.Local;
            if (local == null || local.Root == null) return;
            if (movementOwner != local.Root)
            {
                movementOwner = local.Root;
                movement = local.Root.GetComponentInChildren<SpellSimulator.Player.PlayerMovement>();
            }
            if (movement == null) return;
            Vector3 p = transform.InverseTransformPoint(local.Root.position);
            bool c = !crouched && TerraceRules.Crouched(p, movement.IsLow);
            bool j = !jumped && TerraceRules.Jumped(p, movement.IsGrounded);
            if (c || j) MarkTerraceDone(c, j);
        }

        private void Apply()
        {
            if (terraceGate != null) terraceGate.SetActive(progress.Active && !(crouched && jumped));
            if (crouchGlow != null) crouchGlow.SetActive(crouched);
            if (jumpGlow != null) jumpGlow.SetActive(jumped);
            if (activeOnly != null && activeOnly.activeSelf != progress.Active) activeOnly.SetActive(progress.Active);
            for (int i = 0; i < glows.Length; i++)
            {
                bool lit = progress.LampLit(i);
                if (glows[i] != null && glows[i].activeSelf != lit) glows[i].SetActive(lit);
            }
        }
    }
}
