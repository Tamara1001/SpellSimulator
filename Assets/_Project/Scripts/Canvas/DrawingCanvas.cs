using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Canvas
{
    /// <summary>
    /// Modulo 1: lienzo dinamico. Duenio de su DrawingSession (vectores) y de una RenderTexture donde la tinta
    /// se estampa por GPU (GL + Graphics.DrawTexture), sin acceso a pixeles por CPU.
    /// </summary>
    /// <summary>Por que cara del papel sale la magia.</summary>
    public enum EmissionSide
    {
        /// <summary>Canon: por la cara con tinta (paredes, mesas, sellos fijos).</summary>
        InkSide,
        /// <summary>Hacia el lado opuesto a quien mira (papeles de mano: la magia sale hacia el objetivo, no al lanzador).</summary>
        AwayFromViewer
    }

    [RequireComponent(typeof(Renderer))]
    public class DrawingCanvas : MonoBehaviour
    {
        [Header("Canvas Settings")]
        [SerializeField, Tooltip("Resolucion de la textura para un lienzo de escala 1.")]
        private int resolution = 512;
        [SerializeField, Tooltip("Color de fondo del lienzo al limpiarlo.")]
        private Color clearColor = Color.black;

        [Header("Emission")]
        [SerializeField, Tooltip("Por que cara sale el hechizo. Papeles: AwayFromViewer. Paredes/mesas: InkSide.")]
        private EmissionSide emissionSide = EmissionSide.AwayFromViewer;

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
        private float currentWidth;
        private Bounds meshBounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0f));
        private Vector3 surfaceNormalLocal = Vector3.back;

        /// <summary>Trazos de este lienzo, en UV [0,1].</summary>
        public DrawingSession Session { get; } = new DrawingSession();

        private float inkSign = 1f;

        /// <summary>+1 si la cara con tinta es la visible, -1 si es la de atras (ver NoteInkViewer).</summary>
        public float InkSign => inkSign;
        public void SetInkSign(float sign) => inkSign = sign >= 0f ? 1f : -1f;
        public Material BrushMaterial => brushMaterial;

        /// <summary>Normal de la cara visible del lienzo, en mundo.</summary>
        public Vector3 FaceNormal => transform.TransformDirection(surfaceNormalLocal).normalized;

        /// <summary>
        /// Anota desde que lado se empieza a dibujar: si el lienzo esta vacio, esa cara pasa a ser la cara con tinta
        /// (la magia sale por ahi, como en el canon). Dar vuelta la hoja cambia hacia donde apunta el hechizo.
        /// </summary>
        public void NoteInkViewer(Vector3 viewerPosition)
        {
            if (!Session.IsEmpty) return;
            inkSign = Vector3.Dot(FaceNormal, viewerPosition - transform.position) >= 0f ? 1f : -1f;
        }

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

        /// <summary>Centro de la superficie en mundo (el centro del mesh, que no siempre coincide con el pivote del objeto).</summary>
        public Vector3 WorldCenter => transform.TransformPoint(meshBounds.center);

        /// <summary>Desplazamiento (m, en espacio local escalado) del pivote al centro de la superficie.</summary>
        public Vector3 CenterOffsetLocalScaled => Vector3.Scale(meshBounds.center, transform.lossyScale);

        public RenderTexture GetRenderTexture() => renderTexture;

        /// <summary>
        /// Normal de emision en mundo. Con AwayFromViewer apunta al lado contrario de viewerPosition.
        /// </summary>
        public Vector3 EmissionNormal(Vector3 viewerPosition)
        {
            Vector3 ink = transform.TransformDirection(surfaceNormalLocal).normalized;
            if (emissionSide == EmissionSide.InkSide) return ink * inkSign;
            return Vector3.Dot(ink, viewerPosition - transform.position) > 0f ? -ink : ink;
        }

        /// <summary>UV [0,1] -> punto del mundo sobre la superficie.</summary>
        public Vector3 UVToWorld(Vector2 uv)
        {
            var local = new Vector3(
                Mathf.Lerp(meshBounds.min.x, meshBounds.max.x, uv.x),
                Mathf.Lerp(meshBounds.min.y, meshBounds.max.y, uv.y),
                meshBounds.center.z);
            return transform.TransformPoint(local);
        }

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
            // Tope de 2048 para que un lienzo gigante no pida una textura enorme.
            int width = resolutionIsAbsolute ? resolution : Mathf.Clamp(Mathf.RoundToInt(resolution * scaleX), 16, 2048);
            int height = resolutionIsAbsolute ? resolution : Mathf.Clamp(Mathf.RoundToInt(resolution * scaleY), 16, 2048);

            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear };
            renderTexture.Create();

            // Renderer.material instancia el material: guardamos la instancia para destruirla.
            canvasMaterialInstance = GetComponent<Renderer>().material;
            if (canvasMaterialInstance != null) canvasMaterialInstance.mainTexture = renderTexture;

            ClearTexture();
        }

        /// <summary>
        /// Prepara un lienzo creado por codigo ANTES de que despierte (el objeto debe estar inactivo): resolucion de la
        /// textura, pincel, fondo y cara de emision. Lo usan las calcomanias sobre el entorno.
        /// </summary>
        public void Prepare(int textureResolution, Material brush, Color background, float relativeBrushSize, EmissionSide side)
        {
            resolution = Mathf.Max(32, textureResolution);
            // La textura se dimensiona con la escala: aqui la resolucion ya es por lado en pixeles.
            resolutionIsAbsolute = true;
            brushMaterial = brush;
            clearColor = background;
            brushSize = relativeBrushSize;
            emissionSide = side;
        }

        private bool resolutionIsAbsolute;

        /// <summary>Configura un lienzo creado por codigo (paginas del grimorio): pincel, fondo y tamanio.</summary>
        public void Configure(Material brush, Color background, float relativeBrushSize)
        {
            brushMaterial = brush;
            clearColor = background;
            brushSize = relativeBrushSize;
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
            {
                currentWidth = stroke.Width;
                for (int i = 0; i < stroke.Points.Count; i++)
                    StampTo(stroke.Points[i], i == 0);
            }
            EndDraw();
        }

        /// <summary>
        /// Estampa tinta en uv, interpolando desde el punto anterior si no es el inicio de un trazo.
        /// widthMeters = ancho del trazo (0 = el pincel por defecto del lienzo).
        /// </summary>
        public void DrawAt(Vector2 uv, bool isNewStroke, float widthMeters = 0f)
        {
            if (brushMaterial == null) return;
            currentWidth = widthMeters;
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

        /// <summary>
        /// Rayo -> UV intersectando el plano real del mesh. Un collider mas grueso que el papel (lienzos chicos) hace que
        /// el punto de impacto quede adelantado y, por paralaje, la tinta salga corrida del cursor; el plano no tiene ese error.
        /// </summary>
        public Vector2 GetUVFromRay(Ray ray, Vector3 fallbackHit)
        {
            Vector3 planePoint = transform.TransformPoint(meshBounds.center);
            Vector3 normal = FaceNormal;
            float denom = Vector3.Dot(normal, ray.direction);
            if (Mathf.Abs(denom) > 1e-5f)
            {
                float t = Vector3.Dot(normal, planePoint - ray.origin) / denom;
                if (t > 0f) return GetUVFromWorldPosition(ray.origin + ray.direction * t);
            }
            return GetUVFromWorldPosition(fallbackHit);
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
                // Espaciado entre sellos: el menor entre el fijo y un cuarto del pincel (si no, un trazo rapido o un
                // pincel ancho queda punteado).
                Vector2 stamp = StampSizeUV();
                float spacing = Mathf.Min(1f / lerpStepsPerUnit, Mathf.Max(1e-4f, Mathf.Min(stamp.x, stamp.y) * StampSpacingFraction));
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(lastDrawPosition, uv) / spacing));
                for (int i = 1; i <= steps; i++) Stamp(Vector2.Lerp(lastDrawPosition, uv, (float)i / steps));
            }
            lastDrawPosition = uv;
        }

        private const float StampSpacingFraction = 0.2f;

        /// <summary>Tamanio del sello en UV. Se compensa por la escala para que siga siendo redondo en lienzos estirados.</summary>
        private Vector2 StampSizeUV()
        {
            if (currentWidth > 0f)
            {
                Vector2 size = WorldSize;
                return new Vector2(currentWidth / Mathf.Max(1e-4f, size.x), currentWidth / Mathf.Max(1e-4f, size.y));
            }
            return new Vector2(brushSize / Mathf.Max(0.01f, transform.localScale.x), brushSize / Mathf.Max(0.01f, transform.localScale.y));
        }

        private void Stamp(Vector2 uv)
        {
            Vector2 stamp = StampSizeUV();
            float sizeX = stamp.x, sizeY = stamp.y;
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
