using UnityEngine;

namespace SpellSimulator.Canvas
{
    /// <summary>
    /// Módulo 1: Motor de Trazado.
    /// Gestiona un lienzo dinámico utilizando una RenderTexture para procesar
    /// los trazos en GPU sin generar Garbage Collection en CPU.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class DrawingCanvas : MonoBehaviour
    {
        [Header("Canvas Settings")]
        [SerializeField, Tooltip("Resolución cuadrada de la textura (ej. 512).")]
        private int resolution = 512;
        
        [SerializeField, Tooltip("Color de fondo del lienzo al limpiarlo.")]
        private Color clearColor = Color.black;

        [Header("Brush Settings")]
        [SerializeField, Tooltip("Material utilizado para estampar el pincel. Debe usar un shader Unlit/Transparent o similar.")]
        private Material brushMaterial;
        
        [SerializeField, Range(0.01f, 0.2f), Tooltip("Tamaño del pincel en relación al lienzo (0-1).")]
        private float brushSize = 0.05f;

        [SerializeField, Range(10f, 500f), Tooltip("Cantidad de interpolaciones entre puntos para evitar saltos al mover rápido.")]
        private float lerpStepsPerUnit = 250f;

        private RenderTexture renderTexture;
        private Renderer canvasRenderer;
        
        // Estado del trazo
        private Vector2 lastDrawPosition;

        // Bounding box del trazo en coordenadas UV [0,1]
        // Permite recortar la region dibujada antes de mandarsela al modelo ONNX
        private Vector2 inkBoundsMin = Vector2.one;
        private Vector2 inkBoundsMax = Vector2.zero;
        private bool hasInk = false;

        /// <summary>True si hay al menos un trazo registrado en el lienzo.</summary>
        public bool HasInk => hasInk;

        /// <summary>La sesion de dibujo (vectores) asociada a este lienzo especifico.</summary>
        public Core.DrawingSession Session { get; private set; } = new Core.DrawingSession();

        private void Awake()
        {
            InitializeCanvas();
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        /// <summary>
        /// Crea la RenderTexture, la asigna al material del Quad y la limpia.
        /// </summary>
        private void InitializeCanvas()
        {
            canvasRenderer = GetComponent<Renderer>();

            // Calcular resolución basada en la escala física para evitar pixelado al estirar el lienzo
            float scaleX = Mathf.Max(0.01f, transform.localScale.x);
            float scaleY = Mathf.Max(0.01f, transform.localScale.y);
            
            int width = Mathf.RoundToInt(resolution * scaleX);
            int height = Mathf.RoundToInt(resolution * scaleY);
            width = Mathf.Max(16, width);
            height = Mathf.Max(16, height);

            // Crear RenderTexture dinámica
            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.Create();

            // Asignar al material principal
            if (canvasRenderer.material != null)
            {
                canvasRenderer.material.mainTexture = renderTexture;
            }

            ClearCanvas();
        }

        /// <summary>
        /// Borra todo el contenido del lienzo usando el color de fondo.
        /// </summary>
        public void ClearCanvas()
        {
            Session.Clear(); // Tambien limpiamos los vectores

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, clearColor);
            RenderTexture.active = previousActive;
        }

        /// <summary>
        /// Limpia el lienzo y redibuja todos los trazos de una sesion instantaneamente.
        /// Util para implementar Undo u otras herramientas de edicion.
        /// </summary>
        public void RedrawSession(SpellSimulator.Core.DrawingSession session)
        {
            ClearCanvas();
            ResetInkBounds();

            if (session == null || session.IsEmpty)
                return;

            foreach (var stroke in session.Strokes)
            {
                if (stroke.Points.Count == 0) continue;

                for (int i = 0; i < stroke.Points.Count; i++)
                {
                    DrawAt(stroke.Points[i], i == 0);
                }
            }
        }

        /// <summary>
        /// Devuelve la textura del lienzo de forma segura.
        /// </summary>
        public RenderTexture GetRenderTexture()
        {
            return renderTexture;
        }

        /// <summary>
        /// Devuelve el bounding box UV de la tinta dibujada en el lienzo.
        /// Si no hay tinta, devuelve Rect(0,0,1,1) (canvas completo como fallback).
        /// </summary>
        public Rect GetInkBounds()
        {
            if (!hasInk) return new Rect(0f, 0f, 1f, 1f);

            // Agregar margen del 10% del area dibujada para no cortar bordes
            float padding = 0.08f;
            float x = Mathf.Clamp01(inkBoundsMin.x - padding);
            float y = Mathf.Clamp01(inkBoundsMin.y - padding);
            float w = Mathf.Clamp01(inkBoundsMax.x + padding) - x;
            float h = Mathf.Clamp01(inkBoundsMax.y + padding) - y;
            return new Rect(x, y, Mathf.Max(w, 0.01f), Mathf.Max(h, 0.01f));
        }

        /// <summary>
        /// Reinicia el bounding box de tinta. Llamar despues de disparar la inferencia.
        /// </summary>
        public void ResetInkBounds()
        {
            inkBoundsMin = Vector2.one;
            inkBoundsMax = Vector2.zero;
            hasInk = false;
        }

        /// <summary>
        /// Interfaz pública para recibir coordenadas UV y estampar el pincel.
        /// Aplica interpolación para evitar huecos en movimientos rápidos.
        /// </summary>
        /// <param name="uvPosition">Coordenada UV [0,1] actual del impacto.</param>
        /// <param name="isNewStroke">True si es el inicio de un trazo (click inicial).</param>
        public void DrawAt(Vector2 uvPosition, bool isNewStroke)
        {
            // Expandir el bounding box con cada punto recibido
            inkBoundsMin = Vector2.Min(inkBoundsMin, uvPosition);
            inkBoundsMax = Vector2.Max(inkBoundsMax, uvPosition);
            hasInk = true;

            if (brushMaterial == null) return;

            // OPTIMIZACION CRITICA PARA VR (Meta Quest):
            // Cambiar RenderTexture.active fuerza a la GPU (Tile-based) a volcar la memoria.
            // Si lo hacemos por cada estampa, los FPS mueren. Lo hacemos UNA vez por frame.
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            brushMaterial.SetPass(0);
            
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, 1, 1, 0); 

            if (isNewStroke)
            {
                lastDrawPosition = uvPosition;
                DrawStampInternal(uvPosition);
            }
            else
            {
                float distance = Vector2.Distance(lastDrawPosition, uvPosition);
                
                // Calculamos cuántos "sellos" necesitamos para rellenar el espacio
                int steps = Mathf.Max(1, Mathf.CeilToInt(distance * lerpStepsPerUnit));

                for (int i = 1; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    Vector2 interpolatedUV = Vector2.Lerp(lastDrawPosition, uvPosition, t);
                    DrawStampInternal(interpolatedUV);
                }

                lastDrawPosition = uvPosition;
            }

            GL.PopMatrix();
            RenderTexture.active = previousActive;
        }

        /// <summary>
        /// Estampa la textura del pincel. 
        /// Requiere que RenderTexture.active y GL.LoadPixelMatrix ya estén configurados para evitar lag.
        /// </summary>
        private void DrawStampInternal(Vector2 uvPosition)
        {
            float yPos = 1f - uvPosition.y; 
            
            float scaleX = Mathf.Max(0.01f, transform.localScale.x);
            float scaleY = Mathf.Max(0.01f, transform.localScale.y);
            float sizeX = brushSize / scaleX;
            float sizeY = brushSize / scaleY;

            Rect drawRect = new Rect(
                uvPosition.x - (sizeX * 0.5f), 
                yPos - (sizeY * 0.5f), 
                sizeX, 
                sizeY
            );

            Graphics.DrawTexture(drawRect, Texture2D.whiteTexture, brushMaterial);
        }

        /// <summary>
        /// Convierte un punto del mundo tridimensional en coordenadas UV [0,1]
        /// basándose en la malla local. Esto evita el bug de RaycastHit.textureCoord
        /// devolviendo (0,0) cuando se usa un BoxCollider o un MeshCollider Convexo.
        /// </summary>
        public Vector2 GetUVFromWorldPosition(Vector3 worldPos)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                float u = Mathf.InverseLerp(b.min.x, b.max.x, localPos.x);
                float v = Mathf.InverseLerp(b.min.y, b.max.y, localPos.y);
                return new Vector2(u, v);
            }
            
            // Fallback asumiendo que es un Quad estándar de Unity (-0.5 a 0.5)
            return new Vector2(localPos.x + 0.5f, localPos.y + 0.5f);
        }
    }
}
