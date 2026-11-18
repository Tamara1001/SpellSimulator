using UnityEngine;

namespace SpellSimulator.Canvas
{
    /// <summary>Solo hay dos tipos de lienzo: la hoja suelta y la libreta de hojas (Docs/LIENZOS_PLAN.md).</summary>
    public enum CanvasKind { Sheet = 0, Notebook = 1 }

    public enum CanvasSize { S = 0, M = 1, L = 2 }

    public enum CanvasShape { Square = 0, Rect = 1, Round = 2 }

    /// <summary>Cara por la que sale el hechizo: Front = la cara donde se dibuja (marco dorado), Back = la de atras (marco azul).</summary>
    public enum FiringFace { Front = 0, Back = 1 }

    /// <summary>Especificacion de un lienzo: lo unico que hace falta para colocarlo (el editor) o pedirlo en el restocker.</summary>
    public struct CanvasSpec
    {
        public CanvasKind kind;
        public CanvasSize size;
        public CanvasShape shape;
        /// <summary>Hojas de una libreta (1..<see cref="CanvasRules.MaxPages"/>); una hoja suelta siempre tiene 1.</summary>
        public int pages;
        public FiringFace face;

        public CanvasSpec(CanvasKind kind, CanvasSize size, CanvasShape shape, int pages = 1, FiringFace face = FiringFace.Front)
        {
            this.kind = kind;
            this.size = size;
            this.shape = shape;
            this.pages = pages;
            this.face = face;
        }

        public static CanvasSpec Sheet(CanvasSize size, CanvasShape shape = CanvasShape.Square) => CanvasRules.Sanitize(new CanvasSpec(CanvasKind.Sheet, size, shape));
        public static CanvasSpec Notebook(CanvasSize size, int pages = CanvasRules.DefaultPages) => CanvasRules.Sanitize(new CanvasSpec(CanvasKind.Notebook, size, CanvasShape.Rect, pages));
    }

    /// <summary>
    /// Reglas puras de los lienzos (tamaños, formas permitidas, hojas, cara de disparo y atriles). Sin MonoBehaviour: se prueba en EditMode.
    /// La libreta M es la de siempre (0,24 × 0,32 m).
    /// </summary>
    public static class CanvasRules
    {
        public const int MaxPages = 8, DefaultPages = 8;

        /// <summary>Lado (m) de una hoja cuadrada y ancho × alto de una rectangular, por tamaño.</summary>
        public static readonly float[] SheetSquare = { 0.35f, 0.55f, 1.0f };
        public static readonly Vector2[] SheetRect = { new Vector2(0.3f, 0.4f), new Vector2(0.5f, 0.7f), new Vector2(1.0f, 1.4f) };
        /// <summary>Libretas: S de bolsillo circular (diametro), M la actual, L rectangular grande.</summary>
        public const float NotebookRoundDiameter = 0.18f;
        public static readonly Vector2[] NotebookRect = { new Vector2(0.18f, 0.24f), new Vector2(0.24f, 0.32f), new Vector2(0.36f, 0.48f) };

        /// <summary>Colores del marco por cara de disparo: dorado = frente, azul = espalda.</summary>
        public static readonly Color FrontFrame = new Color(0.95f, 0.75f, 0.25f), BackFrame = new Color(0.3f, 0.55f, 0.95f);

        /// <summary>Distancia (m) a la que un lienzo suelto se acopla al slot de un atril, y separacion al sacar el anterior.</summary>
        public const float SlotSnapDistance = 0.85f, SlotEjectOffset = 0.35f;
        /// <summary>Giro (°) de un atril por pulsacion.</summary>
        public const float EaselTurnStep = 45f;

        /// <summary>Normaliza un spec: formas validas por tipo y tamaño, hojas dentro del rango, la hoja suelta con 1 pagina.</summary>
        public static CanvasSpec Sanitize(CanvasSpec s)
        {
            s.size = (CanvasSize)Mathf.Clamp((int)s.size, 0, 2);
            if (s.kind == CanvasKind.Notebook)
            {
                s.shape = s.size == CanvasSize.S ? CanvasShape.Round : CanvasShape.Rect;
                s.pages = Mathf.Clamp(s.pages, 1, MaxPages);
            }
            else
            {
                if (s.shape == CanvasShape.Round) s.shape = CanvasShape.Square;
                s.pages = 1;
            }
            return s;
        }

        /// <summary>Ancho y alto (m) del lienzo; el circulo mide diametro × diametro.</summary>
        public static Vector2 Dimensions(CanvasSpec spec)
        {
            spec = Sanitize(spec);
            int i = (int)spec.size;
            if (spec.kind == CanvasKind.Notebook)
                return spec.shape == CanvasShape.Round ? new Vector2(NotebookRoundDiameter, NotebookRoundDiameter) : NotebookRect[i];
            return spec.shape == CanvasShape.Rect ? SheetRect[i] : new Vector2(SheetSquare[i], SheetSquare[i]);
        }

        /// <summary>Spec valido mas parecido a un ancho × alto dado (migracion de los lienzos viejos del mapa).</summary>
        public static CanvasSpec Nearest(CanvasKind kind, float width, float height)
        {
            CanvasSpec best = default;
            float bestError = float.MaxValue;
            for (int size = 0; size < 3; size++)
                for (int shape = 0; shape < 3; shape++)
                {
                    CanvasSpec s = new CanvasSpec(kind, (CanvasSize)size, (CanvasShape)shape, kind == CanvasKind.Notebook ? DefaultPages : 1);
                    if (Sanitize(s).shape != s.shape) continue;
                    Vector2 d = Dimensions(s);
                    // Error en escala logaritmica: pasarse al doble pesa lo mismo que quedarse a la mitad.
                    float e = Mathf.Abs(Mathf.Log(width / d.x)) + Mathf.Abs(Mathf.Log(height / d.y));
                    if (e < bestError) { bestError = e; best = s; }
                }
            return best;
        }

        public static FiringFace Flip(FiringFace face) => face == FiringFace.Front ? FiringFace.Back : FiringFace.Front;

        /// <summary>+1 / -1 para <see cref="DrawingCanvas.SetInkSign"/>.</summary>
        public static float InkSign(FiringFace face) => face == FiringFace.Front ? 1f : -1f;

        public static FiringFace FaceOf(float inkSign) => inkSign >= 0f ? FiringFace.Front : FiringFace.Back;

        public static Color FrameColor(FiringFace face) => face == FiringFace.Front ? FrontFrame : BackFrame;

        /// <summary>El lienzo suelto se acopla a un slot si su centro esta a menos de <see cref="SlotSnapDistance"/> de el.</summary>
        public static bool InSnapRange(Vector3 canvasCenter, Vector3 slot) => (canvasCenter - slot).sqrMagnitude <= SlotSnapDistance * SlotSnapDistance;

        /// <summary>Indice del slot mas cercano dentro del alcance (-1 = ninguno). Un slot ocupado cuenta solo si <paramref name="allowOccupied"/> (cambio de lienzo).</summary>
        public static int NearestSlot(Vector3 canvasCenter, Vector3[] slots, bool[] occupied, int count, bool allowOccupied)
        {
            int best = -1;
            float bestD = SlotSnapDistance * SlotSnapDistance;
            for (int i = 0; i < count; i++)
            {
                if (!allowOccupied && occupied[i]) continue;
                float d = (slots[i] - canvasCenter).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>Giro siguiente de un atril (°, 0..360).</summary>
        public static float NextEaselYaw(float yaw) => Mathf.Repeat(yaw + EaselTurnStep, 360f);

        /// <summary>Hoja dentro de 0..pages-1, sin dar la vuelta.</summary>
        public static int ClampPage(int page, int pages) => Mathf.Clamp(page, 0, Mathf.Max(1, pages) - 1);
    }
}
