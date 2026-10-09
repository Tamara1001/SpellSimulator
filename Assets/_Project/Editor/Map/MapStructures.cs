using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;
using SpellSimulator.World;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Estructuras chicas del modo Libre (bloque 6 del plan): torre de vigia, dos cabanas, cueva, cuatro peñascos de
    /// cobertura, circulo de piedras, santuario con estacion de reciclaje, puesto de suministros y los cuatro portales
    /// (estudio de la torre, vigia, circulo y cueva). Dan puntos de reaparicion (cofres, bandejas), tinteros fijos,
    /// coberturas y alturas para PvP, libros de aprendizaje y terminales de portal.
    /// Los sitios salen solo de <see cref="ValleyShape"/> (deterministas, <see cref="Sites"/>): lejos del lago, el arroyo, la
    /// loma de la torre, el campo, los caminos y las zonas de practica; se limpian los pinos y rocas que caen encima.
    /// Todo cuelga de Map/Structs (se rehace con el mapa). Lo llama MapBuilder.Build despues de CanvasDesigns.
    /// Presupuesto Quest/WebGL: una malla por estructura con el material compartido, una sola luz tenue (la cueva).
    /// </summary>
    public static class MapStructures
    {
        public const string RootName = "Structs";
        private const string Prefix = "Struct_";
        private const string PortalMaterialPath = "Assets/_Project/Materials/PortalSurface.mat";

        // Reaparicion (s). Ninguna estructura regala oro: solo el relicario del santuario, lento.
        private const float PaperCooldown = 25f, StoneCooldown = 45f, MetalCooldown = 90f, GoldCooldown = 600f;

        private static readonly Color WoodA = new Color(0.45f, 0.3f, 0.19f);
        private static readonly Color WoodB = new Color(0.33f, 0.21f, 0.13f);
        private static readonly Color Plank = new Color(0.58f, 0.41f, 0.26f);
        private static readonly Color StoneA = new Color(0.68f, 0.64f, 0.57f);
        private static readonly Color StoneB = new Color(0.52f, 0.49f, 0.44f);
        private static readonly Color Rock = new Color(0.58f, 0.55f, 0.52f);
        private static readonly Color Moss = new Color(0.36f, 0.55f, 0.27f);
        private static readonly Color CaveWall = new Color(0.3f, 0.28f, 0.28f);
        private static readonly Color CaveFloor = new Color(0.36f, 0.33f, 0.3f);
        private static readonly Color Plaster = new Color(0.88f, 0.82f, 0.7f);
        private static readonly Color Roof = new Color(0.66f, 0.27f, 0.2f);
        private static readonly Color RoofDark = new Color(0.5f, 0.19f, 0.15f);
        private static readonly Color Shutter = new Color(0.27f, 0.45f, 0.5f);
        private static readonly Color Cloth = new Color(0.75f, 0.2f, 0.16f);
        private static readonly Color ClothLight = new Color(0.95f, 0.9f, 0.78f);
        private static readonly Color Iron = new Color(0.2f, 0.19f, 0.2f);
        private static readonly Color Brass = new Color(0.8f, 0.62f, 0.28f);
        private static readonly Color Keystone = new Color(0.42f, 0.38f, 0.55f);
        private static readonly Color Inlay = new Color(0.32f, 0.36f, 0.62f);
        private static readonly Color Crystal = new Color(0.55f, 0.85f, 0.95f);
        private static readonly Color Cushion = new Color(0.55f, 0.16f, 0.2f);
        private static readonly Color PortalDark = new Color(0.09f, 0.07f, 0.16f);

        /// <summary>Mesas del campo (estampas, herramientas, artilugios): se dejan libres.</summary>
        private static readonly Vector2 StampArea = new Vector2(28f, -16.5f);

        private static Material lit;
        private static Transform map;
        private static readonly StringBuilder report = new StringBuilder();

        // ---------------------------------------------------------------- sitios

        private struct Site
        {
            public Vector2 Pos;
            public Vector2 Front;
            public float Radius;
            /// <summary>Tiene piso propio (cabana, cueva): el pasto no crece adentro.</summary>
            public bool Floor;
        }

        private static Dictionary<string, Site> sites;

        /// <summary>Sitios de las estructuras (centro, frente, radio). Puro y determinista: solo depende de ValleyShape.</summary>
        private static Dictionary<string, Site> Sites()
        {
            if (sites != null) return sites;
            var result = new Dictionary<string, Site>();
            var taken = new List<Site>();
            void Add(string name, Vector2 pos, Vector2 lookAt, float radius, bool floor)
            {
                Vector2 front = lookAt - pos;
                var s = new Site { Pos = pos, Front = front.sqrMagnitude > 1e-4f ? front.normalized : Vector2.up, Radius = radius, Floor = floor };
                result[name] = s;
                taken.Add(s);
            }

            // Torre de vigia: el punto mas alto con piso razonable al noreste (la rampa baja hacia el campo).
            Vector2 best = Polar(50f, 52f);
            float bestHeight = float.MinValue;
            for (float a = 25f; a <= 75f; a += 5f)
                for (float r = 46f; r <= 60f; r += 2f)
                {
                    Vector2 p = Polar(a, r);
                    Vector2 front = (ValleyShape.FieldCenter - p).normalized;
                    if (!Clear(p, 4.5f, false, taken) || Delta(p, 3f) > 1.6f) continue;
                    if (!Clear(p + front * 10f, 2f, false, taken)) continue;
                    float h = ValleyShape.Height(p.x, p.y);
                    if (h > bestHeight) { bestHeight = h; best = p; }
                }
            if (bestHeight == float.MinValue) Debug.LogWarning("[Estructuras] sin sitio ideal para la torre de vigia: se usa el preferido.");
            Add("Watchtower", best, ValleyShape.FieldCenter, 4.5f, false);

            Add("Cave", Find(Polar(122f, 61f), 6f, 6.5f, 1.4f, true, taken), Vector2.zero, 6.5f, true);
            Add("Cabin_1", Find(new Vector2(12f, -40f), 7f, 3.6f, 0.7f, false, taken), ValleyShape.Spawn, 3.6f, true);
            Add("Cabin_2", Find(new Vector2(-28f, 34f), 7f, 3.6f, 0.7f, false, taken), ValleyShape.TowerCenter, 3.6f, true);
            Add("StoneCircle", Find(new Vector2(50f, -10f), 6f, 5.8f, 0.8f, false, taken), ValleyShape.FieldCenter, 5.8f, false);
            Add("Shrine", Find(new Vector2(34f, -36f), 6f, 3.8f, 0.6f, false, taken), ValleyShape.FieldCenter, 3.8f, false);
            Add("SupplyStall", Find(new Vector2(8f, -22f), 5f, 2.8f, 0.4f, false, taken), ValleyShape.Spawn, 2.8f, false);
            Vector2[] crags = { new Vector2(20f, -28f), new Vector2(44f, 10f), new Vector2(-6f, -40f), new Vector2(22f, 30f) };
            for (int i = 0; i < crags.Length; i++)
                Add("Crag_" + (i + 1), Find(crags[i], 7f, 6f, 1.8f, false, taken), ValleyShape.FieldCenter, 6f, false);

            sites = result;
            return sites;
        }

        /// <summary>True si (x, z) cae dentro del piso de una estructura cerrada (lo usa el pasto de MapDetails).</summary>
        public static bool Covers(float x, float z)
        {
            var p = new Vector2(x, z);
            foreach (var s in Sites().Values)
                if (s.Floor && Vector2.Distance(p, s.Pos) < s.Radius) return true;
            return false;
        }

        private static Vector2 Polar(float angleDeg, float radius) =>
            new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad)) * radius;

        /// <summary>Primer punto libre y con poco desnivel en anillos alrededor del preferido.</summary>
        private static Vector2 Find(Vector2 preferred, float search, float radius, float maxDelta, bool mountain, List<Site> taken)
        {
            for (float d = 0f; d <= search + 0.01f; d += 1.5f)
            {
                int n = d < 0.1f ? 1 : Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * d / 1.5f));
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2f / n;
                    Vector2 p = preferred + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (Clear(p, radius, mountain, taken) && Delta(p, radius * 0.8f) <= maxDelta) return p;
                }
            }
            Debug.LogWarning($"[Estructuras] sin sitio libre cerca de {preferred}: se usa el preferido.");
            return preferred;
        }

        /// <summary>Lejos del lago y sus zonas de practica, el arroyo, la loma, el campo, el spawn, los caminos y las otras estructuras.</summary>
        private static bool Clear(Vector2 p, float radius, bool mountain, List<Site> taken)
        {
            if (p.magnitude + radius > (mountain ? 70f : 63f)) return false;
            if (Vector2.Distance(p, ValleyShape.TowerCenter) < ValleyShape.HillRadius + 1f + radius) return false;
            if (Vector2.Distance(p, ValleyShape.FieldCenter) < ValleyShape.FieldRadius + 4f + radius) return false;
            if (Vector2.Distance(p, StampArea) < 5f + radius) return false;
            if (Vector2.Distance(p, ValleyShape.Spawn) < 8f + radius) return false;
            if (ValleyShape.LakeFactor(p.x, p.y) < 2.5f) return false;
            if (ValleyShape.RiverDistance(p.x, p.y) < ValleyShape.RiverHalfWidth + 4f + radius) return false;
            if (ValleyShape.PathDistance(p.x, p.y) < ValleyShape.PathWidth + 1f + radius) return false;
            // Zonas de practica junto a los senderos torre-lago y torre-campo (CanvasDesigns.PathSpots, hasta 7 m de la recta).
            if (Segment(p, ValleyShape.TowerCenter, ValleyShape.LakeCenter) < 8f + radius) return false;
            if (Segment(p, ValleyShape.TowerCenter, ValleyShape.FieldCenter) < 8f + radius) return false;
            foreach (var t in taken)
                if (Vector2.Distance(p, t.Pos) < t.Radius + radius + 3f) return false;
            return true;
        }

        /// <summary>Desnivel (m) del terreno en un cuadrado de semilado half.</summary>
        private static float Delta(Vector2 p, float half)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i <= 4; i++)
                for (int j = 0; j <= 4; j++)
                {
                    float h = ValleyShape.Height(p.x + Mathf.Lerp(-half, half, i / 4f), p.y + Mathf.Lerp(-half, half, j / 4f));
                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                }
            return max - min;
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------------------------------------------------------------- entrada

        public static void Build(Transform mapRoot, Material material)
        {
            lit = material;
            map = mapRoot;
            report.Clear();
            // Idempotente: Map se rehace entero, pero por las dudas se borran restos sueltos con el prefijo.
            foreach (var go in mapRoot.gameObject.scene.GetRootGameObjects())
                if (go.name.StartsWith(Prefix) || go.name == RootName) Object.DestroyImmediate(go);
            var old = mapRoot.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (GameObject.Find("PaperCanvas") == null || GameObject.Find("Inkwell") == null)
                Debug.LogWarning("[Estructuras] faltan PaperCanvas o Inkwell en la escena: sin papeles, placas ni tinteros.");

            var root = new GameObject(RootName).transform;
            root.SetParent(mapRoot, false);
            var s = Sites();
            Watchtower(root, s["Watchtower"]);
            Cabin(root, s["Cabin_1"], 1, "spells-wind-water", "Cabaña del sur");
            Cabin(root, s["Cabin_2"], 2, "spells-fire-earth", "Cabaña del bosque");
            Cave(root, s["Cave"]);
            for (int i = 1; i <= 4; i++) Crag(root, s["Crag_" + i], i);
            StoneCircle(root, s["StoneCircle"]);
            Shrine(root, s["Shrine"]);
            SupplyStall(root, s["SupplyStall"]);
            StudyPortal(root);
            Debug.Log("[Estructuras] posiciones:\n" + report);
        }

        // ---------------------------------------------------------------- torre de vigia

        /// <summary>Plataforma de madera a ~4 m con baranda, techo a cuatro aguas y rampa hacia el campo. Arriba: portal, cofre de piedra y pizarra.</summary>
        private static void Watchtower(Transform parent, Site site)
        {
            var t = StructRoot(parent, "Watchtower", site);
            const float post = 2.3f, edge = 2.45f, gap = 0.75f;
            HeightRange(t, -2.6f, 2.6f, -2.6f, 2.6f, out float min, out float max);
            float deck = max + 4.2f;
            var b = new LowPolyMesh();

            // Postes con zapata de piedra y cruces en los cuatro lados.
            var corners = new[] { new Vector2(-post, -post), new Vector2(post, -post), new Vector2(post, post), new Vector2(-post, post) };
            var feet = new float[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 c = corners[i];
                feet[i] = Rel(t, c.x, c.y);
                float g = feet[i] - 0.4f;
                Solid(b, t, new Vector3(c.x, (g + deck) * 0.5f, c.y), new Vector3(0.34f, deck - g, 0.34f), WoodB);
                B(b, new Vector3(c.x, feet[i] + 0.1f, c.y), new Vector3(0.6f, 0.4f, 0.6f), StoneB);
                Solid(b, t, new Vector3(c.x, deck + 1.75f, c.y), new Vector3(0.2f, 3.5f, 0.2f), WoodA);
            }
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Vector2 a = corners[i], c = corners[j];
                Beam(b, new Vector3(a.x, feet[i] + 0.6f, a.y), new Vector3(c.x, deck - 0.45f, c.y), 0.12f, WoodB);
                Beam(b, new Vector3(c.x, feet[j] + 0.6f, c.y), new Vector3(a.x, deck - 0.45f, a.y), 0.12f, WoodB);
            }

            // Piso de tablas con vigas.
            Solid(b, t, new Vector3(0f, deck - 0.1f, 0f), new Vector3(5.2f, 0.2f, 5.2f), WoodB, Plank);
            for (int k = -4; k <= 4; k++) B(b, new Vector3(k * 0.55f, deck + 0.002f, 0f), new Vector3(0.025f, 0.006f, 5.1f), WoodB);
            foreach (float z in new[] { -1.2f, 1.2f }) B(b, new Vector3(0f, deck - 0.32f, z), new Vector3(5f, 0.24f, 0.2f), WoodB);

            // Baranda con hueco al frente (llegada de la rampa).
            foreach (float y in new[] { deck + 0.5f, deck + 1f })
            {
                B(b, new Vector3(0f, y, -edge), new Vector3(4.98f, 0.08f, 0.08f), WoodA);
                B(b, new Vector3(-edge, y, 0f), new Vector3(0.08f, 0.08f, 4.98f), WoodA);
                B(b, new Vector3(edge, y, 0f), new Vector3(0.08f, 0.08f, 4.98f), WoodA);
                foreach (float sx in new[] { -1f, 1f })
                    B(b, new Vector3(sx * (gap + edge) * 0.5f, y, edge), new Vector3(edge - gap, 0.08f, 0.08f), WoodA);
            }
            for (int k = 0; k <= 6; k++)
            {
                float u = -edge + k * (2f * edge / 6f);
                var size = new Vector3(0.07f, 1.04f, 0.07f);
                B(b, new Vector3(u, deck + 0.52f, -edge), size, WoodB);
                B(b, new Vector3(-edge, deck + 0.52f, u), size, WoodB);
                B(b, new Vector3(edge, deck + 0.52f, u), size, WoodB);
                if (Mathf.Abs(u) >= gap - 0.01f) B(b, new Vector3(u, deck + 0.52f, edge), size, WoodB);
            }
            foreach (float sx in new[] { -1f, 1f }) B(b, new Vector3(sx * gap, deck + 0.6f, edge), new Vector3(0.12f, 1.2f, 0.12f), WoodB);
            Col(t, new Vector3(0f, deck + 0.55f, -edge), new Vector3(5f, 1.1f, 0.12f));
            Col(t, new Vector3(-edge, deck + 0.55f, 0f), new Vector3(0.12f, 1.1f, 5f));
            Col(t, new Vector3(edge, deck + 0.55f, 0f), new Vector3(0.12f, 1.1f, 5f));
            foreach (float sx in new[] { -1f, 1f })
                Col(t, new Vector3(sx * (gap + edge) * 0.5f, deck + 0.55f, edge), new Vector3(edge - gap, 1.1f, 0.12f));

            // Techo a cuatro aguas (cubre del fuego que cae) y remate de laton.
            b.Frustum(Matrix4x4.TRS(new Vector3(0f, deck + 3.45f, 0f), Quaternion.Euler(0f, 45f, 0f), Vector3.one), 4.3f, 0f, 0f, 1.7f, 4, Roof, false, true);
            B(b, new Vector3(0f, deck + 5.25f, 0f), new Vector3(0.14f, 0.3f, 0.14f), Brass);

            // Marco de la pizarra fija, sobre la baranda izquierda.
            Solid(b, t, new Vector3(-edge + 0.06f, deck + 1.3f, -0.6f), new Vector3(0.06f, 0.86f, 1.16f), WoodB);

            // Rampa de 30 grados hacia el frente, con listones, pasamanos y patas.
            float tan = Mathf.Tan(30f * Mathf.Deg2Rad);
            const float zTop = 2.55f;
            float run = deck / tan;
            for (int i = 0; i < 2; i++) run = Mathf.Max(3f, (deck - Rel(t, 0f, zTop + run)) / tan);
            var top = new Vector3(0f, deck, zTop);
            var bottom = new Vector3(0f, Rel(t, 0f, zTop + run), zTop + run);
            Vector3 dir = (bottom - top).normalized;
            float len = Vector3.Distance(top, bottom);
            Quaternion r = Quaternion.LookRotation(dir);
            Vector3 mid = (top + bottom + dir * 0.4f) * 0.5f + r * new Vector3(0f, -0.07f, 0f);
            var rampSize = new Vector3(1.3f, 0.14f, len + 0.4f);
            b.Box(Matrix4x4.TRS(mid, r, Vector3.one), rampSize, WoodB, Plank);
            RotCol(t, "RampCollider", mid, r, rampSize);
            for (float s = 0.35f; s < len - 0.2f; s += 0.45f)
                b.Box(Matrix4x4.TRS(top + dir * s + r * new Vector3(0f, 0.02f, 0f), r, Vector3.one), new Vector3(1.2f, 0.04f, 0.06f), WoodB);
            foreach (float sx in new[] { -0.62f, 0.62f })
            {
                Vector3 side = r * new Vector3(sx, 0f, 0f);
                foreach (float s in new[] { 0.25f, len * 0.5f, len - 0.25f })
                    B(b, top + dir * s + side + Vector3.up * 0.47f, new Vector3(0.07f, 0.95f, 0.07f), WoodA);
                Beam(b, top + dir * 0.25f + side + Vector3.up * 0.95f, top + dir * (len - 0.25f) + side + Vector3.up * 0.95f, 0.07f, WoodA);
                foreach (float s in new[] { len * 0.35f, len * 0.7f })
                {
                    Vector3 p = top + dir * s + r * new Vector3(sx * 0.8f, -0.14f, 0f);
                    float g = Rel(t, p.x, p.z) - 0.2f;
                    B(b, new Vector3(p.x, (p.y + g) * 0.5f, p.z), new Vector3(0.16f, Mathf.Max(0.1f, p.y - g), 0.16f), WoodB);
                }
            }
            Finish(t, b, "Struct_Watchtower");
            ClearVegetation(t.TransformPoint(new Vector3(0f, 0f, zTop + run * 0.5f)), run * 0.5f + 1.2f);

            BuildPortal(t, "watchtower", new Vector3(0f, deck, -1.55f), 0f, PortalBase.Floor);
            var chest = Chest(t, "SupplyChest_Watchtower", new Vector3(-1.6f, deck, 0.9f), 90f, true, out float chestTop);
            Supply(chest, MapContraptions.CreateRune(RuneKind.Stone, "RuneTemplate_Stone", chest.position, lit), chestTop + 0.07f, StoneCooldown, 2, "watchtower-stone");
            // Pizarra fija: la cara mira a la plataforma y el hechizo sale hacia afuera (lejos de quien dibuja).
            FixedCanvas(t, "Slate_Watchtower", t.TransformPoint(new Vector3(-edge + 0.1f, deck + 1.3f, -0.6f)), t.rotation * Quaternion.LookRotation(Vector3.left),
                1f, 0.7f, EmissionSide.AwayFromViewer);
            CanvasDesigns.Sign(t, lit, "Sign_Struct_Watchtower", t.TransformPoint(new Vector3(1.9f, 0f, zTop + run + 0.6f)), t.forward,
                "Torre de vigía", "Subí por la rampa: altura,\nportal y runas de piedra.");
        }

        // ---------------------------------------------------------------- cabanas

        /// <summary>Cabana de entramado con techo a dos aguas, puerta abierta y una ventana. Adentro: mesa, tintero fijo, bandeja de papeles y un libro.</summary>
        private static void Cabin(Transform parent, Site site, int index, string bookId, string title)
        {
            string name = "Cabin_" + index;
            var t = StructRoot(parent, name, site);
            float yaw = t.eulerAngles.y;
            HeightRange(t, -2.7f, 2.7f, -2.2f, 2.2f, out float min, out float max);
            const float wall = 2.5f, rise = 1.4f;
            float f = max + 0.2f, top = f + wall, ridge = top + rise, wy = f + wall * 0.5f, fb = min - 0.4f;
            var b = new LowPolyMesh();

            // Cimiento de piedra con piso de tablas.
            Solid(b, t, new Vector3(0f, (f + fb) * 0.5f, 0f), new Vector3(5.4f, f - fb, 4.4f), StoneB, Plank);
            for (int k = -4; k <= 4; k++) B(b, new Vector3(k * 0.55f, f + 0.002f, 0f), new Vector3(0.02f, 0.006f, 4f), WoodB);

            // Paredes: fondo e izquierda enteras; derecha con ventana; frente con puerta.
            Solid(b, t, new Vector3(0f, wy, -2f), new Vector3(5.2f, wall, 0.2f), Plaster);
            Solid(b, t, new Vector3(-2.5f, wy, 0f), new Vector3(0.2f, wall, 3.8f), Plaster);
            Solid(b, t, new Vector3(2.5f, f + 0.5f, 0f), new Vector3(0.2f, 1f, 3.8f), Plaster);
            Solid(b, t, new Vector3(2.5f, f + 2.2f, 0f), new Vector3(0.2f, 0.6f, 3.8f), Plaster);
            Solid(b, t, new Vector3(2.5f, f + 1.45f, -1.2f), new Vector3(0.2f, 0.9f, 1.4f), Plaster);
            Solid(b, t, new Vector3(2.5f, f + 1.45f, 1.2f), new Vector3(0.2f, 0.9f, 1.4f), Plaster);
            B(b, new Vector3(2.5f, f + 1.45f, 0f), new Vector3(0.24f, 0.06f, 1f), WoodB);
            B(b, new Vector3(2.5f, f + 1.45f, 0f), new Vector3(0.24f, 0.9f, 0.06f), WoodB);
            B(b, new Vector3(2.63f, f + 0.97f, 0f), new Vector3(0.14f, 0.06f, 1.15f), WoodB);
            foreach (float sz in new[] { -1f, 1f }) B(b, new Vector3(2.63f, f + 1.45f, sz * 0.8f), new Vector3(0.04f, 0.95f, 0.55f), Shutter);
            Solid(b, t, new Vector3(-1.55f, wy, 2f), new Vector3(2f, wall, 0.2f), Plaster);
            Solid(b, t, new Vector3(1.55f, wy, 2f), new Vector3(2f, wall, 0.2f), Plaster);
            Solid(b, t, new Vector3(0f, f + 2.3f, 2f), new Vector3(1.1f, 0.4f, 0.2f), Plaster);
            foreach (float sx in new[] { -1f, 1f }) B(b, new Vector3(sx * 0.6f, f + 1.05f, 2f), new Vector3(0.1f, 2.1f, 0.28f), WoodB);
            B(b, new Vector3(0f, f + 2.15f, 2f), new Vector3(1.3f, 0.1f, 0.28f), WoodB);
            // Puerta abierta hacia adentro, contra la pared del frente.
            Solid(b, t, new Vector3(0.47f, f + 1.03f, 1.35f), new Vector3(0.06f, 2f, 1f), WoodA);
            B(b, new Vector3(0.42f, f + 1f, 0.95f), new Vector3(0.04f, 0.06f, 0.06f), Brass);
            // Entramado: esquineros y soleras.
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    B(b, new Vector3(sx * 2.55f, wy, sz * 2.05f), new Vector3(0.26f, wall + 0.05f, 0.26f), WoodB);
            foreach (float sz in new[] { -1f, 1f }) B(b, new Vector3(0f, top + 0.06f, sz * 2.05f), new Vector3(5.4f, 0.14f, 0.26f), WoodB);
            foreach (float sx in new[] { -1f, 1f }) B(b, new Vector3(sx * 2.55f, top + 0.06f, 0f), new Vector3(0.26f, 0.14f, 4.3f), WoodB);

            // Hastiales (de dos caras) y techo a dos aguas con cumbrera y chimenea.
            foreach (float sx in new[] { -1f, 1f })
            {
                var a = new Vector3(sx * 2.5f, top, -2.1f);
                var c = new Vector3(sx * 2.5f, top, 2.1f);
                var p = new Vector3(sx * 2.5f, ridge, 0f);
                b.Tri(a, p, c, Plaster);
                b.Tri(c, p, a, Plaster);
            }
            float slope = Mathf.Atan2(rise, 2.1f);
            float len = (2.1f + 0.45f) / Mathf.Cos(slope);
            foreach (float s in new[] { -1f, 1f })
            {
                var d = new Vector3(0f, -Mathf.Sin(slope), s * Mathf.Cos(slope));
                Quaternion r = Quaternion.LookRotation(d);
                Vector3 c = new Vector3(0f, ridge, 0f) + d * (len * 0.5f) + r * new Vector3(0f, 0.08f, 0f);
                var size = new Vector3(6f, 0.16f, len);
                b.Box(Matrix4x4.TRS(c, r, Vector3.one), size, RoofDark, Roof);
                RotCol(t, "RoofCollider", c, r, size);
            }
            B(b, new Vector3(0f, ridge + 0.12f, 0f), new Vector3(6.1f, 0.16f, 0.2f), RoofDark);
            B(b, new Vector3(-1.4f, ridge - 0.2f, -0.9f), new Vector3(0.5f, 1.6f, 0.5f), StoneA);

            // Atril con el libro (mira al centro de la cabana).
            var lectern = Matrix4x4.TRS(new Vector3(1.5f, f, -1.2f), Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            b.Frustum(lectern, 0.25f, 0.12f, 0f, 0.1f, 6, WoodB);
            b.Box(lectern * Matrix4x4.Translate(new Vector3(0f, 0.6f, 0f)), new Vector3(0.1f, 1.1f, 0.1f), WoodB);
            b.Box(lectern * Matrix4x4.TRS(new Vector3(0f, 1.15f, 0f), Quaternion.Euler(-25f, 0f, 0f), Vector3.one), new Vector3(0.6f, 0.04f, 0.45f), WoodA);
            Col(t, new Vector3(1.5f, f + 0.6f, -1.2f), new Vector3(0.5f, 1.2f, 0.5f));
            Steps(b, t, 0f, 2.2f, 1.4f, f, StoneA);
            Finish(t, b, "Struct_" + name);

            TowerBuilder.Table(t, lit, "CabinTable", t.rotation * new Vector3(-1.2f, f, -1.35f), yaw, 1.6f, 0.7f);
            TowerBuilder.Chair(t, lit, t.rotation * new Vector3(-1.2f, f, -0.55f), yaw + 180f);
            float desk = f + 0.82f;
            FixedInkwell(t, "Inkwell_" + name, t.TransformPoint(new Vector3(-1.75f, desk, -1.45f)), yaw);
            var tray = Tray(t, "PaperTray_" + name, new Vector3(-0.75f, desk, -1.35f));
            Supply(tray, PaperTemplate("PaperTemplate"), 0.08f, PaperCooldown, 2, "cabin" + index + "-paper");
            Matrix4x4 bookAt = lectern * Matrix4x4.TRS(new Vector3(0f, 1.17f, 0f), Quaternion.Euler(-25f, 0f, 0f), Vector3.one);
            Book(t, bookId, t.TransformPoint(bookAt.GetColumn(3)), t.rotation * bookAt.rotation);
            CanvasDesigns.Sign(t, lit, "Sign_Struct_" + name, t.TransformPoint(new Vector3(2.3f, 0f, 3.4f)), t.forward,
                title, "Mesa con tinta, papeles\ny un libro de hechizos.");
        }

        // ---------------------------------------------------------------- cueva

        /// <summary>
        /// Cueva al pie de la montaña: boca de roca con arco, pasillo corto y camara (portal, cofre de runas de metal y
        /// tintero fijo). Interior en tonos oscuros (el ambiente no se ocluye) y una sola luz tenue.
        /// </summary>
        private static void Cave(Transform parent, Site site)
        {
            var t = StructRoot(parent, "Cave", site);
            HeightRange(t, -5f, 5f, -7.5f, 4.5f, out float min, out float max);
            float f = max + 0.1f, bot = min - 0.5f;
            var b = new LowPolyMesh();
            int seed = 5300;

            // Piso, paredes y techos interiores (los colliders siguen estas cajas).
            Solid(b, t, new Vector3(0f, (f + bot) * 0.5f, -1.4f), new Vector3(9f, f - bot, 10.6f), CaveWall, CaveFloor);
            Solid(b, t, new Vector3(0f, f + 1.9f, -6.4f), new Vector3(9f, 3.8f, 1.2f), CaveWall);
            foreach (float sx in new[] { -1f, 1f })
            {
                Solid(b, t, new Vector3(sx * 3.8f, f + 1.9f, -2.9f), new Vector3(1.2f, 3.8f, 7f), CaveWall);
                Solid(b, t, new Vector3(sx * 2.6f, f + 1.9f, 0.5f), new Vector3(2.8f, 3.8f, 1f), CaveWall);
                Solid(b, t, new Vector3(sx * 1.8f, f + 1.6f, 2.1f), new Vector3(1.2f, 3.2f, 2.2f), CaveWall);
            }
            Solid(b, t, new Vector3(0f, f + 3.8f, -2.9f), new Vector3(8.8f, 1f, 7f), CaveWall);
            Solid(b, t, new Vector3(0f, f + 3.2f, 1.6f), new Vector3(3.6f, 1f, 3.2f), CaveWall);

            // Boca: dos rocas y un dintel.
            foreach (float sx in new[] { -1f, 1f })
            {
                float h = f + 3.4f - bot;
                Chunk(b, new Vector3(sx * 2f, bot + h * 0.5f, 3.7f), new Vector3(1.6f, h, 1.4f), sx * 6f, seed + (sx > 0f ? 1 : 2), Rock, Moss);
                Col(t, new Vector3(sx * 2f, bot + h * 0.5f, 3.7f), new Vector3(1.3f, h, 1.1f));
            }
            Chunk(b, new Vector3(0f, f + 3.3f, 3.7f), new Vector3(5.6f, 1.4f, 1.7f), 0f, seed + 3, Rock, Moss);
            Col(t, new Vector3(0f, f + 3.4f, 3.7f), new Vector3(5f, 1.2f, 1.4f));

            // Monte: rocas grandes que tapan las cajas y se apoyan en la ladera.
            Chunk(b, new Vector3(0f, f + 4.9f, -2.6f), new Vector3(10.5f, 2.2f, 9.6f), 4f, seed + 4, Rock, Moss);
            Col(t, new Vector3(0f, f + 4.9f, -2.6f), new Vector3(9.2f, 2f, 8.4f));
            foreach (float sx in new[] { -1f, 1f })
            {
                float h = f + 4.8f - bot;
                Chunk(b, new Vector3(sx * 5.7f, bot + h * 0.5f, -2.4f), new Vector3(3.6f, h, 10f), sx * 3f, seed + (sx > 0f ? 5 : 6), Rock, Moss);
                Col(t, new Vector3(sx * 5.7f, bot + h * 0.5f, -2.4f), new Vector3(3.1f, h, 8.8f));
                float hs = f + 3f - bot;
                Chunk(b, new Vector3(sx * 4.3f, bot + hs * 0.5f, 2.2f), new Vector3(2.8f, hs, 3.2f), sx * 10f, seed + (sx > 0f ? 7 : 8), Rock, Moss);
                Col(t, new Vector3(sx * 4.3f, bot + hs * 0.5f, 2.2f), new Vector3(2.4f, hs, 2.7f));
            }
            float hb = f + 5f - bot;
            Chunk(b, new Vector3(0f, bot + hb * 0.5f, -8.4f), new Vector3(12.5f, hb, 3.6f), -3f, seed + 9, Rock, Moss);
            Col(t, new Vector3(0f, bot + hb * 0.5f, -8.4f), new Vector3(11f, hb, 3.1f));
            Chunk(b, new Vector3(0f, f + 4.1f, 1.9f), new Vector3(5.2f, 1.6f, 3.8f), 2f, seed + 10, Rock, Moss);

            // Cristales en los rincones (color de vertice, sin emision) y altar del tintero.
            foreach (var at in new[] { new Vector3(-2.7f, f, -5.4f), new Vector3(2.8f, f, -5.2f), new Vector3(-2.9f, f, -0.5f) })
                for (int k = 0; k < 3; k++)
                    b.Frustum(Matrix4x4.TRS(at + new Vector3(0.12f * k - 0.12f, 0f, 0.08f * k), Quaternion.Euler(12f * (k - 1), 40f * k, 8f * k), Vector3.one),
                        0.09f + 0.03f * k, 0f, 0f, 0.35f + 0.15f * k, 5, Crystal, false);
            Solid(b, t, new Vector3(2.5f, f + 0.4f, -1.6f), new Vector3(0.6f, 0.8f, 0.6f), StoneB, StoneA);
            Steps(b, t, 0f, 3.9f, 2.4f, f, CaveFloor);
            Finish(t, b, "Struct_Cave");

            BuildPortal(t, "cave", new Vector3(0f, f, -4.9f), 0f, PortalBase.Floor);
            FixedInkwell(t, "Inkwell_Cave", t.TransformPoint(new Vector3(2.5f, f + 0.8f, -1.6f)), t.eulerAngles.y);
            var chest = Chest(t, "SupplyChest_Cave", new Vector3(-2.4f, f, -2.2f), 90f, true, out float chestTop);
            Supply(chest, MapContraptions.CreateRune(RuneKind.Metal, "RuneTemplate_Metal", chest.position, lit), chestTop + 0.08f, MetalCooldown, 2, "cave-metal");

            var lightGo = new GameObject("CaveLight");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0f, f + 2.6f, -2.8f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.72f, 0.8f, 1f);
            light.range = 7f;
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;

            CanvasDesigns.Sign(t, lit, "Sign_Struct_Cave", t.TransformPoint(new Vector3(3.6f, 0f, 6.2f)), t.forward,
                "Cueva", "Portal, tinta y runas\nde metal en la cámara.");
        }

        // ---------------------------------------------------------------- peñascos

        /// <summary>Peñasco de cobertura: roca grande escalonada (se sube saltando) y un muro bajo de piedra al costado.</summary>
        private static void Crag(Transform parent, Site site, int index)
        {
            var t = StructRoot(parent, "Crag_" + index, site);
            float k = 1f + 0.12f * (index % 3);
            int seed = 7100 + index * 131;
            var b = new LowPolyMesh();
            RockBlock(b, t, new Vector2(0f, 0f), new Vector2(3.4f * k, 2.8f * k), 2.8f * k, 8f, seed);
            RockBlock(b, t, new Vector2(2f * k, 1f * k), new Vector2(1.6f, 1.6f), 1.8f * k, -12f, seed + 1);
            RockBlock(b, t, new Vector2(3f * k, 2.1f * k), new Vector2(1.3f, 1.3f), 0.9f, 20f, seed + 2);
            RockBlock(b, t, new Vector2(-1.3f * k, -1.7f * k), new Vector2(1.6f, 1.4f), 1.4f, 30f, seed + 3);

            // Muro bajo (dos hiladas) del lado izquierdo.
            float x0 = -1.9f * k - 0.3f;
            for (int i = 0; i < 4; i++)
            {
                float x = x0 - 0.42f - i * 0.82f;
                Chunk(b, new Vector3(x, Rel(t, x, 2f) + 0.2f, 2f), new Vector3(0.8f, 0.8f, 0.62f), 3f * i, seed + 10 + i, StoneB, StoneA, 0.08f);
            }
            for (int i = 0; i < 3; i++)
            {
                float x = x0 - 0.83f - i * 0.82f;
                Chunk(b, new Vector3(x, Rel(t, x, 2f) + 0.82f, 2f), new Vector3(0.78f, 0.45f, 0.56f), -4f * i, seed + 20 + i, StoneB, Moss, 0.08f);
            }
            float wallMid = x0 - 1.65f;
            Col(t, new Vector3(wallMid, Rel(t, wallMid, 2f) + 0.35f, 2f), new Vector3(3.3f, 1.4f, 0.6f));
            Finish(t, b, "Struct_Crag_" + index);
        }

        private static void RockBlock(LowPolyMesh b, Transform t, Vector2 at, Vector2 size, float height, float yaw, int seed)
        {
            float g = Rel(t, at.x, at.y), low = g;
            for (int i = 0; i < 4; i++)
                low = Mathf.Min(low, Rel(t, at.x + ((i & 1) == 0 ? -0.5f : 0.5f) * size.x, at.y + ((i & 2) == 0 ? -0.5f : 0.5f) * size.y));
            float bottom = low - 0.4f, top = g + height;
            Chunk(b, new Vector3(at.x, (top + bottom) * 0.5f, at.y), new Vector3(size.x, top - bottom, size.y), yaw, seed, Rock, Moss);
            Col(t, new Vector3(at.x, (top - 0.08f + bottom) * 0.5f, at.y), new Vector3(size.x * 0.84f, top - 0.08f - bottom, size.y * 0.84f));
        }

        // ---------------------------------------------------------------- circulo de piedras

        /// <summary>Ocho menhires en ronda (dos trilitos con dintel al fondo) y el portal del campo en el centro, mirando al campo.</summary>
        private static void StoneCircle(Transform parent, Site site)
        {
            var t = StructRoot(parent, "StoneCircle", site);
            var b = new LowPolyMesh();
            const float radius = 4.6f;
            // Grados en el plano local (90 = frente): el hueco del frente es la entrada.
            float[] angles = { 130f, 170f, 210f, 250f, 290f, 330f, 10f, 50f };
            var tops = new Vector3[angles.Length];
            for (int i = 0; i < angles.Length; i++)
            {
                float a = angles[i] * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                float g = Rel(t, p.x, p.z), bottom = g - 0.45f;
                bool trilith = i >= 2 && i <= 5;
                float h = trilith ? 2.6f : 1.7f + 0.8f * Mathf.PerlinNoise(i * 0.73f, 3.1f);
                float yaw = Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg;
                Chunk(b, new Vector3(p.x, (bottom + g + h) * 0.5f, p.z), new Vector3(0.95f, g + h - bottom, 0.6f), yaw, 6200 + i, StoneA, Moss, trilith ? 0.05f : 0.12f);
                var cap = t.gameObject.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.radius = 0.42f;
                cap.height = g + h - bottom;
                cap.center = new Vector3(p.x, (bottom + g + h) * 0.5f, p.z);
                tops[i] = new Vector3(p.x, g + h, p.z);
                // Losa del sendero interior.
                var flag = new Vector3(Mathf.Cos(a) * 3f, 0f, Mathf.Sin(a) * 3f);
                B(b, new Vector3(flag.x, Rel(t, flag.x, flag.z) + 0.02f, flag.z), new Vector3(0.7f, 0.08f, 0.5f), StoneB);
            }
            foreach (var (i, j) in new[] { (2, 3), (4, 5) })
            {
                Vector3 a = tops[i], c = tops[j];
                Vector3 across = new Vector3(c.x - a.x, 0f, c.z - a.z);
                var center = new Vector3((a.x + c.x) * 0.5f, Mathf.Max(a.y, c.y) + 0.2f, (a.z + c.z) * 0.5f);
                Quaternion r = Quaternion.LookRotation(across.normalized);
                var size = new Vector3(0.62f, 0.45f, across.magnitude + 1f);
                b.Box(Matrix4x4.TRS(center, r, Vector3.one), size, StoneB, StoneA);
                RotCol(t, "LintelCollider", center, r, size);
            }
            Finish(t, b, "Struct_StoneCircle");

            HeightRange(t, -1.5f, 1.5f, -1f, 1f, out _, out float max);
            BuildPortal(t, "field-circle", new Vector3(0f, max + 0.15f, 0f), 0f, PortalBase.Terrain);
            CanvasDesigns.Sign(t, lit, "Sign_Struct_StoneCircle", t.TransformPoint(new Vector3(2.6f, 0f, 6.4f)), t.forward,
                "Círculo de piedras", "Dibujá el sello del portal\nen la placa para viajar.");
        }

        // ---------------------------------------------------------------- santuario

        /// <summary>Arco en ruinas con el altar de reciclaje (circulo de entrada en el piso, salida sobre el altar) y el relicario de la runa de oro.</summary>
        private static void Shrine(Transform parent, Site site)
        {
            var t = StructRoot(parent, "Shrine", site);
            float yaw = t.eulerAngles.y;
            var b = new LowPolyMesh();

            // Pilar entero con el muñon del dintel, pilar quebrado y el dintel caido.
            float gl = Rel(t, -1.7f, -1.9f), gr = Rel(t, 1.7f, -1.9f);
            Chunk(b, new Vector3(-1.7f, gl + 1.45f, -1.9f), new Vector3(0.7f, 3.7f, 0.7f), 4f, 6400, StoneA, StoneA, 0.06f);
            Col(t, new Vector3(-1.7f, gl + 1.45f, -1.9f), new Vector3(0.62f, 3.7f, 0.62f));
            Solid(b, t, new Vector3(-1.15f, gl + 3.52f, -1.9f), new Vector3(1.9f, 0.44f, 0.7f), StoneB, StoneA);
            Chunk(b, new Vector3(1.7f, gr + 0.6f, -1.9f), new Vector3(0.7f, 2f, 0.7f), -6f, 6401, StoneA, Moss, 0.3f);
            Col(t, new Vector3(1.7f, gr + 0.55f, -1.9f), new Vector3(0.62f, 1.9f, 0.62f));
            float gf = Rel(t, 2.9f, 0.4f);
            Chunk(b, new Vector3(2.9f, gf + 0.15f, 0.4f), new Vector3(2f, 0.5f, 0.65f), 35f, 6402, StoneB, Moss, 0.15f);
            Col(t, new Vector3(2.9f, gf + 0.15f, 0.4f), new Vector3(1.5f, 0.5f, 1.1f));
            for (int i = 0; i < 3; i++)
            {
                var at = new Vector2(-2.6f + i * 0.5f, -0.6f + i * 0.7f);
                Chunk(b, new Vector3(at.x, Rel(t, at.x, at.y) + 0.1f, at.y), new Vector3(0.45f, 0.4f, 0.4f), 20f * i, 6410 + i, StoneB, Moss, 0.2f);
            }
            Finish(t, b, "Struct_Shrine");

            // Altar de reciclaje (objeto propio con la estacion) y su circulo de entrada adelante.
            float ga = Rel(t, 0f, -0.5f);
            var altar = new GameObject("RecyclingAltar").transform;
            altar.SetParent(t, false);
            altar.localPosition = new Vector3(0f, ga, -0.5f);
            var a = new LowPolyMesh();
            foreach (float sx in new[] { -1f, 1f })
                Solid(a, altar, new Vector3(sx * 0.65f, 0.19f, 0f), new Vector3(0.35f, 1.18f, 0.7f), StoneB);
            Solid(a, altar, new Vector3(0f, 0.85f, 0f), new Vector3(1.8f, 0.14f, 0.9f), StoneB, StoneA);
            a.Frustum(Matrix4x4.Translate(new Vector3(0f, 0.92f, 0f)), 0.3f, 0.3f, 0f, 0.004f, 12, Inlay);
            float zoneTop = Rel(t, 0f, 1f) - ga + 0.08f;
            Solid(a, altar, new Vector3(0f, zoneTop - 0.15f, 1.5f), new Vector3(1.5f, 0.3f, 1.5f), StoneB, StoneA);
            a.Frustum(Matrix4x4.Translate(new Vector3(0f, zoneTop, 1.5f)), 0.58f, 0.58f, 0f, 0.006f, 14, Inlay);
            a.Frustum(Matrix4x4.Translate(new Vector3(0f, zoneTop + 0.001f, 1.5f)), 0.47f, 0.47f, 0f, 0.006f, 14, StoneA);
            for (int i = 0; i < 4; i++)
            {
                float ang = (45f + 90f * i) * Mathf.Deg2Rad;
                a.Box(Matrix4x4.TRS(new Vector3(Mathf.Cos(ang) * 0.62f, zoneTop + 0.004f, 1.5f + Mathf.Sin(ang) * 0.62f), Quaternion.Euler(0f, 45f, 0f), Vector3.one),
                    new Vector3(0.08f, 0.008f, 0.08f), Inlay);
            }
            Finish(altar, a, "RecyclingAltar");

            var drop = new GameObject("DropZone").transform;
            drop.SetParent(altar, false);
            drop.localPosition = new Vector3(0f, zoneTop + 0.55f, 1.5f);
            var trigger = drop.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.3f, 1.1f, 1.3f);
            var output = new GameObject("OutputAnchor").transform;
            output.SetParent(altar, false);
            output.localPosition = new Vector3(0f, 0.92f + 0.08f, 0f);
            output.rotation = Quaternion.Euler(90f, yaw, 0f);

            var station = altar.gameObject.AddComponent<RecyclingStation>();
            station.Configure(drop, output);
            foreach (var kind in new[] { RuneKind.Stone, RuneKind.Metal, RuneKind.Gold })
            {
                var rune = MapContraptions.CreateRune(kind, "RuneTemplate_" + kind, output.position, lit);
                if (rune == null) continue;
                Park(rune, altar, output);
                station.AddRecipe(kind, rune);
            }
            var paper = PaperTemplate("PaperTemplate");
            if (paper != null)
            {
                Park(paper, altar, output);
                station.SetPaperTemplate(paper);
            }

            // Relicario: la unica fuente de runas de oro del mapa, una cada 10 minutos.
            float gq = Rel(t, 1.55f, -0.9f);
            var q = new LowPolyMesh();
            Matrix4x4 id = Matrix4x4.identity;
            q.Frustum(id, 0.26f, 0.24f, -0.4f, 0.1f, 8, StoneB, true, false);
            q.Frustum(id, 0.14f, 0.12f, 0.1f, 0.8f, 8, StoneA, false);
            q.Frustum(id, 0.16f, 0.16f, 0.42f, 0.46f, 8, Brass, false);
            q.Frustum(id, 0.12f, 0.2f, 0.8f, 0.88f, 8, StoneB, false);
            q.Frustum(id, 0.2f, 0.22f, 0.88f, 0.95f, 8, Brass, true);
            q.Frustum(id, 0.17f, 0.15f, 0.95f, 0.99f, 8, Cushion, true);
            var reliquary = MapBuilder.Place("GoldReliquary", t, MapBuilder.SaveMesh(q.Build("GoldReliquary")), lit, t.TransformPoint(new Vector3(1.55f, gq, -0.9f)), t.rotation);
            var rc = reliquary.AddComponent<BoxCollider>();
            rc.center = new Vector3(0f, 0.3f, 0f);
            rc.size = new Vector3(0.42f, 1.4f, 0.42f);
            reliquary.isStatic = true;
            Supply(reliquary.transform, MapContraptions.CreateRune(RuneKind.Gold, "RuneTemplate_Gold", reliquary.transform.position, lit), 0.99f + 0.09f, GoldCooldown, 1, "shrine-gold");

            CanvasDesigns.Sign(t, lit, "Sign_Struct_Shrine", t.TransformPoint(new Vector3(-2.6f, 0f, 2.6f)), t.forward,
                "Santuario", "Dejá en el círculo runas gastadas\no papeles: el altar te da otros.\nEl relicario guarda una runa de oro.");
        }

        // ---------------------------------------------------------------- puesto de suministros

        /// <summary>Puesto con toldo a rayas: mostrador con un cofre de papeles y uno de runas de piedra.</summary>
        private static void SupplyStall(Transform parent, Site site)
        {
            var t = StructRoot(parent, "SupplyStall", site);
            float yaw = t.eulerAngles.y;
            HeightRange(t, -1.6f, 1.6f, -1.4f, 1.3f, out float min, out _);
            var b = new LowPolyMesh();
            foreach (float sx in new[] { -1f, 1f })
                foreach (var (z, h) in new[] { (-0.65f, 2.7f), (0.85f, 2.25f) })
                {
                    float g = Rel(t, sx * 1.35f, z);
                    float top = min + h;
                    Solid(b, t, new Vector3(sx * 1.35f, (g - 0.3f + top) * 0.5f, z), new Vector3(0.1f, top - g + 0.3f, 0.1f), WoodB);
                }
            // Toldo a rayas, inclinado hacia el frente, con banderines.
            var back = new Vector3(0f, min + 2.75f, -0.8f);
            var front = new Vector3(0f, min + 2.3f, 1.05f);
            Quaternion r = Quaternion.LookRotation(front - back);
            float len = Vector3.Distance(back, front);
            for (int k = 0; k < 6; k++)
            {
                float x = -1.25f + 0.5f * k;
                Color c = (k & 1) == 0 ? Cloth : ClothLight;
                b.Box(Matrix4x4.TRS(new Vector3(x, (back.y + front.y) * 0.5f, (back.z + front.z) * 0.5f), r, Vector3.one), new Vector3(0.5f, 0.04f, len), c);
                Vector3 l = new Vector3(x - 0.25f, front.y - 0.02f, front.z), rr = new Vector3(x + 0.25f, front.y - 0.02f, front.z), tip = new Vector3(x, front.y - 0.25f, front.z);
                b.Tri(l, tip, rr, c);
                b.Tri(rr, tip, l, c);
            }
            Solid(b, t, new Vector3(-0.9f, Rel(t, -0.9f, -1.1f) + 0.25f, -1.1f), new Vector3(0.5f, 0.5f, 0.5f), WoodA, Plank);
            Solid(b, t, new Vector3(0.9f, Rel(t, 0.9f, -1.1f) + 0.25f, -1.1f), new Vector3(0.5f, 0.5f, 0.5f), WoodA, Plank);
            Solid(b, t, new Vector3(0.9f, Rel(t, 0.9f, -1.1f) + 0.7f, -1.1f), new Vector3(0.4f, 0.4f, 0.4f), WoodB, Plank);
            Finish(t, b, "Struct_SupplyStall");

            TowerBuilder.Table(t, lit, "StallCounter", t.rotation * new Vector3(0f, min, 0.15f), yaw, 2.4f, 0.8f);
            float counter = min + 0.82f;
            var paperChest = Chest(t, "SupplyChest_Paper", new Vector3(-0.6f, counter, 0.15f), 0f, false, out float paperTop);
            Supply(paperChest, PaperTemplate("PaperTemplate"), paperTop + 0.06f, 20f, 3, "stall-paper");
            var runeChest = Chest(t, "SupplyChest_Stone", new Vector3(0.6f, counter, 0.15f), 0f, false, out float runeTop);
            Supply(runeChest, MapContraptions.CreateRune(RuneKind.Stone, "RuneTemplate_Stone", runeChest.position, lit), runeTop + 0.07f, StoneCooldown, 2, "stall-stone");
            CanvasDesigns.Sign(t, lit, "Sign_Struct_SupplyStall", t.TransformPoint(new Vector3(2.4f, 0f, 1.6f)), t.forward,
                "Puesto de suministros", "Papeles y runas de piedra:\nse reponen solos con el tiempo.");
        }

        // ---------------------------------------------------------------- portal del estudio

        /// <summary>
        /// Portal del estudio de la torre (primer piso), en el lado oeste del area central: el anillo exterior del piso es el
        /// hueco de la rampa de abajo y la rampa de arriba, asi que no hay pared libre sin tocar TowerBuilder. Version
        /// compacta (zocalos angostos, placa chica) para no pisar la mesa de dibujo ni la mesa de trabajo.
        /// </summary>
        private static void StudyPortal(Transform parent)
        {
            var holder = new GameObject(Prefix + "PortalStudy").transform;
            holder.SetParent(parent, false);
            holder.SetPositionAndRotation(MapBuilder.TowerOrigin() + new Vector3(-2f, TowerBuilder.FloorHeight, 0.65f), Quaternion.LookRotation(Vector3.right));
            report.AppendLine($"{holder.name}: {Fmt(holder.position)}");
            BuildPortal(holder, "tower-study", Vector3.zero, 0f, PortalBase.Floor, -1f, 0.95f, 0.55f, 0.52f, true);
        }

        // ---------------------------------------------------------------- portal

        private enum PortalBase { Floor, Terrain }

        /// <summary>
        /// Arco de piedra (dos pilares y siete dovelas) con la superficie apagada (disco oscuro, material emisivo en negro que
        /// enciende Portal en juego), la placa (lienzo FIJO inclinado sobre un atril, a un costado y adelante), el punto de
        /// llegada a 1.5 m al frente mirando hacia afuera y el ancla de efectos en el centro del arco. Frente = +Z local.
        /// </summary>
        private static void BuildPortal(Transform parent, string id, Vector3 local, float localYaw, PortalBase style,
            float plateSide = 1f, float plateLateral = 1.3f, float plateForward = 0.95f, float plateSize = 0.7f, bool compact = false)
        {
            var root = new GameObject("Portal_" + id).transform;
            root.SetParent(parent, false);
            root.localPosition = local;
            root.localRotation = Quaternion.Euler(0f, localYaw, 0f);
            var b = new LowPolyMesh();

            if (style == PortalBase.Terrain)
            {
                float min = 0f;
                for (int i = 0; i <= 4; i++)
                    for (int j = 0; j <= 4; j++)
                        min = Mathf.Min(min, Rel(root, Mathf.Lerp(-1.45f, 1.45f, i / 4f), Mathf.Lerp(-0.85f, 0.85f, j / 4f)));
                float bottom = min - 0.3f;
                Solid(b, root, new Vector3(0f, bottom * 0.5f, 0f), new Vector3(2.9f, -bottom, 1.7f), StoneB, StoneA);
            }

            Vector3 plinth = compact ? new Vector3(0.46f, 0.16f, 0.52f) : new Vector3(0.62f, 0.16f, 0.68f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * 0.95f;
                B(b, new Vector3(x, 0.08f, 0f), plinth, StoneB);
                B(b, new Vector3(x, 0.98f, 0f), new Vector3(0.42f, 1.64f, 0.5f), StoneA);
                B(b, new Vector3(x, 1.85f, 0f), new Vector3(0.52f, 0.1f, 0.58f), StoneB);
                for (int k = 0; k < 3; k++) B(b, new Vector3(x, 0.6f + k * 0.45f, 0.256f), new Vector3(0.16f, 0.05f, 0.01f), Inlay);
                Col(root, new Vector3(x, 0.95f, 0f), new Vector3(0.46f, 1.9f, 0.52f));
            }
            for (int i = 0; i <= 6; i++)
            {
                float a = i * 30f;
                var c = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.95f, 1.9f + Mathf.Sin(a * Mathf.Deg2Rad) * 0.95f, 0f);
                bool key = i == 3;
                b.Box(Matrix4x4.TRS(c, Quaternion.Euler(0f, 0f, a), Vector3.one),
                    key ? new Vector3(0.5f, 0.56f, 0.58f) : new Vector3(0.42f, 0.5f, 0.5f), key ? Keystone : (i & 1) == 0 ? StoneA : StoneB);
            }
            Col(root, new Vector3(0f, 2.86f, 0f), new Vector3(2.3f, 0.42f, 0.5f));

            // Atril de la placa.
            float px = plateSide * plateLateral, pz = plateForward;
            float foot = style == PortalBase.Terrain ? Rel(root, px, pz) : 0f;
            const float lean = 35f * Mathf.Deg2Rad;
            var n = new Vector3(0f, Mathf.Sin(lean), Mathf.Cos(lean));
            var up = new Vector3(0f, Mathf.Cos(lean), -Mathf.Sin(lean));
            var plateCenter = new Vector3(px, foot + 1.12f, pz);
            float footSize = compact ? 0.3f : 0.42f;
            B(b, new Vector3(px, foot - 0.07f, pz + 0.04f), new Vector3(footSize, 0.26f, footSize), StoneB);
            B(b, new Vector3(px, foot + 0.5f, pz + 0.06f), new Vector3(0.1f, 1f, 0.1f), WoodB);
            b.Box(Matrix4x4.TRS(plateCenter - n * 0.035f, Quaternion.LookRotation(n, up), Vector3.one), new Vector3(plateSize + 0.1f, plateSize + 0.1f, 0.04f), WoodA);
            Col(root, new Vector3(px, foot + 0.55f, pz), new Vector3(compact ? 0.3f : 0.5f, 1.1f, 0.5f));
            Finish(root, b, "Struct_Portal_" + id);

            // Superficie: arco de medio punto relleno, de dos caras, sin collider.
            var s = new LowPolyMesh();
            var pts = new List<Vector3> { new Vector3(-0.72f, 0.02f, 0f), new Vector3(0.72f, 0.02f, 0f), new Vector3(0.72f, 1.9f, 0f) };
            for (int k = 1; k <= 7; k++)
            {
                float a = k * Mathf.PI / 8f;
                pts.Add(new Vector3(Mathf.Cos(a) * 0.72f, 1.9f + Mathf.Sin(a) * 0.72f, 0f));
            }
            pts.Add(new Vector3(-0.72f, 1.9f, 0f));
            var middle = new Vector3(0f, 1.2f, 0f);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 p0 = pts[i], p1 = pts[(i + 1) % pts.Count];
                s.Tri(middle, p0, p1, PortalDark);
                s.Tri(middle, p1, p0, PortalDark);
            }
            var surface = new GameObject("PortalSurface");
            surface.transform.SetParent(root, false);
            surface.AddComponent<MeshFilter>().sharedMesh = MapBuilder.SaveMesh(s.Build("PortalSurface"));
            var sr = surface.AddComponent<MeshRenderer>();
            sr.sharedMaterial = PortalMaterial();
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var anchor = new GameObject("EffectAnchor").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = new Vector3(0f, 1.35f, 0f);
            var arrival = new GameObject("Arrival").transform;
            arrival.SetParent(root, false);
            arrival.localPosition = new Vector3(0f, 0.05f, 1.5f);
            arrival.localRotation = Quaternion.identity;
            if (style == PortalBase.Terrain)
            {
                Vector3 w = arrival.position;
                arrival.position = new Vector3(w.x, ValleyShape.Height(w.x, w.z) + 0.05f, w.z);
            }

            var plate = FixedCanvas(root, "PortalPlate_" + id, root.TransformPoint(plateCenter + n * 0.005f),
                root.rotation * Quaternion.LookRotation(-n, up), plateSize, plateSize, EmissionSide.AwayFromViewer);
            root.gameObject.AddComponent<Portal>().Configure(id, plate, arrival, anchor);
        }

        private static Material PortalMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(PortalMaterialPath);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PortalSurface" };
            m.SetColor("_BaseColor", PortalDark);
            m.SetFloat("_Smoothness", 0.2f);
            // Emision habilitada pero en negro: apagado hasta que el portal se active.
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(m, PortalMaterialPath);
            return m;
        }

        // ---------------------------------------------------------------- objetos de juego

        /// <summary>Lienzo FIJO (sin PhysicalCanvas ni Respawnable, kinematico) clonado de PaperCanvas: placas y pizarras.</summary>
        private static DrawingCanvas FixedCanvas(Transform parent, string name, Vector3 position, Quaternion rotation, float width, float height, EmissionSide side)
        {
            var src = GameObject.Find("PaperCanvas");
            if (src == null) return null;
            var go = Object.Instantiate(src, position, rotation);
            go.name = name;
            go.transform.localScale = new Vector3(width, height, src.transform.localScale.z);
            var grab = go.GetComponent<PhysicalCanvas>();
            if (grab != null) Object.DestroyImmediate(grab);
            var respawn = go.GetComponent<Respawnable>();
            if (respawn != null) Object.DestroyImmediate(respawn);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            var canvas = go.GetComponent<DrawingCanvas>();
            if (canvas != null)
            {
                var so = new SerializedObject(canvas);
                so.FindProperty("emissionSide").enumValueIndex = (int)side;
                so.ApplyModifiedProperties();
            }
            go.transform.SetParent(parent, true);
            return canvas;
        }

        /// <summary>Papel agarrable (clon de PaperCanvas, 45 cm, con fisica y Respawnable): plantilla de reaparicion y de reciclaje.</summary>
        private static GameObject PaperTemplate(string name)
        {
            var src = GameObject.Find("PaperCanvas");
            if (src == null) return null;
            var go = Object.Instantiate(src);
            go.name = name;
            go.transform.localScale = new Vector3(0.45f, 0.45f, src.transform.localScale.z);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }
            var grab = go.GetComponent<PhysicalCanvas>();
            if (grab != null)
            {
                var so = new SerializedObject(grab);
                var floating = so.FindProperty("startFloating");
                if (floating != null) floating.boolValue = false;
                so.ApplyModifiedProperties();
            }
            if (go.GetComponent<Respawnable>() == null) go.AddComponent<Respawnable>();
            return go;
        }

        /// <summary>Deja una plantilla desactivada como hija de host, en la pose del ancla.</summary>
        private static void Park(GameObject template, Transform host, Transform anchor)
        {
            template.transform.SetParent(host, true);
            template.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            template.SetActive(false);
        }

        /// <summary>Punto de reaparicion: ancla a height sobre host (acostado, como los papeles) y la plantilla desactivada como hija.</summary>
        private static void Supply(Transform host, GameObject template, float height, float cooldown, int maxLive, string id)
        {
            if (template == null)
            {
                Debug.LogWarning($"[Estructuras] sin plantilla para el punto {id}.");
                return;
            }
            var anchor = new GameObject("SpawnAnchor").transform;
            anchor.SetParent(host, false);
            anchor.localPosition = new Vector3(0f, height, 0f);
            anchor.rotation = Quaternion.Euler(90f, host.eulerAngles.y, 0f);
            Park(template, host, anchor);
            host.gameObject.AddComponent<SupplyPoint>().Configure(template, anchor, cooldown, maxLive, id);
        }

        /// <summary>Tintero FIJO (clon del de la mesa del campo, normal, kinematico) apoyado en position.</summary>
        private static void FixedInkwell(Transform parent, string name, Vector3 position, float yaw)
        {
            var src = GameObject.Find("Inkwell");
            if (src == null) return;
            var clone = Object.Instantiate(src, position, Quaternion.Euler(0f, yaw, 0f));
            clone.name = name;
            ToolModels.MakeInkwell(clone, lit, InkKind.Normal, false);
            var respawn = clone.GetComponent<Respawnable>();
            if (respawn != null) Object.DestroyImmediate(respawn);
            clone.AddComponent<Respawnable>();
            clone.transform.SetParent(parent, true);
        }

        /// <summary>Libro de aprendizaje (mismo modelo que los de la torre).</summary>
        private static void Book(Transform parent, string id, Vector3 position, Quaternion rotation)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Lore/glifos.json");
            var info = asset != null ? Learning.GlyphLore.FromJson(asset.text).Book(id) : null;
            if (info == null)
            {
                Debug.LogWarning($"[Estructuras] no existe el libro {id}.");
                return;
            }
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.03f, 0f)), new Vector3(0.24f, 0.06f, 0.32f), info.Color);
            b.Box(Matrix4x4.Translate(new Vector3(0.01f, 0.03f, 0f)), new Vector3(0.23f, 0.045f, 0.3f), new Color(0.95f, 0.9f, 0.78f));
            b.Box(Matrix4x4.Translate(new Vector3(-0.115f, 0.03f, 0f)), new Vector3(0.015f, 0.065f, 0.32f), info.Color * 0.7f);
            var go = MapBuilder.Place($"Book_{id}", parent, MapBuilder.SaveMesh(b.Build($"Book_{id}")), lit, position, rotation);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.03f, 0f);
            col.size = new Vector3(0.3f, 0.12f, 0.38f);
            go.AddComponent<Learning.LibraryBook>().SetBook(id);
        }

        /// <summary>Cofre cerrado con flejes y cerradura (lo de arriba reaparece sobre la tapa). large: 80 cm; si no, 50 cm.</summary>
        private static Transform Chest(Transform parent, string name, Vector3 local, float localYaw, bool large, out float height)
        {
            Vector3 size = large ? new Vector3(0.8f, 0.45f, 0.5f) : new Vector3(0.5f, 0.3f, 0.36f);
            height = size.y;
            var b = new LowPolyMesh();
            B(b, new Vector3(0f, size.y * 0.35f, 0f), new Vector3(size.x, size.y * 0.7f, size.z), WoodA);
            B(b, new Vector3(0f, size.y * 0.85f, 0f), new Vector3(size.x + 0.02f, size.y * 0.3f, size.z + 0.02f), WoodB, WoodA);
            foreach (float sx in new[] { -0.3f, 0.3f }) B(b, new Vector3(sx * size.x, size.y * 0.5f, 0f), new Vector3(0.05f, size.y + 0.02f, size.z + 0.03f), Iron);
            B(b, new Vector3(0f, size.y * 0.65f, size.z * 0.5f + 0.015f), new Vector3(0.08f, 0.1f, 0.03f), Brass);
            var mesh = MapBuilder.SaveMesh(b.Build(large ? "SupplyChest_L" : "SupplyChest_S"));
            var go = MapBuilder.Place(name, parent, mesh, lit, parent.TransformPoint(local), parent.rotation * Quaternion.Euler(0f, localYaw, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, size.y * 0.5f, 0f);
            col.size = size;
            go.isStatic = true;
            return go.transform;
        }

        /// <summary>Bandeja de papeles con reborde, sobre una mesa.</summary>
        private static Transform Tray(Transform parent, string name, Vector3 local)
        {
            var b = new LowPolyMesh();
            B(b, new Vector3(0f, 0.015f, 0f), new Vector3(0.5f, 0.03f, 0.38f), WoodB);
            foreach (float sx in new[] { -1f, 1f }) B(b, new Vector3(sx * 0.235f, 0.045f, 0f), new Vector3(0.03f, 0.04f, 0.38f), WoodA);
            foreach (float sz in new[] { -1f, 1f }) B(b, new Vector3(0f, 0.045f, sz * 0.175f), new Vector3(0.5f, 0.04f, 0.03f), WoodA);
            var go = MapBuilder.Place(name, parent, MapBuilder.SaveMesh(b.Build("PaperTray")), lit, parent.TransformPoint(local), parent.rotation);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.015f, 0f);
            col.size = new Vector3(0.5f, 0.03f, 0.38f);
            go.isStatic = true;
            return go.transform;
        }

        /// <summary>Escalones de piedra (de 20 cm como mucho) desde un piso elevado hacia +Z local, hasta el terreno.</summary>
        private static void Steps(LowPolyMesh b, Transform t, float x, float z0, float width, float floorTop, Color color)
        {
            float g = Rel(t, x, z0 + 0.6f);
            float rise = floorTop - g;
            if (rise <= 0.22f) return;
            int n = Mathf.CeilToInt(rise / 0.2f);
            float h = rise / n;
            for (int k = 1; k < n; k++)
            {
                float stepTop = floorTop - h * k;
                float z = z0 + 0.15f + 0.3f * (k - 1);
                float bottom = Mathf.Min(g, Rel(t, x, z)) - 0.3f;
                Solid(b, t, new Vector3(x, (stepTop + bottom) * 0.5f, z), new Vector3(width, stepTop - bottom, 0.3f), StoneB, color);
            }
        }

        // ---------------------------------------------------------------- utilidades

        private static Transform StructRoot(Transform parent, string name, Site site)
        {
            var t = new GameObject(Prefix + name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(MapBuilder.Ground(site.Pos.x, site.Pos.y), Quaternion.LookRotation(new Vector3(site.Front.x, 0f, site.Front.y)));
            ClearVegetation(t.position, site.Radius);
            WarnPractice(t.position, site.Radius);
            report.AppendLine($"{t.name}: {Fmt(t.position)}");
            return t;
        }

        private static string Fmt(Vector3 p) => $"({p.x:F1}, {p.y:F1}, {p.z:F1})";

        /// <summary>Saca pinos y rocas que caen sobre la estructura (con margen por la copa de los pinos).</summary>
        private static void ClearVegetation(Vector3 center, float radius)
        {
            foreach (string group in new[] { "Trees", "Rocks" })
            {
                var g = map.Find(group);
                if (g == null) continue;
                for (int i = g.childCount - 1; i >= 0; i--)
                {
                    var c = g.GetChild(i);
                    float reach = radius + (group == "Rocks" ? c.localScale.x : 2f);
                    float dx = c.position.x - center.x, dz = c.position.z - center.z;
                    if (dx * dx + dz * dz < reach * reach) Object.DestroyImmediate(c.gameObject);
                }
            }
        }

        private static void WarnPractice(Vector3 center, float radius)
        {
            foreach (string group in new[] { "Practice", "Props_Field" })
            {
                var g = GameObject.Find(group);
                if (g == null) continue;
                foreach (Transform c in g.transform)
                {
                    float dx = c.position.x - center.x, dz = c.position.z - center.z;
                    if (dx * dx + dz * dz < (radius + 0.5f) * (radius + 0.5f))
                        Debug.LogWarning($"[Estructuras] {c.name} ({group}) queda dentro de una estructura en {Fmt(center)}.");
                }
            }
        }

        /// <summary>Altura del terreno relativa a la raiz, en coordenadas locales de la raiz.</summary>
        private static float Rel(Transform root, float x, float z)
        {
            Vector3 w = root.TransformPoint(new Vector3(x, 0f, z));
            return ValleyShape.Height(w.x, w.z) - root.position.y;
        }

        private static void HeightRange(Transform root, float x0, float x1, float z0, float z1, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            for (int i = 0; i <= 6; i++)
                for (int j = 0; j <= 6; j++)
                {
                    float h = Rel(root, Mathf.Lerp(x0, x1, i / 6f), Mathf.Lerp(z0, z1, j / 6f));
                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                }
        }

        private static void Finish(Transform t, LowPolyMesh b, string meshName, Material material = null)
        {
            var mesh = MapBuilder.SaveMesh(b.Build(meshName));
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material != null ? material : lit;
            t.gameObject.isStatic = true;
        }

        private static void B(LowPolyMesh b, Vector3 center, Vector3 size, Color color, Color? top = null) =>
            b.Box(Matrix4x4.Translate(center), size, color, top);

        private static void Col(Transform t, Vector3 center, Vector3 size)
        {
            var col = t.gameObject.AddComponent<BoxCollider>();
            col.center = center;
            col.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        /// <summary>Caja visible con su collider (en el espacio local de t, sin rotacion propia).</summary>
        private static void Solid(LowPolyMesh b, Transform t, Vector3 center, Vector3 size, Color color, Color? top = null)
        {
            B(b, center, size, color, top);
            Col(t, center, size);
        }

        /// <summary>Collider girado (techos, rampas, dinteles): un hijo vacio con su BoxCollider.</summary>
        private static void RotCol(Transform t, string name, Vector3 localCenter, Quaternion localRotation, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(t, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = localRotation;
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
        }

        private static void Beam(LowPolyMesh b, Vector3 from, Vector3 to, float thickness, Color color)
        {
            Vector3 axis = to - from;
            b.Box(Matrix4x4.TRS((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, axis), Vector3.one), new Vector3(thickness, axis.magnitude, thickness), color);
        }

        /// <summary>
        /// Roca facetada: caja con las esquinas de arriba metidas hacia adentro (al azar, determinista), tapa en cuatro
        /// triangulos con una cresta y tono por cara. Solo achica: nunca invade el volumen de al lado.
        /// </summary>
        private static void Chunk(LowPolyMesh b, Vector3 center, Vector3 size, float yaw, int seed, Color side, Color top, float jitter = 0.16f)
        {
            var rng = new System.Random(seed);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 h = size * 0.5f;
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sy = (i & 2) == 0 ? -1f : 1f, sz = (i & 4) == 0 ? -1f : 1f;
                float shrink = sy > 0f ? 0.06f + (float)rng.NextDouble() * jitter * 1.6f : (float)rng.NextDouble() * jitter * 0.4f;
                float kx = 1f - shrink * (0.6f + 0.8f * (float)rng.NextDouble());
                float kz = 1f - shrink * (0.6f + 0.8f * (float)rng.NextDouble());
                float ky = sy > 0f ? 1f - (float)rng.NextDouble() * jitter * 0.6f : 1f;
                p[i] = center + rot * new Vector3(sx * h.x * kx, sy * h.y * ky, sz * h.z * kz);
            }
            Vector3 P(int x, int y, int z) => p[(x > 0 ? 1 : 0) | (y > 0 ? 2 : 0) | (z > 0 ? 4 : 0)];
            int face = 0;
            Color Tone(Color c) => LowPolyMesh.Jitter(c, 0.1f, seed * 7 + face++);

            Vector3 t0 = P(-1, 1, -1), t1 = P(-1, 1, 1), t2 = P(1, 1, 1), t3 = P(1, 1, -1);
            Vector3 crown = (t0 + t1 + t2 + t3) * 0.25f + Vector3.up * size.y * (0.03f + (float)rng.NextDouble() * jitter * 0.25f);
            b.Tri(t0, t1, crown, Tone(top));
            b.Tri(t1, t2, crown, Tone(top));
            b.Tri(t2, t3, crown, Tone(top));
            b.Tri(t3, t0, crown, Tone(top));
            b.Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), Tone(side));
            b.Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), Tone(side));
            b.Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), Tone(side));
            b.Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), Tone(side));
            b.Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), Tone(side));
        }
    }
}
