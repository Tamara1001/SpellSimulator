using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Net;
using SpellSimulator.Player;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Selector de sigilo o de signo como GRILLA DE DIBUJITOS (Laboratorio v4, punto 3). Los iconos se generan en runtime, una
    /// sola vez, a partir de las plantillas de trazos del diccionario (<see cref="LabSealBench.GlyphTemplate"/>) en un atlas
    /// chico (<see cref="LabGlyphIcons"/>); cada celda es un quad con su collider (blanco de VR = celda entera, contiguas) y la
    /// primera es «ninguno». El elegido lo marca un marco dorado (lo arma MapLab) y su nombre se escribe en <see cref="nameText"/>.
    /// Sondeo del valor a 5 Hz (puede cambiar por una receta o un reinicio), sin allocations por frame.
    /// </summary>
    public class LabGlyphGrid : MonoBehaviour
    {
        private const float PollSeconds = 0.2f;
        private const int CellPixels = 64, AtlasColumns = 4;
        private static readonly Color32 Parchment = new Color32(244, 232, 200, 255);
        private static readonly Color32 Ink = new Color32(38, 24, 14, 255);
        private static readonly Color32 NoneInk = new Color32(150, 120, 100, 255);

        [SerializeField, Tooltip("Variable: Sigil o SignType.")]
        private LabSealVarId variable = LabSealVarId.Sigil;
        [SerializeField, Tooltip("Banco de sellos (vacio = LabSealBench.Current).")]
        private LabSealBench bench;
        [SerializeField, Tooltip("Material base de los iconos (con textura principal, p. ej. Canvas.mat): se instancia una vez con el atlas.")]
        private Material iconMaterial;
        [SerializeField, Tooltip("Marco dorado que marca la celda elegida (hijo de la grilla).")]
        private Transform selectionFrame;
        [SerializeField, Tooltip("Texto con el nombre del glifo elegido.")]
        private TMP_Text nameText;
        [SerializeField, Tooltip("Prefijo del nombre del elegido (p. ej. «Sigilo»).")]
        private string namePrefix = "";
        [SerializeField, Tooltip("Columnas de la grilla.")]
        private int columns = LabBenchLayout.GridColumns;
        [SerializeField, Tooltip("Paso entre celdas (m) y lado del icono.")]
        private float cellPitch = LabBenchLayout.CellPitch, cellSize = 0.077f;
        [SerializeField, Tooltip("Filas maximas reservadas en el panel (si el diccionario trae mas glifos, se avisa).")]
        private int maxRows = 3;

        private readonly List<GameObject> cells = new List<GameObject>(16);
        private readonly List<Mesh> meshes = new List<Mesh>(16);
        private Texture2D atlas;
        private Material material;
        private bool built;
        private float nextPoll;
        private int shownIndex = int.MinValue;

        /// <summary>Variable que elige.</summary>
        public LabSealVarId Variable => variable;
        /// <summary>Celdas creadas (incluida «ninguno»); 0 antes de construir.</summary>
        public int CellCount => cells.Count;
        /// <summary>True cuando ya se armaron las celdas.</summary>
        public bool Built => built;

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(LabSealVarId variable, LabSealBench bench, Material iconMaterial, Transform selectionFrame, TMP_Text nameText, int maxRows, string namePrefix)
        {
            this.namePrefix = namePrefix;
            this.variable = variable;
            this.bench = bench;
            this.iconMaterial = iconMaterial;
            this.selectionFrame = selectionFrame;
            this.nameText = nameText;
            this.maxRows = maxRows;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + PollSeconds;
            if (bench == null) bench = LabSealBench.Current;
            if (bench == null) return;
            if (!built) TryBuild();
            if (built) ShowSelection();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < meshes.Count; i++) if (meshes[i] != null) Destroy(meshes[i]);
            if (atlas != null) Destroy(atlas);
            if (material != null) Destroy(material);
        }

        /// <summary>Elige el valor de una celda (−1 = ninguno). En red no cambia nada.</summary>
        public void Choose(int value)
        {
            if (bench == null) return;
            if (NetSession.Exists)
            {
                Hud.Notify("lab", "Laboratorio: solo en modo Libre", 2.5f);
                return;
            }
            bench.SetVar(variable, value);
            nextPoll = 0f;
        }

        /// <summary>Posicion local del centro de una celda (fila 0 arriba, que es +Z en el marco del panel).</summary>
        public Vector3 CellLocal(int cell, int count)
        {
            int cols = Mathf.Max(1, columns);
            int rows = (count + cols - 1) / cols;
            int col = cell % cols, row = cell / cols;
            return new Vector3((col - (cols - 1) * 0.5f) * cellPitch, 0f, ((rows - 1) * 0.5f - row) * cellPitch);
        }

        // ---------------------------------------------------------------- construccion (una vez)

        private void TryBuild()
        {
            IReadOnlyList<string> ids = variable == LabSealVarId.Sigil ? bench.SigilIds : bench.SignIds;
            if (ids == null || ids.Count == 0) return;
            int count = ids.Count + 1;
            int cols = Mathf.Max(1, columns);
            if ((count + cols - 1) / cols > maxRows)
                Debug.LogWarning($"[Laboratorio] La grilla de {variable} necesita {(count + cols - 1) / cols} filas y el panel reserva {maxRows}: se solapa.", this);

            int atlasRows = (count + AtlasColumns - 1) / AtlasColumns;
            int width = AtlasColumns * CellPixels, height = atlasRows * CellPixels;
            var pixels = new Color32[width * height];
            for (int i = 0; i < count; i++)
            {
                int ax = (i % AtlasColumns) * CellPixels, ay = (i / AtlasColumns) * CellPixels;
                LabGlyphIcons.Fill(pixels, width, ax, ay, CellPixels, Parchment);
                if (i == 0) LabGlyphIcons.DrawNone(pixels, width, ax, ay, CellPixels, NoneInk, 4f);
                else LabGlyphIcons.Draw(bench.GlyphTemplate(ids[i - 1]), pixels, width, ax, ay, CellPixels, Ink, 4.5f);
            }
            atlas = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "LabGlyphAtlas_" + variable, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);

            if (iconMaterial == null)
            {
                Debug.LogWarning("[Laboratorio] LabGlyphGrid sin material de iconos: no se puede mostrar la grilla.", this);
                return;
            }
            material = new Material(iconMaterial) { name = "LabGlyphIcons_" + variable, mainTexture = atlas };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            LabSealVarDef def = LabSealCatalog.Get(variable);
            float lift = LabPanelLayout.TextLift;
            for (int i = 0; i < count; i++)
            {
                int ax = i % AtlasColumns, ay = i / AtlasColumns;
                var uvMin = new Vector2((ax * CellPixels + 0.5f) / width, (ay * CellPixels + 0.5f) / height);
                var uvMax = new Vector2(((ax + 1) * CellPixels - 0.5f) / width, ((ay + 1) * CellPixels - 0.5f) / height);
                Mesh mesh = Quad(cellSize, uvMin, uvMax);
                meshes.Add(mesh);

                var go = new GameObject(i == 0 ? "Cell_none" : "Cell_" + ids[i - 1]);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = CellLocal(i, count) + Vector3.up * lift;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.012f, 0f);
                col.size = new Vector3(cellPitch, 0.03f, cellPitch);
                string name = i == 0 ? "ninguno" : bench.GlyphDisplayName(ids[i - 1]);
                go.AddComponent<LabGlyphCell>().Setup(this, i - 1, renderer,
                    LabHelpStrip.Line(LabSealCatalog.GroupName(def.Group) + " · " + def.Label + ": " + name, def.Help),
                    "Elegir " + name);
                cells.Add(go);
            }
            built = true;
        }

        /// <summary>Quad acostado (normal +Y), arriba del icono hacia +Z.</summary>
        private static Mesh Quad(float size, Vector2 uvMin, Vector2 uvMax)
        {
            float h = size * 0.5f;
            var mesh = new Mesh { name = "LabGlyphCell" };
            mesh.vertices = new[] { new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h), new Vector3(h, 0f, h), new Vector3(-h, 0f, h) };
            mesh.uv = new[] { uvMin, new Vector2(uvMax.x, uvMin.y), uvMax, new Vector2(uvMin.x, uvMax.y) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            // Horario visto desde arriba (+Y): cara visible hacia el jugador.
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ShowSelection()
        {
            int value = Mathf.RoundToInt(bench.GetVar(variable));
            if (value == shownIndex) return;
            shownIndex = value;
            int cell = Mathf.Clamp(value + 1, 0, cells.Count - 1);
            if (selectionFrame != null)
            {
                Vector3 local = CellLocal(cell, cells.Count);
                selectionFrame.localPosition = new Vector3(local.x, selectionFrame.localPosition.y, local.z);
            }
            // Solo al cambiar la eleccion (no por frame).
            if (nameText != null) nameText.SetText(string.IsNullOrEmpty(namePrefix) ? bench.FormatVar(variable, value) : namePrefix + ": " + bench.FormatVar(variable, value));
        }
    }

    /// <summary>Celda de una <see cref="LabGlyphGrid"/>: se aprieta (clic / gatillo) para elegir su glifo; se resalta al apuntar.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LabGlyphCell : MonoBehaviour, IPressable, LabParameters.IHoverTarget, ILabHelp
    {
        private LabGlyphGrid grid;
        private int value;
        private string helpLine, prompt;
        private LabControl.HoverTint tint;

        /// <summary>Valor que elige (−1 = ninguno).</summary>
        public int Value => value;
        public string HelpLine => helpLine;
        public bool PressWithClick => true;

        public void Setup(LabGlyphGrid grid, int value, Renderer renderer, string helpLine, string prompt)
        {
            this.grid = grid;
            this.value = value;
            this.helpLine = helpLine;
            this.prompt = prompt;
            tint.Init(renderer);
        }

        public string GetPressPrompt() => prompt ?? string.Empty;

        public void Press()
        {
            if (grid != null) grid.Choose(value);
        }

        public void SetHovered(bool on)
        {
            tint.Set(on);
            if (on) LabHelpStrip.Focus(this);
            else LabHelpStrip.Unfocus(this);
        }

        private void OnDisable()
        {
            tint.Set(false);
            LabHelpStrip.Unfocus(this);
        }
    }
}
