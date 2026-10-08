using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Lo que un objeto agarrado le cuenta a la interfaz: nombre, color de su icono, teclas de uso y, si tiene, una barra
    /// (por ejemplo la tinta de la pluma). La HUD de abajo lo muestra para el objeto de cada mano y para lo que se apunta.
    /// </summary>
    public interface IHudInfo
    {
        string HudName { get; }
        /// <summary>Color del icono del slot.</summary>
        Color HudColor { get; }
        /// <summary>Teclas de uso de este objeto, ej. "Clic: dibujar | Clic der.: deshacer | R: tinta". Texto estatico (no se arma por frame).</summary>
        string HudHints { get; }
        /// <summary>Barra 0..1 (tinta), o -1 si no tiene.</summary>
        float HudGauge { get; }
    }

    /// <summary>Objeto con varios grosores (pluma, pincel): la HUD dibuja la fila de grosores con su tecla.</summary>
    public interface IWidthSelectable
    {
        int WidthIndex { get; }
        int WidthCount { get; }
        /// <summary>Grosor relativo (0..1, el mas grueso = 1) para dibujar la muestra del trazo.</summary>
        float WidthSample(int index);
        /// <summary>Etiqueta corta del grosor, ej. "fino".</summary>
        string WidthLabel(int index);
        /// <summary>Rueda del mouse (sin nada agarrado): pasa al grosor siguiente (+1) o anterior (-1).</summary>
        void CycleWidth(int delta);
    }

    /// <summary>Objeto con paginas (cuaderno): la rueda pasa de pagina cuando se lo tiene agarrado.</summary>
    public interface IPageable
    {
        /// <summary>Pagina actual, de 0 a PageCount - 1.</summary>
        int Page { get; }
        int PageCount { get; }
        void Turn(int delta);
    }

    /// <summary>
    /// Avisos para el jugador en la esquina superior derecha. Un aviso con segundos &gt; 0 se apaga solo; con segundos = 0 es
    /// permanente (por ejemplo "sin tinta") hasta que se lo quita con <see cref="Clear"/>. El mismo id reemplaza al anterior.
    /// </summary>
    public static class Hud
    {
        public readonly struct Notice
        {
            public readonly string Id;
            public readonly string Text;
            public readonly float Until; // Time.unscaledTime; 0 = permanente
            public Notice(string id, string text, float until) { Id = id; Text = text; Until = until; }
        }

        /// <summary>Cambio de la lista (alta, baja o reemplazo). La HUD vuelve a dibujarla.</summary>
        public static event Action Changed;

        private static readonly List<Notice> notices = new List<Notice>(8);

        /// <summary>Avisos vivos, mas nuevos al final. No modificar.</summary>
        public static IReadOnlyList<Notice> Notices => notices;

        public static void Notify(string id, string text, float seconds = 3f)
        {
            float until = seconds > 0f ? Time.unscaledTime + seconds : 0f;
            for (int i = 0; i < notices.Count; i++)
            {
                if (notices[i].Id != id) continue;
                notices.RemoveAt(i);
                break;
            }
            notices.Add(new Notice(id, text, until));
            while (notices.Count > 6) notices.RemoveAt(0);
            Changed?.Invoke();
        }

        public static void Clear(string id)
        {
            for (int i = 0; i < notices.Count; i++)
            {
                if (notices[i].Id != id) continue;
                notices.RemoveAt(i);
                Changed?.Invoke();
                return;
            }
        }

        /// <summary>Lo llama la HUD cada tanto: quita los avisos vencidos. Devuelve true si cambio algo.</summary>
        public static bool Prune()
        {
            bool changed = false;
            float now = Time.unscaledTime;
            for (int i = notices.Count - 1; i >= 0; i--)
            {
                if (notices[i].Until > 0f && notices[i].Until <= now) { notices.RemoveAt(i); changed = true; }
            }
            if (changed) Changed?.Invoke();
            return changed;
        }
    }
}
