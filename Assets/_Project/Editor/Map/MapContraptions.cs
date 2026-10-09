using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Runas (M6 + bloque 4 del plan): losas con una copia del papel del laboratorio de cara al cielo y un SealTrigger
    /// que deja el sello armado. Todas funcionan igual y por su cuenta; el material (<see cref="RuneKind"/>) cambia
    /// durabilidad, peso y aspecto: PIEDRA gris de bordes irregulares, METAL de acero azulado con marco y remaches de laton,
    /// ORO con borde de muescas, adornos y brillo propio. Cada runa trae sus grietas pre-armadas (<see cref="RuneVisuals"/>).
    /// Campo de practica: dos de piedra sueltas, dos de piedra juntas (para probar la fusion), una de metal y la placa de
    /// presion fija con su pesa. La unica de oro esta en el estudio de la torre, sobre un pedestal.
    /// </summary>
    public static class MapContraptions
    {
        private const string GlowMaterialPath = "Assets/_Project/Materials/RuneGlowGold.mat";

        // Restos de versiones anteriores (sello partido en dos mitades): se quitan de la escena.
        private static readonly string[] Obsolete = { "SplitHalf_A_L", "SplitHalf_A_R" };

        private static readonly Color StoneSide = new Color(0.5f, 0.5f, 0.53f);
        private static readonly Color StoneTop = new Color(0.68f, 0.68f, 0.7f);
        private static readonly Color SteelSide = new Color(0.22f, 0.26f, 0.34f);
        private static readonly Color SteelTop = new Color(0.35f, 0.4f, 0.5f);
        private static readonly Color Brass = new Color(0.8f, 0.62f, 0.28f);
        private static readonly Color RivetColor = new Color(0.72f, 0.74f, 0.78f);
        private static readonly Color GoldSide = new Color(0.86f, 0.64f, 0.2f);
        private static readonly Color GoldTop = new Color(0.98f, 0.8f, 0.36f);
        private static readonly Color GoldBright = new Color(1f, 0.9f, 0.55f);
        private static readonly Color GoldGlow = new Color(1f, 0.82f, 0.42f);
        private static readonly Color CrackColor = new Color(0.07f, 0.06f, 0.06f);
        private static readonly Color Worn = new Color(0.17f, 0.16f, 0.16f);

        public static void Build(Transform map, Material material, Vector3 origin)
        {
            foreach (string obsolete in Obsolete)
            {
                var stale = GameObject.Find(obsolete);
                if (stale != null) Object.DestroyImmediate(stale);
            }
            var template = GameObject.Find("PaperCanvas");
            if (template == null) return;
            var caster = Object.FindFirstObjectByType<SpellCaster>();
            var stoneProfile = AssetDatabase.LoadAssetAtPath<MaterialProfile>("Assets/_Project/Data/Materials/Piedra.asset");

            // Campo: dos piedras de impacto (mediana y chica), una de metal y la placa de presion con su pesa.
            Slab(template, material, caster, stoneProfile, RuneKind.Stone, "ImpactStone_M", origin + new Vector3(-0.6f, 0.2f, 0f), 0.34f, 0.07f, false, SealTrigger.Mode.Impact, 3f);
            Slab(template, material, caster, stoneProfile, RuneKind.Stone, "ImpactStone_S", origin + new Vector3(0.15f, 0.2f, 0f), 0.2f, 0.06f, false, SealTrigger.Mode.Impact, 3f);
            Slab(template, material, caster, stoneProfile, RuneKind.Metal, "ImpactMetal_M", origin + new Vector3(-0.6f, 0.2f, -0.8f), 0.3f, 0.085f, false, SealTrigger.Mode.Impact, 3f);
            Slab(template, material, caster, stoneProfile, RuneKind.Stone, "PressurePlate", origin + new Vector3(0f, 0.1f, 2.2f), 0.9f, 0.1f, true, SealTrigger.Mode.Pressure, 1.5f);
            var plate = GameObject.Find("PressurePlate");
            if (plate != null) plate.GetComponent<SealTrigger>().SetPermanent(5, 3f);
            // Dos piedras comunes juntas (donde estaban las mitades): armadas y en contacto se fusionan en un hechizo.
            Slab(template, material, caster, stoneProfile, RuneKind.Stone, "ImpactStone_J1", origin + new Vector3(-1.1f, 0.2f, -1.5f), 0.26f, 0.06f, false, SealTrigger.Mode.Impact, 3f);
            Slab(template, material, caster, stoneProfile, RuneKind.Stone, "ImpactStone_J2", origin + new Vector3(-0.75f, 0.2f, -1.5f), 0.2f, 0.06f, false, SealTrigger.Mode.Impact, 3f);
            Weight(map, material, stoneProfile, origin + new Vector3(1.0f, 0.3f, 0f));

            // Oro (rara): en el estudio (primer piso), sobre un pedestal frente al estante de 285 grados, en el area
            // central libre (radio 2.2 < 2.6: fuera del anillo de rampas y del hueco de la rampa de abajo).
            Vector3 pedestalAt = MapBuilder.TowerOrigin() + TowerBuilder.Polar(2.2f, 285f, TowerBuilder.FloorHeight);
            float pedestalTop = Pedestal(map, material, pedestalAt);
            Slab(template, material, caster, stoneProfile, RuneKind.Gold, "ImpactGold_S", pedestalAt + Vector3.up * (pedestalTop + 0.09f), 0.18f, 0.06f, false, SealTrigger.Mode.Impact, 3f);
        }

        /// <summary>
        /// Runa suelta de impacto con el mismo armado que las del campo (tamano y espesor por material), para reutilizar fuera de
        /// aca (plantillas de reaparicion y de reciclaje en MapStructures). Queda en la raiz de la escena: el llamador la reubica.
        /// </summary>
        public static GameObject CreateRune(RuneKind kind, string name, Vector3 position, Material material)
        {
            var template = GameObject.Find("PaperCanvas");
            if (template == null) return null;
            var caster = Object.FindFirstObjectByType<SpellCaster>();
            var stoneProfile = AssetDatabase.LoadAssetAtPath<MaterialProfile>("Assets/_Project/Data/Materials/Piedra.asset");
            float size = kind == RuneKind.Metal ? 0.3f : kind == RuneKind.Gold ? 0.18f : 0.26f;
            float thickness = kind == RuneKind.Metal ? 0.085f : 0.06f;
            return Slab(template, material, caster, stoneProfile, kind, name, position, size, thickness, false, SealTrigger.Mode.Impact, 3f);
        }

        private static GameObject Slab(GameObject template, Material material, SpellCaster caster, MaterialProfile profile, RuneKind kind, string name,
            Vector3 position, float size, float thickness, bool isFixed, SealTrigger.Mode mode, float threshold)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var go = Object.Instantiate(template);
            go.name = name;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
            float depth = template.transform.localScale.z;
            go.transform.localScale = new Vector3(size, size, depth);

            var rb = go.GetComponent<Rigidbody>();
            rb.mass = isFixed ? 50f : (1.5f + size * 4f) * (kind == RuneKind.Metal ? 2.5f : 1f);
            rb.isKinematic = isFixed;
            rb.useGravity = true;
            rb.linearDamping = 0.05f;

            var grab = go.GetComponent<PhysicalCanvas>();
            if (isFixed && grab != null) Object.DestroyImmediate(grab);
            else if (grab != null)
            {
                var gso = new SerializedObject(grab);
                gso.FindProperty("settleFlat").boolValue = false;
                gso.FindProperty("objectName").stringValue = kind == RuneKind.Metal ? "runa de metal" : kind == RuneKind.Gold ? "runa de oro" : "runa de piedra";
                gso.ApplyModifiedProperties();
            }

            var canvas = go.GetComponent<DrawingCanvas>();
            var cso = new SerializedObject(canvas);
            cso.FindProperty("emissionSide").enumValueIndex = (int)EmissionSide.InkSide;
            cso.ApplyModifiedProperties();

            var reactive = go.GetComponent<ElementReactive>();
            if (reactive != null && profile != null)
            {
                var rso = new SerializedObject(reactive);
                rso.FindProperty("profile").objectReferenceValue = profile;
                rso.ApplyModifiedProperties();
            }
            // Las runas sueltas conservan el SealBreaker: el agua y el fuego les borran el sello salvo que su material resista
            // (metal y oro, ver Rune.Waterproof/FireProof). La placa fija sigue sin perderlo.
            var breaker = go.GetComponent<SealBreaker>();
            if (isFixed && breaker != null) Object.DestroyImmediate(breaker);

            var trigger = go.AddComponent<SealTrigger>();
            trigger.Configure(mode, caster, threshold);
            // Dos runas armadas que se tocan se fusionan en un solo hechizo (las fijas no se mueven: no se unen).
            if (!isFixed) go.AddComponent<RuneJoin>();
            go.AddComponent<Rune>().Configure(kind);
            BuildLook(go, template, material, kind, name, size, depth, thickness);
            if (go.GetComponent<Respawnable>() == null) go.AddComponent<Respawnable>();
            Undo.RegisterCreatedObjectUndo(go, name);
            return go;
        }

        // ---------------------------------------------------------------- aspecto

        /// <summary>
        /// Arma la losa bajo el papel (hijo "Slab" en metros: compensa la escala no uniforme del padre), su variante
        /// gastada, tres juegos de grietas apagados y, en la de oro, adornos emisivos y una luz tenue. Ejes locales: el
        /// papel mira a -Z (arriba en el mundo) y la losa crece hacia +Z.
        /// </summary>
        private static void BuildLook(GameObject go, GameObject template, Material material, RuneKind kind, string name, float size, float depth, float thickness)
        {
            // Medidas del papel en metros (la malla de la plantilla, escalada como la runa).
            Bounds paper = template.GetComponent<MeshFilter>().sharedMesh.bounds;
            float pw = paper.extents.x * size, ph = paper.extents.y * size;
            float faceZ = Mathf.Min(0f, paper.min.z * depth);
            const float zTop = 0.002f;
            float zBottom = zTop + thickness;
            float border = size * (kind == RuneKind.Gold ? 0.1f : 0.08f);
            float hx = pw + border, hy = ph + border;
            // Altura del marco (metal/oro): a ras del papel, nunca encima del dibujo.
            float rimTop = Mathf.Min(faceZ - 0.001f, zTop - 0.006f);
            float crackZ = Mathf.Min(faceZ, rimTop) - 0.0012f;
            int seed = StableHash(name);

            var visual = new GameObject("Slab");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(1f / size, 1f / size, 1f / depth);

            Vector2[] outline = kind == RuneKind.Stone ? StoneOutline(hx, hy, seed)
                : kind == RuneKind.Metal ? MetalOutline(hx, hy, size * 0.04f)
                : GoldOutline(hx, hy, size);
            Mesh fresh = MapBuilder.SaveMesh(SlabMesh(kind, outline, pw, ph, border, size, zTop, zBottom, rimTop, seed, false).Build($"Rune_{kind}_{name}"));
            Mesh worn = MapBuilder.SaveMesh(SlabMesh(kind, outline, pw, ph, border, size, zTop, zBottom, rimTop, seed, true).Build($"Rune_{kind}_{name}_Gastada"));
            var filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = fresh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = visual.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0f, (zTop + zBottom) * 0.5f);
            box.size = new Vector3(hx * 2f, hy * 2f, thickness);

            // Grietas: finas -> profundas (bajan por los costados) -> rota.
            var rng = new System.Random(seed);
            var cracks = new GameObject[3];
            for (int stage = 0; stage < 3; stage++)
            {
                var m = new LowPolyMesh();
                CrackStage(m, rng, stage, outline, pw, ph, hx, hy, size, crackZ, zTop, zBottom);
                cracks[stage] = Child(visual.transform, $"Cracks{stage}", MapBuilder.SaveMesh(m.Build($"Rune_{kind}_{name}_Grietas{stage}")), material);
                cracks[stage].SetActive(false);
            }

            GameObject glow = null;
            Light light = null;
            if (kind == RuneKind.Gold)
            {
                // Hilo emisivo alrededor del papel (sobre el marco) y cuatro gemas en las esquinas.
                var g = new LowPolyMesh();
                float w = size * 0.022f, z = rimTop - 0.0015f;
                Strip(g, pw + w * 0.5f + 0.001f, ph, w, z, 0.003f, GoldGlow);
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i & 1) == 0 ? -1f : 1f, sy = (i & 2) == 0 ? -1f : 1f;
                    var at = new Vector3(sx * (pw + border * 0.5f), sy * (ph + border * 0.5f), rimTop - 0.003f);
                    g.Box(Matrix4x4.TRS(at, Quaternion.Euler(0f, 0f, 45f), Vector3.one), new Vector3(size * 0.045f, size * 0.045f, 0.008f), GoldGlow);
                }
                glow = Child(visual.transform, "RuneGlow", MapBuilder.SaveMesh(g.Build($"Rune_{kind}_{name}_Brillo")), GlowMaterial());

                // Una sola luz puntual tenue (presupuesto Quest), por encima del papel.
                var lightGo = new GameObject("RuneLight");
                lightGo.transform.SetParent(visual.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0f, -0.12f);
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 1.5f;
                light.intensity = 0.6f;
                light.color = GoldGlow;
                light.shadows = LightShadows.None;
            }

            go.AddComponent<RuneVisuals>().Configure(filter, fresh, worn, cracks, glow, light);
        }

        /// <summary>Losa con bisel (y marco/remaches/adornos segun el material). worn = variante apagada y oscura.</summary>
        private static LowPolyMesh SlabMesh(RuneKind kind, Vector2[] outline, float pw, float ph, float border, float size,
            float zTop, float zBottom, float rimTop, int seed, bool worn)
        {
            Color T(Color c) => worn ? Color.Lerp(c, Worn, 0.62f) : c;
            var m = new LowPolyMesh();
            switch (kind)
            {
                case RuneKind.Stone:
                    Prism(m, outline, zTop, zBottom, size * 0.03f, T(StoneTop), T(Shade(StoneTop, 0.9f)), T(StoneSide), seed, 0.08f);
                    break;
                case RuneKind.Metal:
                {
                    Prism(m, outline, zTop, zBottom, size * 0.025f, T(SteelTop), T(Brass), T(SteelSide), seed, 0.03f);
                    // Marco de laton alrededor del papel y una franja de laton a media altura del canto.
                    float w = border * 0.45f;
                    Strip(m, pw + 0.002f + w * 0.5f, ph + 0.002f, w, (zTop + rimTop) * 0.5f, zTop - rimTop, T(Brass));
                    // Remaches en las cuatro esquinas, sobre el marco.
                    for (int i = 0; i < 4; i++)
                    {
                        float sx = (i & 1) == 0 ? -1f : 1f, sy = (i & 2) == 0 ? -1f : 1f;
                        var at = new Vector3(sx * (pw + border * 0.5f), sy * (ph + border * 0.5f), rimTop);
                        m.Frustum(Matrix4x4.TRS(at, Quaternion.Euler(-90f, 0f, 0f), Vector3.one), size * 0.02f, size * 0.012f, 0f, 0.006f, 6, T(RivetColor));
                    }
                    break;
                }
                case RuneKind.Gold:
                {
                    Prism(m, outline, zTop, zBottom, size * 0.03f, T(GoldTop), T(GoldBright), T(GoldSide), seed, 0.05f);
                    float w = border * 0.4f;
                    Strip(m, pw + 0.002f + w * 0.5f, ph + 0.002f, w, (zTop + rimTop) * 0.5f, zTop - rimTop, T(GoldBright));
                    // Botones redondos a mitad de cada lado del marco.
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 at = i == 0 ? new Vector3(pw + border * 0.55f, 0f, rimTop) : i == 1 ? new Vector3(-pw - border * 0.55f, 0f, rimTop)
                            : i == 2 ? new Vector3(0f, ph + border * 0.55f, rimTop) : new Vector3(0f, -ph - border * 0.55f, rimTop);
                        m.Frustum(Matrix4x4.TRS(at, Quaternion.Euler(-90f, 0f, 0f), Vector3.one), size * 0.028f, size * 0.012f, 0f, 0.006f, 6, T(GoldBright));
                    }
                    break;
                }
            }
            return m;
        }

        /// <summary>Contorno de piedra: rectangulo de esquinas gastadas con variacion radial leve (determinista).</summary>
        private static Vector2[] StoneOutline(float hx, float hy, int seed)
        {
            const int n = 16;
            var rng = new System.Random(seed);
            var pts = new Vector2[n];
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float t = 1f / Mathf.Max(Mathf.Abs(c) / hx, Mathf.Abs(s) / hy);
                float corner = 1f - Mathf.Abs(Mathf.Abs(c) - Mathf.Abs(s));   // 1 en las diagonales
                float r = t * (1f - 0.05f * corner) * (1f + (float)(rng.NextDouble() * 0.06 - 0.035));
                pts[k] = new Vector2(c, s) * r;
            }
            return pts;
        }

        /// <summary>Contorno de metal: placa recta con las esquinas achaflanadas.</summary>
        private static Vector2[] MetalOutline(float hx, float hy, float chamfer) => new[]
        {
            new Vector2(hx, -hy + chamfer), new Vector2(hx, hy - chamfer), new Vector2(hx - chamfer, hy), new Vector2(-hx + chamfer, hy),
            new Vector2(-hx, hy - chamfer), new Vector2(-hx, -hy + chamfer), new Vector2(-hx + chamfer, -hy), new Vector2(hx - chamfer, -hy),
        };

        /// <summary>Contorno de oro: borde ornamentado con dos muescas en V por lado y esquinas recortadas.</summary>
        private static Vector2[] GoldOutline(float hx, float hy, float size)
        {
            float notch = size * 0.035f, half = 0.07f, cut = size * 0.03f;
            var pts = new List<Vector2>();
            // Cuatro lados en sentido antihorario: (esquina inicial, direccion a lo largo, normal hacia afuera, semilargo).
            var sides = new[]
            {
                (new Vector2(hx, -hy), Vector2.up, Vector2.right, hy),
                (new Vector2(hx, hy), Vector2.left, Vector2.up, hx),
                (new Vector2(-hx, hy), Vector2.down, Vector2.left, hy),
                (new Vector2(-hx, -hy), Vector2.right, Vector2.down, hx),
            };
            foreach (var (corner, along, outward, len) in sides)
            {
                Vector2 mid = corner + along * len;
                pts.Add(corner + along * cut);
                foreach (float u in new[] { -0.5f, 0.5f })
                {
                    pts.Add(mid + along * (u - half) * len);
                    pts.Add(mid + along * u * len - outward * notch);
                    pts.Add(mid + along * (u + half) * len);
                }
                pts.Add(corner + along * (2f * len - cut));
            }
            return pts.ToArray();
        }

        /// <summary>
        /// Prisma desde un contorno (estrellado respecto de su centro): tapa arriba (-Z) metida por el bisel, bisel con su
        /// propio color, costados verticales y tapa abajo. jitter = variacion de tono por cara.
        /// </summary>
        private static void Prism(LowPolyMesh m, Vector2[] outline, float zTop, float zBottom, float bevel, Color top, Color bevelColor, Color side, int seed, float jitter)
        {
            int n = outline.Length;
            Vector2 center = Vector2.zero;
            foreach (var p in outline) center += p;
            center /= n;
            var topRing = new Vector3[n];
            var midRing = new Vector3[n];
            var botRing = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i];
                Vector2 inset = p + (center - p).normalized * bevel;
                topRing[i] = new Vector3(inset.x, inset.y, zTop);
                midRing[i] = new Vector3(p.x, p.y, zTop + bevel * 0.8f);
                botRing[i] = new Vector3(p.x, p.y, zBottom);
            }
            var topCenter = new Vector3(center.x, center.y, zTop);
            var botCenter = new Vector3(center.x, center.y, zBottom);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 outN = EdgeNormal(outline, i);
                Face(m, topCenter, topRing[i], topRing[j], Vector3.back, LowPolyMesh.Jitter(top, jitter * 0.5f, seed + i));
                Vector3 bevelN = (outN + Vector3.back).normalized;
                Color bc = LowPolyMesh.Jitter(bevelColor, jitter * 0.5f, seed + 31 + i);
                Face(m, topRing[i], topRing[j], midRing[j], bevelN, bc);
                Face(m, topRing[i], midRing[j], midRing[i], bevelN, bc);
                Color sc = LowPolyMesh.Jitter(side, jitter, seed + 67 + i);
                Face(m, midRing[i], midRing[j], botRing[j], outN, sc);
                Face(m, midRing[i], botRing[j], botRing[i], outN, sc);
                Face(m, botCenter, botRing[j], botRing[i], Vector3.forward, Shade(side, 0.8f));
            }
        }

        /// <summary>Marco de cuatro listones alrededor de un rectangulo (semiancho hx, semialto hy medidos al eje del liston).</summary>
        private static void Strip(LowPolyMesh m, float hx, float hy, float width, float z, float height, Color color)
        {
            float outer = hx + width * 0.5f;
            m.Box(Matrix4x4.Translate(new Vector3(hx, 0f, z)), new Vector3(width, (hy + width) * 2f, height), color);
            m.Box(Matrix4x4.Translate(new Vector3(-hx, 0f, z)), new Vector3(width, (hy + width) * 2f, height), color);
            m.Box(Matrix4x4.Translate(new Vector3(0f, hy + width * 0.5f, z)), new Vector3(outer * 2f, width, height), color);
            m.Box(Matrix4x4.Translate(new Vector3(0f, -hy - width * 0.5f, z)), new Vector3(outer * 2f, width, height), color);
        }

        // ---------------------------------------------------------------- grietas

        /// <summary>
        /// Un tramo de grietas (se encienden acumuladas): 0 = una o dos finas desde el borde; 1 = dos profundas con rama
        /// que cruzan el papel y bajan por dos costados; 2 = rota: tres grietas anchas por el centro, todos los costados
        /// agrietados y esquirlas oscuras en las esquinas. Todo en un solo color oscuro, legible a distancia.
        /// </summary>
        private static void CrackStage(LowPolyMesh m, System.Random rng, int stage, Vector2[] outline, float pw, float ph,
            float hx, float hy, float size, float z, float zTop, float zBottom)
        {
            Vector2 clamp = new Vector2(hx * 0.92f, hy * 0.92f);
            switch (stage)
            {
                case 0:
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 start = EdgePoint(rng, pw, ph);
                        Crack(m, rng, start, -start + Rand2(rng) * size * 0.2f, size * 0.45f, size * 0.012f, 5, z, clamp, 0);
                    }
                    break;
                case 1:
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 start = EdgePoint(rng, pw, ph);
                        Crack(m, rng, start, -start + Rand2(rng) * size * 0.3f, size * 0.75f, size * 0.022f, 7, z, clamp, 1);
                    }
                    for (int i = 0; i < 2; i++) SideCrack(m, rng, outline, rng.Next(outline.Length), zTop, zBottom, size * 0.018f);
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = (float)(i * 2.1 + rng.NextDouble() * 0.6);
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Vector2 start = new Vector2(dir.x * pw, dir.y * ph);
                        Crack(m, rng, start, -dir, size * 1.0f, size * 0.03f, 8, z, clamp, 2);
                    }
                    for (int i = 0; i < outline.Length; i += 2) SideCrack(m, rng, outline, i, zTop, zBottom, size * 0.025f);
                    // Esquirlas: triangulos oscuros en las cuatro esquinas, como si faltaran trozos.
                    for (int i = 0; i < 4; i++)
                    {
                        float sx = (i & 1) == 0 ? -1f : 1f, sy = (i & 2) == 0 ? -1f : 1f;
                        var c = new Vector3(sx * hx * 0.9f, sy * hy * 0.9f, z);
                        float k = size * (0.07f + 0.04f * (float)rng.NextDouble());
                        Face(m, c, c - new Vector3(sx * k, 0f, 0f), c - new Vector3(0f, sy * k * 0.8f, 0f), Vector3.back, CrackColor);
                    }
                    break;
            }
        }

        /// <summary>Grieta en zigzag que se afina hacia la punta, recortada al contorno; ramas = sub-grietas mas cortas.</summary>
        private static void Crack(LowPolyMesh m, System.Random rng, Vector2 start, Vector2 dir, float length, float width, int segments,
            float z, Vector2 clamp, int branches)
        {
            var pts = new List<Vector2> { start };
            Vector2 d = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.right;
            float step = length / segments;
            for (int i = 1; i <= segments; i++)
            {
                d = Rotate(d, (float)(rng.NextDouble() * 70.0 - 35.0));
                Vector2 next = pts[i - 1] + d * step * (0.7f + 0.6f * (float)rng.NextDouble());
                if (Mathf.Abs(next.x) > clamp.x || Mathf.Abs(next.y) > clamp.y) break;
                pts.Add(next);
            }
            int count = pts.Count;
            if (count < 2) return;
            for (int i = 0; i < count - 1; i++)
            {
                float w0 = width * (1f - 0.85f * i / (count - 1)) * 0.5f;
                float w1 = width * (1f - 0.85f * (i + 1) / (count - 1)) * 0.5f;
                Vector2 n0 = Perp(pts, i), n1 = Perp(pts, i + 1);
                Vector3 a = new Vector3(pts[i].x + n0.x * w0, pts[i].y + n0.y * w0, z);
                Vector3 b = new Vector3(pts[i + 1].x + n1.x * w1, pts[i + 1].y + n1.y * w1, z);
                Vector3 c = new Vector3(pts[i + 1].x - n1.x * w1, pts[i + 1].y - n1.y * w1, z);
                Vector3 e = new Vector3(pts[i].x - n0.x * w0, pts[i].y - n0.y * w0, z);
                Face(m, a, b, c, Vector3.back, CrackColor);
                Face(m, a, c, e, Vector3.back, CrackColor);
            }
            for (int k = 0; k < branches && count > 2; k++)
            {
                int at = 1 + rng.Next(count - 2);
                Vector2 along = pts[at + 1] - pts[at];
                Vector2 side = Rotate(along, rng.NextDouble() < 0.5 ? 50f : -50f);
                Crack(m, rng, pts[at], side, length * 0.4f, width * 0.6f, 3, z, clamp, 0);
            }
        }

        /// <summary>Grieta vertical en zigzag sobre el canto (cara del contorno edge), apenas despegada hacia afuera.</summary>
        private static void SideCrack(LowPolyMesh m, System.Random rng, Vector2[] outline, int edge, float zTop, float zBottom, float width)
        {
            Vector2 p0 = outline[edge], p1 = outline[(edge + 1) % outline.Length];
            Vector3 outN = EdgeNormal(outline, edge);
            Vector2 tangent = (p1 - p0).normalized;
            float len = (p1 - p0).magnitude;
            Vector2 at = Vector2.Lerp(p0, p1, 0.3f + 0.4f * (float)rng.NextDouble());
            const int segments = 3;
            float z0 = zTop + (zBottom - zTop) * 0.15f, z1 = zBottom - (zBottom - zTop) * 0.1f;
            float prevT = 0f;
            for (int i = 0; i < segments; i++)
            {
                float nextT = Mathf.Clamp((float)(rng.NextDouble() - 0.5) * width * 2.5f, -len * 0.3f, len * 0.3f);
                float za = Mathf.Lerp(z0, z1, (float)i / segments), zb = Mathf.Lerp(z0, z1, (float)(i + 1) / segments);
                float wa = width * 0.5f * (1f - 0.6f * i / segments), wb = width * 0.5f * (1f - 0.6f * (i + 1) / segments);
                Vector3 Pt(float t, float zz, float off) => new Vector3(at.x + tangent.x * (t + off), at.y + tangent.y * (t + off), zz) + outN * 0.0015f;
                Vector3 a = Pt(prevT, za, -wa), b = Pt(prevT, za, wa), c = Pt(nextT, zb, wb), e = Pt(nextT, zb, -wb);
                Face(m, a, b, c, outN, CrackColor);
                Face(m, a, c, e, outN, CrackColor);
                prevT = nextT;
            }
        }

        // ---------------------------------------------------------------- utilidades

        /// <summary>Triangulo con la cara hacia outward (da vuelta el orden si hace falta): evita pensar el sentido a mano.</summary>
        private static void Face(LowPolyMesh m, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color color)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) m.Tri(a, c, b, color);
            else m.Tri(a, b, c, color);
        }

        /// <summary>Normal hacia afuera (en el plano XY) del lado i del contorno, segun su sentido de giro.</summary>
        private static Vector3 EdgeNormal(Vector2[] outline, int i)
        {
            float area = 0f;
            for (int k = 0; k < outline.Length; k++)
            {
                Vector2 p = outline[k], q = outline[(k + 1) % outline.Length];
                area += p.x * q.y - q.x * p.y;
            }
            Vector2 e = outline[(i + 1) % outline.Length] - outline[i];
            Vector2 n = area > 0f ? new Vector2(e.y, -e.x) : new Vector2(-e.y, e.x);
            n.Normalize();
            return new Vector3(n.x, n.y, 0f);
        }

        /// <summary>Tono mas oscuro conservando el alfa (el color de vertice no debe volverse translucido).</summary>
        private static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>Hash estable entre sesiones (string.GetHashCode no lo garantiza): misma runa, mismas grietas.</summary>
        private static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in s) h = h * 31 + ch;
                return h & 0x7fffffff;
            }
        }

        private static Vector2 Perp(List<Vector2> pts, int i)
        {
            Vector2 d = i == 0 ? pts[1] - pts[0] : i == pts.Count - 1 ? pts[i] - pts[i - 1] : pts[i + 1] - pts[i - 1];
            d.Normalize();
            return new Vector2(-d.y, d.x);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static Vector2 Rand2(System.Random rng) => new Vector2((float)(rng.NextDouble() * 2.0 - 1.0), (float)(rng.NextDouble() * 2.0 - 1.0));

        /// <summary>Punto al azar sobre el borde del papel (de donde nacen las grietas).</summary>
        private static Vector2 EdgePoint(System.Random rng, float pw, float ph)
        {
            float u = (float)(rng.NextDouble() * 2.0 - 1.0);
            switch (rng.Next(4))
            {
                case 0: return new Vector2(pw, u * ph);
                case 1: return new Vector2(-pw, u * ph);
                case 2: return new Vector2(u * pw, ph);
                default: return new Vector2(u * pw, -ph);
            }
        }

        private static GameObject Child(Transform parent, string name, Mesh mesh, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return child;
        }

        /// <summary>Material emisivo dorado suave de la runa de oro (se crea una vez, como InkwellGlow en ToolModels).</summary>
        private static Material GlowMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "RuneGlowGold" };
            m.SetColor("_BaseColor", GoldGlow);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", GoldGlow * 1.6f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(m, GlowMaterialPath);
            return m;
        }

        /// <summary>Pedestal de piedra con remate de laton y almohadon (decorativo, estatico). Devuelve la altura de su cara superior.</summary>
        private static float Pedestal(Transform map, Material material, Vector3 position)
        {
            const float top = 0.95f;
            var b = new LowPolyMesh();
            Matrix4x4 id = Matrix4x4.identity;
            var stoneDark = new Color(0.5f, 0.47f, 0.43f);
            var stoneLight = new Color(0.67f, 0.63f, 0.57f);
            b.Frustum(id, 0.23f, 0.21f, 0f, 0.09f, 8, stoneDark, true, true);       // zocalo
            b.Frustum(id, 0.13f, 0.11f, 0.09f, 0.82f, 8, stoneLight, false);        // columna
            b.Frustum(id, 0.15f, 0.15f, 0.42f, 0.45f, 8, Brass, false);             // anillo
            b.Frustum(id, 0.12f, 0.2f, 0.82f, 0.88f, 8, stoneDark, false);          // capitel
            b.Frustum(id, 0.2f, 0.2f, 0.88f, 0.91f, 8, Brass, true);                // remate de laton
            b.Frustum(id, 0.17f, 0.15f, 0.91f, top, 8, new Color(0.55f, 0.16f, 0.2f), true); // almohadon
            var go = MapBuilder.Place("RunePedestal_Study", map, MapBuilder.SaveMesh(b.Build("RunePedestal")), material, position, Quaternion.identity);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, top * 0.5f, 0f);
            col.size = new Vector3(0.32f, top, 0.32f);
            go.isStatic = true;
            return top;
        }

        private static void Weight(Transform map, Material material, MaterialProfile profile, Vector3 position)
        {
            var old = GameObject.Find("Weight");
            if (old != null) Object.DestroyImmediate(old);
            var mesh = new LowPolyMesh();
            mesh.Box(Matrix4x4.identity, new Vector3(0.45f, 0.45f, 0.45f), new Color(0.42f, 0.42f, 0.45f), new Color(0.55f, 0.55f, 0.58f));
            var go = MapBuilder.Place("Weight", map, MapBuilder.SaveMesh(mesh.Build("Weight")), material, position, Quaternion.identity);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 6f;
            go.AddComponent<BoxCollider>().size = new Vector3(0.45f, 0.45f, 0.45f);
            go.AddComponent<Respawnable>();
            var reactive = go.AddComponent<ElementReactive>();
            if (profile != null)
            {
                var so = new SerializedObject(reactive);
                so.FindProperty("profile").objectReferenceValue = profile;
                so.ApplyModifiedProperties();
            }
        }
    }
}
