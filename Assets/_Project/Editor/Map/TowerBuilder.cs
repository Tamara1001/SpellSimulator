using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// La torre del modo Libre (referencias: torre de piedra con almenas + torre de Rapunzel): muro cilindrico de
    /// piedra con puerta al sur y ventanas, tres pisos (biblioteca, estudio, terraza) unidos por rampas en espiral
    /// pegadas al muro (el jugador es un Rigidbody: rampas, no escalones), almenas y techo conico violeta sobre postes.
    /// Interior: estantes con libros, mesas, sillas, escritorio con papel, luces calidas y violetas.
    /// Angulos: 0 = este (+X), 90 = norte (+Z), 270 = sur (puerta).
    /// </summary>
    public static class TowerBuilder
    {
        public const float OuterRadius = 5.5f;
        public const float Wall = 0.6f;
        public const float InnerRadius = OuterRadius - Wall;
        public const float FloorHeight = 4.5f;
        public const int Floors = 3;
        public const float TerraceY = FloorHeight * Floors;
        public const float ParapetHeight = 1f;
        public const int Sides = 20;
        public const float Row = 0.75f;

        // Rampas: radio interno/externo, giro total y angulo de inicio por piso.
        public const float RampInner = 2.9f;
        public const float RampOuter = InnerRadius - 0.05f;
        public const float RampTurn = 270f;
        public static readonly float[] RampStart = { 0f, 300f, 240f };
        /// <summary>Altura libre sobre la rampa (el jugador mide ~2.1 m; prueba manual 2026-10-07: chocaba con 2.0).</summary>
        public const float Headroom = 3f;
        public const float FloorThickness = 0.3f;

        private static readonly Color StoneA = new Color(0.66f, 0.7f, 0.62f);
        private static readonly Color StoneB = new Color(0.58f, 0.62f, 0.55f);
        private static readonly Color WoodFloor = new Color(0.52f, 0.36f, 0.24f);
        private static readonly Color WoodDark = new Color(0.36f, 0.22f, 0.15f);
        private static readonly Color Roof = new Color(0.45f, 0.36f, 0.72f);
        private static readonly Color RoofDark = new Color(0.36f, 0.28f, 0.6f);
        private static readonly Color Gold = new Color(0.95f, 0.78f, 0.35f);

        public static int Build(Transform map, Material material, Vector3 origin)
        {
            var root = new GameObject("Tower").transform;
            root.SetParent(map, false);
            root.position = origin;
            int tris = 0;

            tris += Solid(root, "TowerWall", WallMesh(), material);
            for (int f = 0; f <= Floors; f++) tris += Solid(root, $"TowerFloor_{f}", FloorMesh(f), material);
            for (int f = 0; f < Floors; f++) tris += Solid(root, $"TowerRamp_{f}", RampMesh(f), material);
            tris += Solid(root, "TowerTop", TopMesh(), material);
            tris += Visual(root, "TowerRoof", RoofMesh(), material);
            tris += Door(root, material);
            tris += Interior(root, material);
            Lights(root);
            return tris;
        }

        private static int Solid(Transform parent, string name, LowPolyMesh b, Material material)
        {
            var mesh = MapBuilder.SaveMesh(b.Build(name));
            var go = MapBuilder.Place(name, parent, mesh, material, parent.position, Quaternion.identity);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            return b.TriangleCount;
        }

        private static int Visual(Transform parent, string name, LowPolyMesh b, Material material)
        {
            var mesh = MapBuilder.SaveMesh(b.Build(name));
            MapBuilder.Place(name, parent, mesh, material, parent.position, Quaternion.identity).isStatic = true;
            return b.TriangleCount;
        }

        private static Vector3 Polar(float radius, float angleDeg, float y) =>
            new Vector3(Mathf.Cos(angleDeg * Mathf.Deg2Rad) * radius, y, Mathf.Sin(angleDeg * Mathf.Deg2Rad) * radius);

        // ---------------------------------------------------------------- muro

        /// <summary>Huecos (puerta y ventanas): segmento y filas.</summary>
        private static bool IsOpening(int segment, int row)
        {
            float y = row * Row;
            if (segment == 15 && y < 3f) return true;                       // puerta (sur)
            int floor = Mathf.FloorToInt(y / FloorHeight);
            float local = y - floor * FloorHeight;
            if (local < 1.5f || local >= 3f) return false;                  // altura de ventana
            switch (floor)
            {
                case 0: return segment == 5 || segment == 0 || segment == 10;
                case 1: return segment == 3 || segment == 8 || segment == 13 || segment == 18;
                case 2: return segment == 1 || segment == 6 || segment == 11 || segment == 17;
                default: return false;
            }
        }

        private static LowPolyMesh WallMesh()
        {
            var b = new LowPolyMesh();
            int rows = Mathf.RoundToInt(TerraceY / Row);
            float step = 360f / Sides;
            for (int s = 0; s < Sides; s++)
            for (int r = 0; r < rows; r++)
            {
                if (IsOpening(s, r)) continue;
                // Ladrillos: el color varia por celda (sin desfasaje: los bordes de puerta y ventanas quedan limpios).
                float a0 = s * step - step * 0.5f, a1 = a0 + step;
                float y0 = r * Row, y1 = y0 + Row;
                Color c = LowPolyMesh.Jitter((s + r) % 3 == 0 ? StoneB : StoneA, 0.08f, s * 97 + r * 13);
                // Cara exterior y cara interior.
                b.Quad(Polar(OuterRadius, a0, y0), Polar(OuterRadius, a0, y1), Polar(OuterRadius, a1, y1), Polar(OuterRadius, a1, y0), c);
                b.Quad(Polar(InnerRadius, a1, y0), Polar(InnerRadius, a1, y1), Polar(InnerRadius, a0, y1), Polar(InnerRadius, a0, y0), StoneB * 0.95f);
                // Jambas, dintel y alfeizar alrededor de los huecos.
                if (IsOpening(Wrap(s - 1), r))
                    b.Quad(Polar(InnerRadius, a0, y0), Polar(InnerRadius, a0, y1), Polar(OuterRadius, a0, y1), Polar(OuterRadius, a0, y0), StoneB);
                if (IsOpening(Wrap(s + 1), r))
                    b.Quad(Polar(OuterRadius, a1, y0), Polar(OuterRadius, a1, y1), Polar(InnerRadius, a1, y1), Polar(InnerRadius, a1, y0), StoneB);
                if (r > 0 && IsOpening(s, r - 1))
                    b.Quad(Polar(OuterRadius, a0, y0), Polar(OuterRadius, a1, y0), Polar(InnerRadius, a1, y0), Polar(InnerRadius, a0, y0), StoneB);
                if (r + 1 < rows && IsOpening(s, r + 1))
                    b.Quad(Polar(InnerRadius, a0, y1), Polar(InnerRadius, a1, y1), Polar(OuterRadius, a1, y1), Polar(OuterRadius, a0, y1), StoneB);
            }
            // Zocalo: base de piedra un poco mas ancha, cortada en la puerta y baja (prueba manual: subia 60 cm en la
            // entrada y solo se podia pasar saltando).
            for (int s = 0; s < Sides; s++)
            {
                if (s == 15) continue;
                float a0 = s * step - step * 0.5f, a1 = a0 + step;
                const float top = 0.25f, r0 = OuterRadius + 0.35f, r1 = OuterRadius + 0.15f;
                b.Quad(Polar(r0, a0, -1.5f), Polar(r1, a0, top), Polar(r1, a1, top), Polar(r0, a1, -1.5f), StoneB);
                b.Quad(Polar(r1, a0, top), Polar(OuterRadius, a0, top), Polar(OuterRadius, a1, top), Polar(r1, a1, top), StoneB);
            }
            return b;
        }

        private static int Wrap(int s) => (s % Sides + Sides) % Sides;

        // ---------------------------------------------------------------- pisos y rampas

        /// <summary>El piso f tiene un hueco donde llega la rampa de abajo y no hay altura para pasar.</summary>
        private static bool FloorHole(int floor, float angle)
        {
            if (floor == 0) return false;
            float start = RampStart[floor - 1];
            float d = Mathf.Repeat(angle - start, 360f);
            float holeFrom = RampTurn * (1f - (Headroom + FloorThickness) / FloorHeight);
            return d >= holeFrom && d <= RampTurn;
        }

        private static LowPolyMesh FloorMesh(int floor)
        {
            var b = new LowPolyMesh();
            float y = floor * FloorHeight + (floor == 0 ? 0.05f : 0f);
            const float thickness = FloorThickness;
            const int sectors = 36;
            for (int i = 0; i < sectors; i++)
            {
                float a0 = i * 360f / sectors, a1 = (i + 1) * 360f / sectors;
                Color plank = LowPolyMesh.Jitter(floor == Floors ? StoneA : WoodFloor, 0.1f, i * 7 + floor * 131);
                // Centro: siempre. Anillo externo: salvo el hueco de la rampa.
                b.Tri(new Vector3(0f, y, 0f), Polar(RampInner, a1, y), Polar(RampInner, a0, y), plank);
                if (floor > 0) b.Tri(new Vector3(0f, y - thickness, 0f), Polar(RampInner, a0, y - thickness), Polar(RampInner, a1, y - thickness), WoodDark);
                if (FloorHole(floor, (a0 + a1) * 0.5f))
                {
                    // Borde del hueco.
                    b.Quad(Polar(RampInner, a0, y - thickness), Polar(RampInner, a0, y), Polar(RampInner, a1, y), Polar(RampInner, a1, y - thickness), WoodDark);
                    continue;
                }
                b.Quad(Polar(RampInner, a0, y), Polar(RampInner, a1, y), Polar(InnerRadius + 0.05f, a1, y), Polar(InnerRadius + 0.05f, a0, y), plank);
                if (floor > 0) b.Quad(Polar(RampInner, a1, y - thickness), Polar(RampInner, a0, y - thickness), Polar(InnerRadius + 0.05f, a0, y - thickness), Polar(InnerRadius + 0.05f, a1, y - thickness), WoodDark);
            }
            return b;
        }

        private static LowPolyMesh RampMesh(int floor)
        {
            var b = new LowPolyMesh();
            float start = RampStart[floor];
            float baseY = floor * FloorHeight;
            const int steps = 54;
            const float thickness = 0.25f, rail = 0.9f;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps, t1 = (i + 1) / (float)steps;
                float a0 = start + RampTurn * t0, a1 = start + RampTurn * t1;
                float y0 = baseY + FloorHeight * t0 + 0.02f, y1 = baseY + FloorHeight * t1 + 0.02f;
                Color c = LowPolyMesh.Jitter(StoneA, 0.06f, i + floor * 61);
                // Superficie, parte de abajo y baranda del lado interno.
                b.Quad(Polar(RampInner, a0, y0), Polar(RampInner, a1, y1), Polar(RampOuter, a1, y1), Polar(RampOuter, a0, y0), c);
                b.Quad(Polar(RampOuter, a0, y0 - thickness), Polar(RampOuter, a1, y1 - thickness), Polar(RampInner, a1, y1 - thickness), Polar(RampInner, a0, y0 - thickness), StoneB);
                if (t0 < 0.97f)
                {
                    b.Quad(Polar(RampInner, a0, y0 - thickness), Polar(RampInner, a0, y0 + rail), Polar(RampInner, a1, y1 + rail), Polar(RampInner, a1, y1 - thickness), WoodDark);
                    b.Quad(Polar(RampInner - 0.08f, a1, y1 - thickness), Polar(RampInner - 0.08f, a1, y1 + rail), Polar(RampInner - 0.08f, a0, y0 + rail), Polar(RampInner - 0.08f, a0, y0 - thickness), WoodDark);
                }
            }
            return b;
        }

        // ---------------------------------------------------------------- terraza y techo

        private static LowPolyMesh TopMesh()
        {
            var b = new LowPolyMesh();
            float step = 360f / Sides;
            float y0 = TerraceY, y1 = TerraceY + ParapetHeight;
            for (int s = 0; s < Sides; s++)
            {
                float a0 = s * step - step * 0.5f, a1 = a0 + step;
                // Parapeto con almenas: un segmento si, uno no, mas alto.
                float top = (s & 1) == 0 ? y1 + 0.9f : y1;
                Color c = LowPolyMesh.Jitter(StoneA, 0.08f, s * 17);
                b.Quad(Polar(OuterRadius + 0.2f, a0, y0), Polar(OuterRadius + 0.2f, a0, top), Polar(OuterRadius + 0.2f, a1, top), Polar(OuterRadius + 0.2f, a1, y0), c);
                b.Quad(Polar(InnerRadius, a1, y0), Polar(InnerRadius, a1, top), Polar(InnerRadius, a0, top), Polar(InnerRadius, a0, y0), StoneB);
                b.Quad(Polar(InnerRadius, a0, top), Polar(InnerRadius, a1, top), Polar(OuterRadius + 0.2f, a1, top), Polar(OuterRadius + 0.2f, a0, top), StoneA);
                if ((s & 1) == 0)
                {
                    b.Quad(Polar(InnerRadius, a0, y1), Polar(InnerRadius, a0, top), Polar(OuterRadius + 0.2f, a0, top), Polar(OuterRadius + 0.2f, a0, y1), StoneB);
                    b.Quad(Polar(OuterRadius + 0.2f, a1, y1), Polar(OuterRadius + 0.2f, a1, top), Polar(InnerRadius, a1, top), Polar(InnerRadius, a1, y1), StoneB);
                }
                // Cornisa: anillo saliente bajo el parapeto.
                b.Quad(Polar(OuterRadius, a0, y0 - 0.4f), Polar(OuterRadius + 0.2f, a0, y0), Polar(OuterRadius + 0.2f, a1, y0), Polar(OuterRadius, a1, y0 - 0.4f), StoneB);
            }
            // Postes que sostienen el techo.
            for (int p = 0; p < 6; p++)
            {
                var m = Matrix4x4.Translate(Polar(3.6f, p * 60f + 30f, TerraceY));
                b.Frustum(m, 0.18f, 0.18f, 0f, 3.6f, 6, WoodDark);
            }
            return b;
        }

        private static LowPolyMesh RoofMesh()
        {
            var b = new LowPolyMesh();
            float y = TerraceY + 3.6f;
            b.Frustum(Matrix4x4.identity, 4.6f, 4.9f, y - 0.4f, y, 12, Gold, true, true);   // aro dorado
            b.Frustum(Matrix4x4.identity, 5.4f, 3.2f, y, y + 1.6f, 12, RoofDark, false, true); // alero
            b.Frustum(Matrix4x4.identity, 3.2f, 0f, y + 1.6f, y + 7.5f, 12, Roof, false);       // cono
            b.Frustum(Matrix4x4.identity, 0.12f, 0f, y + 7.2f, y + 9.2f, 6, Gold, false);       // aguja
            var orb = Matrix4x4.Translate(new Vector3(0f, y + 8.2f, 0f));
            b.Frustum(orb, 0.3f, 0f, 0f, 0.35f, 8, Gold, false, true);
            b.Frustum(orb, 0.3f, 0f, 0f, -0.35f, 8, Gold, false);
            return b;
        }

        // ---------------------------------------------------------------- puerta

        /// <summary>Puerta de tablones con herrajes, abierta hacia adentro contra el muro.</summary>
        private static int Door(Transform root, Material material)
        {
            var b = new LowPolyMesh();
            const float width = 1.6f, height = 2.9f;
            for (int i = 0; i < 4; i++)
            {
                float x = width / 8f + i * width / 4f;
                b.Box(Matrix4x4.Translate(new Vector3(x, height / 2f, 0f)), new Vector3(width / 4f - 0.02f, height, 0.08f),
                    LowPolyMesh.Jitter(new Color(0.52f, 0.33f, 0.2f), 0.1f, i * 13));
            }
            foreach (float y in new[] { 0.5f, 2.3f })
                b.Box(Matrix4x4.Translate(new Vector3(width / 2f, y, 0.05f)), new Vector3(width * 0.95f, 0.1f, 0.03f), new Color(0.2f, 0.2f, 0.22f));
            b.Box(Matrix4x4.Translate(new Vector3(width - 0.2f, 1.2f, -0.07f)), new Vector3(0.06f, 0.12f, 0.06f), Gold);
            var mesh = MapBuilder.SaveMesh(b.Build("TowerDoor"));
            // Bisagra en el borde oeste del hueco (segmento 15, centrado en 270 grados), girada hacia adentro.
            Vector3 hinge = root.position + Polar(InnerRadius - 0.05f, 270f - 360f / Sides * 0.5f, 0.05f);
            var go = MapBuilder.Place("TowerDoor", root, mesh, material, hinge, Quaternion.Euler(0f, -80f, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(width / 2f, height / 2f, 0f);
            col.size = new Vector3(width, height, 0.1f);
            return b.TriangleCount;
        }

        // ---------------------------------------------------------------- interior

        private static int Interior(Transform root, Material material)
        {
            int tris = 0;
            var props = new GameObject("Interior").transform;
            props.SetParent(root, false);
            props.position = root.position;

            // Biblioteca (planta baja): estantes donde la rampa deja altura y lejos de la puerta.
            float[] shelves0 = { 170f, 195f, 220f, 245f, 300f, 325f, 350f };
            foreach (float a in shelves0) tris += Shelf(props, material, a, 0f);
            // Estudio (primer piso): estantes donde la rampa de arriba deja altura y fuera del hueco de la de abajo.
            float[] shelves1 = { 285f };
            foreach (float a in shelves1) tris += Shelf(props, material, a, FloorHeight);

            tris += Table(props, material, "LibraryDesk", new Vector3(0f, 0f, 0.6f), 0f, 1.8f, 0.9f);
            tris += Table(props, material, "ReadingTable", new Vector3(-1.4f, 0f, -1.2f), 30f, 1.3f, 0.8f);
            // Silla detras del escritorio (lado norte): el paso entre la puerta y la rampa queda libre.
            tris += Chair(props, material, new Vector3(0f, 0f, 1.45f), 180f);
            tris += Chair(props, material, new Vector3(-1.9f, 0f, -0.4f), 200f);
            tris += Table(props, material, "StudyTable", new Vector3(0.6f, FloorHeight, 0.4f), 75f, 2f, 1f);
            tris += Chair(props, material, new Vector3(1.6f, FloorHeight, 0.9f), 255f);
            return tris;
        }

        private static int Shelf(Transform parent, Material material, float angle, float y)
        {
            var b = new LowPolyMesh();
            const float width = 1.5f, depth = 0.45f, height = 2.3f;
            b.Box(Matrix4x4.Translate(new Vector3(0f, height / 2f, 0f)), new Vector3(width, height, depth), WoodDark);
            // Estantes con libros de colores (hacia el centro de la torre = -Z local).
            Color[] books = { new Color(0.7f, 0.2f, 0.2f), new Color(0.2f, 0.35f, 0.65f), new Color(0.25f, 0.5f, 0.3f), new Color(0.75f, 0.6f, 0.25f), new Color(0.45f, 0.25f, 0.55f) };
            int seed = Mathf.RoundToInt(angle * 7f + y);
            for (int shelf = 0; shelf < 4; shelf++)
            {
                float sy = 0.25f + shelf * 0.52f;
                b.Box(Matrix4x4.Translate(new Vector3(0f, sy, -0.05f)), new Vector3(width - 0.1f, 0.05f, depth), WoodFloor);
                float x = -width / 2f + 0.1f;
                int k = 0;
                while (x < width / 2f - 0.15f)
                {
                    float w = 0.06f + 0.05f * Mathf.PerlinNoise(seed + k * 0.37f, shelf);
                    float h = 0.3f + 0.12f * Mathf.PerlinNoise(k * 0.71f, seed * 0.13f + shelf);
                    b.Box(Matrix4x4.Translate(new Vector3(x + w / 2f, sy + 0.025f + h / 2f, -0.12f)), new Vector3(w * 0.92f, h, 0.28f), books[(k + shelf + seed) % books.Length]);
                    x += w;
                    k++;
                }
            }
            var mesh = MapBuilder.SaveMesh(b.Build($"Shelf_{Mathf.RoundToInt(angle)}_{Mathf.RoundToInt(y)}"));
            Vector3 pos = parent.position + Polar(InnerRadius - depth / 2f - 0.05f, angle, y + 0.05f);
            // Frente (-Z local) hacia el centro.
            var rot = Quaternion.LookRotation(new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)));
            var go = MapBuilder.Place("Bookshelf", parent, mesh, material, pos, rot);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, height / 2f, 0f);
            col.size = new Vector3(width, height, depth);
            go.isStatic = true;
            return b.TriangleCount;
        }

        public static int Table(Transform parent, Material material, string name, Vector3 local, float yaw, float width, float depth)
        {
            var b = new LowPolyMesh();
            const float height = 0.82f;
            b.Box(Matrix4x4.Translate(new Vector3(0f, height - 0.04f, 0f)), new Vector3(width, 0.08f, depth), WoodFloor);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -width / 2f + 0.1f : width / 2f - 0.1f;
                float z = i < 2 ? -depth / 2f + 0.1f : depth / 2f - 0.1f;
                b.Box(Matrix4x4.Translate(new Vector3(x, (height - 0.08f) / 2f, z)), new Vector3(0.08f, height - 0.08f, 0.08f), WoodDark);
            }
            var mesh = MapBuilder.SaveMesh(b.Build($"Table_{name}"));
            var go = MapBuilder.Place(name, parent, mesh, material, parent.position + local, Quaternion.Euler(0f, yaw, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, height / 2f, 0f);
            col.size = new Vector3(width, height, depth);
            return b.TriangleCount;
        }

        private static int Chair(Transform parent, Material material, Vector3 local, float yaw)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.45f, 0f)), new Vector3(0.45f, 0.06f, 0.45f), new Color(0.65f, 0.2f, 0.2f));
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.8f, -0.2f)), new Vector3(0.45f, 0.65f, 0.06f), WoodDark);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -0.18f : 0.18f, z = i < 2 ? -0.18f : 0.18f;
                b.Box(Matrix4x4.Translate(new Vector3(x, 0.21f, z)), new Vector3(0.05f, 0.42f, 0.05f), WoodDark);
            }
            var mesh = MapBuilder.SaveMesh(b.Build("Chair"));
            var go = MapBuilder.Place("Chair", parent, mesh, material, parent.position + local, Quaternion.Euler(0f, yaw, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(0.45f, 1f, 0.45f);
            return b.TriangleCount;
        }

        private static void Lights(Transform root)
        {
            void PointLight(string name, Vector3 local, Color color, float range, float intensity)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.position = root.position + local;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = color;
                l.range = range;
                l.intensity = intensity;
                l.shadows = LightShadows.None;
            }
            PointLight("LibraryWarm", new Vector3(0f, 3.2f, 0f), new Color(1f, 0.75f, 0.45f), 8f, 2.2f);
            PointLight("LibraryMagic", new Vector3(-2f, 2f, 2f), new Color(0.75f, 0.4f, 1f), 5f, 1.6f);
            PointLight("StudyWarm", new Vector3(0f, FloorHeight + 3.2f, 0f), new Color(1f, 0.75f, 0.45f), 8f, 2f);
            PointLight("SecondFloor", new Vector3(0f, FloorHeight * 2 + 3.2f, 0f), new Color(0.8f, 0.6f, 1f), 8f, 1.5f);
        }
    }
}
