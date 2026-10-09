using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Escribe sobre el papel lo que el sistema leyo: el nombre de cada glifo, una flecha por signo (hacia donde apunta)
    /// y las advertencias debajo del lienzo. Sirve para aprender a dibujar sellos y para las pruebas manuales.
    /// Las marcas duran mientras el lienzo no cambie. Una sola instancia atiende todos los lienzos (pool por lienzo).
    /// </summary>
    public class SealAnnotations : MonoBehaviour
    {
        [SerializeField] private SpellCaster caster;
        [SerializeField, Tooltip("Material de las flechas (con color por vertice, ej. Sprites/Default).")]
        private Material lineMaterial;
        [SerializeField] private bool show = true;

        [Header("Estilo")]
        [SerializeField, Tooltip("Alto del texto en metros por metro de lienzo.")]
        private float textHeightPerMeter = 0.045f;
        [SerializeField] private Color sigilColor = new Color(0.2f, 0.75f, 0.35f);
        [SerializeField] private Color signColor = new Color(0.2f, 0.5f, 0.95f);
        [SerializeField] private Color ambiguousColor = new Color(0.95f, 0.65f, 0.15f);
        [SerializeField] private Color unknownColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField, Tooltip("Separacion de las marcas sobre la cara del papel (m), para que no se mezclen con la tinta.")]
        private float surfaceOffset = 0.004f;

        private class CanvasMarks
        {
            public readonly List<TextMeshPro> Labels = new List<TextMeshPro>();
            public readonly List<LineRenderer> Arrows = new List<LineRenderer>();
            public TextMeshPro Warnings;
            public int StrokeCount = -1;
            public Transform Root;
        }

        private readonly Dictionary<DrawingCanvas, CanvasMarks> marks = new Dictionary<DrawingCanvas, CanvasMarks>();
        private readonly List<DrawingCanvas> canvases = new List<DrawingCanvas>();
        private readonly Vector3[] arrowPoints = new Vector3[5];

        private void OnEnable()
        {
            if (caster != null) caster.SealAnalyzed += OnSealAnalyzed;
        }

        private void OnDisable()
        {
            if (caster != null) caster.SealAnalyzed -= OnSealAnalyzed;
        }

        private void Update()
        {
            // Cualquier cambio en el lienzo (trazo, goma, trapo, deshacer) invalida las marcas.
            for (int i = canvases.Count - 1; i >= 0; i--)
            {
                var canvas = canvases[i];
                // Un lienzo destruido (por ejemplo al reiniciar el mundo) arrastra sus marcas: se olvida sin tocarlas.
                if (canvas == null) { canvases.RemoveAt(i); continue; }
                if (!marks.TryGetValue(canvas, out var m)) { canvases.RemoveAt(i); continue; }
                if (m.StrokeCount < 0) continue;
                if (canvas.Session.Strokes.Count != m.StrokeCount) Hide(m);
            }
        }

        private void OnSealAnalyzed(GlyphAST ast, SpellIR spell, DrawingCanvas canvas)
        {
            if (!show || canvas == null) return;
            var m = MarksFor(canvas);
            Hide(m);
            // Compensa la escala (no uniforme) del lienzo para que el texto no se deforme.
            Vector3 lossy = canvas.transform.lossyScale;
            m.Root.localScale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(lossy.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(lossy.y)), 1f / Mathf.Max(1e-4f, Mathf.Abs(lossy.z)));

            Vector3 normal = canvas.transform.TransformDirection(canvas.SurfaceNormalLocal).normalized;
            Vector3 up = canvas.transform.up;
            Quaternion facingViewer = Quaternion.LookRotation(-normal, up);
            Vector2 size = canvas.WorldSize;
            float textHeight = textHeightPerMeter * Mathf.Max(size.x, size.y);
            Vector2 iso = canvas.UvToIsotropic;

            var items = SealAnnotationLayout.Build(ast, spell);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                Vector2 position = item.Position;
                if (spell.analysisBoost > 1f) position = spell.analysisPivot + (position - spell.analysisPivot) / spell.analysisBoost;
                Vector2 uv = new Vector2(position.x / iso.x, position.y / iso.y);
                Vector3 at = canvas.UVToWorld(uv) + normal * surfaceOffset;
                Color color = ColorOf(item.Kind);

                var label = Label(m, i, canvas);
                label.text = item.Label;
                label.color = color;
                label.fontSize = textHeight * 10f;
                label.transform.SetPositionAndRotation(at + up * textHeight * 1.6f, facingViewer);
                label.gameObject.SetActive(true);

                if (item.Facing.sqrMagnitude > 0f && lineMaterial != null)
                {
                    float length = spell.sealRadius * 0.3f;
                    var arrow = Arrow(m, i, canvas);
                    Vector3 dir = (canvas.transform.right * item.Facing.x + up * item.Facing.y).normalized;
                    Vector3 side = Vector3.Cross(normal, dir);
                    Vector3 tail = at - dir * length * 0.5f;
                    Vector3 tip = at + dir * length * 0.5f;
                    float head = length * 0.3f;
                    arrowPoints[0] = tail;
                    arrowPoints[1] = tip;
                    arrowPoints[2] = tip - dir * head + side * head * 0.6f;
                    arrowPoints[3] = tip;
                    arrowPoints[4] = tip - dir * head - side * head * 0.6f;
                    arrow.widthMultiplier = textHeight * 0.18f;
                    arrow.startColor = arrow.endColor = color;
                    arrow.SetPositions(arrowPoints);
                    arrow.gameObject.SetActive(true);
                }
            }

            var warnings = SealAnnotationLayout.Warnings(spell);
            if (warnings.Count > 0)
            {
                var text = WarningsLabel(m, canvas);
                text.text = string.Join("\n", warnings);
                text.color = spell.valid ? ambiguousColor : unknownColor;
                text.fontSize = textHeight * 10f;
                Vector3 bottom = canvas.UVToWorld(new Vector2(0.5f, 0f)) + normal * surfaceOffset;
                text.rectTransform.sizeDelta = new Vector2(size.x * 1.4f, textHeight * 1.4f * warnings.Count);
                text.transform.SetPositionAndRotation(bottom - up * textHeight * (0.8f + 0.7f * warnings.Count), facingViewer);
                text.gameObject.SetActive(true);
            }

            m.StrokeCount = canvas.Session.Strokes.Count;
        }

        private Color ColorOf(AnnotationKind kind)
        {
            switch (kind)
            {
                case AnnotationKind.Sigil: return sigilColor;
                case AnnotationKind.Sign: return signColor;
                case AnnotationKind.Ambiguous: return ambiguousColor;
                default: return unknownColor;
            }
        }

        private CanvasMarks MarksFor(DrawingCanvas canvas)
        {
            if (marks.TryGetValue(canvas, out var m)) return m;
            m = new CanvasMarks { Root = new GameObject($"Annotations_{canvas.name}").transform };
            // Hijas del lienzo: si el papel se mueve, las marcas lo acompanian.
            m.Root.SetParent(canvas.transform, false);
            marks[canvas] = m;
            canvases.Add(canvas);
            return m;
        }

        private static void Hide(CanvasMarks m)
        {
            // Las marcas cuelgan del lienzo: si este se destruyo, sus textos y flechas tambien (referencias nulas de Unity).
            foreach (var l in m.Labels) if (l != null) l.gameObject.SetActive(false);
            foreach (var a in m.Arrows) if (a != null) a.gameObject.SetActive(false);
            if (m.Warnings != null) m.Warnings.gameObject.SetActive(false);
            m.StrokeCount = -1;
        }

        private TextMeshPro Label(CanvasMarks m, int index, DrawingCanvas canvas)
        {
            while (m.Labels.Count <= index) m.Labels.Add(NewText(m.Root, "Label", TextAlignmentOptions.Center));
            return m.Labels[index];
        }

        private TextMeshPro WarningsLabel(CanvasMarks m, DrawingCanvas canvas)
        {
            if (m.Warnings == null) m.Warnings = NewText(m.Root, "Warnings", TextAlignmentOptions.Top);
            return m.Warnings;
        }

        private LineRenderer Arrow(CanvasMarks m, int index, DrawingCanvas canvas)
        {
            while (m.Arrows.Count <= index)
            {
                var go = new GameObject("Arrow");
                go.transform.SetParent(m.Root, false);
                var line = go.AddComponent<LineRenderer>();
                // En espacio de mundo: la flecha se recalcula en cada lectura; si el papel se mueve se oculta al cambiar.
                line.useWorldSpace = true;
                line.positionCount = arrowPoints.Length;
                line.sharedMaterial = lineMaterial;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                go.SetActive(false);
                m.Arrows.Add(line);
            }
            return m.Arrows[index];
        }

        private static TextMeshPro NewText(Transform parent, string name, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontStyle = FontStyles.Bold;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            go.SetActive(false);
            return text;
        }
    }
}
