using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Output;
using SpellSimulator.DebugTools;
using SpellSimulator.Reactions;

namespace SpellSimulator.World
{
    /// <summary>
    /// Calcomania de tinta pegada a una superficie. Sigue a lo que se mueve (una caja), desaparece si el objeto se rompe
    /// o se reinicia el mundo, y el sello se rompe como en el canon cuando algo daña la superficie debajo: un cráter, una
    /// quemadura o un charco sobre el piso, o el fuego, el agua o la fractura sobre el objeto.
    /// </summary>
    public class InscribedDecal : MonoBehaviour
    {
        private DrawingCanvas canvas;
        private SpellCaster caster;
        private Transform target;
        private ElementReactive targetReactive;
        private float radius;
        private bool follows;
        private Vector3 localPosition;
        private Quaternion localRotation;
        private Vector3 lastTargetPosition;
        private Quaternion lastTargetRotation;

        public DrawingCanvas Canvas => canvas;

        public void Init(DrawingCanvas decalCanvas, SpellCaster spellCaster, Transform surface, float size)
        {
            canvas = decalCanvas;
            caster = spellCaster;
            target = surface;
            radius = size * 0.5f;
            targetReactive = surface != null ? surface.GetComponentInParent<ElementReactive>() : null;

            // Solo lo que se mueve necesita seguirlo (el terreno y la torre no).
            var body = surface != null ? surface.GetComponentInParent<Rigidbody>() : null;
            follows = body != null && !body.isKinematic;
            if (follows)
            {
                localPosition = surface.InverseTransformPoint(transform.position);
                localRotation = Quaternion.Inverse(surface.rotation) * transform.rotation;
                lastTargetPosition = surface.position;
                lastTargetRotation = surface.rotation;
            }
        }

        private void OnEnable()
        {
            GroundMarks.Marked += OnGroundMarked;
            MagicWorld.WorldReset += OnWorldReset;
            if (targetReactive != null) targetReactive.Reacted += OnReacted;
        }

        private void OnDisable()
        {
            GroundMarks.Marked -= OnGroundMarked;
            MagicWorld.WorldReset -= OnWorldReset;
            if (targetReactive != null) targetReactive.Reacted -= OnReacted;
        }

        private void OnDestroy() => Inscribable.Unregister(this);

        private void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            if (!follows) return;
            if (target.position == lastTargetPosition && target.rotation == lastTargetRotation) return;
            lastTargetPosition = target.position;
            lastTargetRotation = target.rotation;
            transform.SetPositionAndRotation(target.TransformPoint(localPosition), target.rotation * localRotation);
        }

        private void OnWorldReset() => Destroy(gameObject);

        // Un crater, una quemadura o un charco encima del sello lo rompe.
        private void OnGroundMarked(ElementType element, Vector3 point)
        {
            if (follows) return; // las marcas son del piso
            if (OwnSpellActive()) return; // el hechizo de este sello no lo rompe a si mismo
            if (Vector3.Distance(point, transform.position) < radius * 1.1f) Break($"{Describe(element)} arruino el sello.");
        }

        private bool OwnSpellActive()
        {
            var fields = MagicField.Active;
            for (int i = 0; i < fields.Count; i++)
                if (fields[i] != null && fields[i].Source == gameObject) return true;
            return false;
        }

        private void OnReacted(ElementReactive source, ReactionEvents events)
        {
            if ((events & ReactionEvents.Soaked) != 0) Break("La superficie se mojo: la tinta se corrio.");
            else if ((events & ReactionEvents.Ignited) != 0) Break("La superficie se prendio fuego.");
        }

        private static string Describe(ElementType element)
        {
            switch (element)
            {
                case ElementType.Earth: return "Un crater";
                case ElementType.Fire: return "El fuego";
                default: return "El agua";
            }
        }

        private void Break(string reason)
        {
            if (canvas == null || canvas.Session.IsEmpty) return;
            canvas.Clear();
            SealBreaker.NotifyBroken(canvas);
            if (caster != null) caster.OnSealEdited(canvas);
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke($"<b>[Sello] Roto</b>\n{reason}");
        }
    }
}
