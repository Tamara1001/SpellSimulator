using System.Reflection;
using UnityEngine;
using SpellSimulator.Reactions;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Arena del Duelo (D1c): una plataforma cuadrada en el cielo, lejos del mapa, con muros altos, pilares y muros bajos de cobertura y una fuente de
    /// agua en el centro. Se arma en cada maquina con las mismas medidas (nada se replica: son colliders fijos), solo mientras dura el duelo.
    /// </summary>
    public static class DuelArena
    {
        public static readonly Vector3 Center = new Vector3(0f, 100f, 0f);
        public const float Half = 20f;
        public static GameObject Root { get; private set; }

        public static bool Exists => Root != null;

        public static void Build()
        {
            if (Root != null) return;
            Root = new GameObject("DuelArena");
            Root.transform.position = Center;
            var stone = MakeMaterial(new Color(0.55f, 0.55f, 0.58f));
            var floorMat = MakeMaterial(new Color(0.42f, 0.5f, 0.38f));

            Piece("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(Half * 2f, 0.5f, Half * 2f), floorMat);
            // muros del perimetro (2,5 m de alto) para no caerse
            Piece("WallN", new Vector3(0f, 1.25f, Half), new Vector3(Half * 2f + 1f, 2.5f, 1f), stone);
            Piece("WallS", new Vector3(0f, 1.25f, -Half), new Vector3(Half * 2f + 1f, 2.5f, 1f), stone);
            Piece("WallE", new Vector3(Half, 1.25f, 0f), new Vector3(1f, 2.5f, Half * 2f + 1f), stone);
            Piece("WallW", new Vector3(-Half, 1.25f, 0f), new Vector3(1f, 2.5f, Half * 2f + 1f), stone);
            // cobertura: pilares altos y muros bajos, simetricos para que el duelo sea justo
            foreach (float sx in new[] { -1f, 1f })
            {
                Piece("Pillar", new Vector3(sx * 5f, 2f, 7f), new Vector3(2f, 4f, 2f), stone);
                Piece("Pillar", new Vector3(sx * 5f, 2f, -7f), new Vector3(2f, 4f, 2f), stone);
                Piece("LowWall", new Vector3(sx * 13f, 0.75f, 0f), new Vector3(1f, 1.5f, 7f), stone);
                Piece("LowWall", new Vector3(sx * 9f, 0.75f, 11f), new Vector3(5f, 1.5f, 1f), stone);
                Piece("LowWall", new Vector3(sx * 9f, 0.75f, -11f), new Vector3(5f, 1.5f, 1f), stone);
            }
            BuildFountain();
        }

        public static void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
            Root = null;
        }

        private static void BuildFountain()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Fountain";
            go.transform.SetParent(Root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            go.transform.localScale = new Vector3(7f, 0.6f, 7f);
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true; // el agua se atraviesa: es un volumen, no un bloque
            var water = go.AddComponent<WaterVolume>();
            // Toma la superficie y el aspecto de un agua que ya existe en el mapa (campos privados del componente).
            Renderer mine = go.GetComponent<Renderer>();
            if (WaterVolume.All.Count > 0)
            {
                WaterVolume reference = WaterVolume.All[0];
                const BindingFlags f = BindingFlags.Instance | BindingFlags.NonPublic;
                var surfaceField = typeof(WaterVolume).GetField("surface", f);
                var outputField = typeof(WaterVolume).GetField("output", f);
                var refSurface = surfaceField != null ? surfaceField.GetValue(reference) as Renderer : null;
                if (refSurface != null) mine.sharedMaterial = refSurface.sharedMaterial;
                if (surfaceField != null) surfaceField.SetValue(water, mine);
                if (outputField != null) outputField.SetValue(water, outputField.GetValue(reference));
            }
            else mine.sharedMaterial = MakeMaterial(new Color(0.25f, 0.5f, 0.85f));
        }

        private static void Piece(string name, Vector3 localPos, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(Root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }
    }
}
