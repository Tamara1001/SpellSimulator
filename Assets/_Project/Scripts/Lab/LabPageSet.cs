using System.Collections;
using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Paginas de un panel lateral de la consola del Laboratorio v5 (punto A.3): en vez de cuatro paneles altos alrededor del
    /// jugador, cada lateral muestra una pagina por vez y se cambia con las pestañas de abajo (<see cref="LabPageTab"/>). Todas
    /// las paginas arrancan activas un frame (los controles se enlazan en Start y las grillas arman sus celdas en el primer
    /// Update) y despues queda visible solo <see cref="InitialPage"/>. Cambiar de pagina es un SetActive (no por frame).
    /// </summary>
    public class LabPageSet : MonoBehaviour
    {
        [SerializeField, Tooltip("Raiz de cada pagina (en orden de las pestañas).")]
        private GameObject[] pages;
        [SerializeField, Tooltip("Pestañas, una por pagina (mismo orden).")]
        private LabPageTab[] tabs;
        [SerializeField, Tooltip("Pagina visible al empezar.")]
        private int initialPage;

        /// <summary>Pagina visible (-1 antes de terminar el primer frame).</summary>
        public int CurrentPage { get; private set; } = -1;
        /// <summary>Cantidad de paginas.</summary>
        public int PageCount => pages != null ? pages.Length : 0;
        /// <summary>Pagina con que arranca.</summary>
        public int InitialPage => initialPage;

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(GameObject[] pages, LabPageTab[] tabs, int initialPage = 0)
        {
            this.pages = pages;
            this.tabs = tabs;
            this.initialPage = initialPage;
        }

        /// <summary>Raiz de una pagina (null fuera de rango).</summary>
        public GameObject Page(int index) => pages != null && index >= 0 && index < pages.Length ? pages[index] : null;

        private IEnumerator Start()
        {
            // Un frame con todo activo: Start de los controles (enlace) y primer Update de las grillas (celdas).
            yield return null;
            if (CurrentPage < 0) Show(initialPage);
        }

        /// <summary>Muestra la pagina <paramref name="index"/> y marca su pestaña.</summary>
        public void Show(int index)
        {
            if (pages == null || pages.Length == 0) return;
            index = Mathf.Clamp(index, 0, pages.Length - 1);
            CurrentPage = index;
            for (int i = 0; i < pages.Length; i++)
                if (pages[i] != null && pages[i].activeSelf != (i == index)) pages[i].SetActive(i == index);
            if (tabs == null) return;
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i] != null) tabs[i].SetSelected(i == index);
        }
    }
}
