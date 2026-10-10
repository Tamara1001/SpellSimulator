using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using SpellSimulator.Learning;
using SpellSimulator.Player;

namespace SpellSimulator.UI
{
    /// <summary>
    /// Menu principal (UI Toolkit) en la misma escena del juego: al arrancar queda en pantalla, la entrada del jugador
    /// cerrada (<see cref="PlayerInputReader.Gate"/>), el cursor libre (<see cref="FocusMode"/>), la HUD oculta y la camara
    /// del jugador orbitando la torre (se apaga el CinemachineBrain mientras tanto). Botones: Modo Sandbox (entrega el
    /// control), Modo Duelo (deshabilitado), Informacion (grimorio a pantalla completa; al cerrarlo vuelve al menu),
    /// Creditos y Salir. Desde el juego se vuelve con "Menú principal" en Opciones del grimorio
    /// (<see cref="Grimoire.MainMenuRequested"/>). En VR no aparece: se entra directo a Sandbox.
    /// Navegacion: mouse, flechas / W S / cruceta y Enter / Espacio. Sin GC por frame (motas y camara solo con structs).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public partial class MainMenu : MonoBehaviour
    {
        [SerializeField, Tooltip("Entrada del jugador (se cierra mientras el menu esta en pantalla).")]
        private PlayerInputReader input;
        [SerializeField, Tooltip("Centro de la orbita cinematica (la torre).")]
        private Transform orbitCenter;
        [SerializeField, Tooltip("Componentes que mueven la camara en juego (CinemachineBrain): se apagan en el menu.")]
        private Behaviour[] cameraDrivers;
        [SerializeField, Tooltip("Canvas uGUI de juego (mira, depuracion) que se ocultan en el menu.")]
        private UnityEngine.Canvas[] hiddenCanvases;
        [SerializeField, Tooltip("Mostrar el menu al arrancar (apagado: se entra directo a Sandbox).")]
        private bool showOnStart = true;

        [Header("Orbita")]
        [SerializeField] private float orbitRadius = 34f;
        [SerializeField, Tooltip("Altura de la camara sobre la base de la torre.")]
        private float orbitHeight = 12f;
        [SerializeField, Tooltip("Altura del punto mirado sobre la base de la torre.")]
        private float lookHeight = 9f;
        [SerializeField, Tooltip("Grados por segundo.")]
        private float orbitSpeed = 3.5f;
        [SerializeField, Tooltip("Angulo inicial (grados; 180 = del lado de la puerta, -Z).")]
        private float startAngle = 200f;
        [SerializeField, Tooltip("Giro del encuadre (grados) para que la torre quede a la derecha de la columna.")]
        private float framingYaw = -15f;

        private const int MoteCount = 26;
        private const float FadeSeconds = 0.45f;
        private const float BlendSeconds = 1.6f;

        private enum State { Playing, Menu, Credits, Info, Net }

        public static MainMenu Instance { get; private set; }

        /// <summary>True mientras el menu (o algo abierto desde el, como Informacion) tiene el control.</summary>
        public static bool IsActive => Instance != null && Instance.state != State.Playing;

        /// <summary>El menu tomo (true) o entrego (false) el control.</summary>
        public static event Action<bool> ActiveChanged;

        private UIDocument document;
        private VisualElement menu, motesBox, creditsPanel, crestGlow;
        private Button sandbox, duel, info, credits, quit, creditsBack;
        private readonly List<Button> order = new List<Button>(6);
        private bool bound;

        private State state = State.Playing;
        private float revealAt = -1f, hideAt = -1f, creditsRevealAt = -1f, creditsHideAt = -1f;

        // Camara.
        private Camera cam;
        private Vector3 savedLocalPos;
        private Quaternion savedLocalRot = Quaternion.identity;
        private bool cameraTaken;
        private float angle;
        private Vector3 blendFromPos;
        private Quaternion blendFromRot;
        private float blendStart = -1f;

        // Motas.
        private readonly VisualElement[] motes = new VisualElement[MoteCount];
        private readonly Vector4[] moteSeeds = new Vector4[MoteCount]; // x base (0..1), velocidad, fase, amplitud

        private void Awake()
        {
            Instance = this;
            document = GetComponent<UIDocument>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
        }

        private void OnEnable()
        {
            Grimoire.MainMenuRequested += ShowMenu;
            Grimoire.Closed += OnGrimoireClosed;
            Net.NetSession.Disconnected += OnNetLost;
        }

        private void OnDisable()
        {
            Grimoire.MainMenuRequested -= ShowMenu;
            Grimoire.Closed -= OnGrimoireClosed;
            Net.NetSession.Disconnected -= OnNetLost;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            bound = Bind();
            if (bound) menu.AddToClassList("mm--hidden");
            if (!showOnStart || PlatformDetector.IsVR || !bound) return;
            ShowMenu();
        }

        // ---------------------------------------------------------------- armado

        private bool Bind()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            menu = root?.Q("menu");
            if (menu == null) return false;
            motesBox = menu.Q("motes");
            creditsPanel = menu.Q("credits-panel");
            crestGlow = menu.Q(className: "mm-crest-glow");
            sandbox = menu.Q<Button>("sandbox");
            duel = menu.Q<Button>("duel");
            info = menu.Q<Button>("info");
            credits = menu.Q<Button>("credits");
            quit = menu.Q<Button>("quit");
            creditsBack = menu.Q<Button>("credits-back");

            sandbox.clicked += EnterSandbox;
            info.clicked += OpenInfo;
            credits.clicked += OpenCredits;
            quit.clicked += Quit;
            creditsBack.clicked += CloseCredits;
            BindNet();
            menu.Q("credits-backdrop").RegisterCallback<ClickEvent>(_ => CloseCredits());
            duel.clicked += OpenDuel;

            order.Clear();
            order.Add(sandbox);
            order.Add(multiplayer);
            order.Add(info);
            order.Add(credits);
            order.Add(quit);
            // Entrada escalonada de los botones.
            Button[] all = { sandbox, multiplayer, duel, info, credits, quit };
            for (int i = 0; i < all.Length; i++)
            {
                Button b = all[i];
                b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledSelf && state == State.Menu) b.Focus(); });
                float d = 0.18f + i * 0.07f;
                b.style.transitionDelay = new List<TimeValue> { new TimeValue(d), new TimeValue(d), new TimeValue(0f), new TimeValue(0f) };
            }
            creditsBack.RegisterCallback<PointerEnterEvent>(_ => creditsBack.Focus());

            // La navegacion la maneja este script (teclado y cruceta): se ignoran los eventos de navegacion del panel
            // para que Enter no apriete dos veces.
            menu.RegisterCallback<NavigationMoveEvent>(e => { e.StopPropagation(); menu.focusController?.IgnoreEvent(e); }, TrickleDown.TrickleDown);
            menu.RegisterCallback<NavigationSubmitEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);

            Label version = menu.Q<Label>("version");
            if (version != null) version.text = "v" + Application.version + "  ·  prototipo";

            BuildMotes();
            return true;
        }

        private void BuildMotes()
        {
            var rng = new System.Random(1337);
            for (int i = 0; i < MoteCount; i++)
            {
                var m = new VisualElement { pickingMode = PickingMode.Ignore };
                m.AddToClassList("mm-mote");
                if (i % 4 == 0) m.AddToClassList("mm-mote--violet");
                float size = 5f + (float)rng.NextDouble() * 6f;
                m.style.width = size;
                m.style.height = size;
                motesBox.Add(m);
                motes[i] = m;
                moteSeeds[i] = new Vector4((float)rng.NextDouble(), 0.012f + (float)rng.NextDouble() * 0.03f,
                    (float)rng.NextDouble() * 10f, 10f + (float)rng.NextDouble() * 30f);
            }
        }

        // ---------------------------------------------------------------- acciones

        /// <summary>Muestra el menu y toma el control (al arrancar o desde "Menú principal" del grimorio).</summary>
        public void ShowMenu()
        {
            if (!bound) bound = Bind();
            if (!bound) return;
            bool wasPlaying = state == State.Playing;
            // volver al menu principal desde el juego = salir de la sala
            if (wasPlaying && Net.NetSession.Exists && Net.NetSession.Instance.IsConnected) { Net.NetSession.Instance.Leave(); ResetWorldAfterNet(); }
            if (BookReaderUI.IsOpen) BookReaderUI.CloseReader();
            state = State.Menu;
            if (input != null) input.Gate = InputGate.None;
            FocusMode.Enter();
            if (!cameraTaken) TakeCamera(!wasPlaying || Time.timeSinceLevelLoad < 0.5f);
            SetCanvases(false);

            hideAt = -1f;
            menu.RemoveFromClassList("mm--hidden");
            menu.RemoveFromClassList("mm--fading");
            menu.AddToClassList("mm--enter");
            revealAt = Time.unscaledTime + 0.05f;
            HideCreditsNow();
            RefreshNetView();
            if (wasPlaying) ActiveChanged?.Invoke(true);
        }

        /// <summary>Modo Sandbox: entrega el control al jugador (publico para el harness).</summary>
        public void EnterSandbox()
        {
            if (state == State.Playing) return;
            Grimoire book = Grimoire.Instance;
            state = State.Playing;
            if (book != null && book.IsOpen) book.Close();
            if (input != null) input.Gate = InputGate.Full;
            ReleaseCamera();
            SetCanvases(true);
            FocusMode.Exit();
            FadeOutMenu();
            ActiveChanged?.Invoke(false);
        }

        /// <summary>Informacion: el grimorio a pantalla completa; al cerrarlo (Tab / Esc) se vuelve al menu.</summary>
        public void OpenInfo()
        {
            Grimoire book = Grimoire.Instance;
            if (book == null || state != State.Menu) return;
            state = State.Info;
            if (input != null) input.Gate = InputGate.BookOnly;
            FadeOutMenu();
            book.OpenFullscreen();
            if (!book.IsOpen) ShowMenu();
        }

        public void OpenCredits()
        {
            if (state != State.Menu) return;
            state = State.Credits;
            creditsHideAt = -1f;
            creditsPanel.RemoveFromClassList("mm-credits--hidden");
            creditsPanel.AddToClassList("mm-credits--fading");
            creditsRevealAt = Time.unscaledTime + 0.03f;
            creditsBack.Focus();
        }

        public void CloseCredits()
        {
            if (state != State.Credits) return;
            state = State.Menu;
            creditsPanel.AddToClassList("mm-credits--fading");
            creditsHideAt = Time.unscaledTime + 0.25f;
            credits.Focus();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private void OnGrimoireClosed()
        {
            // Cerrar el grimorio abierto desde Informacion vuelve al menu (no se reanima la entrada de la columna).
            if (state != State.Info) return;
            ShowMenu();
        }

        private void FadeOutMenu()
        {
            menu.AddToClassList("mm--fading");
            revealAt = -1f;
            hideAt = Time.unscaledTime + FadeSeconds;
            if (menu.focusController?.focusedElement is VisualElement f) f.Blur();
        }

        private void HideCreditsNow()
        {
            creditsRevealAt = creditsHideAt = -1f;
            creditsPanel.AddToClassList("mm-credits--hidden");
            creditsPanel.RemoveFromClassList("mm-credits--fading");
            HideNetNow();
        }

        private void SetCanvases(bool on)
        {
            if (hiddenCanvases == null) return;
            for (int i = 0; i < hiddenCanvases.Length; i++) if (hiddenCanvases[i] != null) hiddenCanvases[i].enabled = on;
        }

        // ---------------------------------------------------------------- camara

        private void TakeCamera(bool snap)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            cameraTaken = true;
            savedLocalPos = cam.transform.localPosition;
            savedLocalRot = cam.transform.localRotation;
            if (cameraDrivers != null)
                for (int i = 0; i < cameraDrivers.Length; i++) if (cameraDrivers[i] != null) cameraDrivers[i].enabled = false;
            if (snap)
            {
                angle = startAngle;
                blendStart = -1f;
            }
            else
            {
                // Desde el juego: la orbita arranca del lado donde esta el jugador y la camara se desliza hasta ella.
                Vector3 c = Center();
                Vector3 d = cam.transform.position - c;
                angle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                blendFromPos = cam.transform.position;
                blendFromRot = cam.transform.rotation;
                blendStart = Time.unscaledTime;
            }
            PlaceCamera();
        }

        private void ReleaseCamera()
        {
            if (!cameraTaken || cam == null) return;
            cameraTaken = false;
            cam.transform.localPosition = savedLocalPos;
            cam.transform.localRotation = savedLocalRot;
            if (cameraDrivers != null)
                for (int i = 0; i < cameraDrivers.Length; i++) if (cameraDrivers[i] != null) cameraDrivers[i].enabled = true;
        }

        private Vector3 Center() => orbitCenter != null ? orbitCenter.position : Vector3.zero;

        private void PlaceCamera()
        {
            Vector3 c = Center();
            float rad = angle * Mathf.Deg2Rad;
            float bob = Mathf.Sin(Time.unscaledTime * 0.35f) * 0.6f;
            Vector3 pos = c + new Vector3(Mathf.Sin(rad) * orbitRadius, orbitHeight + bob, Mathf.Cos(rad) * orbitRadius);
            Vector3 target = c + Vector3.up * lookHeight;
            Quaternion rot = Quaternion.AngleAxis(framingYaw, Vector3.up) * Quaternion.LookRotation(target - pos, Vector3.up);
            if (blendStart >= 0f)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - blendStart) / BlendSeconds);
                t = t * t * (3f - 2f * t);
                pos = Vector3.Lerp(blendFromPos, pos, t);
                rot = Quaternion.Slerp(blendFromRot, rot, t);
                if (t >= 1f) blendStart = -1f;
            }
            cam.transform.SetPositionAndRotation(pos, rot);
        }

        // ---------------------------------------------------------------- por frame

        private void Update()
        {
            if (!bound) return;
            float now = Time.unscaledTime;
            if (revealAt > 0f && now >= revealAt)
            {
                revealAt = -1f;
                menu.RemoveFromClassList("mm--enter");
                if (state == State.Menu) sandbox.Focus();
            }
            if (hideAt > 0f && now >= hideAt)
            {
                hideAt = -1f;
                if (state == State.Playing || state == State.Info)
                {
                    menu.AddToClassList("mm--hidden");
                    menu.RemoveFromClassList("mm--fading");
                }
            }
            TickNet(now);
            if (creditsRevealAt > 0f && now >= creditsRevealAt) { creditsRevealAt = -1f; creditsPanel.RemoveFromClassList("mm-credits--fading"); }
            if (creditsHideAt > 0f && now >= creditsHideAt) { creditsHideAt = -1f; if (state != State.Credits) HideCreditsNow(); }

            if (state == State.Playing) return;
            // Otro sistema pudo soltar el cursor (p. ej. el grimorio al salir de su modo fijo): el menu lo quiere libre.
            if (state != State.Info && !FocusMode.Active) FocusMode.Enter();
            if (state == State.Menu || state == State.Credits || state == State.Net)
            {
                HandleKeys();
                AnimateMotes(now);
            }
        }

        private void LateUpdate()
        {
            if (state == State.Playing || cam == null || !cameraTaken) return;
            angle += orbitSpeed * Time.unscaledDeltaTime;
            if (angle > 360f) angle -= 360f;
            PlaceCamera();
        }

        private void HandleKeys()
        {
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            bool back = (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
            bool submit = (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                || (g != null && g.buttonSouth.wasPressedThisFrame);
            int move = 0;
            if ((k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)) || (g != null && g.dpad.down.wasPressedThisFrame)) move = 1;
            else if ((k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame)) || (g != null && g.dpad.up.wasPressedThisFrame)) move = -1;

            if (state == State.Credits)
            {
                if (back || submit) CloseCredits();
                return;
            }
            if (state == State.Net)
            {
                if (back) CloseNet();
                return;
            }
            if (revealAt > 0f) return;
            Button focused = Focused();
            if (move != 0)
            {
                int i = focused != null ? order.IndexOf(focused) : -1;
                i = i < 0 ? 0 : (i + move + order.Count) % order.Count;
                order[i].Focus();
            }
            else if (submit)
            {
                if (focused == sandbox) EnterSandbox();
                else if (focused == multiplayer) OpenNet();
                else if (focused == info) OpenInfo();
                else if (focused == credits) OpenCredits();
                else if (focused == quit) Quit();
                else sandbox.Focus();
            }
        }

        private Button Focused()
        {
            Focusable f = menu.focusController?.focusedElement;
            for (int i = 0; i < order.Count; i++) if (f == order[i]) return order[i];
            return null;
        }

        private void AnimateMotes(float t)
        {
            float w = motesBox.resolvedStyle.width, h = motesBox.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0f) return;
            for (int i = 0; i < MoteCount; i++)
            {
                Vector4 s = moteSeeds[i];
                float y = 1.08f - Mathf.Repeat(s.z * 0.1f + t * s.y, 1.16f);
                float x = s.x * w + Mathf.Sin(t * 0.5f + s.z) * s.w;
                float twinkle = 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * (1.2f + s.y * 30f) + s.z * 3f));
                // Se apagan al acercarse a los bordes de arriba y abajo.
                float edge = Mathf.Clamp01(Mathf.Min(y, 1f - y) * 6f);
                VisualElement m = motes[i];
                m.style.translate = new Translate(x, y * h);
                m.style.opacity = twinkle * edge;
            }
            if (crestGlow != null) crestGlow.style.opacity = 0.45f + 0.2f * Mathf.Sin(t * 1.7f);
        }
    }
}
