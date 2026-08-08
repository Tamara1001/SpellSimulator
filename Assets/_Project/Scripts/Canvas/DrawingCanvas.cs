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

        [SerializeField, Range(1f, 100f), Tooltip("Cantidad de interpolaciones entre puntos para evitar saltos al mover rápido.")]
        private float lerpStepsPerUnit = 50f;

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

            // Crear RenderTexture. Formato ARGB32 es seguro y estándar.
            renderTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32);
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
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, clearColor);
            RenderTexture.active = previousActive;
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

            if (isNewStroke)
            {
                lastDrawPosition = uvPosition;
                DrawStamp(uvPosition);
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
                    DrawStamp(interpolatedUV);
                }

                lastDrawPosition = uvPosition;
            }
        }

        /// <summary>
        /// Dibuja el material del pincel en la coordenada UV dada usando GPU Blitting.
        /// Cero asignaciones (GC.Alloc) por llamada.
        /// </summary>
        /// <param name="uvPosition">Coordenada normalizada [0,1] donde estampar.</param>
        private void DrawStamp(Vector2 uvPosition)
        {
            if (brushMaterial == null) return;

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;

            // Configurar matriz ortográfica para dibujar en espacio 2D [0,1]
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, 1, 1, 0); 

            // El origen UV (0,0) en Unity Quad suele ser abajo a la izquierda.
            // Graphics.DrawTexture usa el sistema (0,0) arriba a la izquierda.
            // Dependiendo de cómo esté mapeado el Quad, podría requerir invertir Y.
            // Para un Quad estándar, invertir Y suele alinear el hit del Raycast con la textura.
            float yPos = 1f - uvPosition.y; 

            // Rectángulo donde se dibujará la textura
            Rect drawRect = new Rect(
                uvPosition.x - (brushSize * 0.5f), 
                yPos - (brushSize * 0.5f), 
                brushSize, 
                brushSize
            );

            // Estampar en GPU
            Graphics.DrawTexture(drawRect, Texture2D.whiteTexture, brushMaterial);

            GL.PopMatrix();
            RenderTexture.active = previousActive;
        }
    }
}
