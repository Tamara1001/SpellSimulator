using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;
using SpellSimulator.Net;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// Mueble «restocker» (Docs/LIENZOS_PLAN.md, Bloque C): da pinceles (catalogo de <see cref="BrushRules"/>) y lienzos o libreta a medida
    /// (tipo, tamaño, forma y hojas; selector en <see cref="RestockSelection"/>). Lo que se saca vive en el mundo y NO se guarda: al recargar
    /// la escena desaparece. Hay un tope por clase (<see cref="RestockRules.MaxSpawnedPerKind"/>); al pasarse se borra el mas viejo.
    /// Solo en modo Libre (en red no saca nada). Pantalla y botones los arma el editor (MapBuilder).
    /// </summary>
    public class CanvasRestocker : MonoBehaviour
    {
        [SerializeField, Tooltip("Pluma de plantilla (desactivada, sin Respawnable): se clona para cada pincel.")]
        private PhysicalTool penTemplate;
        [SerializeField, Tooltip("Plantilla de papel (PaperCanvas del mundo).")]
        private GameObject paperTemplate;
        [SerializeField] private Material coverMaterial;
        [SerializeField, Tooltip("Donde sale un pincel (centro, sobre la repisa).")]
        private Transform brushOutput;
        [SerializeField, Tooltip("Donde sale un lienzo o libreta.")]
        private Transform canvasOutput;
        [SerializeField] private TMP_Text canvasReadout;
        [SerializeField] private TMP_Text brushReadout;

        private RestockSelection selection = RestockSelection.Default;
        private readonly List<GameObject> spawnedBrushes = new List<GameObject>();
        private readonly List<GameObject> spawnedCanvases = new List<GameObject>();

        public RestockSelection Selection => selection;
        public IReadOnlyList<GameObject> SpawnedBrushes => spawnedBrushes;
        public IReadOnlyList<GameObject> SpawnedCanvases => spawnedCanvases;

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(PhysicalTool pen, GameObject paper, Material cover,
            Transform brushOutput, Transform canvasOutput, TMP_Text canvasReadout, TMP_Text brushReadout)
        {
            penTemplate = pen;
            paperTemplate = paper;
            coverMaterial = cover;
            this.brushOutput = brushOutput;
            this.canvasOutput = canvasOutput;
            this.canvasReadout = canvasReadout;
            this.brushReadout = brushReadout;
        }

        private void Start() => RefreshReadouts();

        public void Press(RestockerAction action, int argument)
        {
            switch (action)
            {
                case RestockerAction.Brush: DispenseBrush(argument); return;
                case RestockerAction.Kind: selection = RestockRules.ToggleKind(selection); break;
                case RestockerAction.Size: selection = RestockRules.CycleSize(selection); break;
                case RestockerAction.Shape: selection = RestockRules.CycleShape(selection); break;
                case RestockerAction.PagesLess: selection = RestockRules.AddPages(selection, -1); break;
                case RestockerAction.PagesMore: selection = RestockRules.AddPages(selection, 1); break;
                case RestockerAction.DispenseCanvas: DispenseCanvas(); return;
            }
            RefreshReadouts();
        }

        private bool Blocked()
        {
            if (!NetSession.Exists) return false;
            Hud.Notify("restock", "Restocker: solo en modo Libre", 2.5f);
            return true;
        }

        public GameObject DispenseBrush(int index)
        {
            if (Blocked() || penTemplate == null || brushOutput == null || BrushRules.Catalog.Length == 0) return null;
            BrushSpec spec = BrushRules.Catalog[Mathf.Clamp(index, 0, BrushRules.Catalog.Length - 1)];
            GameObject go = Instantiate(penTemplate.gameObject, brushOutput.position, brushOutput.rotation);
            go.SetActive(true);
            go.name = "Brush_" + spec.name;
            go.GetComponent<PhysicalTool>().ApplyBrush(spec);
            Track(spawnedBrushes, go);
            if (brushReadout != null) brushReadout.text = RestockRules.Describe(spec);
            Hud.Notify("restock", "Restocker: " + spec.name, 1.5f);
            return go;
        }

        public GameObject DispenseCanvas()
        {
            if (Blocked() || paperTemplate == null || canvasOutput == null) return null;
            CanvasSpec spec = selection.ToSpec();
            // Sale parado y mirando a quien lo pidio (la cara visible hacia el frente del mueble), flotando un poco.
            Quaternion rotation = Quaternion.LookRotation(-canvasOutput.forward, Vector3.up);
            GameObject go = CanvasFactory.Spawn(paperTemplate, spec, canvasOutput.position, rotation, coverMaterial);
            var physical = go.GetComponent<PhysicalCanvas>();
            if (physical != null) physical.Pin(); // queda flotando donde salio (y cae solo al minuto)
            Track(spawnedCanvases, go);
            Hud.Notify("restock", "Restocker: " + (spec.kind == CanvasKind.Notebook ? "libreta " : "lienzo ") + spec.size, 1.5f);
            return go;
        }

        private static void Track(List<GameObject> list, GameObject go)
        {
            list.RemoveAll(x => x == null);
            list.Add(go);
            while (list.Count > RestockRules.MaxSpawnedPerKind)
            {
                if (list[0] != null) Destroy(list[0]);
                list.RemoveAt(0);
            }
        }

        private void RefreshReadouts()
        {
            if (canvasReadout != null) canvasReadout.text = RestockRules.Describe(selection);
        }
    }
}
