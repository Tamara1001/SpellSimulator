using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.World;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Ayudas del lienzo de practica del piso 1 (tutorial): una plantilla fantasma tenue del sello de una receta (para calcarla,
    /// no cuenta como trazo ni entra al reconocimiento) y, mientras se sostiene el papel, un rayo que muestra hacia donde sale
    /// el hechizo (la normal de emision del lienzo).
    /// </summary>
    [RequireComponent(typeof(DrawingCanvas))]
    public class TutorialPaperGuide : MonoBehaviour
    {
        [SerializeField] private string recipeId = "gust";
        [SerializeField] private float aimLength = 3f;

        private static readonly Color GhostColor = new Color(0.25f, 0.5f, 0.95f, 0.75f);
        private static readonly Color AimColor = new Color(0.45f, 0.8f, 1f, 0.7f);

        private DrawingCanvas canvas;
        private PhysicalCanvas grab;
        private LineRenderer aim;
        private Camera cam;
        private Material lineMaterial;

        private void Start() => Build();

        /// <summary>Arma la plantilla y el rayo (lo llama Start; publico para poder verlo en el editor).</summary>
        public void Build()
        {
            canvas = GetComponent<DrawingCanvas>();
            grab = GetComponent<PhysicalCanvas>();
            // Idempotente: una segunda llamada reemplaza lo anterior en vez de duplicarlo.
            foreach (string childName in new[] { "AimGuide", "GhostTemplate" })
            {
                Transform old = transform.Find(childName);
                if (old != null) DestroyImmediate(old.gameObject);
            }
            if (lineMaterial != null) DestroyImmediate(lineMaterial);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            lineMaterial = new Material(shader);
            BuildAim();
            BuildGhost();
        }

        private void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        private void BuildAim()
        {
            var go = new GameObject("AimGuide");
            go.transform.SetParent(transform, false);
            aim = go.AddComponent<LineRenderer>();
            aim.useWorldSpace = true;
            aim.positionCount = 2;
            aim.widthMultiplier = 0.02f;
            aim.numCapVertices = 4;
            aim.sharedMaterial = lineMaterial;
            aim.startColor = AimColor;
            aim.endColor = new Color(AimColor.r, AimColor.g, AimColor.b, 0f);
            aim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aim.enabled = false;
        }

        private void BuildGhost()
        {
            var bench = FindFirstObjectByType<LabSealBench>();
            List<List<Vector2>> strokes = bench != null ? bench.RecipeStrokes(recipeId) : null;
            if (strokes == null || canvas == null) return;
            var root = new GameObject("GhostTemplate").transform;
            root.SetParent(transform, false);
            Vector2 iso = canvas.UvToIsotropic;
            float width = 0.010f / Mathf.Max(1e-4f, Mathf.Abs(transform.lossyScale.x));
            Vector3 lift = canvas.FaceNormal * 0.003f;
            for (int k = 0; k < strokes.Count; k++)
            {
                List<Vector2> pts = strokes[k];
                if (pts == null || pts.Count < 2) continue;
                var go = new GameObject("Stroke" + k);
                go.transform.SetParent(root, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = pts.Count;
                line.widthMultiplier = width;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.sharedMaterial = lineMaterial;
                line.startColor = line.endColor = GhostColor;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 uv = SpellSimulator.Lab.LabSealBenchMath.SquareToCanvasUv(pts[i], iso);
                    line.SetPosition(i, transform.InverseTransformPoint(canvas.UVToWorld(uv) + lift));
                }
            }
        }

        private void Update()
        {
            if (aim == null) return;
            bool show = grab != null && grab.IsHeld;
            if (aim.enabled != show) aim.enabled = show;
            if (!show) return;
            if (cam == null) cam = Camera.main;
            Vector3 from = canvas.WorldCenter;
            Vector3 viewer = cam != null ? cam.transform.position : from - transform.forward;
            Vector3 dir = canvas.EmissionNormal(viewer);
            aim.SetPosition(0, from);
            aim.SetPosition(1, from + dir * aimLength);
        }
    }
}
