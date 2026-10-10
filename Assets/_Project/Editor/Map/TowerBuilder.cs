using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// La torre del modo Libre (referencias: torre de piedra con almenas + torre de Rapunzel): muro cilindrico de
    /// piedra calida con puerta al sur y ventanas enmarcadas, tres pisos (biblioteca, estudio, terraza) unidos por rampas
    /// en espiral pegadas al muro (el jugador es un Rigidbody: rampas, no escalones), almenas y techo conico de teja roja
    /// con bandera. Interior: vigas, alfombras, cortinas, faroles colgantes, estantes con libros, mesas y sillas.
    /// Afuera: estandartes y faroles a los lados de la puerta. La puerta se abre y cierra con E (World.TowerDoor).
    /// Planta baja (E5): entrada con el atril del libro de los cuatro elementos, estacion de viento al sureste y estacion
    /// de agua al oeste (ver TowerProps); el fuego queda para la terraza y el exterior.
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

        /// <summary>Puerta: ancho, alto, giro cerrada y abierta (grados, contra el muro oeste de la entrada) y duracion del giro.</summary>
        public const float DoorWidth = 1.6f, DoorHeight = 2.9f, DoorClosedYaw = 0f, DoorOpenYaw = -150f, DoorSwing = 0.5f;

        // Piedra calida (afuera arenisca, adentro revoque ocre), madera con veta y teja roja.
        private static readonly Color StoneA = new Color(0.76f, 0.7f, 0.58f);
        private static readonly Color StoneB = new Color(0.66f, 0.6f, 0.49f);
        private static readonly Color InnerA = new Color(0.82f, 0.71f, 0.56f);
        private static readonly Color InnerB = new Color(0.73f, 0.61f, 0.47f);
        private static readonly Color WoodFloor = new Color(0.6f, 0.41f, 0.25f);
        private static readonly Color WoodFloorB = new Color(0.52f, 0.34f, 0.2f);
        private static readonly Color WoodDark = new Color(0.38f, 0.23f, 0.14f);
        private static readonly Color Roof = new Color(0.66f, 0.27f, 0.2f);
        private static readonly Color RoofDark = new Color(0.5f, 0.19f, 0.15f);
        private static readonly Color Gold = new Color(0.95f, 0.78f, 0.35f);
        private static readonly Color Iron = new Color(0.2f, 0.19f, 0.2f);
        private static readonly Color Cloth = new Color(0.62f, 0.17f, 0.15f);
        private static readonly Color ClothDark = new Color(0.45f, 0.11f, 0.1f);
        private static readonly Color Ochre = new Color(0.86f, 0.62f, 0.28f);
        /// <summary>Vidrio de farol encendido: color HDR (> 1) para que el bloom lo haga brillar.</summary>
        public static readonly Color LanternGlow = new Color(2.4f, 1.6f, 0.7f);
        public static readonly Color WarmLight = new Color(1f, 0.72f, 0.42f);

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
            tris += Visual(root, "TowerTrim", TrimMesh(), material);
            tris += Visual(root, "TowerBeams", BeamsMesh(), material);
            tris += Visual(root, "TowerRugs", RugsMesh(), material);
            tris += Door(root, material);
            tris += Interior(root, material);
            Lights(root, material);
            return tris;
        }

        private static int Solid(Transform parent, string name, LowPolyMesh b, Material material)
        {
            var mesh = MapBuilder.SaveMesh(b.Build(name));
            var go = MapBuilder.Place(name, parent, mesh, material, parent.position, Quaternion.identity);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<World.Breakable>(); // la tierra con Crush (Wall Breaker) puede abrir huecos
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, (UnityEditor.StaticEditorFlags)~0 & ~UnityEditor.StaticEditorFlags.BatchingStatic); // sin batching: la malla se tiene que poder leer
            return b.TriangleCount;
        }

        private static int Visual(Transform parent, string name, LowPolyMesh b, Material material)
        {
            var mesh = MapBuilder.SaveMesh(b.Build(name));
            MapBuilder.Place(name, parent, mesh, material, parent.position, Quaternion.identity).isStatic = true;
            return b.TriangleCount;
        }

        public static Vector3 Polar(float radius, float angleDeg, float y) =>
            new Vector3(Mathf.Cos(angleDeg * Mathf.Deg2Rad) * radius, y, Mathf.Sin(angleDeg * Mathf.Deg2Rad) * radius);

        /// <summary>Yaw (grados de Unity) que deja el +X local tangente al muro en ese angulo (y el -Z local mirando al centro).</summary>
        public static float TangentYaw(float angleDeg) => 270f - angleDeg;

        /// <summary>Altura de la superficie de la rampa del piso indicado sobre ese angulo, o NaN si la rampa no pasa por ahi.</summary>
        public static float RampY(int ramp, float angleDeg)
        {
            float d = Mathf.Repeat(angleDeg - RampStart[ramp], 360f);
            return d > RampTurn ? float.NaN : ramp * FloorHeight + FloorHeight * d / RampTurn;
        }

        /// <summary>True si ninguna rampa (superficie, baranda y espesor) cruza la franja de alturas en ese arco del anillo exterior.</summary>
        public static bool RampClear(float fromDeg, float toDeg, float yLow, float yHigh)
        {
            for (float a = fromDeg; a <= toDeg + 0.01f; a += 1f)
                for (int r = 0; r < Floors; r++)
                {
                    float y = RampY(r, a);
                    if (float.IsNaN(y)) continue;
                    if (y + 1.2f > yLow && y - 0.35f < yHigh) return false;
                }
            return true;
        }

        // ---------------------------------------------------------------- muro

        /// <summary>Ventanas por piso (segmentos del muro de 20 lados).</summary>
        private static readonly int[][] WindowSegments =
        {
            new[] { 5, 0, 10 },
            new[] { 3, 8, 13, 18 },
            new[] { 1, 6, 11, 17 },
        };

        private const int DoorSegment = 15;
        private const float WindowBottom = 1.5f, WindowTop = 3f;

        /// <summary>Huecos (puerta y ventanas): segmento y filas.</summary>
        private static bool IsOpening(int segment, int row)
        {
            float y = row * Row;
            if (segment == DoorSegment && y < 3f) return true;                // puerta (sur)
            int floor = Mathf.FloorToInt(y / FloorHeight);
            float local = y - floor * FloorHeight;
            if (local < WindowBottom || local >= WindowTop || floor >= Floors) return false;
            return System.Array.IndexOf(WindowSegments[floor], segment) >= 0;
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
                // Sillares: el color varia por celda (sin desfasaje: los bordes de puerta y ventanas quedan limpios).
                float a0 = s * step - step * 0.5f, a1 = a0 + step;
                float y0 = r * Row, y1 = y0 + Row;
                Color c = LowPolyMesh.Jitter((s + r) % 3 == 0 ? StoneB : StoneA, 0.08f, s * 97 + r * 13);
                Color inner = LowPolyMesh.Jitter((s * 2 + r) % 3 == 0 ? InnerB : InnerA, 0.07f, s * 31 + r * 57);
                // Cara exterior y cara interior (revoque ocre calido: antes era gris verdoso y la planta baja se veia fria).
                b.Quad(Polar(OuterRadius, a0, y0), Polar(OuterRadius, a0, y1), Polar(OuterRadius, a1, y1), Polar(OuterRadius, a1, y0), c);
                b.Quad(Polar(InnerRadius, a1, y0), Polar(InnerRadius, a1, y1), Polar(InnerRadius, a0, y1), Polar(InnerRadius, a0, y0), inner);
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
                if (s == DoorSegment) continue;
                float a0 = s * step - step * 0.5f, a1 = a0 + step;
                const float top = 0.25f, r0 = OuterRadius + 0.35f, r1 = OuterRadius + 0.15f;
                Color plinth = LowPolyMesh.Jitter(StoneB * 0.92f, 0.06f, s * 7);
                b.Quad(Polar(r0, a0, -1.5f), Polar(r1, a0, top), Polar(r1, a1, top), Polar(r0, a1, -1.5f), plinth);
                b.Quad(Polar(r1, a0, top), Polar(OuterRadius, a0, top), Polar(OuterRadius, a1, top), Polar(r1, a1, top), plinth);
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
                // Tablas alternadas en dos tonos (la terraza es de piedra).
                Color plank = floor == Floors ? LowPolyMesh.Jitter(StoneA, 0.08f, i * 7)
                    : LowPolyMesh.Jitter((i & 1) == 0 ? WoodFloor : WoodFloorB, 0.07f, i * 7 + floor * 131);
                // Centro: siempre. Anillo externo: salvo el hueco de la rampa.
                b.Tri(new Vector3(0f, y, 0f), Polar(RampInner, a1, y), Polar(RampInner, a0, y), plank);
                if (floor > 0) b.Tri(new Vector3(0f, y - thickness, 0f), Polar(RampInner, a0, y - thickness), Polar(RampInner, a1, y - thickness), WoodDark);
                if (FloorHole(floor, (a0 + a1) * 0.5f))
                {
                    // Borde del hueco.
                    b.Quad(Polar(RampInner, a0, y - thickness), Polar(RampInner, a0, y), Polar(RampInner, a1, y), Polar(RampInner, a1, y - thickness), WoodDark);
                    continue;
                }
                b.Quad(Polar(RampInner, a0, y), Polar(RampInner, a1, y), Polar(InnerRadius + 0.05f, a1, y), Polar(InnerRadius + 0.05f, a0, y), plank * 0.96f);
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
                Color c = LowPolyMesh.Jitter((i & 1) == 0 ? StoneA : StoneB, 0.05f, i + floor * 61);
                // Superficie, parte de abajo y baranda del lado interno.
                b.Quad(Polar(RampInner, a0, y0), Polar(RampInner, a1, y1), Polar(RampOuter, a1, y1), Polar(RampOuter, a0, y0), c);
                b.Quad(Polar(RampOuter, a0, y0 - thickness), Polar(RampOuter, a1, y1 - thickness), Polar(RampInner, a1, y1 - thickness), Polar(RampInner, a0, y0 - thickness), StoneB * 0.9f);
                if (t0 < 0.97f)
                {
                    b.Quad(Polar(RampInner, a0, y0 - thickness), Polar(RampInner, a0, y0 + rail), Polar(RampInner, a1, y1 + rail), Polar(RampInner, a1, y1 - thickness), WoodDark);
                    b.Quad(Polar(RampInner - 0.08f, a1, y1 - thickness), Polar(RampInner - 0.08f, a1, y1 + rail), Polar(RampInner - 0.08f, a0, y0 + rail), Polar(RampInner - 0.08f, a0, y0 - thickness), WoodDark);
                    // Pasamanos claro arriba de la baranda.
                    b.Quad(Polar(RampInner - 0.1f, a0, y0 + rail), Polar(RampInner - 0.1f, a1, y1 + rail), Polar(RampInner + 0.02f, a1, y1 + rail), Polar(RampInner + 0.02f, a0, y0 + rail), WoodFloor);
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
                b.Quad(Polar(InnerRadius, a1, y0), Polar(InnerRadius, a1, top), Polar(InnerRadius, a0, top), Polar(InnerRadius, a0, y0), InnerB);
                b.Quad(Polar(InnerRadius, a0, top), Polar(InnerRadius, a1, top), Polar(OuterRadius + 0.2f, a1, top), Polar(OuterRadius + 0.2f, a0, top), StoneA);
                if ((s & 1) == 0)
                {
                    b.Quad(Polar(InnerRadius, a0, y1), Polar(InnerRadius, a0, top), Polar(OuterRadius + 0.2f, a0, top), Polar(OuterRadius + 0.2f, a0, y1), StoneB);
                    b.Quad(Polar(OuterRadius + 0.2f, a1, y1), Polar(OuterRadius + 0.2f, a1, top), Polar(InnerRadius, a1, top), Polar(InnerRadius, a1, y1), StoneB);
                }
                // Cornisa: anillo saliente bajo el parapeto.
                b.Quad(Polar(OuterRadius, a0, y0 - 0.4f), Polar(OuterRadius + 0.2f, a0, y0), Polar(OuterRadius + 0.2f, a1, y0), Polar(OuterRadius, a1, y0 - 0.4f), StoneB);
            }
            // Postes que sostienen el techo: seis, cada 24 grados, en el arco macizo de la terraza (centrado en 231).
            // La rampa de arriba entra por 240 y el hueco del piso de la terraza va de 312 a 150: FloorHole descarta
            // cualquier angulo en hueco, asi ningun poste queda en el aire.
            const float postCenter = 231f;
            for (int p = 0; p < 6; p++)
            {
                float angle = postCenter + (p - 2.5f) * 24f;
                if (FloorHole(Floors, angle)) continue;
                var m = Matrix4x4.Translate(Polar(3.6f, angle, TerraceY));
                b.Frustum(m, 0.18f, 0.18f, 0f, 3.6f, 6, WoodDark);
            }
            return b;
        }

        private static LowPolyMesh RoofMesh()
        {
            var b = new LowPolyMesh();
            float y = TerraceY + 3.6f;
            b.Frustum(Matrix4x4.identity, 4.6f, 4.9f, y - 0.4f, y, 12, Gold, true, true);     // aro dorado
            b.Frustum(Matrix4x4.identity, 5.4f, 3.2f, y, y + 1.6f, 12, RoofDark, false, true);  // alero
            // Cono de teja roja en tres franjas (la del medio mas oscura) y una aguja dorada.
            b.Frustum(Matrix4x4.identity, 3.2f, 2.2f, y + 1.6f, y + 3.4f, 12, Roof, false);
            b.Frustum(Matrix4x4.identity, 2.2f, 1.2f, y + 3.4f, y + 5.4f, 12, RoofDark, false);
            b.Frustum(Matrix4x4.identity, 1.2f, 0f, y + 5.4f, y + 7.5f, 12, Roof, false);
            b.Frustum(Matrix4x4.identity, 0.12f, 0f, y + 7.2f, y + 9.6f, 6, Gold, false);        // aguja
            var orb = Matrix4x4.Translate(new Vector3(0f, y + 8.2f, 0f));
            b.Frustum(orb, 0.3f, 0f, 0f, 0.35f, 8, Gold, false, true);
            b.Frustum(orb, 0.3f, 0f, 0f, -0.35f, 8, Gold, false);
            // Bandera en la aguja, sobre la esfera: triangulo de dos caras hacia el este.
            Vector3 f0 = new Vector3(0.03f, y + 8.75f, 0f), f1 = new Vector3(0.03f, y + 9.4f, 0f), tip = new Vector3(1.3f, y + 9.0f, 0.06f);
            b.Tri(f0, f1, tip, Cloth);
            b.Tri(f1, f0, tip, ClothDark);
            Vector3 g0 = new Vector3(0.03f, y + 8.95f, 0.002f), g1 = new Vector3(0.03f, y + 9.2f, 0.002f), gTip = new Vector3(0.6f, y + 9.06f, 0.03f);
            b.Tri(g0, g1, gTip, Gold);
            b.Tri(g1, g0, gTip, Gold);
            return b;
        }

        // ---------------------------------------------------------------- detalles del muro

        /// <summary>
        /// Marcos de madera en ventanas y puerta, cruces de ventana, estandartes y soportes de faroles afuera, cortinas
        /// adentro (solo donde ninguna rampa pasa por delante).
        /// </summary>
        private static LowPolyMesh TrimMesh()
        {
            var b = new LowPolyMesh();
            float step = 360f / Sides;
            float halfOut = OuterRadius * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad);
            float faceOut = OuterRadius * Mathf.Cos(step * 0.5f * Mathf.Deg2Rad);
            float halfIn = InnerRadius * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad);
            float faceIn = InnerRadius * Mathf.Cos(step * 0.5f * Mathf.Deg2Rad);

            for (int f = 0; f < Floors; f++)
                foreach (int s in WindowSegments[f])
                {
                    float c = s * step;
                    float y0 = f * FloorHeight + WindowBottom, y1 = f * FloorHeight + WindowTop;
                    Matrix4x4 outside = FaceFrame(c, faceOut);
                    // Marco exterior: jambas, dintel, alfeizar saliente y cruz.
                    Frame(b, outside, halfOut, y0, y1, 0.04f);
                    b.Box(outside * Matrix4x4.Translate(new Vector3(0f, y0 - 0.06f, 0.1f)), new Vector3(halfOut * 2f + 0.3f, 0.1f, 0.26f), StoneB * 0.95f);
                    b.Box(outside * Matrix4x4.Translate(new Vector3(0f, (y0 + y1) * 0.5f, -0.05f)), new Vector3(0.06f, y1 - y0, 0.06f), WoodDark);
                    b.Box(outside * Matrix4x4.Translate(new Vector3(0f, y0 + (y1 - y0) * 0.55f, -0.05f)), new Vector3(halfOut * 2f, 0.06f, 0.06f), WoodDark);
                    // Cortinas recogidas a los lados (rojo con lazo dorado), del lado de adentro.
                    if (RampClear(c - 12f, c + 12f, y0 - 0.3f, y1 + 0.25f)) Curtains(b, FaceFrame(c, faceIn - 0.06f), halfIn, y0, y1);
                }

            // Puerta: marco de madera adentro y afuera, y dintel grueso afuera.
            float door = DoorSegment * step;
            Frame(b, FaceFrame(door, faceOut), halfOut, 0f, 3f, 0.05f);
            b.Box(FaceFrame(door, faceOut) * Matrix4x4.Translate(new Vector3(0f, 3.12f, 0.06f)), new Vector3(halfOut * 2f + 0.5f, 0.24f, 0.16f), WoodDark);
            Frame(b, FaceFrame(door, faceIn - 0.12f), halfIn, 0f, 3f, 0.04f);

            // Estandartes a los lados de la puerta (segmentos 14 y 16), sobre los faroles.
            foreach (int s in new[] { DoorSegment - 1, DoorSegment + 1 })
                Banner(b, FaceFrame(s * step, faceOut));
            return b;
        }

        /// <summary>Sistema local de una cara del muro: +Z hacia afuera, origen en el centro de la cara a altura 0.</summary>
        private static Matrix4x4 FaceFrame(float angleDeg, float distance)
        {
            Vector3 outward = Polar(1f, angleDeg, 0f);
            return Matrix4x4.TRS(outward * distance, Quaternion.LookRotation(outward), Vector3.one);
        }

        private static void Frame(LowPolyMesh b, Matrix4x4 face, float half, float y0, float y1, float proud)
        {
            const float bar = 0.12f;
            b.Box(face * Matrix4x4.Translate(new Vector3(-half - bar * 0.3f, (y0 + y1) * 0.5f, proud)), new Vector3(bar, y1 - y0 + bar, 0.1f), WoodDark);
            b.Box(face * Matrix4x4.Translate(new Vector3(half + bar * 0.3f, (y0 + y1) * 0.5f, proud)), new Vector3(bar, y1 - y0 + bar, 0.1f), WoodDark);
            b.Box(face * Matrix4x4.Translate(new Vector3(0f, y1 + bar * 0.4f, proud)), new Vector3(half * 2f + bar * 2f, bar, 0.1f), WoodDark);
        }

        private static void Curtains(LowPolyMesh b, Matrix4x4 face, float half, float y0, float y1)
        {
            // Barral de hierro con remates dorados.
            b.Box(face * Matrix4x4.Translate(new Vector3(0f, y1 + 0.16f, -0.02f)), new Vector3(half * 2f + 0.3f, 0.035f, 0.035f), Iron);
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(face * Matrix4x4.Translate(new Vector3(side * (half + 0.17f), y1 + 0.16f, -0.02f)), new Vector3(0.07f, 0.07f, 0.07f), Gold);
                float x = side * (half - 0.12f);
                float tie = y0 + (y1 - y0) * 0.45f;
                // Pano de arriba (ancho), lazo y caida de abajo (mas angosta, pegada al costado).
                b.Box(face * Matrix4x4.Translate(new Vector3(x, (tie + y1 + 0.14f) * 0.5f, -0.05f)), new Vector3(0.4f, y1 + 0.14f - tie, 0.05f), Cloth);
                b.Box(face * Matrix4x4.Translate(new Vector3(x + side * 0.04f, tie, -0.06f)), new Vector3(0.24f, 0.07f, 0.07f), Gold);
                b.Box(face * Matrix4x4.Translate(new Vector3(x + side * 0.06f, (tie + y0 - 0.25f) * 0.5f, -0.05f)), new Vector3(0.28f, tie - y0 + 0.25f, 0.05f), ClothDark);
            }
        }

        /// <summary>Estandarte de tela roja con borde y rombo dorados, colgado de un barral, y debajo el soporte de un farol.</summary>
        private static void Banner(LowPolyMesh b, Matrix4x4 face)
        {
            const float top = 5.7f, width = 0.8f, length = 2.1f, z = 0.06f;
            b.Box(face * Matrix4x4.Translate(new Vector3(0f, top + 0.05f, z + 0.03f)), new Vector3(width + 0.25f, 0.06f, 0.06f), WoodDark);
            b.Box(face * Matrix4x4.Translate(new Vector3(0f, top - length * 0.5f + 0.1f, z)), new Vector3(width, length - 0.2f, 0.03f), Cloth);
            // Punta en V (cola de golondrina) abajo.
            Vector3 P(float x, float y) => face.MultiplyPoint3x4(new Vector3(x, y, z + 0.016f));
            float yb = top - length + 0.2f;
            b.Tri(P(-width * 0.5f, yb), P(0f, yb - 0.25f), P(0f, yb), Cloth);
            b.Tri(P(0f, yb), P(0f, yb - 0.25f), P(width * 0.5f, yb), Cloth);
            b.Box(face * Matrix4x4.Translate(new Vector3(0f, top - 0.12f, z + 0.02f)), new Vector3(width, 0.06f, 0.02f), Gold);
            b.Box(face * Matrix4x4.Translate(new Vector3(0f, yb + 0.12f, z + 0.02f)), new Vector3(width, 0.05f, 0.02f), Gold);
            var emblem = face * Matrix4x4.TRS(new Vector3(0f, top - length * 0.45f, z + 0.025f), Quaternion.Euler(0f, 0f, 45f), Vector3.one);
            b.Box(emblem, new Vector3(0.36f, 0.36f, 0.02f), Gold);
            b.Box(emblem * Matrix4x4.Translate(new Vector3(0f, 0f, 0.012f)), new Vector3(0.2f, 0.2f, 0.02f), ClothDark);
            // Soporte del farol (el farol con su luz lo pone Lights).
            b.Box(face * Matrix4x4.Translate(new Vector3(0f, 2.75f, 0.2f)), new Vector3(0.06f, 0.06f, 0.42f), Iron);
            b.Box(face * Matrix4x4.Translate(new Vector3(0f, 2.62f, 0.03f)), new Vector3(0.16f, 0.36f, 0.05f), Iron);
        }

        // ---------------------------------------------------------------- vigas y alfombras

        /// <summary>Vigas bajo el piso del estudio (solo en el disco central y donde no baja la rampa) y un florón central.</summary>
        private static LowPolyMesh BeamsMesh()
        {
            var b = new LowPolyMesh();
            float y = FloorHeight - FloorThickness - 0.09f;
            for (int i = 0; i < 6; i++)
            {
                float a = 30f + i * 60f;
                // Hasta el muro solo en el arco sin rampa de la planta baja ni hueco arriba (de 300 a 50 grados).
                float d = Mathf.Repeat(a + 60f, 360f);
                float outer = d < 110f ? InnerRadius - 0.05f : RampInner - 0.08f;
                Vector3 dir = Polar(1f, a, 0f);
                b.Box(Matrix4x4.TRS(dir * (outer + 0.3f) * 0.5f + Vector3.up * y, Quaternion.LookRotation(dir), Vector3.one), new Vector3(0.18f, 0.18f, outer - 0.3f), WoodDark);
            }
            // Anillo de vigas en el borde del disco y florón.
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f + 15f;
                Vector3 dir = Polar(1f, a, 0f);
                b.Box(Matrix4x4.TRS(dir * (RampInner - 0.16f) + Vector3.up * (y - 0.02f), Quaternion.LookRotation(Polar(1f, a + 90f, 0f)), Vector3.one),
                    new Vector3(0.14f, 0.14f, 2f * (RampInner - 0.16f) * Mathf.Sin(15f * Mathf.Deg2Rad) + 0.1f), WoodFloorB);
            }
            b.Frustum(Matrix4x4.Translate(Vector3.up * (FloorHeight - FloorThickness)), 0.35f, 0.45f, -0.35f, 0f, 8, WoodDark, false, true);
            return b;
        }

        /// <summary>Alfombras chatas (visuales): pasillo de la puerta al centro y redonda bajo el escritorio.</summary>
        private static LowPolyMesh RugsMesh()
        {
            var b = new LowPolyMesh();
            const float y = 0.05f;
            b.Box(Matrix4x4.Translate(new Vector3(0f, y + 0.006f, -3.25f)), new Vector3(1.15f, 0.012f, 2.5f), Ochre);
            b.Box(Matrix4x4.Translate(new Vector3(0f, y + 0.008f, -3.25f)), new Vector3(0.95f, 0.012f, 2.3f), Cloth);
            b.Box(Matrix4x4.Translate(new Vector3(0f, y + 0.01f, -3.25f)), new Vector3(0.3f, 0.012f, 1.9f), ClothDark);
            var round = Matrix4x4.Translate(new Vector3(0f, y, 0.3f));
            b.Frustum(round, 1.75f, 1.75f, 0f, 0.012f, 20, ClothDark, true, false);
            b.Frustum(round, 1.55f, 1.55f, 0f, 0.015f, 20, Ochre, true, false);
            b.Frustum(round, 1.4f, 1.4f, 0f, 0.018f, 20, Cloth, true, false);
            b.Frustum(round, 0.55f, 0.55f, 0f, 0.021f, 10, Ochre, true, false);
            return b;
        }

        // ---------------------------------------------------------------- puerta

        /// <summary>Puerta de tablones con herrajes y aldaba; la abre y cierra World.TowerDoor (arranca abierta).</summary>
        private static int Door(Transform root, Material material)
        {
            var b = new LowPolyMesh();
            const float width = DoorWidth, height = DoorHeight;
            for (int i = 0; i < 4; i++)
            {
                float x = width / 8f + i * width / 4f;
                b.Box(Matrix4x4.Translate(new Vector3(x, height / 2f, 0f)), new Vector3(width / 4f - 0.02f, height, 0.08f),
                    LowPolyMesh.Jitter(new Color(0.55f, 0.34f, 0.2f), 0.1f, i * 13));
            }
            // Herrajes de los dos lados, clavos y tiradores (afuera = -Z local con la puerta cerrada).
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float y in new[] { 0.5f, 2.3f })
                {
                    b.Box(Matrix4x4.Translate(new Vector3(width / 2f, y, side * 0.05f)), new Vector3(width * 0.95f, 0.1f, 0.03f), Iron);
                    for (int k = 0; k < 4; k++)
                        b.Box(Matrix4x4.Translate(new Vector3(width / 8f + k * width / 4f, y, side * 0.07f)), new Vector3(0.035f, 0.035f, 0.02f), Gold * 0.8f);
                }
                b.Box(Matrix4x4.Translate(new Vector3(width - 0.2f, 1.2f, side * 0.07f)), new Vector3(0.06f, 0.12f, 0.06f), Gold);
            }
            // Aldaba (aro) afuera.
            b.Frustum(Matrix4x4.TRS(new Vector3(width * 0.5f, 1.45f, -0.08f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), 0.09f, 0.09f, -0.01f, 0.01f, 8, Gold, true, true);
            var mesh = MapBuilder.SaveMesh(b.Build("TowerDoor"));
            // Bisagra en el borde oeste del hueco (segmento 15, centrado en 270 grados).
            Vector3 hinge = root.position + Polar(InnerRadius - 0.05f, 270f - 360f / Sides * 0.5f, 0.05f);
            var go = MapBuilder.Place("TowerDoor", root, mesh, material, hinge, Quaternion.Euler(0f, DoorOpenYaw, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(width / 2f, height / 2f, 0f);
            col.size = new Vector3(width, height, 0.1f);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            go.AddComponent<SpellSimulator.World.TowerDoor>().Configure(DoorClosedYaw, DoorOpenYaw, DoorSwing);
            return b.TriangleCount;
        }

        // ---------------------------------------------------------------- interior

        private static int Interior(Transform root, Material material)
        {
            int tris = 0;
            var props = new GameObject("Interior").transform;
            props.SetParent(root, false);
            props.position = root.position;

            // Biblioteca (planta baja). Estantes altos donde la rampa deja altura (228 junto al atril, 292 al lado de la
            // puerta) y uno bajo bajo la rampa (143). El sureste queda para la estacion de viento y el oeste para la de agua.
            tris += Shelf(props, material, 228f, 0f);
            tris += Shelf(props, material, 292f, 0f);
            tris += Shelf(props, material, 143f, 0f, null, 3);
            // Estudio (primer piso): estantes donde la rampa de arriba deja altura y fuera del hueco de la de abajo.
            tris += Shelf(props, material, 285f, FloorHeight);

            // Escritorio central (sobre la alfombra redonda) y mesa de lectura al este; el pasillo puerta-centro queda libre.
            tris += Table(props, material, "LibraryDesk", new Vector3(0f, 0f, 0.3f), 0f, 1.8f, 0.9f);
            tris += Table(props, material, "ReadingTable", new Vector3(1.85f, 0f, 0.5f), 90f, 1.3f, 0.8f);
            tris += Chair(props, material, new Vector3(0f, 0f, 1.15f), 180f);
            tris += Chair(props, material, new Vector3(1.85f, 0f, -0.55f), 0f);
            tris += Table(props, material, "StudyTable", new Vector3(0.6f, FloorHeight, 0.4f), 75f, 2f, 1f);
            tris += Chair(props, material, new Vector3(1.6f, FloorHeight, 0.9f), 255f);
            return tris;
        }

        /// <summary>Estante contra el muro (radio por defecto) o exento a un radio dado; levels = estantes (4 = alto, 2.3 m).</summary>
        public static int Shelf(Transform parent, Material material, float angle, float y, float? radius = null, int levels = 4)
        {
            var b = new LowPolyMesh();
            const float width = 1.5f, depth = 0.45f;
            float height = 0.25f + levels * 0.52f + 0.01f;
            b.Box(Matrix4x4.Translate(new Vector3(0f, height / 2f, 0.02f)), new Vector3(width, height, depth - 0.04f), WoodDark);
            // Cornisa arriba y zocalo.
            b.Box(Matrix4x4.Translate(new Vector3(0f, height + 0.03f, -0.02f)), new Vector3(width + 0.08f, 0.06f, depth + 0.04f), WoodFloorB);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.06f, -0.03f)), new Vector3(width + 0.02f, 0.12f, depth), WoodFloorB);
            // Estantes con libros de colores (hacia el centro de la torre = -Z local).
            Color[] books = { new Color(0.7f, 0.2f, 0.18f), new Color(0.2f, 0.35f, 0.6f), new Color(0.25f, 0.48f, 0.3f), new Color(0.78f, 0.6f, 0.25f), new Color(0.5f, 0.3f, 0.2f), new Color(0.85f, 0.78f, 0.6f) };
            int seed = Mathf.RoundToInt(angle * 7f + y);
            for (int shelf = 0; shelf < levels; shelf++)
            {
                float sy = 0.25f + shelf * 0.52f;
                b.Box(Matrix4x4.Translate(new Vector3(0f, sy, -0.05f)), new Vector3(width - 0.1f, 0.05f, depth), WoodFloor);
                float x = -width / 2f + 0.1f;
                int k = 0;
                while (x < width / 2f - 0.15f)
                {
                    float w = 0.06f + 0.05f * Mathf.PerlinNoise(seed + k * 0.37f, shelf);
                    float h = 0.3f + 0.12f * Mathf.PerlinNoise(k * 0.71f, seed * 0.13f + shelf);
                    // Algun hueco y algun libro inclinado para que no parezca una pared de colores.
                    float gap = Mathf.PerlinNoise(k * 1.3f + seed, shelf * 2.1f);
                    if (gap > 0.78f) { x += 0.08f; k++; continue; }
                    float lean = gap < 0.12f ? 12f : 0f;
                    b.Box(Matrix4x4.TRS(new Vector3(x + w / 2f, sy + 0.025f + h / 2f, -0.12f), Quaternion.Euler(0f, 0f, lean), Vector3.one),
                        new Vector3(w * 0.92f, h, 0.28f), books[(k + shelf + seed) % books.Length]);
                    x += w + (lean > 0f ? 0.04f : 0f);
                    k++;
                }
            }
            var mesh = MapBuilder.SaveMesh(b.Build($"Shelf_{Mathf.RoundToInt(angle)}_{Mathf.RoundToInt(y)}"));
            Vector3 pos = parent.position + Polar(radius ?? InnerRadius - depth / 2f - 0.05f, angle, y + 0.05f);
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
            // Faldon bajo la cubierta y patas torneadas (dos tramos).
            b.Box(Matrix4x4.Translate(new Vector3(0f, height - 0.13f, 0f)), new Vector3(width - 0.16f, 0.1f, depth - 0.16f), WoodDark);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -width / 2f + 0.1f : width / 2f - 0.1f;
                float z = i < 2 ? -depth / 2f + 0.1f : depth / 2f - 0.1f;
                b.Box(Matrix4x4.Translate(new Vector3(x, (height - 0.08f) / 2f, z)), new Vector3(0.08f, height - 0.08f, 0.08f), WoodDark);
                b.Box(Matrix4x4.Translate(new Vector3(x, 0.04f, z)), new Vector3(0.11f, 0.08f, 0.11f), WoodFloorB);
            }
            var mesh = MapBuilder.SaveMesh(b.Build($"Table_{name}"));
            var go = MapBuilder.Place(name, parent, mesh, material, parent.position + local, Quaternion.Euler(0f, yaw, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, height / 2f, 0f);
            col.size = new Vector3(width, height, depth);
            return b.TriangleCount;
        }

        public static int Chair(Transform parent, Material material, Vector3 local, float yaw)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.45f, 0f)), new Vector3(0.45f, 0.06f, 0.45f), new Color(0.65f, 0.2f, 0.18f));
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

        // ---------------------------------------------------------------- luces y faroles

        private static void Lights(Transform root, Material material)
        {
            // Luz general calida de la biblioteca (sin luces violetas: pegaban manchas magenta en el cielorraso).
            PointLight(root, "LibraryWarm", new Vector3(0f, 3.2f, 0f), WarmLight, 8.5f, 2.4f);
            PointLight(root, "StudyWarm", new Vector3(0f, FloorHeight + 3.2f, 0f), WarmLight, 8f, 2f);
            PointLight(root, "AlchemyWarm", new Vector3(0f, FloorHeight * 2 + 2.6f, 0f), new Color(1f, 0.8f, 0.5f), 6f, 1.6f);

            // Faroles colgados de las vigas sobre las dos estaciones (viento al sureste, agua al oeste).
            float ceiling = FloorHeight - FloorThickness - 0.18f;
            HangingLantern(root, material, "Lantern_Wind", Polar(2.3f, 330f, 0f), ceiling, 2.5f);
            HangingLantern(root, material, "Lantern_Water", Polar(2.3f, 210f, 0f), ceiling, 2.5f);
            // Faroles de pared afuera, a los lados de la puerta (bajo los estandartes).
            float step = 360f / Sides;
            float face = OuterRadius * Mathf.Cos(step * 0.5f * Mathf.Deg2Rad);
            foreach (int s in new[] { DoorSegment - 1, DoorSegment + 1 })
            {
                Vector3 at = Polar(face + 0.38f, s * step, 0f);
                HangingLantern(root, material, $"Lantern_Door_{s}", at, 2.72f, 2.2f, false);
            }
        }

        private static Light PointLight(Transform root, string name, Vector3 local, Color color, float range, float intensity)
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
            return l;
        }

        /// <summary>Farol de hierro con vidrio encendido (color HDR) colgado de una cadena hasta hangY, con su luz.</summary>
        public static void HangingLantern(Transform root, Material material, string name, Vector3 local, float hangY, float bottomY, bool chain = true)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.02f, 0f)), new Vector3(0.24f, 0.04f, 0.24f), Iron);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.17f, 0f)), new Vector3(0.15f, 0.26f, 0.15f), LanternGlow);
            for (int i = 0; i < 4; i++)
                b.Box(Matrix4x4.Translate(new Vector3((i & 1) == 0 ? -0.09f : 0.09f, 0.17f, i < 2 ? -0.09f : 0.09f)), new Vector3(0.025f, 0.28f, 0.025f), Iron);
            b.Frustum(Matrix4x4.Translate(new Vector3(0f, 0.3f, 0f)), 0.17f, 0.03f, 0f, 0.13f, 4, Iron, true, true);
            float top = hangY - bottomY;
            b.Frustum(Matrix4x4.TRS(new Vector3(0f, 0.47f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), 0.05f, 0.05f, -0.01f, 0.01f, 6, Iron);
            if (chain && top > 0.5f)
                b.Box(Matrix4x4.Translate(new Vector3(0f, (0.52f + top) * 0.5f, 0f)), new Vector3(0.02f, top - 0.52f, 0.02f), Iron);
            var mesh = MapBuilder.SaveMesh(b.Build($"Lantern_{(chain ? "Hanging" : "Wall")}"));
            var go = MapBuilder.Place(name, root, mesh, material, root.position + local + Vector3.up * bottomY, Quaternion.Euler(0f, 45f, 0f));
            go.isStatic = true;
            var light = PointLight(go.transform, name + "_Light", Vector3.zero, WarmLight, 5f, 1.5f);
            light.transform.localPosition = new Vector3(0f, 0.17f, 0f);
        }
    }
}
