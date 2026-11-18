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
    /// cada nivel pasado enciende su farol y al cruzar la puerta la barrera se apaga. Vive en el pie de la torre (su transform
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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Empieza (o rearma) el tutorial: barrera puesta, faroles apagados y trabas en su lugar.</summary>
        public void Begin()
        {
            progress.Begin();
            VrTutorialTexts.Apply();
            TutorialExercise.ResetAll();
            // VR: agacharse no es obligatorio (se puede por boton o con el cuerpo, pero la viga no traba la reja).
            crouched = Player.PlatformDetector.IsVR;
            jumped = false;
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
            if (door != null) door.Lock();
            checkTimer = 0f;
            Apply();
        }

        /// <summary>Lo corta (salteo): la barrera se apaga.</summary>
        public void Skip()
        {
            progress.Skip();
            if (door != null && door.Locked) door.Unlock(true, true);
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
                bool lit = i < progress.Passed;
                if (glows[i] != null && glows[i].activeSelf != lit) glows[i].SetActive(lit);
            }
        }
    }
}
