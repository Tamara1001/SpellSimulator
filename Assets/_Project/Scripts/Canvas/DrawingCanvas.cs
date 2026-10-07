using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Canvas
{
    /// <summary>
    /// Modulo 1: lienzo dinamico. Duenio de su DrawingSession (vectores) y de una RenderTexture donde la tinta
    /// se estampa por GPU (GL + Graphics.DrawTexture), sin acceso a pixeles por CPU.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class DrawingCanvas : MonoBehaviour
    {
        [Header("Canvas Settings")]
        [SerializeField, Tooltip("Resolucion de la textura para un lienzo de escala 1.")]
        private int resolution = 512;
        [SerializeField, Tooltip("Color de fondo del lienzo al limpiarlo.")]
        private Color clearColor = Color.black;

        [Header("Brush Settings")]
        [SerializeField, Tooltip("Material para estampar el pincel (Unlit/Transparent).")]
        private Material brushMaterial;
        [SerializeField, Range(0.01f, 0.2f), Tooltip("Tamanio del pincel relativo al lienzo.")]
        private float brushSize = 0.05f;
        [SerializeField, Range(10f, 500f), Tooltip("Sellos por unidad UV entre puntos, para no dejar huecos.")]
        private float lerpStepsPerUnit = 250f;

        private RenderTexture renderTexture;
        private Material canvasMaterialInstance;
        private Vector2 lastDrawPosition;
        private Bounds meshBounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0f));
        private Vector3 surfaceNormalLocal = Vector3.back;

        /// <summary>Trazos de este lienzo, en UV [0,1].</summary>
        public DrawingSession Session { get; } = new DrawingSession();

        /// <summary>Normal "hacia afuera del papel" en espacio local (la cara visible del mesh).</summary>
        public Vector3 SurfaceNormalLocal => surfaceNormalLocal;

        /// <summary>
        /// Escala UV -> espacio isotropico (el lado mayor mide 1). En un lienzo no cuadrado, un circulo dibujado
        /// en UV es una elipse; el pipeline de reconocimiento trabaja en este espacio.
        /// </summary>
        public Vector2 UvToIsotropic
        {
            get
            {
                Vector2 size = WorldSize;
                float largest = Mathf.Max(size.x, size.y, 1e-6f);
                return new Vector2(size.x / largest, size.y / largest);
            }
        }

        /// <summary>Tamanio del lienzo en metros (mesh x escala).</summary>
        public Vector2 WorldSize
        {
            get
            {
                Vector3 scale = transform.lossyScale;
                return new Vector2(Mathf.Abs(meshBounds.size.x * scale.x), Mathf.Abs(meshBounds.size.y * scale.y));
            }
        }

        public RenderTexture GetRenderTexture() => renderTexture;

        private void Awake()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Mesh mesh = meshFilter.sharedMesh;
                meshBounds = mesh.bounds;
                if (mesh.normals.Length > 0) surfaceNormalLocal = mesh.normals[0];
            }
            InitializeTexture();
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            if (canvasMaterialInstance != null) Destroy(canvasMaterialInstance);
        }

        private void InitializeTexture()
        {
            // Resolucion proporcional a la escala fisica para no pixelar lienzos estirados.
            float scaleX = Mathf.Max(0.01f, transform.localScale.x);
            float scaleY = Mathf.Max(0.01f, transform.localScale.y);
            int width = Mathf.Max(16, Mathf.RoundToInt(resolution * scaleX));
            int height = Mathf.Max(16, Mathf.RoundToInt(resolution * scaleY));

            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear };
            renderTexture.Create();

            // Renderer.material instancia el material: guardamos la instancia para destruirla.
            canvasMaterialInstance = GetComponent<Renderer>().material;
            if (canvasMaterialInstance != null) canvasMaterialInstance.mainTexture = renderTexture;

            ClearTexture();
        }

        /// <summary>Borra trazos y tinta.</summary>
        public void Clear()
        {
            Session.Clear();
            ClearTexture();
        }

        /// <summary>Repinta la textura desde la sesion actual (tras borrar o deshacer).</summary>
        public void Redraw()
        {
            ClearTexture();
            if (Session.IsEmpty || brushMaterial == null) return;

            BeginDraw();
            foreach (var stroke in Session.Strokes)
                for (int i = 0; i < stroke.Points.Count; i++)
                    StampTo(stroke.Points[i], i == 0);
            EndDraw();
        }

        /// <summary>Estampa tinta en uv, interpolando desde el punto anterior si no es el inicio de un trazo.</summary>
        public void DrawAt(Vector2 uv, bool isNewStroke)
        {
            if (brushMaterial == null) return;
            BeginDraw();
            StampTo(uv, isNewStroke);
            EndDraw();
        }

        /// <summary>Punto del mundo -> UV usando los bounds del mesh (RaycastHit.textureCoord falla con BoxCollider).</summary>
        public Vector2 GetUVFromWorldPosition(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            return new Vector2(
                Mathf.InverseLerp(meshBounds.min.x, meshBounds.max.x, local.x),
                Mathf.InverseLerp(meshBounds.min.y, meshBounds.max.y, local.y));
        }

        private RenderTexture previousActive;

        // Cambiar RenderTexture.active fuerza un resolve en GPUs tile-based (Quest): una vez por lote, no por sello.
        private void BeginDraw()
        {
            previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            brushMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, 1, 1, 0);
        }

        private void EndDraw()
        {
            GL.PopMatrix();
            RenderTexture.active = previousActive;
        }

        private void StampTo(Vector2 uv, bool isNewStroke)
        {
            if (isNewStroke)
            {
                Stamp(uv);
            }
            else
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(lastDrawPosition, uv) * lerpStepsPerUnit));
                for (int i = 1; i <= steps; i++) Stamp(Vector2.Lerp(lastDrawPosition, uv, (float)i / steps));
            }
            lastDrawPosition = uv;
        }

        private void Stamp(Vector2 uv)
        {
            // El pincel se compensa por la escala para que siga siendo redondo en lienzos estirados.
            float sizeX = brushSize / Mathf.Max(0.01f, transform.localScale.x);
            float sizeY = brushSize / Mathf.Max(0.01f, transform.localScale.y);
            var rect = new Rect(uv.x - sizeX * 0.5f, 1f - uv.y - sizeY * 0.5f, sizeX, sizeY);
            Graphics.DrawTexture(rect, Texture2D.whiteTexture, brushMaterial);
        }

        private void ClearTexture()
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, clearColor);
            RenderTexture.active = previous;
        }
    }
}
