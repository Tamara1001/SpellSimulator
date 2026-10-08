using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SpellSimulator.Glyphs;
using SpellSimulator.Learning;
using SpellSimulator.Player;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Genera el aspecto del grimorio (estilo Witch Hat Atelier: cuero violeta, pergamino calido, oro viejo, cera roja):
    /// texturas procedurales (pergamino envejecido con filete y esquineros, cuero con grano y gofrado, filigrana y emblema)
    /// como PNG, los materiales URP que las usan y los asigna al <see cref="Grimoire"/> de la escena abierta.
    /// Idempotente: se puede correr de nuevo (sobrescribe texturas y valores). Menu: SpellSimulator > Learning > Setup Grimoire.
    /// </summary>
    public static class GrimoireSetup
    {
        private const string TextureDir = "Assets/_Project/Art/Grimoire";
        private const string MaterialDir = "Assets/_Project/Materials/Grimoire";

        // Medidas de la pagina (m), iguales a Grimoire.PageWidth/PageHeight.
        private const float PageW = 0.27f, PageH = 0.36f;

        [MenuItem("SpellSimulator/Learning/Setup Grimoire")]
        public static string Setup()
        {
            Directory.CreateDirectory(TextureDir);
            Directory.CreateDirectory(MaterialDir);

            Texture2D parchment = SaveTexture("GrimoireParchment", Parchment(768, 1024), TextureWrapMode.Clamp, false);
            Texture2D leatherTooled = SaveTexture("GrimoireLeatherTooled", Leather(512, 512, true), TextureWrapMode.Clamp, false);
            Texture2D leatherPlain = SaveTexture("GrimoireLeather", Leather(256, 256, false), TextureWrapMode.Repeat, false);
            Texture2D ornament = SaveTexture("GrimoireOrnament", Ornament(512, 64), TextureWrapMode.Clamp, true);
            Texture2D emblem = SaveTexture("GrimoireEmblem", Emblem(512), TextureWrapMode.Clamp, true);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader page = Shader.Find("SpellSimulator/GrimoirePage");
            Shader ink = Shader.Find("SpellSimulator/GrimoireInk");

            Material pageLeft = Mat("GrimoirePageLeft", page, m => { m.SetTexture("_Parchment", parchment); m.SetFloat("_Mirror", 0f); m.SetColor("_Tint", Color.white); });
            Material pageRight = Mat("GrimoirePageRight", page, m => { m.SetTexture("_Parchment", parchment); m.SetFloat("_Mirror", 1f); m.SetColor("_Tint", Color.white); });
            Material cover = Mat("GrimoireLeatherCover", lit, m => LitSetup(m, leatherTooled, Color.white, 0f, 0.38f));
            Material leather = Mat("GrimoireLeather", lit, m => LitSetup(m, leatherPlain, new Color(0.62f, 0.58f, 0.66f), 0f, 0.32f));
            Material leatherHover = Mat("GrimoireLeatherHover", lit, m => LitSetup(m, leatherPlain, new Color(1.05f, 0.82f, 0.95f), 0f, 0.45f));
            Material gold = Mat("GrimoireGold", lit, m => LitSetup(m, null, new Color(0.84f, 0.64f, 0.27f), 0.85f, 0.62f));
            Material edge = Mat("GrimoirePageEdge", lit, m => LitSetup(m, null, new Color(0.95f, 0.9f, 0.76f), 0f, 0.08f));
            Material ribbon = Mat("GrimoireRibbon", lit, m => LitSetup(m, null, new Color(0.5f, 0.07f, 0.12f), 0f, 0.45f));
            Material wax = Mat("GrimoireWax", lit, m => LitSetup(m, null, new Color(0.62f, 0.09f, 0.07f), 0f, 0.6f));
            Material orn = Mat("GrimoireOrnament", ink, m => { m.SetTexture("_BaseMap", ornament); m.SetColor("_BaseColor", new Color(0.52f, 0.34f, 0.13f, 0.95f)); });
            Material emb = Mat("GrimoireEmblem", ink, m => { m.SetTexture("_BaseMap", emblem); m.SetColor("_BaseColor", new Color(0.9f, 0.72f, 0.36f, 1f)); });
            AssetDatabase.SaveAssets();

            // Asignar al grimorio de la escena abierta.
            var grimoire = Object.FindFirstObjectByType<Grimoire>(FindObjectsInactive.Include);
            if (grimoire == null) return "Materiales listos; no hay Grimoire en la escena abierta (correr Learning > Setup Current Scene).";
            var so = new SerializedObject(grimoire);
            Set(so, "pageMaterial", pageLeft);
            Set(so, "pageRightMaterial", pageRight);
            Set(so, "coverMaterial", cover);
            Set(so, "goldMaterial", gold);
            Set(so, "edgeMaterial", edge);
            Set(so, "ribbonMaterial", ribbon);
            Set(so, "waxMaterial", wax);
            Set(so, "buttonMaterial", leather);
            Set(so, "buttonHoverMaterial", leatherHover);
            Set(so, "ornamentMaterial", orn);
            Set(so, "emblemMaterial", emb);
            if (so.FindProperty("brushMaterial").objectReferenceValue == null)
                Set(so, "brushMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/BrushMaterial.mat"));
            if (so.FindProperty("dictionary").objectReferenceValue == null)
                Set(so, "dictionary", AssetDatabase.LoadAssetAtPath<GlyphDictionaryAsset>("Assets/_Project/Data/GlyphDictionary.asset"));
            if (so.FindProperty("input").objectReferenceValue == null)
                Set(so, "input", Object.FindFirstObjectByType<PlayerInputReader>());
            AssignSpellFiles(so);
            so.FindProperty("openDistance").floatValue = 0.72f;
            so.ApplyModifiedProperties();

            var scene = grimoire.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"Grimorio equipado en {scene.name}: texturas en {TextureDir}, materiales en {MaterialDir}.";
        }

        /// <summary>Asigna los JSON de Data/Spells al grimorio (seccion Hechizos). Idempotente.</summary>
        private static void AssignSpellFiles(SerializedObject so)
        {
            var array = so.FindProperty("spellFiles");
            if (array == null) return;
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Project/Data/Spells" });
            array.arraySize = guids.Length;
            for (int i = 0; i < guids.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guids[i]));
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        // ---------------------------------------------------------------- assets

        private static Material Mat(string name, Shader shader, System.Action<Material> configure)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            configure(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void LitSetup(Material m, Texture2D tex, Color color, float metallic, float smoothness)
        {
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
        }

        private static Texture2D SaveTexture(string name, Texture2D tex, TextureWrapMode wrap, bool alpha)
        {
            string path = $"{TextureDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = alpha;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.wrapMode = wrap;
            importer.anisoLevel = 4;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------- texturas procedurales

        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x + i * 17.3f, y + i * 31.7f) * amp;
                norm += amp;
                x *= 2.03f;
                y *= 2.03f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>Cobertura antialiasada de una linea a distancia d (m) con medio grosor half; px = tamano del pixel.</summary>
        private static float Stroke(float d, float half, float px) => Mathf.Clamp01((half - Mathf.Abs(d)) / px + 0.5f);

        /// <summary>
        /// Pergamino calido: fibras y manchas suaves, bordes tostados con ruido, sombra del lomo a la derecha (u = 1),
        /// doble filete sepia con esquineros (anillo + rombo) y rombos a mitad de los lados.
        /// </summary>
        private static Texture2D Parchment(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var pixels = new Color[w * h];
            float px = PageW / w;
            var light = new Color(0.965f, 0.91f, 0.775f);
            var dark = new Color(0.885f, 0.8f, 0.62f);
            var burnt = new Color(0.55f, 0.38f, 0.2f);
            var sepia = new Color(0.5f, 0.33f, 0.15f);
            var rng = new System.Random(7);
            // Manchas (centro en m, radio en m).
            var stains = new Vector3[5];
            for (int i = 0; i < stains.Length; i++)
                stains[i] = new Vector3((float)rng.NextDouble() * PageW, (float)rng.NextDouble() * PageH, 0.02f + (float)rng.NextDouble() * 0.05f);

            const float Line1 = 0.0115f, Line2 = 0.0145f;
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float u = (i + 0.5f) / w, v = (j + 0.5f) / h;
                    float x = u * PageW, y = v * PageH;
                    float n = Fbm(x * 18f, y * 18f, 4);
                    float fibers = Mathf.PerlinNoise(x * 900f, y * 120f) * 0.6f + Mathf.PerlinNoise(x * 300f + 5f, y * 1400f) * 0.4f;
                    Color c = Color.Lerp(light, dark, Mathf.SmoothStep(0.3f, 0.75f, n));
                    c *= 0.975f + 0.045f * fibers;

                    // Manchas: un velo leve y un anillo un poco mas oscuro (como de taza o de agua).
                    float stain = 0f;
                    foreach (var s in stains)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(s.x, s.y));
                        float wobble = (Mathf.PerlinNoise(x * 60f + s.x * 10f, y * 60f) - 0.5f) * s.z * 0.3f;
                        float r = s.z + wobble;
                        stain += Mathf.Exp(-Mathf.Pow((d - r) / (s.z * 0.06f), 2f)) * 0.03f;
                        stain += Mathf.Clamp01(1f - d / r) * 0.025f;
                    }
                    c = Color.Lerp(c, burnt, Mathf.Clamp01(stain));

                    // Bordes tostados e irregulares.
                    float edge = Mathf.Min(Mathf.Min(x, PageW - x), Mathf.Min(y, PageH - y));
                    edge += (Fbm(x * 40f, y * 40f, 3) - 0.5f) * 0.012f;
                    c = Color.Lerp(c, burnt, Mathf.Pow(Mathf.Clamp01(1f - edge / 0.016f), 2f) * 0.55f);
                    c = Color.Lerp(c, burnt, Mathf.Clamp01(1f - edge / 0.045f) * 0.12f);

                    // Sombra del lomo (lado derecho de la pagina izquierda; la derecha usa el pergamino espejado).
                    float gutter = Mathf.Clamp01((u - 0.82f) / 0.18f);
                    c *= 1f - gutter * gutter * 0.3f;

                    // Doble filete y adornos.
                    float ink = 0f;
                    float dx1 = Mathf.Max(Line1 - x, x - (PageW - Line1)), dy1 = Mathf.Max(Line1 - y, y - (PageH - Line1));
                    float rect1 = Mathf.Max(dx1, dy1); // 0 sobre el filete, <0 adentro
                    float dx2 = Mathf.Max(Line2 - x, x - (PageW - Line2)), dy2 = Mathf.Max(Line2 - y, y - (PageH - Line2));
                    float rect2 = Mathf.Max(dx2, dy2);
                    ink = Mathf.Max(ink, Stroke(rect1, 0.00055f, px));
                    ink = Mathf.Max(ink, Stroke(rect2, 0.00025f, px) * 0.85f);
                    // Esquineros: anillo y rombo en cada esquina interior.
                    float cx = x < PageW / 2f ? Line2 + 0.0085f : PageW - Line2 - 0.0085f;
                    float cy = y < PageH / 2f ? Line2 + 0.0085f : PageH - Line2 - 0.0085f;
                    float dc = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    ink = Mathf.Max(ink, Stroke(dc - 0.0055f, 0.0003f, px));
                    float diamond = (Mathf.Abs(x - cx) + Mathf.Abs(y - cy)) - 0.0028f;
                    ink = Mathf.Max(ink, Mathf.Clamp01(-diamond / px + 0.5f));
                    // Rombos chicos a mitad de arriba y abajo, sobre el filete.
                    float my = y < PageH / 2f ? Line1 : PageH - Line1;
                    float md = (Mathf.Abs(x - PageW / 2f) + Mathf.Abs(y - my) * 1.6f) - 0.0032f;
                    ink = Mathf.Max(ink, Mathf.Clamp01(-md / px + 0.5f));
                    c = Color.Lerp(c, sepia, ink * 0.8f);

                    c.a = 1f;
                    pixels[j * w + i] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Cuero violeta con grano y arrugas; tooled = gofrado dorado (doble filete) y bordes gastados.</summary>
        private static Texture2D Leather(int w, int h, bool tooled)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var pixels = new Color[w * h];
            var baseColor = new Color(0.36f, 0.19f, 0.44f);
            var deep = new Color(0.2f, 0.09f, 0.26f);
            var gold = new Color(0.8f, 0.62f, 0.28f);
            float px = 1f / w;
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float u = (i + 0.5f) / w, v = (j + 0.5f) / h;
                    float mottle = Fbm(u * 5f, v * 5f, 4);
                    float grain = Mathf.PerlinNoise(u * 140f, v * 140f);
                    float crease = 1f - Mathf.Abs(Fbm(u * 22f + 3f, v * 22f, 3) * 2f - 1f);
                    Color c = Color.Lerp(baseColor * 1.12f, deep, Mathf.SmoothStep(0.25f, 0.8f, mottle));
                    c *= 0.9f + 0.18f * grain;
                    c = Color.Lerp(c, deep, Mathf.Pow(crease, 12f) * 0.5f);
                    if (tooled)
                    {
                        // Bordes gastados (mas oscuros) que fingen un bisel.
                        float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                        c = Color.Lerp(c, deep * 0.7f, Mathf.Clamp01(1f - edge / 0.035f) * 0.6f);
                        // Gofrado dorado: doble filete con hueco.
                        float rect = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f));
                        float tool = Mathf.Max(Stroke(rect - 0.44f, 0.004f, px), Stroke(rect - 0.425f, 0.0018f, px));
                        float wear = Mathf.Lerp(0.55f, 1f, Mathf.PerlinNoise(u * 30f, v * 30f));
                        c = Color.Lerp(c, gold, tool * wear);
                    }
                    c.a = 1f;
                    pixels[j * w + i] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Filigrana horizontal: linea que se afina, rombo central calado, volutas y puntos (alfa).</summary>
        private static Texture2D Ornament(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var pixels = new Color[w * h];
            float half = w / (float)h / 2f; // x en [-half, half], y en [-0.5, 0.5]
            float px = 1f / h;
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float x = ((i + 0.5f) / w - 0.5f) * 2f * half;
                    float y = (j + 0.5f) / h - 0.5f;
                    float ax = Mathf.Abs(x);
                    float a = 0f;
                    // Linea que se afina hacia las puntas, cortada en el centro.
                    float reach = half - 0.35f;
                    if (ax > 0.62f && ax < reach)
                    {
                        float t = (ax - 0.62f) / (reach - 0.62f);
                        float thick = Mathf.Lerp(0.05f, 0.012f, t);
                        a = Mathf.Max(a, Stroke(y, thick, px));
                    }
                    // Rombo calado y punto central.
                    float d = ax + Mathf.Abs(y) * 1.1f;
                    a = Mathf.Max(a, Stroke(d - 0.36f, 0.04f, px));
                    a = Mathf.Max(a, Mathf.Clamp01((0.11f - d) / px + 0.5f));
                    // Puntos a los lados del rombo y en las puntas.
                    a = Mathf.Max(a, Mathf.Clamp01((0.07f - Vector2.Distance(new Vector2(ax, y), new Vector2(0.52f, 0f))) / px + 0.5f));
                    a = Mathf.Max(a, Mathf.Clamp01((0.06f - Vector2.Distance(new Vector2(ax, y), new Vector2(reach + 0.12f, 0f))) / px + 0.5f));
                    // Volutas: medio anillo arriba y medio abajo (forma de S) a cada lado.
                    float sx = ax - 1.25f;
                    float r1 = Vector2.Distance(new Vector2(sx, y), new Vector2(-0.18f, 0.17f));
                    float r2 = Vector2.Distance(new Vector2(sx, y), new Vector2(0.18f, -0.17f));
                    if (y > 0.04f || sx < -0.18f) a = Mathf.Max(a, Stroke(r1 - 0.17f, 0.026f, px) * (y > -0.02f ? 1f : 0f));
                    if (y < -0.04f || sx > 0.18f) a = Mathf.Max(a, Stroke(r2 - 0.17f, 0.026f, px) * (y < 0.02f ? 1f : 0f));
                    pixels[j * w + i] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Emblema de la contratapa: doble anillo con puntos, estrella de ocho puntas calada y centro (alfa).</summary>
        private static Texture2D Emblem(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var pixels = new Color[size * size];
            float px = 1f / size;
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) / size - 0.5f, y = (j + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    float ang = Mathf.Atan2(y, x);
                    float a = 0f;
                    a = Mathf.Max(a, Stroke(r - 0.46f, 0.012f, px));
                    a = Mathf.Max(a, Stroke(r - 0.405f, 0.005f, px));
                    // Puntos entre los anillos.
                    float step = Mathf.PI * 2f / 24f;
                    float k = Mathf.Round(ang / step) * step;
                    var dot = new Vector2(Mathf.Cos(k), Mathf.Sin(k)) * 0.433f;
                    a = Mathf.Max(a, Mathf.Clamp01((0.009f - Vector2.Distance(new Vector2(x, y), dot)) / px + 0.5f));
                    // Estrella de ocho puntas: dos cuadrados (uno girado 45 grados), solo el contorno.
                    float sq1 = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                    float rx = (x + y) * 0.7071f, ry = (x - y) * 0.7071f;
                    float sq2 = Mathf.Max(Mathf.Abs(rx), Mathf.Abs(ry));
                    a = Mathf.Max(a, Stroke(sq1 - 0.2f, 0.007f, px));
                    a = Mathf.Max(a, Stroke(sq2 - 0.2f, 0.007f, px));
                    a = Mathf.Max(a, Stroke(r - 0.13f, 0.005f, px));
                    a = Mathf.Max(a, Mathf.Clamp01((0.055f - r) / px + 0.5f));
                    pixels[j * size + i] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
