using UnityEditor;
using UnityEngine;
using SpellSimulator.Reactions;
using SpellSimulator.World;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    public static partial class MapStructures
    {
        // ---------------------------------------------------------------- campo de viento

        // Ejes locales del campo: +Z = frente (hacia donde se dispara), +X = derecha. El jugador dibuja parado en el centro.
        // El camino de acceso llega por atras a la izquierda (≈ (-7, -3.9) local con el frente actual hacia FieldCenter): ese
        // corredor (~2.5 m) queda libre; todo lo demas va adelante o a los costados.
        private const float WfVaneHeight = 2.3f;     // pivote de la flecha de la veleta sobre el piso
        private const float WfSockHeight = 3.3f;     // pivote de la manga sobre el piso
        private const float WfSockDroop = 50f;       // caida inicial de la manga (grados bajo la horizontal)
        private const float WfSockRadius = 0.16f, WfSockTaper = 0.82f, WfSockSegment = 0.2f;
        private const int WfSockSegments = 4;
        private const float WfRingRadius = 0.9f, WfRingTube = 0.12f, WfRingCenter = 1.6f, WfRingZ = 9f;
        private const int WfRingSegments = 12;
        private const string WfPaperProfilePath = "Assets/_Project/Data/Materials/Papel.asset";
        private static readonly Color WfRope = new Color(0.75f, 0.65f, 0.45f);
        private static readonly Color WfSky = new Color(0.3f, 0.52f, 0.7f);

        /// <summary>
        /// Campo de viento (llano abierto, Docs/MAPA_FLUJO_PLAN.md §12b punto 8): tres veletas (flecha con bisagra vertical),
        /// dos mangas de viento (articuladas: giran y se levantan con la rafaga), una soga con ropa tendida (telas con bisagra),
        /// una mesa baja con hojas y plumas (y hojas en el pasto), un aro-diana a 9 m con cajas livianas para atravesarlo,
        /// una mesa con papeles y tintero fijo, un atril vacio (el libro de viento llega despues) y tres carteles.
        /// Una sola malla estatica (Struct_WindField); lo que se mueve son cuerpos aparte, empujados por MagicField (WindDrag).
        /// </summary>
        private static void WindField(Transform parent, Site site)
        {
            var t = StructRoot(parent, "WindField", site);
            float yaw = t.eulerAngles.y;
            var b = new LowPolyMesh();

            // Veletas a 3, 5.5 y 7.5 m del centro, alternando lados (la linea al aro, x = 0, queda libre).
            var vaneMesh = MapBuilder.SaveMesh(WfVaneMesh().Build("WindField_VaneArrow"));
            WfVane(b, t, vaneMesh, "WindField_Vane_1", new Vector2(1.6f, 3.0f), 90f);
            WfVane(b, t, vaneMesh, "WindField_Vane_2", new Vector2(-1.8f, 5.5f), 200f);
            WfVane(b, t, vaneMesh, "WindField_Vane_3", new Vector2(1.9f, 7.5f), 300f);

            // Mangas de viento en postes altos, a los costados del fondo.
            var swivelMesh = MapBuilder.SaveMesh(WfSwivelMesh().Build("WindField_SockSwivel"));
            var sockMesh = MapBuilder.SaveMesh(WfSockMesh().Build("WindField_Sock"));
            WfWindSock(b, t, swivelMesh, sockMesh, "WindField_Sock_1", new Vector2(-4.4f, 6.2f), 130f);
            WfWindSock(b, t, swivelMesh, sockMesh, "WindField_Sock_2", new Vector2(4.8f, 6.6f), 240f);

            // Ropa tendida a la izquierda (soga a lo ancho: el viento hacia adelante la levanta).
            WfLaundry(b, t, -6.6f, -2.2f, 2.6f);

            // Aro-diana a 9 m adelante.
            WfRingTarget(b, t);

            // Mesa baja con hojas y plumas (adelante a la derecha) y hojas sueltas en el pasto delante de ella.
            Vector2 low = new Vector2(3.0f, 1.2f);
            HeightRange(t, low.x - 0.6f, low.x + 0.6f, low.y - 0.3f, low.y + 0.3f, out float lowMin, out _);
            B(b, new Vector3(low.x, lowMin + 0.47f, low.y), new Vector3(1.2f, 0.06f, 0.6f), Plank);
            for (int i = 0; i < 4; i++)
            {
                float lx = low.x + ((i & 1) == 0 ? -0.52f : 0.52f), lz = low.y + (i < 2 ? -0.24f : 0.24f);
                B(b, new Vector3(lx, lowMin + 0.2f, lz), new Vector3(0.06f, 0.5f, 0.06f), WoodB);
            }
            Col(t, new Vector3(low.x, lowMin + 0.25f, low.y), new Vector3(1.2f, 0.5f, 0.6f));

            Finish(t, b, "Struct_WindField");

            WfLightObjects(t, low, lowMin + 0.5f, yaw);

            // Cajas livianas: blancos de empuje (la chica pasa por el aro).
            MovablesSetup.Prop(t, lit, "WindField_Crate_A", MovablesSetup.Kind.SmallBox, t.TransformPoint(new Vector3(0.4f, 0f, 2.4f)), yaw + 10f, true);
            MovablesSetup.Prop(t, lit, "WindField_Crate_B", MovablesSetup.Kind.SmallBox, t.TransformPoint(new Vector3(-0.6f, 0f, 4.2f)), yaw - 14f, true);
            MovablesSetup.Prop(t, lit, "WindField_Crate_C", MovablesSetup.Kind.SmallBox, t.TransformPoint(new Vector3(3.6f, 0f, 5.2f)), yaw + 25f, true);

            // Mesa con papeles y tintero fijo (atras a la derecha, fuera del corredor de entrada).
            HeightRange(t, 2.5f, 3.9f, -2.55f, -1.85f, out float tableMin, out _);
            TowerBuilder.Table(t, lit, "WindFieldTable", t.rotation * new Vector3(3.2f, tableMin, -2.2f), yaw, 1.4f, 0.7f);
            float desk = tableMin + 0.82f;
            FixedInkwell(t, "Inkwell_WindField", t.TransformPoint(new Vector3(2.8f, desk, -2.25f)), yaw);
            var tray = Tray(t, "PaperTray_WindField", new Vector3(3.5f, desk, -2.2f));
            Supply(tray, PaperTemplate("PaperTemplate"), 0.08f, PaperCooldown, 2, "windfield-paper");

            // Libro de hechizos de viento (paso 7).
            StandBook(t, "spells-wind", new Vector2(1.0f, -3.6f), new Vector2(0.8f, -2.8f));

            // Carteles (titulo en celeste).
            Vector3 Dir(float x, float z) => t.TransformDirection(new Vector3(x, 0f, z).normalized);
            CanvasDesigns.Sign(t, lit, "Sign_WindField_1", t.TransformPoint(new Vector3(-4.2f, 0f, -4.6f)), Dir(-1.3f, 1.8f),
                WfTitle("Campo de viento"), "Dibujá el círculo con el signo\nde <b>Viento</b>: sopla hacia\nadelante. Mirá cómo giran las\nveletas y se inflan las mangas.");
            CanvasDesigns.Sign(t, lit, "Sign_WindField_2", t.TransformPoint(new Vector3(-4.4f, 0f, 0.6f)), Dir(1f, -0.1f),
                WfTitle("Formas del viento"), "Viento + <b>columna</b>: ráfaga dirigida.\nViento + <b>dispersión</b>: abanico.\nViento + <b>levitación</b>: sostiene\nlos objetos en el aire.");
            CanvasDesigns.Sign(t, lit, "Sign_WindField_3", t.TransformPoint(new Vector3(4.4f, 0f, 0f)), Dir(-1f, -0.1f),
                WfTitle("Para probar"), "Empujá las hojas y las plumas,\nhacé pasar una caja por el\n<b>aro</b> y agitá la ropa tendida.");
        }

        private static string WfTitle(string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(WfSky)}>{text}</color>";

        // ---------------------------------------------------------------- veletas

        /// <summary>Flecha de veleta: pivote en el eje del poste; punta hacia +Z, cola (aleta) hacia -Z.</summary>
        private static LowPolyMesh WfVaneMesh()
        {
            var v = new LowPolyMesh();
            v.Frustum(Matrix4x4.Translate(new Vector3(0f, -0.04f, 0f)), 0.035f, 0.035f, 0f, 0.08f, 6, Brass);
            v.Box(Matrix4x4.Translate(new Vector3(0f, 0.02f, -0.03f)), new Vector3(0.025f, 0.025f, 0.72f), Iron);
            Vector3 up = new Vector3(0f, 0.11f, 0.3f), tip = new Vector3(0f, 0.02f, 0.5f), down = new Vector3(0f, -0.07f, 0.3f);
            v.Tri(up, tip, down, Brass);
            v.Tri(down, tip, up, Brass * 0.85f);
            v.Box(Matrix4x4.Translate(new Vector3(0f, 0.07f, -0.33f)), new Vector3(0.012f, 0.22f, 0.2f), Cloth);
            return v;
        }

        /// <summary>
        /// Veleta: poste y rosa de los vientos en la malla estatica; la flecha es un cuerpo liviano con bisagra vertical anclada al
        /// mundo. El collider va del lado de la cola: el centro de masa queda detras del eje y el arrastre del viento la orienta.
        /// </summary>
        private static void WfVane(LowPolyMesh b, Transform t, Mesh arrow, string name, Vector2 at, float startYaw)
        {
            float g = Rel(t, at.x, at.y);
            b.Frustum(Matrix4x4.Translate(new Vector3(at.x, g, at.y)), 0.045f, 0.035f, -0.1f, WfVaneHeight - 0.04f, 6, WoodB, false);
            B(b, new Vector3(at.x, g + 0.06f, at.y), new Vector3(0.3f, 0.16f, 0.3f), StoneB, StoneA);
            // Rosa de los vientos (cruz fija bajo la flecha) y su remate.
            B(b, new Vector3(at.x, g + WfVaneHeight - 0.3f, at.y), new Vector3(0.5f, 0.025f, 0.025f), Iron);
            B(b, new Vector3(at.x, g + WfVaneHeight - 0.3f, at.y), new Vector3(0.025f, 0.025f, 0.5f), Iron);
            Col(t, new Vector3(at.x, g + 1.05f, at.y), new Vector3(0.12f, 2.1f, 0.12f)); // hasta 2.1 m: no toca la flecha

            var go = MapBuilder.Place(name, t, arrow, lit, t.TransformPoint(new Vector3(at.x, g + WfVaneHeight, at.y)), t.rotation * Quaternion.Euler(0f, startYaw, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.06f, -0.27f);
            col.size = new Vector3(0.04f, 0.24f, 0.28f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.35f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var hinge = go.AddComponent<HingeJoint>();
            hinge.anchor = Vector3.zero;
            hinge.axis = Vector3.up;
        }

        // ---------------------------------------------------------------- mangas de viento

        /// <summary>Giratorio de la manga: collar en el eje, varilla y aro de laton (boca de la manga en z = 0.16).</summary>
        private static LowPolyMesh WfSwivelMesh()
        {
            var s = new LowPolyMesh();
            s.Frustum(Matrix4x4.Translate(new Vector3(0f, -0.05f, 0f)), 0.05f, 0.05f, 0f, 0.1f, 6, Iron);
            s.Box(Matrix4x4.Translate(new Vector3(0f, 0f, 0.08f)), new Vector3(0.02f, 0.02f, 0.16f), Iron);
            s.Frustum(Matrix4x4.TRS(new Vector3(0f, 0f, 0.16f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), WfSockRadius + 0.012f, WfSockRadius + 0.012f, -0.03f, 0.03f, 8, Brass, false, false);
            return s;
        }

        /// <summary>Manga: cuatro tramos conicos a rayas (cara de afuera y de adentro) a lo largo de +Z desde la boca.</summary>
        private static LowPolyMesh WfSockMesh()
        {
            var s = new LowPolyMesh();
            float r = WfSockRadius;
            for (int i = 0; i < WfSockSegments; i++)
            {
                var m = Matrix4x4.TRS(new Vector3(0f, 0f, i * WfSockSegment), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                Color c = i % 2 == 0 ? Cloth : ClothLight;
                s.Frustum(m, r, r * WfSockTaper, 0f, WfSockSegment, 8, c, false, false);
                s.Frustum(m, r * WfSockTaper * 0.97f, r * 0.97f, WfSockSegment, 0f, 8, c * 0.85f, false, false);
                r *= WfSockTaper;
            }
            return s;
        }

        /// <summary>
        /// Manga de viento: mastil alto en la malla estatica; un giratorio con bisagra vertical (anclado al mundo) y la manga
        /// colgada de el con bisagra horizontal. Quieta cuelga caida; el viento la levanta (se "infla") y la hace girar.
        /// El collider del mastil llega solo a 2.2 m: la manga colgando no lo toca.
        /// </summary>
        private static void WfWindSock(LowPolyMesh b, Transform t, Mesh swivelMesh, Mesh sockMesh, string name, Vector2 at, float startYaw)
        {
            float g = Rel(t, at.x, at.y);
            b.Frustum(Matrix4x4.Translate(new Vector3(at.x, g, at.y)), 0.07f, 0.045f, -0.1f, WfSockHeight - 0.05f, 6, WoodA, false);
            B(b, new Vector3(at.x, g + 0.1f, at.y), new Vector3(0.4f, 0.24f, 0.4f), StoneB, StoneA);
            Col(t, new Vector3(at.x, g + 1.1f, at.y), new Vector3(0.16f, 2.2f, 0.16f));

            Quaternion yawRot = t.rotation * Quaternion.Euler(0f, startYaw, 0f);
            Vector3 pivot = t.TransformPoint(new Vector3(at.x, g + WfSockHeight, at.y));
            var swivel = MapBuilder.Place(name, t, swivelMesh, lit, pivot, yawRot);
            var sc = swivel.AddComponent<BoxCollider>();
            sc.center = new Vector3(0f, 0f, 0.1f);
            sc.size = new Vector3(0.08f, 0.08f, 0.16f);
            var srb = swivel.AddComponent<Rigidbody>();
            srb.mass = 0.15f;
            srb.linearDamping = 0.1f;
            srb.angularDamping = 0.5f;
            srb.interpolation = RigidbodyInterpolation.Interpolate;
            var yawHinge = swivel.AddComponent<HingeJoint>();
            yawHinge.anchor = Vector3.zero;
            yawHinge.axis = Vector3.up;

            var sock = MapBuilder.Place(name + "_Tela", t, sockMesh, lit, pivot + yawRot * new Vector3(0f, 0f, 0.16f), yawRot * Quaternion.Euler(WfSockDroop, 0f, 0f));
            var col = sock.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0f, WfSockSegment * WfSockSegments * 0.5f);
            col.size = new Vector3(0.24f, 0.24f, WfSockSegment * WfSockSegments);
            var rb = sock.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var hinge = sock.AddComponent<HingeJoint>();
            hinge.connectedBody = srb;
            hinge.anchor = Vector3.zero;
            hinge.axis = Vector3.right;
            hinge.useLimits = true;
            // Limites casi simetricos respecto de la caida inicial (50°): entre ~horizontal y ~vertical, sea cual sea el signo.
            hinge.limits = new JointLimits { min = -40f, max = 35f };
        }

        // ---------------------------------------------------------------- ropa tendida

        /// <summary>Dos postes con travesaño, soga con caida suave (a lo largo de X local) y cinco telas con bisagra colgadas.</summary>
        private static void WfLaundry(LowPolyMesh b, Transform t, float x0, float x1, float z)
        {
            float g0 = Rel(t, x0, z), g1 = Rel(t, x1, z);
            float top = Mathf.Max(g0, g1) + 2.0f;
            foreach (var (x, g) in new[] { (x0, g0), (x1, g1) })
            {
                float bottom = g - 0.15f, postTop = top + 0.1f;
                Solid(b, t, new Vector3(x, (bottom + postTop) * 0.5f, z), new Vector3(0.12f, postTop - bottom, 0.12f), WoodA);
                B(b, new Vector3(x, top + 0.04f, z), new Vector3(0.08f, 0.08f, 0.5f), WoodB);
            }
            const float sag = 0.12f;
            Vector3 L(float s) => new Vector3(Mathf.Lerp(x0, x1, s), top - sag * 4f * s * (1f - s), z);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p0 = L(i / 4f), p1 = L((i + 1) / 4f);
                b.Box(Matrix4x4.TRS((p0 + p1) * 0.5f, Quaternion.LookRotation(p1 - p0), Vector3.one), new Vector3(0.02f, 0.02f, (p1 - p0).magnitude + 0.01f), WfRope);
            }

            float[] widths = { 0.5f, 0.42f, 0.6f, 0.38f, 0.52f };
            float[] heights = { 0.62f, 0.5f, 0.45f, 0.7f, 0.55f };
            Color[] colors = { ClothLight, Shutter, Cloth, new Color(0.92f, 0.7f, 0.25f), Moss };
            for (int i = 0; i < widths.Length; i++)
            {
                float w = widths[i], h = heights[i];
                var c = new LowPolyMesh();
                c.Box(Matrix4x4.Translate(new Vector3(0f, -h * 0.5f - 0.01f, 0f)), new Vector3(w, h, 0.012f), colors[i]);
                c.Box(Matrix4x4.Translate(new Vector3(0f, -h * 0.78f, 0f)), new Vector3(w + 0.002f, 0.05f, 0.014f), Color.Lerp(colors[i], Color.white, 0.35f));
                foreach (float px in new[] { -0.35f, 0.35f })
                    c.Box(Matrix4x4.Translate(new Vector3(px * w, -0.01f, 0f)), new Vector3(0.02f, 0.05f, 0.025f), WoodA);
                var mesh = MapBuilder.SaveMesh(c.Build($"WindField_Laundry_{i}"));
                Vector3 at = t.TransformPoint(L((i + 0.5f) / widths.Length)) + Vector3.down * 0.012f;
                var go = MapBuilder.Place($"WindField_Laundry_{i}", t, mesh, lit, at, t.rotation);
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, -h * 0.5f, 0f);
                col.size = new Vector3(w, h, 0.03f);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.08f;
                rb.linearDamping = 0.8f;
                rb.angularDamping = 0.6f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                // Bisagra a lo largo de la soga, anclada al mundo (como los banderines de la torre).
                var hinge = go.AddComponent<HingeJoint>();
                hinge.anchor = Vector3.zero;
                hinge.axis = Vector3.right;
                hinge.useLimits = true;
                hinge.limits = new JointLimits { min = -80f, max = 80f };
            }
        }

        // ---------------------------------------------------------------- aro-diana

        /// <summary>Aro de 0.9 m de radio (doce tramos a rayas, con collider por tramo) sobre dos patas, mirando al centro.</summary>
        private static void WfRingTarget(LowPolyMesh b, Transform t)
        {
            float chord = 2f * WfRingRadius * Mathf.Sin(Mathf.PI / WfRingSegments) * 1.06f;
            float legX = WfRingRadius + WfRingTube * 0.5f + 0.06f;
            float g = Mathf.Min(Rel(t, -legX, WfRingZ), Rel(t, legX, WfRingZ));
            float cy = Mathf.Max(Rel(t, -legX, WfRingZ), Rel(t, legX, WfRingZ)) + WfRingCenter;
            for (int i = 0; i < WfRingSegments; i++)
            {
                float a = i * 360f / WfRingSegments;
                float ar = a * Mathf.Deg2Rad;
                var center = new Vector3(Mathf.Cos(ar) * WfRingRadius, cy + Mathf.Sin(ar) * WfRingRadius, WfRingZ);
                var rot = Quaternion.Euler(0f, 0f, a + 90f);
                var size = new Vector3(chord, WfRingTube, WfRingTube);
                b.Box(Matrix4x4.TRS(center, rot, Vector3.one), size, i % 2 == 0 ? Cloth : ClothLight);
                RotCol(t, $"WindField_RingCol_{i}", center, rot, size);
            }
            foreach (float sx in new[] { -legX, legX })
            {
                float bottom = Rel(t, sx, WfRingZ) - 0.3f, legTop = cy + 0.1f;
                Solid(b, t, new Vector3(sx, (bottom + legTop) * 0.5f, WfRingZ), new Vector3(0.12f, legTop - bottom, 0.12f), WoodA);
                B(b, new Vector3(sx, Rel(t, sx, WfRingZ) + 0.08f, WfRingZ), new Vector3(0.3f, 0.2f, 0.3f), StoneB, StoneA);
            }
            if (cy - g > WfRingCenter + 0.5f) Debug.LogWarning($"[Estructuras] el aro del campo de viento queda sobre terreno desparejo ({cy - g:F2} m).");
        }

        // ---------------------------------------------------------------- hojas y plumas

        /// <summary>Seis hojas y tres plumas sobre la mesa baja; cuatro hojas en el pasto delante de ella. Sin solaparse al arrancar.</summary>
        private static void WfLightObjects(Transform t, Vector2 table, float top, float yaw)
        {
            int n = 0;
            for (int row = 0; row < 2; row++)
                for (int k = 0; k < 3; k++, n++)
                {
                    var local = new Vector3(table.x - 0.4f + k * 0.4f, top + 0.003f, table.y - 0.17f + row * 0.17f);
                    WfFlutter(WfLightProp(t, $"WindField_Leaf_{n}", WfLeafMesh(n), t.TransformPoint(local), yaw + WfHash(n, 3) * 360f,
                        new Vector3(0.12f, 0.012f, 0.09f), new Vector3(0f, 0.006f, 0f), 0.003f, "hoja", 0.2f));
                }
            for (int k = 0; k < 4; k++, n++)
            {
                float x = table.x - 0.6f + k * 0.4f, z = table.y + 0.9f + WfHash(n, 2) * 0.6f;
                var local = new Vector3(x, Rel(t, x, z) + 0.02f, z);
                WfFlutter(WfLightProp(t, $"WindField_Leaf_{n}", WfLeafMesh(n), t.TransformPoint(local), yaw + WfHash(n, 3) * 360f,
                    new Vector3(0.12f, 0.012f, 0.09f), new Vector3(0f, 0.006f, 0f), 0.003f, "hoja", 0.2f));
            }
            for (int i = 0; i < 3; i++)
            {
                var local = new Vector3(table.x - 0.35f + i * 0.35f, top + 0.003f, table.y + 0.18f);
                WfFlutter(WfLightProp(t, $"WindField_Feather_{i}", WfFeatherMesh(i), t.TransformPoint(local), yaw + (i - 1) * 8f,
                    new Vector3(0.24f, 0.012f, 0.05f), new Vector3(0f, 0.006f, 0f), 0.004f, "pluma", 0.25f));
            }
        }

        /// <summary>Numero pseudoaleatorio estable (0..1): la escena se reconstruye igual cada vez.</summary>
        private static float WfHash(int i, int channel) => Mathf.Abs(Mathf.Sin(i * 12.9898f + channel * 78.233f) * 43758.5453f) % 1f;

        /// <summary>Hoja de arbol (mismo modelo que TowerProps.Leaf): rombo chato con nervio, verde u otonal.</summary>
        private static LowPolyMesh WfLeafMesh(int variant)
        {
            Color[] tones = { new Color(0.36f, 0.58f, 0.25f), new Color(0.48f, 0.66f, 0.28f), new Color(0.82f, 0.55f, 0.2f), new Color(0.7f, 0.32f, 0.16f) };
            Color c = tones[variant % tones.Length];
            var b = new LowPolyMesh();
            Vector3 tip = new Vector3(0f, 0.005f, 0.06f), tail = new Vector3(0f, 0.005f, -0.05f), l = new Vector3(-0.045f, 0.005f, 0.005f), r = new Vector3(0.045f, 0.005f, 0.005f);
            b.Tri(tail, l, tip, c);
            b.Tri(tail, tip, r, c * 0.92f);
            b.Tri(tail, tip, l, c * 0.8f);
            b.Tri(tail, r, tip, c * 0.75f);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.0055f, 0f)), new Vector3(0.006f, 0.002f, 0.11f), c * 0.7f);
            return b;
        }

        /// <summary>Pluma (mismo modelo que TowerProps.Feather): canon y barbas, a lo largo de X.</summary>
        private static LowPolyMesh WfFeatherMesh(int variant)
        {
            var b = new LowPolyMesh();
            Color[] vanes = { new Color(0.95f, 0.93f, 0.88f), new Color(0.55f, 0.4f, 0.3f), new Color(0.3f, 0.45f, 0.55f) };
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.004f, 0f)), new Vector3(0.24f, 0.006f, 0.008f), new Color(0.85f, 0.8f, 0.65f));
            Vector3 a = new Vector3(-0.07f, 0.006f, 0f), c = new Vector3(0.12f, 0.006f, 0f);
            b.Tri(a, new Vector3(0.03f, 0.007f, 0.025f), c, vanes[variant % vanes.Length]);
            b.Tri(a, c, new Vector3(0.03f, 0.007f, -0.025f), vanes[variant % vanes.Length] * 0.9f);
            return b;
        }

        private static GameObject WfFlutter(GameObject go)
        {
            if (go != null && go.GetComponent<Flutter>() == null) go.AddComponent<Flutter>();
            return go;
        }

        /// <summary>Objeto liviano movible (copia de TowerProps.LightProp): Rigidbody, Movable, reaccion de papel y Respawnable.</summary>
        private static GameObject WfLightProp(Transform parent, string name, LowPolyMesh mesh, Vector3 position, float yaw,
            Vector3 colliderSize, Vector3 colliderCenter, float mass, string label, float airDrag)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var saved = MapBuilder.SaveMesh(mesh.Build($"Prop_{name}"));
            var go = MapBuilder.Place(name, parent, saved, lit, position, Quaternion.Euler(0f, yaw, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.size = colliderSize;
            col.center = colliderCenter;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = airDrag;
            rb.angularDamping = Mathf.Min(1f, airDrag * 0.4f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var movable = go.AddComponent<Movable>();
            var so = new SerializedObject(movable);
            so.FindProperty("objectName").stringValue = label;
            so.ApplyModifiedProperties();
            var profile = AssetDatabase.LoadAssetAtPath<MaterialProfile>(WfPaperProfilePath);
            if (profile != null)
            {
                var reactive = go.AddComponent<ElementReactive>();
                var rso = new SerializedObject(reactive);
                rso.FindProperty("profile").objectReferenceValue = profile;
                rso.ApplyModifiedProperties();
            }
            go.AddComponent<Respawnable>();
            return go;
        }

    }
}
