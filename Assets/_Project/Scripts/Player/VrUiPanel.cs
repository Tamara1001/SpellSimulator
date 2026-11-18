using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using SpellSimulator.Interfaces;
using SpellSimulator.UI;

namespace SpellSimulator.Player
{
    /// <summary>
    /// [VR] Muestra la MISMA interfaz de PC (HUD, menu principal; UI Toolkit) en un panel flotante: el panel se dibuja en una
    /// RenderTexture de 1920x1080 (la resolucion de referencia, asi el diseño queda igual) y esa textura va en un cuadro delante de la
    /// cabeza. En juego el cuadro sigue la mirada con retraso (no queda pegado a la cara); con el menu abierto se queda quieto y se
    /// apunta con el rayo de cualquier mano: el gatillo aprieta el boton apuntado. El clic del stick derecho abre y cierra el menu.
    /// El PanelSettings es un asset compartido: se le pone la textura al empezar y se restaura al salir.
    /// </summary>
    public class VrUiPanel : MonoBehaviour
    {
        private const int Width = 1920, Height = 1080;
        private const float HudWidth = 1.7f, HudDistance = 1.6f, MenuWidth = 2.0f, MenuDistance = 1.3f;
        private const float FollowDeadZone = 22f, FollowSpeed = 2.5f;

        private PlayerInteractor interactor;
        private PlayerInputReader input;
        private UIDocument document;
        private PanelSettings settings;
        private RenderTexture texture;
        private Transform quad;
        private InputAction toggleAction;
        private RenderTexture prevTarget;
        private bool prevClear;
        private Color prevClearColor;
        private Camera head;
        private float yaw;
        private Vector3 position;
        private bool placed, wasMenu;
        private bool wasClickL, wasClickR;
        private VisualElement hovered;

        public void Init(PlayerInteractor owner, PlayerInputReader reader)
        {
            interactor = owner;
            input = reader;
            document = FindFirstObjectByType<UIDocument>();
            if (document == null || document.panelSettings == null) { Debug.LogWarning("[VrUiPanel] No hay UIDocument: sin interfaz en VR.", this); return; }
            settings = document.panelSettings;
            prevTarget = settings.targetTexture;
            prevClear = settings.clearColor;
            prevClearColor = settings.colorClearValue;
            texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "VrUiTexture", antiAliasing = 1 };
            texture.Create();
            settings.targetTexture = texture;
            settings.clearColor = true;
            settings.colorClearValue = Color.clear;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "VrUiQuad";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(null);
            quad = go.transform;
            var rend = go.GetComponent<MeshRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, renderQueue = 4000 };

            toggleAction = new InputAction("VrMenuToggle", InputActionType.Button);
            toggleAction.AddBinding("<XRController>{RightHand}/thumbstickClicked");
            toggleAction.Enable();
            Hud.Notify("vrmenu", "Clic del stick der.: menú", 6f);
        }

        private void OnDestroy() => Restore();
        private void OnApplicationQuit() => Restore();

        private void Restore()
        {
            if (toggleAction != null) { toggleAction.Disable(); toggleAction.Dispose(); toggleAction = null; }
            if (settings != null)
            {
                settings.targetTexture = prevTarget;
                settings.clearColor = prevClear;
                settings.colorClearValue = prevClearColor;
                settings = null;
            }
            if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
            if (quad != null) Destroy(quad.gameObject);
        }

        private void Update()
        {
            if (settings == null) return;
            if (toggleAction != null && toggleAction.WasPressedThisFrame() && MainMenu.Instance != null)
            {
                if (MainMenu.IsActive) MainMenu.Instance.EnterSandbox(); else MainMenu.Instance.ShowMenu();
            }
            if (MainMenu.IsActive) HandlePointers();
            else { ClearHover(); wasClickL = wasClickR = true; }
        }

        private void LateUpdate()
        {
            if (settings == null || quad == null) return;
            if (head == null) head = Camera.main;
            if (head == null) return;
            bool menu = MainMenu.IsActive;
            Transform h = head.transform;
            Vector3 flat = h.forward; flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            float headYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float distance = menu ? MenuDistance : HudDistance;
            float dt = Time.unscaledDeltaTime;

            if (!placed || menu != wasMenu)
            {
                // Al abrir el menu (o la primera vez) aparece justo adelante.
                yaw = headYaw;
                position = h.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance + Vector3.down * 0.05f;
                placed = true;
            }
            else if (!menu)
            {
                float delta = Mathf.DeltaAngle(yaw, headYaw);
                if (Mathf.Abs(delta) > FollowDeadZone) yaw += delta * (1f - Mathf.Exp(-FollowSpeed * dt));
                Vector3 target = h.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance + Vector3.down * 0.05f;
                position = Vector3.Lerp(position, target, 1f - Mathf.Exp(-6f * dt));
            }
            wasMenu = menu;

            float width = menu ? MenuWidth : HudWidth;
            quad.SetPositionAndRotation(position, Quaternion.LookRotation(position - h.position, Vector3.up));
            quad.localScale = new Vector3(width, width * Height / Width, 1f);
        }

        // ---------------------------------------------------------------- puntero

        private void HandlePointers()
        {
            VisualElement root = document.rootVisualElement;
            if (root == null || root.panel == null) return;
            bool clickL = input.VrTrigger(false) > 0.6f, clickR = input.VrTrigger(true) > 0.6f;
            Button hitL = PickButton(false, root), hitR = PickButton(true, root);
            Button preferred = interactor.VrRayRight ? (hitR ?? hitL) : (hitL ?? hitR);
            SetHover(preferred);

            Button pick = null;
            if (clickL && !wasClickL) pick = hitL;
            if (pick == null && clickR && !wasClickR) pick = hitR;
            wasClickL = clickL;
            wasClickR = clickR;
            if (pick == null) return;
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = pick;
                pick.SendEvent(submit);
            }
            Haptics.Pulse(HandType.Right, 0.3f, 0.05f);
        }

        /// <summary>Boton del menu bajo el rayo de esa mano (null si el rayo no toca el cuadro o no hay boton ahi).</summary>
        private Button PickButton(bool right, VisualElement root)
        {
            Ray ray = interactor.VrPointerRay(right);
            Vector3 normal = quad.forward;
            float denom = Vector3.Dot(normal, ray.direction);
            if (Mathf.Abs(denom) < 1e-4f) return null;
            float t = Vector3.Dot(quad.position - ray.origin, normal) / denom;
            if (t <= 0f || t > 5f) return null;
            Vector3 local = quad.InverseTransformPoint(ray.origin + ray.direction * t);
            if (Mathf.Abs(local.x) > 0.5f || Mathf.Abs(local.y) > 0.5f) return null;
            var panelPos = new Vector2((local.x + 0.5f) * Width, (0.5f - local.y) * Height);
            // Con ScaleWithScreenSize y la RT a la resolucion de referencia, 1 pixel de textura = 1 unidad del panel.
            VisualElement element = root.panel.Pick(panelPos);
            for (; element != null; element = element.parent) if (element is Button b) return b;
            return null;
        }

        private void SetHover(VisualElement target)
        {
            if (hovered == target) return;
            ClearHover();
            hovered = target;
            if (hovered != null) hovered.style.scale = new StyleScale(new Scale(new Vector3(1.05f, 1.05f, 1f)));
        }

        private void ClearHover()
        {
            if (hovered == null) return;
            hovered.style.scale = StyleKeyword.Null;
            hovered = null;
        }
    }
}
