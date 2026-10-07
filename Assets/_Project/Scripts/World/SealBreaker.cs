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
        [SerializeField] private SpellCaster caster;

        private DrawingCanvas canvas;
        private ElementReactive reactive;

        private void Awake()
        {
            canvas = GetComponent<DrawingCanvas>();
            reactive = GetComponent<ElementReactive>();
        }

        private void OnEnable() => reactive.Reacted += OnReacted;
        private void OnDisable() => reactive.Reacted -= OnReacted;

        private void OnReacted(ElementReactive source, ReactionEvents events)
        {
            string reason = null;
            if ((events & ReactionEvents.Soaked) != 0) reason = "El papel se mojo: la tinta se corrio.";
            else if ((events & ReactionEvents.Ignited) != 0) reason = "El papel se prendio fuego.";
            if (reason == null || canvas.Session.IsEmpty) return;

            canvas.Clear();
            if (caster != null) caster.OnSealEdited(canvas);
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke($"<b>[Sello] Roto</b>\n{reason}");
            Debug.Log($"[Mundo] Sello roto en {name}: {reason}");
        }
    }
}
