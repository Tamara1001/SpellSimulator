using UnityEngine;
using TMPro;

namespace SpellSimulator.Player
{
    /// <summary>
    /// [VR] Cartel flotante con TODOS los controles explicados simple. Aparece solo al empezar (unos segundos) y se abre / cierra
    /// manteniendo 0,8 s el clic del stick izquierdo (<see cref="PlayerInputReader.VrControlsToggle"/>). Se planta delante de la cabeza
    /// al abrirse y se queda quieto (no sigue la mirada), mirando al jugador. Sin interfaz de PC: es texto 3D (TextMeshPro).
    /// </summary>
    public class VrControlsPanel : MonoBehaviour
    {
        private const float Distance = 1.25f, PanelWidth = 1.7f, PanelHeight = 1.0f, AutoCloseSeconds = 15f, AutoOpenDelay = 3f;

        private const string Title = "<b>CONTROLES VR</b>";
        private const string Left =
            "<b><color=#FFD27A>MANO IZQUIERDA</color></b>\n" +
            "<b>Stick</b>: caminar\n" +
            "<b>Clic del stick</b>: toque = agacharse / levantarse\n" +
            "     mantener 1 s = abrir / cerrar este cartel\n" +
            "     seguir 3 s = recalibrar la altura\n" +
            "<b>Gatillo</b>: usar (si lleva la pluma)\n" +
            "<b>Grip</b>: agarrar\n" +
            "<b>X</b>: saltar (si la pluma está en la otra mano)\n" +
            "<b>Y</b>: toque = cambiar tinta | mantener = reiniciar\n" +
            "<b>Menú</b>: grimorio (libro de hechizos)";
        private const string Right =
            "<b><color=#7AD2FF>MANO DERECHA</color></b>\n" +
            "<b>Stick izq. o der.</b> (un golpe): girar / pasar página\n" +
            "<b>Stick arriba o abajo</b> (un golpe): tamaño de la goma\n" +
            "<b>Clic del stick</b>: menú del juego\n" +
            "<b>Gatillo</b>: dibujar / usar la herramienta\n" +
            "<b>Grip</b>: agarrar. Toque corto = queda pegado\n" +
            "     (otro grip lo suelta). Soltar moviendo = lanzar\n" +
            "<b>A</b>: deshacer el último trazo (o saltar)\n" +
            "<b>B</b>: toque = otro objeto | mantener = arrojar";
        private const string Footer =
            "<color=#CFCFCF>Dibujar: apoyá la punta de la pluma en el papel (o apuntá y apretá el gatillo).\n" +
            "La herramienta va en la mano que la agarró. Agacharte de verdad también funciona.</color>";

        private PlayerInputReader input;
        private GameObject root;
        private bool open;
        private float closeAt;
        private float openAt;
        private bool autoShown;

        public void Init(PlayerInputReader reader)
        {
            input = reader;
            openAt = Time.unscaledTime + AutoOpenDelay;
            autoShown = true; // Apertura manual: no tapa la visión al inicio (UI2-05)
            Hud.Notify("vrcontrols", "Mantené el clic del stick izq.: controles", 8f);
        }

        private void Update()
        {
            if (input == null) return;
            if (!autoShown && Time.unscaledTime >= openAt && Camera.main != null)
            {
                autoShown = true;
                if (!open) Show();
            }
            if (input.VrControlsToggle)
            {
                autoShown = true;
                if (open) Hide(); else Show();
            }
            if (open && (Time.unscaledTime >= closeAt || input.MoveValue.sqrMagnitude > 0.2f)) Hide();
        }

        private void OnDestroy() { if (root != null) Destroy(root); }

        private void Show()
        {
            Camera head = Camera.main;
            if (head == null) return;
            if (root == null) Build();
            Vector3 flat = head.transform.forward; flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            flat.Normalize();
            Vector3 position = head.transform.position + flat * Distance;
            position.y = head.transform.position.y - 0.05f;
            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.transform.position, Vector3.up));
            root.SetActive(true);
            open = true;
            closeAt = Time.unscaledTime + AutoCloseSeconds;
        }

        /// <summary>Arma el cartel sin mostrarlo (lo usan las capturas de prueba).</summary>
        public GameObject BuildPreview()
        {
            if (root == null) Build();
            return root;
        }

        private void Hide()
        {
            if (root != null) root.SetActive(false);
            open = false;
        }

        private void Build()
        {
            root = new GameObject("VrControlsPanel");
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = "Fondo";
            Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(root.transform, false);
            back.transform.localScale = new Vector3(PanelWidth, PanelHeight, 1f);
            var rend = back.GetComponent<MeshRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.05f, 0.06f, 0.1f, 0.92f), renderQueue = 3900 };

            // El panel mira con +Z alejandose del jugador: el quad (cara visible en -Z) y el texto TMP (legible desde -Z) se ven bien.
            AddText("Titulo", Title, 0.07f, TextAlignmentOptions.Center, new Vector2(0f, PanelHeight * 0.5f - 0.08f), new Vector2(PanelWidth, 0.12f));
            AddText("Izquierda", Left, 0.030f, TextAlignmentOptions.TopLeft, new Vector2(-PanelWidth * 0.25f - 0.02f, 0.05f), new Vector2(PanelWidth * 0.5f - 0.06f, 0.7f));
            AddText("Derecha", Right, 0.030f, TextAlignmentOptions.TopLeft, new Vector2(PanelWidth * 0.25f + 0.02f, 0.05f), new Vector2(PanelWidth * 0.5f - 0.06f, 0.7f));
            AddText("Pie", Footer, 0.026f, TextAlignmentOptions.Center, new Vector2(0f, -PanelHeight * 0.5f + 0.12f), new Vector2(PanelWidth - 0.1f, 0.2f));
        }

        private void AddText(string name, string text, float size, TextAlignmentOptions align, Vector2 center, Vector2 rect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(center.x, center.y, -0.005f);
            go.transform.localRotation = Quaternion.identity;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size * FontScale; // TMP 3D: el tamaño se mide en "puntos"; FontScale lo pasa a metros del mundo
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.richText = true;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = rect;
        }

        /// <summary>Tamaño de fuente (TMP 3D) por metro de alto de letra (1 punto = 1 unidad de mundo con escala 1).</summary>
        private const float FontScale = 10f;
    }
}
