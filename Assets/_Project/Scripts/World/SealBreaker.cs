using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.DebugTools;
using SpellSimulator.Reactions;

namespace SpellSimulator.World
{
    /// <summary>
    /// Canon: romper el sello detiene la magia. Si el papel se empapa (la tinta se corre) o se prende fuego, el dibujo
    /// se pierde. Va en el mismo objeto que el DrawingCanvas y su ElementReactive.
    /// </summary>
    [RequireComponent(typeof(DrawingCanvas), typeof(ElementReactive))]
    public class SealBreaker : MonoBehaviour
    {
        /// <summary>(lienzo) cuando un sello se pierde por agua o fuego.</summary>
        public static event System.Action<DrawingCanvas> SealBroken;

        /// <summary>Para quien rompe un sello sin ser un SealBreaker (calcomanias sobre el entorno).</summary>
        public static void NotifyBroken(DrawingCanvas canvas) => SealBroken?.Invoke(canvas);

        [SerializeField] private SpellCaster caster;

        private DrawingCanvas canvas;
        private ElementReactive reactive;
        private Rune rune;

        private void Awake()
        {
            canvas = GetComponent<DrawingCanvas>();
            reactive = GetComponent<ElementReactive>();
            rune = GetComponent<Rune>();
        }

        private void OnEnable() => reactive.Reacted += OnReacted;
        private void OnDisable() => reactive.Reacted -= OnReacted;

        private void OnReacted(ElementReactive source, ReactionEvents events)
        {
            string reason = null;
            // Tinta resistente: el agua no corre el sello (funciona mojado y bajo el agua); el fuego si lo quema.
            // Runas de metal y oro: resisten agua y fuego por el material.
            bool waterproof = Core.InkKindInfo.IsWaterproof(Core.InkMath.Dominant(canvas.Session.Strokes)) || (rune != null && rune.Waterproof);
            bool fireProof = rune != null && rune.FireProof;
            if ((events & ReactionEvents.Soaked) != 0 && !waterproof) reason = "El papel se mojo: la tinta se corrio.";
            else if ((events & ReactionEvents.Ignited) != 0 && !fireProof) reason = "El papel se prendio fuego.";
            if (reason == null || canvas.Session.IsEmpty) return;

            canvas.Clear();
            SealBroken?.Invoke(canvas);
            if (caster != null) caster.OnSealEdited(canvas);
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke($"<b>[Sello] Roto</b>\n{reason}");
            Debug.Log($"[Mundo] Sello roto en {name}: {reason}");
        }
    }
}
