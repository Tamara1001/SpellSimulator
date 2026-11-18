using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpellSimulator.Player
{
    /// <summary>
    /// [VR] Menu y ayuda de controles en un panel flotante (la UI de pantalla no se ve en el visor). Se abre y cierra con el clic del
    /// stick derecho; aparece frente a la cabeza y se queda ahi. Se apunta con el rayo de cualquier mano y se elige con su gatillo.
    /// Mientras esta abierto la entrada del juego queda bloqueada (no se dibuja ni se agarra). Se arma en codigo, sin escena ni prefab.
    /// </summary>
    public class VrMenu : MonoBehaviour
    {
        private const float PanelW = 600f, PanelH = 880f, Pixel = 0.0013f, Distance = 1.15f;
        private const float ButtonH = 66f, ButtonGap = 10f;

        private struct Item
        {
            public RectTransform rect;
            public Image image;
            public TextMeshProUGUI label;
            public System.Action click;
        }

        private PlayerInteractor interactor;
        private PlayerInputReader input;
        private Transform leftHand, rightHand;
        private UnityEngine.Canvas canvas;
        private TextMeshProUGUI help;
        private readonly List<Item> items = new List<Item>();
        private LineRenderer lineL, lineR;
        private InputAction toggleAction;
        private InputGate previousGate = InputGate.Full;
        private bool open, showHelp;
        private bool wasClickL, wasClickR;
        private int mainHandItem;
        private static readonly Color Idle = new Color(0.22f, 0.15f, 0.3f, 0.95f);
        private static readonly Color Hover = new Color(0.45f, 0.3f, 0.6f, 1f);

        public bool IsOpen => open;

        public void Init(PlayerInteractor owner, PlayerInputReader reader, Transform left, Transform right)
        {
            interactor = owner;
            input = reader;
            leftHand = left;
            rightHand = right;
            toggleAction = new InputAction("VrMenu", InputActionType.Button);
            toggleAction.AddBinding("<XRController>{RightHand}/thumbstickClicked");
            toggleAction.Enable();
            Build();
            Hud.Notify("vrmenu", "Clic del stick der.: menú y controles", 6f);
        }

        private void OnDestroy()
        {
            if (toggleAction != null) { toggleAction.Disable(); toggleAction.Dispose(); }
        }

        private void Build()
        {
            var go = new GameObject("VrMenuCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(PanelW, PanelH);
            rect.localScale = Vector3.one * Pixel;

            var bg = new GameObject("Fondo").AddComponent<Image>();
            bg.transform.SetParent(go.transform, false);
            bg.color = new Color(0.06f, 0.04f, 0.09f, 0.94f);
            bg.raycastTarget = false;
            Stretch(bg.rectTransform, 0f);

            AddLabel(go.transform, "Titulo", "<b>Menú</b>", 40f, new Vector2(0f, PanelH * 0.5f - 40f), new Vector2(PanelW - 40f, 60f), TextAlignmentOptions.Center);

            float y = PanelH * 0.5f - 110f;
            AddButton(go.transform, "Continuar", ref y, Close);
            AddButton(go.transform, "Controles", ref y, () => { showHelp = !showHelp; Refresh(); });
            mainHandItem = items.Count;
            AddButton(go.transform, "Mano principal", ref y, ToggleMainHand);
            AddButton(go.transform, "Saltear tutorial", ref y, () => { Close(); var s = FindFirstObjectByType<PlayerSpawn>(); if (s != null) s.Begin(true); });
            AddButton(go.transform, "Ir al Nexo", ref y, () => { Close(); var s = FindFirstObjectByType<PlayerSpawn>(); if (s != null) s.Respawn(); });
            AddButton(go.transform, "Salir del juego", ref y, Quit);

            help = AddLabel(go.transform, "Ayuda", "", 25f, new Vector2(0f, y - 190f), new Vector2(PanelW - 40f, 380f), TextAlignmentOptions.TopLeft);
            help.text = HelpText;

            lineL = MakeLine(leftHand);
            lineR = MakeLine(rightHand);
            canvas.enabled = false;
            Refresh();
        }

        private const string HelpText =
            "<b>Gatillo</b>: usar la herramienta y dibujar (a distancia, o apoyando la punta en el papel)\n" +
            "<b>Grip</b>: agarrar con esa mano. Apretón corto = queda pegado (otro apretón lo suelta). Soltar con envión = arrojar; quieto = queda flotando\n" +
            "<b>A / X</b> de la mano libre: saltar. De la mano con la herramienta: deshacer el último trazo\n" +
            "<b>Borrar</b>: tomá la goma con el grip y pasala por el trazo con el gatillo\n" +
            "<b>B</b>: cambiar de objeto · <b>Y</b>: cambiar tinta (mantener: reiniciar)\n" +
            "<b>Stick izq.</b>: caminar hacia donde mirás · clic: agacharse\n" +
            "<b>Stick der.</b>: girar (golpe) / pasar página · clic: este menú\n" +
            "<b>Botón menú</b> izq.: grimorio";

        private static void Stretch(RectTransform r, float margin)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(margin, margin); r.offsetMax = new Vector2(-margin, -margin);
        }

        private static TextMeshProUGUI AddLabel(Transform parent, string name, string text, float size, Vector2 pos, Vector2 box, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = new Color(0.95f, 0.9f, 0.8f);
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.text = text;
            t.rectTransform.anchoredPosition = pos;
            t.rectTransform.sizeDelta = box;
            return t;
        }

        private void AddButton(Transform parent, string text, ref float y, System.Action click)
        {
            var go = new GameObject("Boton_" + text);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = Idle;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchoredPosition = new Vector2(0f, y - ButtonH * 0.5f);
            r.sizeDelta = new Vector2(PanelW - 80f, ButtonH);
            var label = AddLabel(go.transform, "Texto", text, 34f, Vector2.zero, r.sizeDelta, TextAlignmentOptions.Center);
            items.Add(new Item { rect = r, image = img, label = label, click = click });
            y -= ButtonH + ButtonGap;
        }

        private LineRenderer MakeLine(Transform host)
        {
            if (host == null) return null;
            var go = new GameObject("VrMenuRay");
            go.transform.SetParent(host, false);
            var line = go.AddComponent<LineRenderer>();
            line.startWidth = 0.006f; line.endWidth = 0.003f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = Color.white; line.endColor = new Color(1f, 1f, 1f, 0.2f);
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.enabled = false;
            return line;
        }

        private void Refresh()
        {
            items[mainHandItem].label.text = "Mano principal: " + (PlayerSettings.MainHandRight ? "derecha" : "izquierda");
            help.gameObject.SetActive(showHelp);
        }

        private void ToggleMainHand()
        {
            bool right = !PlayerSettings.MainHandRight;
            PlayerSettings.MainHandRight = right;
            interactor.SetMainHandVr(right);
            Refresh();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Open()
        {
            Camera head = Camera.main;
            if (head == null) return;
            Vector3 flat = head.transform.forward; flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            Vector3 pos = head.transform.position + flat.normalized * Distance + Vector3.down * 0.05f;
            canvas.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - head.transform.position, Vector3.up));
            canvas.enabled = true;
            open = true;
            previousGate = input.Gate;
            input.Gate = InputGate.None;
            Refresh();
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            canvas.enabled = false;
            if (lineL != null) lineL.enabled = false;
            if (lineR != null) lineR.enabled = false;
            input.Gate = previousGate;
            // El gatillo que acaba de elegir no debe empezar un trazo.
            wasClickL = wasClickR = true;
        }

        private void Update()
        {
            if (toggleAction != null && toggleAction.WasPressedThisFrame())
            {
                if (open) Close(); else Open();
                return;
            }
            if (!open) return;
            bool clickL = input.VrTrigger(false) > 0.6f, clickR = input.VrTrigger(true) > 0.6f;
            int hoverL = Point(leftHand, lineL), hoverR = Point(rightHand, lineR);
            for (int i = 0; i < items.Count; i++) items[i].image.color = i == hoverL || i == hoverR ? Hover : Idle;
            int pick = -1;
            if (clickL && !wasClickL && hoverL >= 0) pick = hoverL;
            else if (clickR && !wasClickR && hoverR >= 0) pick = hoverR;
            wasClickL = clickL;
            wasClickR = clickR;
            if (pick >= 0) items[pick].click?.Invoke();
        }

        /// <summary>Apunta con el rayo de una mano: dibuja la linea y devuelve el boton bajo la punta (-1 si ninguno).</summary>
        private int Point(Transform hand, LineRenderer line)
        {
            if (hand == null) return -1;
            Vector3 origin = hand.position, dir = hand.forward;
            Transform plane = canvas.transform;
            Vector3 normal = plane.forward;
            float denom = Vector3.Dot(normal, dir);
            float t = Mathf.Abs(denom) > 1e-4f ? Vector3.Dot(plane.position - origin, normal) / denom : -1f;
            if (line != null)
            {
                line.enabled = true;
                line.SetPosition(0, origin);
                line.SetPosition(1, origin + dir * (t > 0f && t < 4f ? t : 1.5f));
            }
            if (t <= 0f || t > 4f) return -1;
            Vector3 local = plane.InverseTransformPoint(origin + dir * t);
            for (int i = 0; i < items.Count; i++)
            {
                Rect r = items[i].rect.rect;
                Vector2 center = items[i].rect.anchoredPosition;
                if (Mathf.Abs(local.x - center.x) <= r.width * 0.5f && Mathf.Abs(local.y - center.y) <= r.height * 0.5f) return i;
            }
            return -1;
        }
    }
}
