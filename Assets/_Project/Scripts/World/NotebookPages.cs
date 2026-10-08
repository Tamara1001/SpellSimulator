using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// Cuaderno de paginas: cada pagina guarda su propio dibujo. Con el cuaderno agarrado, la rueda pasa de pagina.
    /// La pagina actual vive en canvas.Session; las demas, en la lista de paginas. Requiere DrawingCanvas en el mismo objeto.
    /// Cada pagina tiene su propio sello: un hechizo lanzado en otra pagina no cuenta en esta.
    /// </summary>
    [RequireComponent(typeof(DrawingCanvas))]
    public class NotebookPages : MonoBehaviour, IPageable, IResettable
    {
        [SerializeField, Min(1), Tooltip("Cantidad de paginas del cuaderno.")]
        private int pageCount = 10;

        private DrawingCanvas canvas;
        private SpellCaster caster;
        private readonly List<DrawingSession> pages = new List<DrawingSession>();

        /// <summary>Pagina actual, de 0 a PageCount - 1.</summary>
        public int Page { get; private set; }
        public int PageCount => pages.Count;

        private void Awake()
        {
            canvas = GetComponent<DrawingCanvas>();
            for (int i = 0; i < pageCount; i++) pages.Add(new DrawingSession());
        }

        /// <summary>Pasa de pagina (delta +1 / -1). En los extremos no da la vuelta: no hace nada.</summary>
        public void Turn(int delta)
        {
            int next = Mathf.Clamp(Page + delta, 0, PageCount - 1);
            if (next == Page) return;

            SaveCurrentPage();
            Page = next;
            LoadPage(Page);
            RefreshSeal();
            Hud.Notify("page", $"Pagina {Page + 1} de {PageCount}", 1.5f);
        }

        /// <summary>Vacia todas las paginas y el lienzo.</summary>
        public void ClearAllPages()
        {
            for (int i = 0; i < pages.Count; i++) pages[i].Clear();
            canvas.Clear();
            RefreshSeal();
        }

        /// <summary>Vacia el cuaderno y vuelve a la pagina 0.</summary>
        public void ResetPages()
        {
            ClearAllPages();
            Page = 0;
        }

        /// <summary>Reinicio del mundo (IResettable): mismo efecto que ResetPages.</summary>
        public void ResetToInitial() => ResetPages();

        /// <summary>Copia los trazos del lienzo a la pagina actual.</summary>
        private void SaveCurrentPage()
        {
            var stored = pages[Page].Strokes;
            stored.Clear();
            stored.AddRange(canvas.Session.Strokes);
        }

        /// <summary>Carga los trazos de una pagina en el lienzo y lo repinta.</summary>
        private void LoadPage(int index)
        {
            var strokes = canvas.Session.Strokes;
            strokes.Clear();
            strokes.AddRange(pages[index].Strokes);
            canvas.Redraw();
        }

        /// <summary>Recalcula la fase del sello de este lienzo (sin lanzar). El caster se busca una sola vez.</summary>
        private void RefreshSeal()
        {
            if (caster == null) caster = Object.FindFirstObjectByType<SpellCaster>();
            if (caster != null) caster.OnSealEdited(canvas);
        }
    }
}
