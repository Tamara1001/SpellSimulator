using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Parte de <see cref="PlayerInteractor"/>: MIRAR. Rayo de la mira (o del controlador en VR), que se puede agarrar o
    /// apretar, pistas junto a la mira para la HUD y objetivo de la vista completa. Solo lectura del mundo: no cambia estado.
    /// </summary>
    public partial class PlayerInteractor
    {
        private static readonly RaycastHit[] hoverHits = new RaycastHit[24];

        private IInteractable currentHovered;
        private IPressable hoveredPressable;
        private IFlippable hoveredFlippable;
        private IInkSource hoveredInkSource;
        private float hoverDistance;

        /// <summary>Texto de lo que se mira (compatibilidad; la HUD nueva solo muestra la tecla).</summary>
        public string HoverPrompt { get; private set; } = "";
        /// <summary>True si lo que se mira se aprieta con clic (botones del grimorio) y no con E.</summary>
        public bool HoverUsesClick => hoveredPressable != null && hoveredPressable.PressWithClick;
        /// <summary>True si lo que se mira se puede agarrar o apretar con E (la HUD muestra el simbolo [E]).</summary>
        public bool HoverInteractable => (hoveredPressable != null && !hoveredPressable.PressWithClick) || (hoveredPressable == null && currentHovered != null);
        /// <summary>Lo que se esta mirando y se puede agarrar o apretar (null si nada).</summary>
        public GameObject HoveredObject => currentHovered is MonoBehaviour mb ? mb.gameObject : hoveredPressable is MonoBehaviour pb ? pb.gameObject : null;

        /// <summary>Pistas junto a la mira ("E: Agarrar | V: Vista completa | R: Recargar tinta"), vacio si no hay nada que hacer.</summary>
        public string HoverHints { get; private set; } = "";

        // Superficie mirada que admite vista completa (V): un lienzo, o una parte de una superficie del ambiente.
        private DrawingCanvas viewCanvas;
        private bool viewSurface;
        private Vector3 viewPoint, viewNormal;

        private static readonly string[] HoverHintTable = BuildHoverHintTable();
        private IPressable hintPressable;
        private string hintPressableText = "";
        private string hintPressablePrompt;

        // Pistas del tintero apuntado. Fijo: "Tintero fijo | R: Recargar tinta". Portatil: "<clase> | E: Guardar tintero".
        // Un string por (clase, fijo/portatil), armado la primera vez y reutilizado: sin allocations por frame.
        private readonly string[,] inkwellHints = new string[3, 2];

        private static string[] BuildHoverHintTable()
        {
            // Mascara: 1 = agarrar, 2 = vista completa, 4 = recargar tinta.
            var table = new string[8];
            for (int mask = 0; mask < 8; mask++)
            {
                string text = "";
                if ((mask & 1) != 0) text += "E: Agarrar";
                if ((mask & 2) != 0) text += (text.Length > 0 ? " | " : "") + "Tab: Vista completa";
                if ((mask & 4) != 0) text += (text.Length > 0 ? " | " : "") + "R: Recargar tinta";
                table[mask] = text;
            }
            return table;
        }

        private string ComputeHoverHints()
        {
            if (hoveredPressable != null)
            {
                if (hoveredPressable.PressWithClick) return "";
                // Se rearma si cambia lo mirado o su texto (puerta: Abrir/Cerrar). Los textos son constantes cacheadas:
                // comparar referencias no aloca.
                string p = hoveredPressable.GetPressPrompt();
                if (!ReferenceEquals(hintPressable, hoveredPressable) || !ReferenceEquals(hintPressablePrompt, p))
                {
                    hintPressable = hoveredPressable;
                    hintPressablePrompt = p;
                    hintPressableText = string.IsNullOrEmpty(p) ? "E" : "E: " + p;
                }
                return hintPressableText;
            }
            hintPressable = null;
            int mask = 0;
            if (currentHovered != null) mask |= 1;
            if (viewCanvas != null || viewSurface) mask |= 2;
            if (hoveredInkSource != null && !(hoveredInkSource is InkSupply)) mask |= 4;
            if ((mask & 4) != 0 && hoveredInkSource is World.Inkwell well) return InkwellHint(mask, well);
            return HoverHintTable[mask];
        }

        private string InkwellHint(int mask, World.Inkwell well)
        {
            int k = (int)well.Kind;
            int f = well.Portable ? 1 : 0;
            string text = inkwellHints[k, f];
            if (text != null) return text;
            text = well.Portable
                ? SpellSimulator.Core.InkKindInfo.DisplayName(well.Kind) + " | E: Guardar tintero"
                : "Tintero fijo (" + SpellSimulator.Core.InkKindInfo.DisplayName(well.Kind) + ") | R: Recargar tinta"
                    + (World.PhysicalTool.InkCycleAvailable ? " | V: cambiar tinta" : "");
            inkwellHints[k, f] = text;
            return text;
        }

        /// <summary>Cambio el stock de cartuchos: rehace las pistas de "V: cambiar tinta" (se cachean; no se arman por frame).</summary>
        private void RefreshInkCycleHints()
        {
            World.PhysicalTool.InkCycleAvailable = inkSupply != null && inkSupply.Cartridges.CanCycle;
            System.Array.Clear(inkwellHints, 0, inkwellHints.Length);
        }

        /// <summary>Decide si lo mirado admite vista completa: lienzos (no del grimorio) y superficies dibujables del ambiente (no el suelo).</summary>
        private void FindViewTarget(RaycastHit hit)
        {
            if (PlatformDetector.IsVR) return;
            Collider c = hit.collider;
            DrawingCanvas canvas = c.GetComponentInParent<DrawingCanvas>();
            if (canvas != null)
            {
                if (canvas.GetComponentInParent<Learning.Grimoire>() != null) return;
                viewCanvas = canvas;
                return;
            }
            if (c is TerrainCollider || hit.normal.y > 0.7f) return;
            if (c.GetComponentInParent<World.Inscribable>() == null) return;
            viewSurface = true;
            viewPoint = hit.point;
            viewNormal = hit.normal;
        }

        /// <summary>En vista completa no se mira nada: limpia lo apuntado y las pistas (la superficie de la vista se conserva).</summary>
        private void ClearHover()
        {
            currentHovered = null;
            hoveredPressable = null;
            hoveredFlippable = null;
            hoveredInkSource = null;
            HoverHints = "";
        }

        // ---------------------------------------------------------------- mirar

        private void HandleHover()
        {
            bool vrRay = PlatformDetector.IsVR && vrRightController != null;
            Transform origin = vrRay ? vrRightController : cameraTransform;
            if (origin == null) return;

            Vector3 direction = vrRay ? Quaternion.AngleAxis(vrRayPitchOffset, origin.right) * origin.forward : origin.forward;
            Vector3 end = origin.position + direction * interactRange;

            currentHovered = null;
            hoveredPressable = null;
            hoveredInkSource = null;
            hoveredFlippable = null;
            viewCanvas = null;
            viewSurface = false;
            if (PickHit(origin.position, direction, out RaycastHit hit))
            {
                end = hit.point;
                hoverDistance = hit.distance;
                hoveredInkSource = hit.collider.GetComponentInParent<IInkSource>();
                hoveredPressable = hit.collider.GetComponentInParent<IPressable>();
                hoveredFlippable = hit.collider.GetComponentInParent<IFlippable>();
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null) currentHovered = interactable;
                FindViewTarget(hit);
            }
            HoverPrompt = hoveredPressable != null ? (hoveredPressable.PressWithClick ? "" : hoveredPressable.GetPressPrompt())
                : currentHovered != null ? currentHovered.GetInteractionPrompt() : "";
            HoverHints = ComputeHoverHints();

            if (vrPointerLine != null)
            {
                vrPointerLine.SetPosition(0, origin.position);
                vrPointerLine.SetPosition(1, end);
            }
        }

        /// <summary>
        /// Primer golpe util del rayo: ignora el propio cuerpo, lo que se lleva en la mano (salvo botones) y los volumenes
        /// disparadores que no se pueden usar (agua, campos magicos).
        /// </summary>
        private bool PickHit(Vector3 origin, Vector3 direction, out RaycastHit best)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hoverHits, interactRange, interactLayer, QueryTriggerInteraction.Collide);
            best = default;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hoverHits[i];
                if (hit.distance >= bestDistance) continue;
                Collider c = hit.collider;
                if (c.transform.IsChildOf(transform)) continue;
                bool pressable = c.GetComponentInParent<IPressable>() != null;
                bool held = IsInMyHands(c.transform);
                if (held && !pressable) continue;
                if (c.isTrigger && !pressable && c.GetComponentInParent<IInteractable>() == null &&
                    c.GetComponentInParent<IInkSource>() == null && c.GetComponentInParent<IFlippable>() == null) continue;
                best = hit;
                bestDistance = hit.distance;
            }
            return bestDistance < float.MaxValue;
        }

        /// <summary>
        /// True si el collider es de lo que llevo EN LA MANO (principal o secundaria en VR). No usa <see cref="World.Holdable"/>
        /// a proposito: lo arrastrado por el movedor tambien esta "sostenido" pero se sigue pudiendo mirar (pistas iguales a antes).
        /// </summary>
        private bool IsInMyHands(Transform t)
        {
            GameObject main = inventory.HeldMain;
            GameObject hand = inventory.HandSecondary;
            return (main != null && t.IsChildOf(main.transform)) || (hand != null && t.IsChildOf(hand.transform));
        }
    }
}
