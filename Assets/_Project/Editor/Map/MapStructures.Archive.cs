using UnityEngine;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    public static partial class MapStructures
    {
        // ---------------------------------------------------------------- archivo

        // Marco local (StructRoot mira al Nexo): +Z = norte, +X = este. Angulos en grados desde +X hacia +Z (0 = este, 90 = norte).
        // Los puestos de MapRestocker quedan afuera (arco de r ~13): la pergola es un anillo completo de 15 columnas en r 8.8.
        // Vanos segun los caminos REALES de ValleyShape.Paths: norte = entrada (el camino entra en (1, 9) local, cruza r 8.8 a ~84
        // grados), oeste = agua (camino al lago), suroeste = fuego (lleva al extremo oeste del camino del sur, en el borde del campo de
        // fuego), este = tierra (camino directo). El camino al viento sale al estenoreste (24.9 grados) por el tramo 12.5-39; su
        // estandarte va en un mastil suelto, afuera.
        private const float ArcFloorRadius = 9f, ArcCurbRadius = 9.5f, ArcFloorLift = 0.03f;
        private const int ArcFloorSegments = 24, ArcBeamSegments = 24;
        private const float ArcColumnRadius = 8.8f, ArcColumnHeight = 3.8f, ArcShaftRadius = 0.25f;
        private const float ArcBeamHeight = 0.3f, ArcRafterHeight = 0.12f, ArcPurlinHeight = 0.1f;
        private const float ArcWaterAngle = 180f, ArcFireAngle = 225f, ArcEarthAngle = 360f;
        /// <summary>Medio angulo de los vanos con estandarte (luz libre ~2,7 m) y del vano este (~3,3 m: deja libres los caminos a tierra y viento).</summary>
        private const float ArcVanoHalf = 10.5f, ArcEarthVanoHalf = 12.5f;
        /// <summary>
        /// Vano de entrada norte: luz libre 3,2 m (medio angulo 12.15 a r 8.8). Centrado en 82 grados y no en 84: el camino (2,2 m)
        /// sigue cabiendo entero (cruza r 8.8 entre 77 y 92 grados) y el tramo hacia la columna del viento queda en ~31 grados.
        /// </summary>
        private const float ArcNorthAngle = 82f, ArcNorthVanoHalf = 12.15f;
        /// <summary>Columna entre el camino del viento (cruza r 8.8 entre 17.7 y 32.1 grados) y el vano norte.</summary>
        private const float ArcWindColumnAngle = 39f;
        /// <summary>Cartel de bienvenida: afuera, al oeste del camino de entrada (2,5 m del eje), mirando a quien llega.</summary>
        private static readonly Vector2 ArcWelcomeSign = new Vector2(-0.63f, 11.15f);
        /// <summary>Mastil del estandarte del viento (entre el camino a tierra y el del viento) y hacia donde cuelga la bandera.</summary>
        private static readonly Vector2 ArcWindMast = new Vector2(9.6f, 1.9f);
        private static readonly Vector2 ArcWindFlagDir = new Vector2(-0.5f, 0.87f);
        private const float ArcLecternRadius = 4.2f, ArcReaderRadius = 3.3f, ArcSignRadius = 8.1f;
        private const float ArcVariantsAngle = 202.5f, ArcMotionAngle = 245f, ArcShapeAngle = 296f, ArcRareAngle = 338f;
        /// <summary>Estantes de la memoria contra el anillo (frente 7.9, fondo 8.3; los plintos empiezan en 8.45).</summary>
        private const float ArcShelfRadius = 8.1f, ArcDaisHeight = 0.15f;
        private const string ArcTitleColor = "#3D4F8C";

        private static readonly Color ArcSlab = new Color(0.86f, 0.82f, 0.73f);
        private static readonly Color ArcSlabB = new Color(0.8f, 0.76f, 0.67f);
        private static readonly Color ArcCurb = new Color(0.58f, 0.55f, 0.5f);
        private static readonly Color ArcRuneBlue = new Color(0.34f, 0.4f, 0.62f);
        private static readonly Color ArcRuneGold = new Color(0.78f, 0.64f, 0.34f);
        private static readonly Color ArcWalnut = new Color(0.36f, 0.22f, 0.13f);
        private static readonly Color ArcOak = new Color(0.6f, 0.43f, 0.26f);
        private static readonly Color ArcSlot = new Color(0.16f, 0.11f, 0.08f);
        private static readonly Color ArcLanternGlow = new Color(1f, 0.82f, 0.45f);
        private static readonly Color ArcDusk = new Color(0.22f, 0.24f, 0.38f);
        private static readonly Color ArcSage = new Color(0.45f, 0.55f, 0.4f);
        private static readonly Color ArcCanopyTop = new Color(0.34f, 0.52f, 0.56f);
        private static readonly Color ArcLampShade = new Color(0.9f, 0.84f, 0.66f);
        private static readonly Color ArcLampOff = new Color(0.62f, 0.6f, 0.55f);
        private static readonly Color ArcWicker = new Color(0.72f, 0.56f, 0.32f);
        private static readonly Color ArcClay = new Color(0.7f, 0.42f, 0.28f);
        private static readonly Color ArcSand = new Color(0.88f, 0.78f, 0.56f);
        private static readonly Color ArcGlass = new Color(0.66f, 0.85f, 0.9f);
        private static readonly Color ArcPotionGreen = new Color(0.4f, 0.72f, 0.35f);
        private static readonly Color ArcPotionViolet = new Color(0.55f, 0.32f, 0.7f);
        private static readonly Color ArcPotionAmber = new Color(0.9f, 0.62f, 0.22f);
        private static readonly Color ArcPotionRed = new Color(0.75f, 0.25f, 0.25f);
        private static readonly Color ArcWaterCloth = new Color(0.16f, 0.3f, 0.68f), ArcWaterEmblem = new Color(0.75f, 0.86f, 0.96f);
        private static readonly Color ArcFireCloth = new Color(0.85f, 0.3f, 0.12f), ArcFireEmblem = new Color(0.98f, 0.7f, 0.25f);
        private static readonly Color ArcEarthCloth = new Color(0.3f, 0.5f, 0.24f), ArcEarthEmblem = new Color(0.8f, 0.62f, 0.25f);
        private static readonly Color ArcWindCloth = new Color(0.92f, 0.68f, 0.2f), ArcWindEmblem = new Color(0.98f, 0.94f, 0.78f);

        /// <summary>
        /// Archivo (Docs/Archivo/ARCHIVO_PLAN.md, Docs/MAPA_FLUJO_PLAN.md §18): rotonda abierta de r = 9 con piso de losas y anillos de
        /// mosaico, pergola anular completa sobre 15 columnas con faroles, vano de entrada norte de 3,2 m, tres estandartes en sus
        /// vanos (agua, fuego, tierra) y estandarte del viento en un mastil. Adentro: mesa central con atril vacio,
        /// tintero y papeles; tres nichos de estudio con su libro (variants, motion, shape) y zona de prueba; el rincon de los signos
        /// raros sobre una tarima con mesa de alquimia; dos estantes de la memoria. Una sola malla estatica (Struct_Archive); libros,
        /// atriles, mesa de alquimia, caja, plumas y carteles son objetos aparte. Pasillos de >= 2,2 m de cada vano y del portal (sur,
        /// paso aparte) hasta el centro.
        /// </summary>
        private static void Archive(Transform parent, Site site)
        {
            var t = StructRoot(parent, "Archive", site);
            float yaw = t.eulerAngles.y;
            var b = new LowPolyMesh();

            // Piso a un solo nivel, 3 cm sobre el punto mas alto del terreno dentro del zocalo (el terreno ya viene aplanado).
            ArcGroundRange(t, ArcCurbRadius, out float groundMin, out float groundMax);
            float floorY = groundMax + ArcFloorLift;
            if (groundMax - groundMin > 0.25f)
                Debug.LogWarning($"[Estructuras] el piso del Archivo cubre un desnivel de {groundMax - groundMin:F2} m: revisar el aplanado.");

            // 1. Piso, mosaicos y zocalo (con collider).
            ArcFloor(b, t, floorY, groundMin);

            // 2. Anillo completo de columnas, viga anular y aro de cabriadas (sin colliders arriba: el jugador no llega a 3,8 m),
            // faroles y estandartes. La entrada norte es un vano mas del anillo (sin estandarte).
            float top = floorY + ArcColumnHeight;
            float[] cols = ArcColumnAngles();
            foreach (float angle in cols)
                ArcColumn(b, t, ArcPolar(ArcColumnRadius, angle), floorY, top, ArcFacing(angle));
            float first = cols[0], last = first + 360f;
            ArcRingBeam(b, ArcColumnRadius - 0.16f, ArcColumnRadius + 0.16f, top, top + ArcBeamHeight, first, last, ArcBeamSegments, WoodB);
            float rafterTop = top + ArcBeamHeight + ArcRafterHeight;
            ArcRingBeam(b, 7.8f, 7.92f, rafterTop, rafterTop + ArcPurlinHeight, first, last, ArcBeamSegments, WoodA);
            for (int i = 0; i < cols.Length; i++)
            {
                float next = i + 1 < cols.Length ? cols[i + 1] : cols[0] + 360f;
                ArcRafter(b, cols[i], top + ArcBeamHeight);
                ArcRafter(b, (cols[i] + next) * 0.5f, top + ArcBeamHeight);
                // Faroles (solo malla, sin luz real: Quest) en cada tramo, salvo los de estandarte, el vano norte y el del portal (sur).
                float mid = (cols[i] + next) * 0.5f;
                if (ArcIsLanternBay(mid))
                    ArcLantern(b, ArcAt(7.95f, mid, top + ArcBeamHeight), 0.85f);
            }
            ArcVanoBanner(b, ArcWaterAngle, top, ArcWaterCloth, ArcWaterEmblem);
            ArcVanoBanner(b, ArcFireAngle, top, ArcFireCloth, ArcFireEmblem);
            ArcVanoBanner(b, ArcEarthAngle, top, ArcEarthCloth, ArcEarthEmblem);
            ArcWindStandard(b, t, ArcWindCloth, ArcWindEmblem);

            // 3. Nichos de estudio (atril de cara al centro: quien lee mira hacia su zona de prueba) y rincon de los signos raros.
            ArcVariantsZone(b, t, floorY);
            ArcMotionZone(b, t, floorY);
            ArcShapeZone(b, t, floorY);
            ArcRareNook(b, t, floorY, groundMin);

            // 5. Mesa central con atril vacio. 6. Estantes de la memoria contra el anillo, al noreste (entre el pasillo del viento y el
            // norte) y al noroeste (~3,5 m cada uno).
            ArcCenter(b, t, floorY);
            ArcShelf(b, t, "Archive_ShelfNE", 35f, 59.5f, floorY);
            ArcShelf(b, t, "Archive_ShelfNW", 120.5f, 145f, floorY);

            report.AppendLine($"  archivo: {b.TriangleCount} triangulos estaticos, piso a {floorY:F2} m (desnivel {groundMax - groundMin:F2} m).");
            Finish(t, b, "Struct_Archive");

            // Libros reales de los nichos.
            StandBook(t, "variants", ArcPolar(ArcLecternRadius, ArcVariantsAngle), ArcPolar(ArcReaderRadius, ArcVariantsAngle), floorY);
            StandBook(t, "motion", ArcPolar(ArcLecternRadius, ArcMotionAngle), ArcPolar(ArcReaderRadius, ArcMotionAngle), floorY);
            StandBook(t, "shape", ArcPolar(ArcLecternRadius, ArcShapeAngle), ArcPolar(ArcReaderRadius, ArcShapeAngle), floorY);
            StandBook(t, "rare", ArcPolar(6.45f, ArcRareAngle), ArcPolar(5.5f, ArcRareAngle), floorY + ArcDaisHeight);

            // Mesa de alquimia (decorativa; sus alambiques y frascos estan en la malla estatica).
            Quaternion rareRot = t.rotation * ArcFacing(ArcRareAngle);
            TowerBuilder.Table(t, lit, "Archive_AlchemyTable", t.rotation * ArcAt(7.45f, ArcRareAngle, floorY + ArcDaisHeight), rareRot.eulerAngles.y, 1.6f, 0.6f);

            // Mesa central: tintero fijo y bandeja de papeles que se repone (como en el taller de tierra).
            float desk = floorY + 0.8f;
            FixedInkwell(t, "Inkwell_Archive", t.TransformPoint(new Vector3(-0.65f, desk, -0.3f)), yaw);
            var tray = Tray(t, "PaperTray_Archive", new Vector3(0.6f, desk, -0.3f));
            Supply(tray, PaperTemplate("PaperTemplate"), 0.08f, PaperCooldown, 2, "archive-paper");

            // Zona de movimiento: caja liviana y plumas en el cuenco.
            ArcMotionProps(t, floorY, yaw);

            ArcSigns(t);
        }

        // ---------------------------------------------------------------- geometria polar

        private static Vector2 ArcPolar(float r, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        private static Vector3 ArcAt(float r, float deg, float y)
        {
            Vector2 p = ArcPolar(r, deg);
            return new Vector3(p.x, y, p.y);
        }

        private static Vector3 ArcToCenter(float deg)
        {
            Vector2 p = ArcPolar(1f, deg);
            return new Vector3(-p.x, 0f, -p.y);
        }

        /// <summary>Giro con +Z hacia el centro y +X tangencial (sentido de los angulos crecientes).</summary>
        private static Quaternion ArcFacing(float deg) => Quaternion.LookRotation(ArcToCenter(deg));

        /// <summary>Marco local en (r, angulo, y) mirando al centro.</summary>
        private static Matrix4x4 ArcFrame(float r, float deg, float y) => Matrix4x4.TRS(ArcAt(r, deg, y), ArcFacing(deg), Vector3.one);

        private static void ArcBox(LowPolyMesh b, Matrix4x4 frame, Vector3 center, Vector3 size, Color color, Color? top = null) =>
            b.Box(frame * Matrix4x4.Translate(center), size, color, top);

        /// <summary>Caja en un marco girado, con su collider girado (hijo vacio).</summary>
        private static void ArcSolid(LowPolyMesh b, Transform t, string name, Matrix4x4 frame, Vector3 center, Vector3 size, Color color, Color? top = null)
        {
            ArcBox(b, frame, center, size, color, top);
            RotCol(t, name, frame.MultiplyPoint3x4(center), frame.rotation, size);
        }

        /// <summary>Altura minima y maxima del terreno (relativa a la raiz) en un disco de radio radius.</summary>
        private static void ArcGroundRange(Transform t, float radius, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            for (int ring = 0; ring <= 4; ring++)
            {
                float r = radius * ring / 4f;
                int n = ring == 0 ? 1 : 16;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = ArcPolar(r, i * 360f / n);
                    float h = Rel(t, p.x, p.y);
                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                }
            }
        }

        /// <summary>
        /// Quince columnas en orden creciente desde 12.5 (columna norte del vano de tierra, entre los caminos a tierra y a viento):
        /// 12.5, 39, 69.85 | vano norte | 94.15, 119.3, 144.4, 169.5 | agua | 190.5, 214.5 | fuego | 235.5, 257.9, 280.3, 302.7,
        /// 325.1, 347.5 | tierra. Tramos de 21 a 31 grados.
        /// </summary>
        private static float[] ArcColumnAngles()
        {
            float northEast = ArcNorthAngle - ArcNorthVanoHalf, northWest = ArcNorthAngle + ArcNorthVanoHalf;
            float waterNorth = ArcWaterAngle - ArcVanoHalf;
            float southStart = ArcFireAngle + ArcVanoHalf, southEnd = ArcEarthAngle - ArcEarthVanoHalf;
            var a = new float[15];
            a[0] = ArcEarthAngle + ArcEarthVanoHalf - 360f;
            a[1] = ArcWindColumnAngle;
            a[2] = northEast;
            a[3] = northWest;
            a[4] = Mathf.Lerp(northWest, waterNorth, 1f / 3f);
            a[5] = Mathf.Lerp(northWest, waterNorth, 2f / 3f);
            a[6] = waterNorth;
            a[7] = ArcWaterAngle + ArcVanoHalf;
            a[8] = ArcFireAngle - ArcVanoHalf;
            a[9] = southStart;
            for (int k = 1; k <= 4; k++) a[9 + k] = Mathf.Lerp(southStart, southEnd, k / 5f);
            a[14] = southEnd;
            return a;
        }

        /// <summary>Tramo con farol: no los de estandarte (agua, fuego, tierra), ni el vano norte, ni el que queda sobre el portal (sur, 269 grados).</summary>
        private static bool ArcIsLanternBay(float mid)
        {
            float m = Mathf.Repeat(mid, 360f);
            bool Near(float angle) => Mathf.Abs(Mathf.DeltaAngle(m, angle)) < 3f;
            return !Near(ArcWaterAngle) && !Near(ArcFireAngle) && !Near(ArcEarthAngle) && !Near(ArcNorthAngle) && !Near(269.1f);
        }

        // ---------------------------------------------------------------- piso

        /// <summary>
        /// Abanico central y anillos de losas claras con tres anillos de mosaico runico (rombos a dos tonos), zocalo de piedra de r 9 a
        /// 9,5 al ras del piso con su cara exterior hasta debajo del terreno, y colliders en franjas (circulo rasterizado en 5 cajas).
        /// </summary>
        private static void ArcFloor(LowPolyMesh b, Transform t, float floorY, float groundMin)
        {
            float[] radii = { 2.9f, 3.25f, 5.5f, 5.85f, 8.15f, 8.45f, ArcFloorRadius, ArcCurbRadius };
            float step = 360f / ArcFloorSegments;
            var center = new Vector3(0f, floorY, 0f);
            for (int i = 0; i < ArcFloorSegments; i++)
            {
                float a0 = i * step, a1 = (i + 1) * step;
                b.Tri(center, ArcAt(radii[0], a1, floorY), ArcAt(radii[0], a0, floorY), LowPolyMesh.Jitter(ArcSlab, 0.04f, 900 + i));
                for (int k = 0; k + 1 < radii.Length; k++)
                {
                    Vector3 i0 = ArcAt(radii[k], a0, floorY), i1 = ArcAt(radii[k], a1, floorY);
                    Vector3 o0 = ArcAt(radii[k + 1], a0, floorY), o1 = ArcAt(radii[k + 1], a1, floorY);
                    bool rune = k == 0 || k == 2 || k == 4;
                    bool curb = k == radii.Length - 2;
                    if (rune)
                    {
                        Vector3 mid = ArcAt((radii[k] + radii[k + 1]) * 0.5f, (a0 + a1) * 0.5f, floorY);
                        bool even = (i + k) % 2 == 0;
                        Color dark = even ? ArcRuneBlue : ArcRuneGold, light = even ? ArcRuneGold : ArcRuneBlue;
                        b.Tri(i0, i1, mid, dark);
                        b.Tri(i1, o1, mid, light);
                        b.Tri(o1, o0, mid, dark);
                        b.Tri(o0, i0, mid, light);
                    }
                    else
                    {
                        Color slab = curb ? LowPolyMesh.Jitter(ArcCurb, 0.08f, 950 + i)
                            : LowPolyMesh.Jitter((i + k) % 2 == 0 ? ArcSlab : ArcSlabB, 0.05f, 1000 + i * 8 + k);
                        b.Quad(i0, i1, o1, o0, slab);
                    }
                }
                // Cara exterior del zocalo.
                Vector3 low0 = ArcAt(ArcCurbRadius, a0, groundMin - 0.15f), low1 = ArcAt(ArcCurbRadius, a1, groundMin - 0.15f);
                Vector3 high0 = ArcAt(ArcCurbRadius, a0, floorY), high1 = ArcAt(ArcCurbRadius, a1, floorY);
                b.Quad(low0, high0, high1, low1, LowPolyMesh.Jitter(StoneB, 0.08f, 980 + i));
            }

            float bottom = groundMin - 0.3f, height = floorY - bottom, cy = (floorY + bottom) * 0.5f;
            float r = ArcFloorRadius + 0.3f;
            float HalfWidth(float z) => Mathf.Sqrt(Mathf.Max(0f, r * r - z * z));
            Col(t, new Vector3(0f, cy, 0f), new Vector3(2f * HalfWidth(3.2f), height, 6.4f));
            foreach (float side in new[] { -1f, 1f })
            {
                Col(t, new Vector3(0f, cy, side * 4.7f), new Vector3(2f * HalfWidth(6.2f), height, 3.0f));
                Col(t, new Vector3(0f, cy, side * 7.3f), new Vector3(2f * HalfWidth(8.4f), height, 2.2f));
            }
        }

        // ---------------------------------------------------------------- columnas y pergola

        /// <summary>
        /// Plinto de piedra, fuste de madera, capitel de piedra y menzula a lo largo de la viga (rot: +X = direccion de la viga; el
        /// plinto y el capitel tambien giran con rot, asi su cara interior queda en r 8.45 y no invade los estantes).
        /// </summary>
        private static void ArcColumn(LowPolyMesh b, Transform t, Vector2 at, float baseY, float top, Quaternion rot)
        {
            float g = Mathf.Min(Rel(t, at.x, at.y), baseY) - 0.15f;
            float plinthTop = baseY + 0.25f, capitalBottom = top - 0.37f;
            b.Box(Matrix4x4.TRS(new Vector3(at.x, (g + plinthTop) * 0.5f, at.y), rot, Vector3.one), new Vector3(0.7f, plinthTop - g, 0.7f), StoneB, StoneA);
            b.Frustum(Matrix4x4.Translate(new Vector3(at.x, plinthTop, at.y)), ArcShaftRadius, ArcShaftRadius * 0.88f, 0f, capitalBottom - plinthTop, 6, WoodA, false);
            b.Box(Matrix4x4.TRS(new Vector3(at.x, capitalBottom + 0.125f, at.y), rot, Vector3.one), new Vector3(0.62f, 0.25f, 0.62f), StoneA);
            b.Box(Matrix4x4.TRS(new Vector3(at.x, top - 0.06f, at.y), rot, Vector3.one), new Vector3(1.0f, 0.12f, 0.36f), StoneB, StoneA);
            Col(t, new Vector3(at.x, (g + top) * 0.5f, at.y), new Vector3(0.55f, top - g, 0.55f));
        }

        /// <summary>Viga en arco (de a0 a a1 grados, crecientes) entre los radios r0 y r1, con tapas en los extremos si no cierra el anillo.</summary>
        private static void ArcRingBeam(LowPolyMesh b, float r0, float r1, float y0, float y1, float a0, float a1, int segments, Color color)
        {
            bool closed = a1 - a0 >= 359.9f;
            for (int i = 0; i < segments; i++)
            {
                float s0 = Mathf.Lerp(a0, a1, i / (float)segments), s1 = Mathf.Lerp(a0, a1, (i + 1) / (float)segments);
                Vector3 i0b = ArcAt(r0, s0, y0), i1b = ArcAt(r0, s1, y0), o0b = ArcAt(r1, s0, y0), o1b = ArcAt(r1, s1, y0);
                Vector3 i0t = ArcAt(r0, s0, y1), i1t = ArcAt(r0, s1, y1), o0t = ArcAt(r1, s0, y1), o1t = ArcAt(r1, s1, y1);
                Color c = LowPolyMesh.Jitter(color, 0.06f, 700 + i);
                b.Quad(i0t, i1t, o1t, o0t, c);         // arriba
                b.Quad(o0b, o1b, i1b, i0b, c * 0.9f);  // abajo
                b.Quad(o0b, o0t, o1t, o1b, c);         // afuera
                b.Quad(i1b, i1t, i0t, i0b, c);         // adentro
                if (!closed && i == 0) b.Quad(i0b, i0t, o0t, o0b, c);
                if (!closed && i == segments - 1) b.Quad(o1b, o1t, i1t, i1b, c);
            }
        }

        /// <summary>Cabriada radial corta (r 7.7 a 9.6) apoyada sobre la viga anular.</summary>
        private static void ArcRafter(LowPolyMesh b, float angle, float y0) =>
            b.Box(Matrix4x4.TRS(ArcAt(8.65f, angle, y0 + ArcRafterHeight * 0.5f), ArcFacing(angle), Vector3.one), new Vector3(0.1f, ArcRafterHeight, 1.9f), WoodB, WoodA);

        /// <summary>Farol de bronce colgante: cadena, sombrero conico y cuerpo calido cerrado abajo.</summary>
        private static void ArcLantern(LowPolyMesh b, Vector3 hook, float chain)
        {
            float bodyTop = hook.y - chain;
            B(b, new Vector3(hook.x, hook.y - chain * 0.5f, hook.z), new Vector3(0.02f, chain, 0.02f), Iron);
            b.Frustum(Matrix4x4.Translate(new Vector3(hook.x, bodyTop, hook.z)), 0.13f, 0f, 0f, 0.12f, 6, Brass);
            b.Frustum(Matrix4x4.Translate(new Vector3(hook.x, bodyTop - 0.3f, hook.z)), 0.08f, 0.1f, 0f, 0.3f, 6, ArcLanternGlow, false, true);
        }

        /// <summary>
        /// Estandarte en el marco m (+X = ancho de la tela, Z = normal): asta horizontal a barY con remates de bronce, cuerdas hasta
        /// cordTop (si es mas alto), tela con franja, emblema en rombo y punta de dos caras. La punta queda a ~1,37 m bajo el asta.
        /// </summary>
        private static void ArcBanner(LowPolyMesh b, Matrix4x4 m, float barY, float cordTop, Color cloth, Color emblem)
        {
            const float width = 1.3f, height = 1.05f, tip = 0.28f, bar = 1.55f;
            b.Box(m * Matrix4x4.Translate(new Vector3(0f, barY, 0f)), new Vector3(bar, 0.05f, 0.05f), WoodB);
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box(m * Matrix4x4.Translate(new Vector3(sx * (bar * 0.5f + 0.03f), barY, 0f)), new Vector3(0.07f, 0.07f, 0.07f), Brass);
                if (cordTop > barY + 0.01f)
                    b.Box(m * Matrix4x4.Translate(new Vector3(sx * 0.55f, (barY + cordTop) * 0.5f, 0f)), new Vector3(0.015f, cordTop - barY, 0.015f), Iron);
            }
            float clothTop = barY - 0.03f, clothBottom = clothTop - height;
            b.Box(m * Matrix4x4.Translate(new Vector3(0f, (clothTop + clothBottom) * 0.5f, 0f)), new Vector3(width, height, 0.025f), cloth);
            b.Box(m * Matrix4x4.Translate(new Vector3(0f, clothTop - 0.08f, 0f)), new Vector3(width + 0.01f, 0.08f, 0.03f), Brass);
            b.Box(m * Matrix4x4.TRS(new Vector3(0f, clothBottom + height * 0.45f, 0f), Quaternion.Euler(0f, 0f, 45f), Vector3.one), new Vector3(0.3f, 0.3f, 0.035f), emblem);
            float half = width * 0.5f;
            Vector3 left = new Vector3(-half, clothBottom, 0f), right = new Vector3(half, clothBottom, 0f), point = new Vector3(0f, clothBottom - tip, 0f);
            Vector3 back = new Vector3(0f, 0f, -0.0126f), front = new Vector3(0f, 0f, 0.0126f);
            b.Tri(m.MultiplyPoint3x4(left + back), m.MultiplyPoint3x4(right + back), m.MultiplyPoint3x4(point + back), cloth);           // cara -Z
            b.Tri(m.MultiplyPoint3x4(right + front), m.MultiplyPoint3x4(left + front), m.MultiplyPoint3x4(point + front), cloth * 0.9f); // cara +Z
        }

        /// <summary>Estandarte colgado bajo la viga, en el centro del vano (plano tangencial).</summary>
        private static void ArcVanoBanner(LowPolyMesh b, float angle, float top, Color cloth, Color emblem) =>
            ArcBanner(b, ArcFrame(ArcColumnRadius, angle, 0f), top - 0.15f, top, cloth, emblem);

        /// <summary>Mastil suelto del viento (afuera del piso, con collider hasta 2,5 m) y bandera colgando de un brazo sobre el camino.</summary>
        private static void ArcWindStandard(LowPolyMesh b, Transform t, Color cloth, Color emblem)
        {
            Vector2 p = ArcWindMast;
            float g = Rel(t, p.x, p.y);
            const float mastHeight = 4.3f, barY = 3.9f;
            b.Frustum(Matrix4x4.Translate(new Vector3(p.x, g - 0.1f, p.y)), 0.07f, 0.05f, 0f, mastHeight, 6, WoodA, false);
            b.Frustum(Matrix4x4.Translate(new Vector3(p.x, g - 0.1f + mastHeight, p.y)), 0.07f, 0f, 0f, 0.16f, 6, Brass);
            B(b, new Vector3(p.x, g + 0.08f, p.y), new Vector3(0.4f, 0.26f, 0.4f), StoneB, StoneA);
            Col(t, new Vector3(p.x, g + 1.25f, p.y), new Vector3(0.16f, 2.5f, 0.16f));
            Vector3 d = new Vector3(ArcWindFlagDir.x, 0f, ArcWindFlagDir.y).normalized;
            var m = Matrix4x4.TRS(new Vector3(p.x, g, p.y) + d * 0.7f, Quaternion.LookRotation(Vector3.Cross(d, Vector3.up)), Vector3.one);
            ArcBanner(b, m, barY, barY, cloth, emblem);
        }

        // ---------------------------------------------------------------- nichos

        /// <summary>Bipiramide facetada (prisma de cristal) con el ecuador en center.</summary>
        private static void ArcGem(LowPolyMesh b, Vector3 center, float radius, float height, int sides, Color color)
        {
            float h = height * 0.5f;
            Vector3 bottom = center + Vector3.down * h;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                b.Tri(bottom, p0, p1, LowPolyMesh.Jitter(color, 0.12f, 60 + i));
            }
            b.Frustum(Matrix4x4.Translate(center), radius, 0f, 0f, h, sides, color);
        }

        /// <summary>Variantes (luz, cristal): rincon en penumbra bajo una marquesina, pedestal con prisma y lampara de pie apagada (decorativa).</summary>
        private static void ArcVariantsZone(LowPolyMesh b, Transform t, float floorY)
        {
            var z = ArcFrame(6.6f, ArcVariantsAngle, floorY);
            ArcBox(b, z, new Vector3(0f, 0.008f, 0f), new Vector3(1.9f, 0.012f, 1.9f), ArcDusk);

            ArcSolid(b, t, "Archive_VariantsPedestal", z, new Vector3(-0.45f, 0.47f, 0.1f), new Vector3(0.36f, 0.94f, 0.36f), StoneB, StoneA);
            ArcBox(b, z, new Vector3(-0.45f, 0.97f, 0.1f), new Vector3(0.48f, 0.06f, 0.48f), StoneA);
            ArcGem(b, z.MultiplyPoint3x4(new Vector3(-0.45f, 1.17f, 0.1f)), 0.1f, 0.34f, 6, Crystal);

            // Lampara de pie apagada: no hay un mecanismo simple de "reaccionar a la luz", queda decorativa.
            Matrix4x4 lamp = z * Matrix4x4.Translate(new Vector3(0.5f, 0f, 0.15f));
            b.Frustum(lamp, 0.16f, 0.12f, 0f, 0.05f, 6, Iron);
            b.Box(lamp * Matrix4x4.Translate(new Vector3(0f, 0.73f, 0f)), new Vector3(0.04f, 1.36f, 0.04f), Iron);
            b.Frustum(lamp * Matrix4x4.Translate(new Vector3(0f, 1.3f, 0f)), 0.24f, 0.13f, 0f, 0.26f, 8, ArcLampShade, false);
            b.Frustum(lamp * Matrix4x4.Translate(new Vector3(0f, 1.3f, 0f)), 0.125f, 0.235f, 0.26f, 0f, 8, ArcLampShade * 0.7f, false);
            b.Box(lamp * Matrix4x4.Translate(new Vector3(0f, 1.36f, 0f)), new Vector3(0.08f, 0.1f, 0.08f), ArcLampOff);
            Col(t, lamp.MultiplyPoint3x4(new Vector3(0f, 0.8f, 0f)), new Vector3(0.3f, 1.6f, 0.3f));

            // Marquesina: cuatro postes y un toldo plano a 2,5 m (da la sombra del rincon).
            foreach (var (px, pz) in new[] { (-0.95f, -0.95f), (0.95f, -0.95f), (-0.95f, 0.85f), (0.95f, 0.85f) })
            {
                ArcBox(b, z, new Vector3(px, 1.225f, pz), new Vector3(0.09f, 2.45f, 0.09f), WoodB);
                Col(t, z.MultiplyPoint3x4(new Vector3(px, 1.225f, pz)), new Vector3(0.12f, 2.45f, 0.12f));
            }
            ArcBox(b, z, new Vector3(0f, 2.475f, -0.05f), new Vector3(2.15f, 0.05f, 2.05f), Shutter, ArcCanopyTop);
        }

        /// <summary>Movimiento: alfombra (con collider, las plumas se apoyan encima), canasta-blanco sobre un poste y cuenco abierto para las plumas.</summary>
        private static void ArcMotionZone(LowPolyMesh b, Transform t, float floorY)
        {
            var z = ArcFrame(6.8f, ArcMotionAngle, floorY);
            ArcSolid(b, t, "Archive_MotionRug", z, new Vector3(0f, 0.008f, 0f), new Vector3(1.75f, 0.012f, 1.75f), ArcSage);

            const float basketZ = -0.5f, rim = 0.9f;
            ArcSolid(b, t, "Archive_BasketPost", z, new Vector3(0f, rim * 0.5f, basketZ), new Vector3(0.08f, rim, 0.08f), WoodB);
            Matrix4x4 basket = z * Matrix4x4.Translate(new Vector3(0f, rim, basketZ));
            b.Frustum(basket, 0.19f, 0.27f, 0f, 0.26f, 8, ArcWicker, false);
            b.Frustum(basket, 0.26f, 0.18f, 0.26f, 0f, 8, ArcWicker * 0.7f, false);
            ArcBox(b, z, new Vector3(0f, rim + 0.012f, basketZ), new Vector3(0.3f, 0.02f, 0.3f), ArcWicker * 0.6f);
            RotCol(t, "Archive_BasketCol_0", z.MultiplyPoint3x4(new Vector3(0f, rim + 0.01f, basketZ)), z.rotation, new Vector3(0.36f, 0.03f, 0.36f));
            for (int k = 0; k < 4; k++)
            {
                bool alongX = k < 2;
                float s = (k & 1) == 0 ? -0.24f : 0.24f;
                var c = new Vector3(alongX ? 0f : s, rim + 0.14f, basketZ + (alongX ? s : 0f));
                RotCol(t, $"Archive_BasketCol_{k + 1}", z.MultiplyPoint3x4(c), z.rotation, alongX ? new Vector3(0.5f, 0.26f, 0.04f) : new Vector3(0.04f, 0.26f, 0.5f));
            }

            Matrix4x4 bowl = z * Matrix4x4.Translate(ArcBowlLocal);
            b.Frustum(bowl, 0.26f, 0.32f, 0f, 0.12f, 8, ArcClay, false);
            b.Frustum(bowl, 0.31f, 0.25f, 0.12f, 0f, 8, ArcClay * 0.75f, false);
        }

        /// <summary>Posicion del cuenco de plumas en el marco de la zona de movimiento.</summary>
        private static readonly Vector3 ArcBowlLocal = new Vector3(0.45f, 0f, 0.35f);

        /// <summary>Caja liviana y tres plumas (mismo recurso que las del campo de viento: WfLightProp + Flutter) apoyadas en la alfombra.</summary>
        private static void ArcMotionProps(Transform t, float floorY, float yaw)
        {
            var z = ArcFrame(6.8f, ArcMotionAngle, floorY);
            float zoneYaw = yaw + z.rotation.eulerAngles.y;
            MovablesSetup.Prop(t, lit, "Archive_Crate", MovablesSetup.Kind.SmallBox, t.TransformPoint(z.MultiplyPoint3x4(new Vector3(-0.45f, 0.02f, 0.35f))), zoneYaw + 12f, false);
            for (int i = 0; i < 3; i++)
            {
                Vector3 local = z.MultiplyPoint3x4(ArcBowlLocal + new Vector3(0f, 0.018f, -0.09f + i * 0.09f));
                WfFlutter(WfLightProp(t, $"Archive_Feather_{i}", WfFeatherMesh(i), t.TransformPoint(local), zoneYaw + (i - 1) * 6f,
                    new Vector3(0.24f, 0.012f, 0.05f), new Vector3(0f, 0.006f, 0f), 0.004f, "pluma", 0.25f));
            }
        }

        /// <summary>Forma: bandeja baja de 1,8 x 1,8 m con arena (rayable con columna, region, aplastar) y seis estacas de distintas alturas.</summary>
        private static void ArcShapeZone(LowPolyMesh b, Transform t, float floorY)
        {
            var z = ArcFrame(6.6f, ArcShapeAngle, floorY);
            const float half = 0.9f, rimH = 0.14f, sandTop = 0.1f;
            ArcSolid(b, t, "Archive_SandBed", z, new Vector3(0f, sandTop * 0.5f, 0f), new Vector3(2f * half - 0.2f, sandTop, 2f * half - 0.2f), ArcSand * 0.9f, ArcSand);
            for (int k = 0; k < 4; k++)
            {
                bool alongX = k < 2;
                float s = (k & 1) == 0 ? -(half - 0.05f) : half - 0.05f;
                var c = new Vector3(alongX ? 0f : s, rimH * 0.5f, alongX ? s : 0f);
                var size = alongX ? new Vector3(2f * half, rimH, 0.1f) : new Vector3(0.1f, rimH, 2f * half - 0.2f);
                ArcSolid(b, t, $"Archive_SandRim_{k}", z, c, size, WoodA, Plank);
            }
            float[] heights = { 0.25f, 0.45f, 0.65f, 0.35f, 0.55f, 0.3f };
            Vector2[] spots =
            {
                new Vector2(-0.5f, -0.4f), new Vector2(0f, -0.45f), new Vector2(0.5f, -0.35f),
                new Vector2(-0.45f, 0.3f), new Vector2(0.05f, 0.35f), new Vector2(0.5f, 0.4f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                float h = heights[i] + 0.05f; // 5 cm enterrada en la arena
                var c = new Vector3(spots[i].x, sandTop - 0.05f + h * 0.5f, spots[i].y);
                ArcSolid(b, t, $"Archive_Stake_{i}", z, c, new Vector3(0.07f, h, 0.07f), WoodB, Plank);
            }
        }

        /// <summary>Signos raros: tarima de 15 cm (2,4 x 1,8 m) con el atril adelante y la mesa de alquimia atras (alambique y frascos).</summary>
        private static void ArcRareNook(LowPolyMesh b, Transform t, float floorY, float groundMin)
        {
            var d = ArcFrame(7.0f, ArcRareAngle, floorY);
            float bottom = groundMin - floorY - 0.05f;
            var size = new Vector3(2.4f, ArcDaisHeight - bottom, 1.8f);
            ArcSolid(b, t, "Archive_RareDais", d, new Vector3(0f, (ArcDaisHeight + bottom) * 0.5f, 0f), size, StoneB, Plank);
            ArcBox(b, d, new Vector3(0f, ArcDaisHeight - 0.02f, 0.91f), new Vector3(2.42f, 0.04f, 0.03f), WoodB);
            ArcAlchemy(b, ArcFrame(7.45f, ArcRareAngle, floorY + ArcDaisHeight + 0.82f));
        }

        /// <summary>Alambique (caldera, cuello, capitel, cuello de cisne y recipiente) y cuatro frascos sobre la mesa (marco desk: y = 0 en la cubierta).</summary>
        private static void ArcAlchemy(LowPolyMesh b, Matrix4x4 desk)
        {
            Matrix4x4 still = desk * Matrix4x4.Translate(new Vector3(-0.6f, 0f, -0.05f));
            b.Frustum(still, 0.07f, 0.14f, 0f, 0.12f, 8, ArcPotionGreen, false);
            b.Frustum(still, 0.14f, 0.06f, 0.12f, 0.24f, 8, ArcGlass);
            b.Frustum(still, 0.035f, 0.03f, 0.24f, 0.36f, 6, ArcGlass);
            b.Frustum(still, 0.07f, 0f, 0.36f, 0.44f, 6, Brass);
            Beam(b, desk.MultiplyPoint3x4(new Vector3(-0.56f, 0.4f, -0.05f)), desk.MultiplyPoint3x4(new Vector3(-0.26f, 0.15f, -0.05f)), 0.025f, Brass);
            Matrix4x4 receiver = desk * Matrix4x4.Translate(new Vector3(-0.24f, 0f, -0.05f));
            b.Frustum(receiver, 0.05f, 0.08f, 0f, 0.08f, 8, ArcPotionViolet, false);
            b.Frustum(receiver, 0.08f, 0.025f, 0.08f, 0.16f, 8, ArcGlass);

            Color[] potions = { ArcPotionAmber, ArcPotionGreen, ArcPotionViolet, ArcPotionRed };
            for (int i = 0; i < potions.Length; i++)
            {
                Matrix4x4 f = desk * Matrix4x4.Translate(new Vector3(0.08f + i * 0.17f, 0f, (i & 1) == 0 ? 0.1f : -0.12f));
                b.Frustum(f, 0.045f, 0.05f, 0f, 0.1f, 6, potions[i], false);
                b.Frustum(f, 0.05f, 0.018f, 0.1f, 0.16f, 6, ArcGlass, false);
                b.Frustum(f, 0.018f, 0.018f, 0.16f, 0.21f, 6, WoodB);
            }
        }

        // ---------------------------------------------------------------- centro y estantes

        /// <summary>Mesa redonda de nogal (r 1,25, collider cuadrado inscripto) y atril VACIO al norte, de cara a quien entra.</summary>
        private static void ArcCenter(LowPolyMesh b, Transform t, float floorY)
        {
            const float radius = 1.25f, height = 0.8f;
            Matrix4x4 at = Matrix4x4.Translate(new Vector3(0f, floorY, 0f));
            b.Frustum(at, radius, radius, height - 0.06f, height, 12, ArcWalnut, true, true);
            b.Frustum(at, radius - 0.12f, radius - 0.12f, height - 0.14f, height - 0.06f, 12, WoodB, false);
            b.Frustum(at, 0.22f, 0.16f, 0.08f, height - 0.14f, 8, ArcWalnut, false);
            b.Box(at * Matrix4x4.Translate(new Vector3(0f, 0.04f, 0f)), new Vector3(1.3f, 0.08f, 0.16f), WoodB);
            b.Box(at * Matrix4x4.Translate(new Vector3(0f, 0.04f, 0f)), new Vector3(0.16f, 0.08f, 1.3f), WoodB);
            Col(t, new Vector3(0f, floorY + height * 0.5f, 0f), new Vector3(1.76f, height, 1.76f));

            // Atril vacio: «Primeros hechizos» todavia no existe en glifos.json.
            // A 1,25 m del borde de la mesa (pasillo libre entre ambos).
            // TODO: StandBook(t, "primeros-hechizos", new Vector2(0f, 2.75f), new Vector2(0f, 3.55f), floorY); y sacar este atril decorativo
            // (StandBook arma su propio atril con collider).
            var lectern = new Vector2(0f, 2.75f);
            Vector2 v = (new Vector2(0f, 3.55f) - lectern).normalized;
            float lecternYaw = Mathf.Atan2(-v.x, -v.y) * Mathf.Rad2Deg;
            Matrix4x4 m = Matrix4x4.TRS(new Vector3(lectern.x, floorY, lectern.y), Quaternion.Euler(0f, lecternYaw, 0f), Vector3.one);
            b.Frustum(m, 0.25f, 0.12f, 0f, 0.1f, 6, WoodB);
            b.Box(m * Matrix4x4.Translate(new Vector3(0f, 0.6f, 0f)), new Vector3(0.1f, 1.1f, 0.1f), WoodB);
            b.Box(m * Matrix4x4.TRS(new Vector3(0f, 1.15f, 0f), Quaternion.Euler(-25f, 0f, 0f), Vector3.one), new Vector3(0.6f, 0.04f, 0.45f), WoodA, Plank);
            Col(t, new Vector3(lectern.x, floorY + 0.6f, lectern.y), new Vector3(0.5f, 1.2f, 0.5f));
        }

        /// <summary>Estante curvo bajo de roble (3 tramos de cuerda entre a0 y a1 grados, r = ArcShelfRadius) con dos filas de ranuras vacias.</summary>
        private static void ArcShelf(LowPolyMesh b, Transform t, string name, float a0, float a1, float floorY)
        {
            const int segments = 3;
            const float depth = 0.4f, height = 1.0f;
            for (int i = 0; i < segments; i++)
            {
                float s0 = Mathf.Lerp(a0, a1, i / (float)segments), s1 = Mathf.Lerp(a0, a1, (i + 1) / (float)segments);
                float len = 2f * ArcShelfRadius * Mathf.Sin((s1 - s0) * 0.5f * Mathf.Deg2Rad);
                // 2 mm de diferencia por tramo: las tapas vecinas se pisan en las juntas y no deben quedar coplanares.
                var f = ArcFrame(ArcShelfRadius, (s0 + s1) * 0.5f, floorY + i * 0.002f);
                ArcSolid(b, t, $"{name}_Col_{i}", f, new Vector3(0f, height * 0.5f, 0f), new Vector3(len, height, depth), ArcOak);
                // TODO: lomos por libro leído (DiscoveryState). Por ahora las ranuras quedan vacias.
                foreach (float slotY in new[] { 0.32f, 0.7f })
                    ArcBox(b, f, new Vector3(0f, slotY, depth * 0.5f + 0.006f), new Vector3(len - 0.14f, 0.26f, 0.012f), ArcSlot);
                ArcBox(b, f, new Vector3(0f, height + 0.02f, 0.02f), new Vector3(len, 0.04f, depth + 0.06f), WoodB, Plank);
            }
            foreach (float end in new[] { a0, a1 })
                ArcBox(b, ArcFrame(ArcShelfRadius, end, floorY), new Vector3(0f, (height + 0.06f) * 0.5f, 0.01f), new Vector3(0.05f, height + 0.06f, depth + 0.08f), WoodB);
        }

        // ---------------------------------------------------------------- carteles

        private static void ArcSigns(Transform t)
        {
            string Title(string text) => $"<color={ArcTitleColor}>{text}</color>";
            Vector3 Outer(float deg) => t.TransformPoint(ArcAt(ArcSignRadius, deg, 0f));
            Vector3 Inward(float deg) => t.TransformDirection(ArcToCenter(deg));

            // Afuera de la entrada, al oeste del camino, de cara a quien llega por el (rumbo de salida del camino: (0.51, 0.86)).
            CanvasDesigns.Sign(t, lit, "Sign_Archive_0", t.TransformPoint(new Vector3(ArcWelcomeSign.x, 0f, ArcWelcomeSign.y)), t.TransformDirection(new Vector3(0.8f, 0f, 0.6f).normalized),
                Title("Archivo"), "Libros, signos y práctica.\nElegí un arco para ir a un campo:\nagua (oeste), fuego (suroeste),\ntierra (este), viento (noreste).");
            CanvasDesigns.Sign(t, lit, "Sign_Archive_1", Outer(ArcVariantsAngle), Inward(ArcVariantsAngle),
                Title("Variantes y parientes"), "Leé el libro del atril y probá\nla <b>luz</b> y el <b>cristal</b> en el\nrincón en sombra: el prisma y\nla lámpara están para eso.");
            CanvasDesigns.Sign(t, lit, "Sign_Archive_2", Outer(ArcMotionAngle), Inward(ArcMotionAngle),
                Title("Signos del movimiento"), "<b>Dispersión</b>, <b>atracción</b>,\n<b>flotar</b> y <b>levitar</b>: mové la\ncaja y las plumas.\n¿Llegan a la canasta?");
            CanvasDesigns.Sign(t, lit, "Sign_Archive_3", Outer(ArcShapeAngle), Inward(ArcShapeAngle),
                Title("Dar forma a la magia"), "<b>Columna</b>, <b>convergencia</b>,\n<b>región</b> y <b>aplastar</b>:\nprobalos sobre la arena\ny las estacas.");
            CanvasDesigns.Sign(t, lit, "Sign_Archive_4", t.TransformPoint(ArcAt(8.2f, ArcRareAngle, 0f)), Inward(ArcRareAngle),
                Title("Signos raros"), "Diez signos poco comunes.\nLeelos con calma: varios\nse entienden mejor\ncombinados.");
        }
    }
}
