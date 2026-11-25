using UnityEngine;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    public static partial class MapStructures
    {
        // ---------------------------------------------------------------- campo de fuego

        // Claro de ceniza (m): el centro queda en terreno pelado (ahi se dibuja: el terreno es rayable y una calcomania de tinta
        // queda a 6 mm, debajo de la ceniza). La ceniza va en dos bandas, de FireAshInner a FireFloorRadius.
        private const float FireAshInner = 2.6f, FireAshMid = 4.1f, FireAshLift = 0.015f;
        private const int FireAshSegments = 20;
        /// <summary>Radio del piso propio (Site.FloorRadius = 0,7 x ElementFieldRadius): sin pasto adentro.</summary>
        private const float FireFloorRadius = 5.6f;
        /// <summary>Anillo de piedras: radio, candidatos y medio angulo (grados) de la abertura hacia el camino.</summary>
        private const float FireRingRadius = 5.95f, FireGateHalfAngle = 24f;
        private const int FireRingSlots = 16;
        /// <summary>Si el camino mas cercano termina mas lejos que esto, se usa el sureste del mundo como entrada.</summary>
        private const float FireGateSearch = 14f;

        private static readonly Color FireAshDark = new Color(0.2f, 0.17f, 0.15f);
        private static readonly Color FireAsh = new Color(0.34f, 0.28f, 0.24f);
        private static readonly Color FireCharcoal = new Color(0.12f, 0.1f, 0.09f);
        private static readonly Color FireStraw = new Color(0.86f, 0.72f, 0.38f);
        private static readonly Color FireStrawLight = new Color(0.93f, 0.82f, 0.5f);
        private static readonly Color FireWater = new Color(0.3f, 0.55f, 0.72f);
        private const string FireTitleColor = "#A8401C";

        /// <summary>
        /// Campo de fuego (Docs/MAPA_FLUJO_PLAN.md §12b punto 8): claro de ceniza con anillo de piedras abierto hacia el camino,
        /// fogata apagada en el centro-frente, tres blancos de madera/paja inflamables al frente (empalizada, leñera, muñeco de paja)
        /// y dos cajas movibles, atril vacio para el libro de fuego (paso 7), mesa con papeles y tintero fijo, tres carteles y un
        /// abrevadero con balde en la entrada. Una sola malla estatica (Struct_FireField); los blancos son objetos aparte con
        /// ElementReactive y el perfil Madera (el mismo mecanismo que FlammableSetup).
        /// </summary>
        private static void FireField(Transform parent, Site site)
        {
            var t = StructRoot(parent, "FireField", site);
            float yaw = t.eulerAngles.y;
            var b = new LowPolyMesh();

            // Entrada: el extremo de camino mas cercano al sitio, en coordenadas locales (+Z = frente, hacia el Archivo).
            Vector2 gateWorld = site.Pos + new Vector2(0.7f, -0.7f) * 8f;
            float bestGate = FireGateSearch;
            foreach (var path in ValleyShape.Paths)
            {
                if (path == null || path.Length == 0) continue;
                foreach (var end in new[] { path[0], path[path.Length - 1] })
                {
                    float d = Vector2.Distance(end, site.Pos);
                    if (d < bestGate) { bestGate = d; gateWorld = end; }
                }
            }
            Vector3 gateLocal = t.InverseTransformDirection(new Vector3(gateWorld.x - site.Pos.x, 0f, gateWorld.y - site.Pos.y));
            Vector2 gateDir = new Vector2(gateLocal.x, gateLocal.z);
            gateDir = gateDir.sqrMagnitude > 1e-4f ? gateDir.normalized : new Vector2(1f, -1f).normalized;
            Vector2 gatePerp = new Vector2(gateDir.y, -gateDir.x);

            // 1. Claro de ceniza: anillo plano en dos bandas (mas oscuro adentro), siguiendo el terreno.
            Vector3 AshPoint(float r, float a)
            {
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                return new Vector3(x, Rel(t, x, z) + FireAshLift, z);
            }
            float[] radii = { FireAshInner, FireAshMid, FireFloorRadius };
            for (int band = 0; band < 2; band++)
                for (int i = 0; i < FireAshSegments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / FireAshSegments, a1 = (i + 1) * Mathf.PI * 2f / FireAshSegments;
                    Vector3 i0 = AshPoint(radii[band], a0), i1 = AshPoint(radii[band], a1);
                    Vector3 o0 = AshPoint(radii[band + 1], a0), o1 = AshPoint(radii[band + 1], a1);
                    Color c = LowPolyMesh.Jitter(band == 0 ? FireAshDark : FireAsh, 0.12f, 31 + band * 50 + i);
                    b.Tri(i0, i1, o0, c);
                    b.Tri(i1, o1, o0, c);
                }

            // Anillo de piedras bajas, con la abertura hacia el camino.
            int stones = 0;
            for (int k = 0; k < FireRingSlots; k++)
            {
                float a = (k + 0.5f) * Mathf.PI * 2f / FireRingSlots;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (Vector2.Angle(dir, gateDir) < FireGateHalfAngle) continue;
                var size = new Vector3(0.75f + 0.1f * (k % 3), 0.36f + 0.05f * (k % 2), 0.55f);
                float x = dir.x * FireRingRadius, z = dir.y * FireRingRadius;
                var center = new Vector3(x, Rel(t, x, z) + size.y * 0.5f - 0.06f, z);
                float stoneYaw = -(a * Mathf.Rad2Deg + 90f);
                Chunk(b, center, size, stoneYaw, 400 + k, StoneB, StoneA);
                RotCol(t, $"FireRingStone_{k}", center, Quaternion.Euler(0f, stoneYaw, 0f), size * 0.92f);
                stones++;
            }

            // 2. Fogata apagada (centro-frente): lecho de carbon, piedras alrededor y leña en tipi.
            const float pitX = 0f, pitZ = 1.7f;
            float pg = Rel(t, pitX, pitZ);
            var pit = new Vector3(pitX, pg, pitZ);
            b.Frustum(Matrix4x4.Translate(pit), 0.45f, 0.42f, 0f, 0.04f, 8, FireCharcoal);
            for (int k = 0; k < 7; k++)
            {
                float a = k * Mathf.PI * 2f / 7f;
                Vector3 c = pit + new Vector3(Mathf.Cos(a) * 0.58f, 0.09f, Mathf.Sin(a) * 0.58f);
                Chunk(b, c, new Vector3(0.3f, 0.22f, 0.22f), -(a * Mathf.Rad2Deg + 90f), 200 + k, StoneB, StoneA, 0.2f);
            }
            for (int k = 0; k < 4; k++)
            {
                Quaternion around = Quaternion.Euler(0f, 45f + 90f * k, 0f);
                Vector3 c = pit + around * Vector3.forward * 0.17f + Vector3.up * 0.18f;
                b.Box(Matrix4x4.TRS(c, around * Quaternion.Euler(35f, 0f, 0f), Vector3.one), new Vector3(0.09f, 0.09f, 0.6f), (k & 1) == 0 ? WoodB : WoodA);
            }
            Col(t, pit + new Vector3(0f, 0.15f, 0f), new Vector3(1.3f, 0.3f, 1.3f));

            // 4. Libro de hechizos de fuego (paso 7), a la izquierda, mirando al centro.
            StandBook(t, "spells-fire", new Vector2(-4.1f, -1.0f), Vector2.zero);

            // 7. Abrevadero y balde con agua en la entrada (decorativos), del lado izquierdo del camino.
            Vector2 trough = gateDir * 6.6f - gatePerp * 1.8f;
            {
                float g0 = Mathf.Min(Rel(t, trough.x - 0.6f, trough.y), Rel(t, trough.x + 0.6f, trough.y)) - 0.1f;
                float top = Mathf.Max(Rel(t, trough.x - 0.6f, trough.y), Rel(t, trough.x + 0.6f, trough.y)) + 0.5f;
                Solid(b, t, new Vector3(trough.x, (top + g0) * 0.5f, trough.y), new Vector3(1.2f, top - g0, 0.5f), WoodB, WoodA);
                B(b, new Vector3(trough.x, top + 0.004f, trough.y), new Vector3(1.02f, 0.012f, 0.34f), FireWater);
                float bx = trough.x - 0.95f, bz = trough.y + 0.15f, bgr = Rel(t, bx, bz);
                b.Frustum(Matrix4x4.Translate(new Vector3(bx, bgr, bz)), 0.15f, 0.19f, 0f, 0.32f, 8, WoodA);
                b.Frustum(Matrix4x4.Translate(new Vector3(bx, bgr + 0.3f, bz)), 0.18f, 0.18f, 0f, 0.025f, 8, FireWater);
                Col(t, new Vector3(bx, bgr + 0.16f, bz), new Vector3(0.38f, 0.32f, 0.38f));
            }

            report.AppendLine($"  campo de fuego: {b.TriangleCount} triangulos estaticos, {stones} piedras en el anillo.");
            Finish(t, b, "Struct_FireField");

            // 3. Blancos inflamables al frente (madera y paja, perfil Madera como los muebles de FlammableSetup).
            // Perfil de arbol (Data/Materials/Arbol.asset): arde 30 s y queda carbonizado en pie, sin destruirse, asi los blancos sirven para practicar
            // varias veces. Si falta, cae al perfil Madera (que los destruye al terminar de arder).
            var wood = UnityEditor.AssetDatabase.LoadAssetAtPath<global::SpellSimulator.Reactions.MaterialProfile>("Assets/_Project/Data/Materials/Arbol.asset") ?? MovablesSetup.WoodProfile();
            if (wood == null) Debug.LogWarning("[Estructuras] campo de fuego: sin perfil Madera, los blancos no arden.");
            GameObject Target(string name, LowPolyMesh mesh, float x, float z, Vector3 colCenter, Vector3 colSize)
            {
                var local = new Vector3(x, Rel(t, x, z), z);
                var toCenter = new Vector3(-x, 0f, -z).normalized;
                var go = MapBuilder.Place(name, t, MapBuilder.SaveMesh(mesh.Build(name)), lit, t.TransformPoint(local), t.rotation * Quaternion.LookRotation(toCenter));
                var col = go.AddComponent<BoxCollider>();
                col.center = colCenter;
                col.size = colSize;
                if (wood != null && go.GetComponent<global::SpellSimulator.Reactions.ElementReactive>() == null)
                {
                    var reactive = go.AddComponent<global::SpellSimulator.Reactions.ElementReactive>();
                    var so = new UnityEditor.SerializedObject(reactive);
                    so.FindProperty("profile").objectReferenceValue = wood;
                    so.ApplyModifiedProperties();
                }
                return go;
            }

            // Empalizada: tres estacas y dos tablones (los tablones del lado del centro).
            var stakes = new LowPolyMesh();
            foreach (float sx in new[] { -0.5f, 0f, 0.5f })
            {
                stakes.Box(Matrix4x4.Translate(new Vector3(sx, 0.6f, 0f)), new Vector3(0.14f, 1.2f, 0.14f), WoodB, WoodA);
                stakes.Frustum(Matrix4x4.Translate(new Vector3(sx, 1.2f, 0f)), 0.09f, 0f, 0f, 0.14f, 4, WoodA);
            }
            stakes.Box(Matrix4x4.Translate(new Vector3(0f, 0.5f, 0.1f)), new Vector3(1.4f, 0.2f, 0.05f), Plank);
            stakes.Box(Matrix4x4.Translate(new Vector3(0f, 0.95f, 0.1f)), new Vector3(1.4f, 0.2f, 0.05f), Plank);
            Target("FireTarget_Stakes_FireField", stakes, -2.6f, 4.2f, new Vector3(0f, 0.65f, 0.03f), new Vector3(1.4f, 1.3f, 0.3f));

            // Muñeco de paja: palo con base, fardo de cuerpo, cabeza y brazos.
            var dummy = new LowPolyMesh();
            dummy.Box(Matrix4x4.Translate(new Vector3(0f, 0.04f, 0f)), new Vector3(0.5f, 0.08f, 0.5f), WoodB);
            dummy.Box(Matrix4x4.Translate(new Vector3(0f, 0.85f, 0f)), new Vector3(0.1f, 1.7f, 0.1f), WoodB);
            dummy.Box(Matrix4x4.Translate(new Vector3(0f, 1.0f, 0f)), new Vector3(0.5f, 0.7f, 0.32f), FireStraw, FireStrawLight);
            dummy.Box(Matrix4x4.Translate(new Vector3(0f, 1.0f, 0f)), new Vector3(0.52f, 0.06f, 0.34f), WoodA); // cuerda
            dummy.Box(Matrix4x4.Translate(new Vector3(0f, 1.56f, 0f)), new Vector3(0.3f, 0.3f, 0.28f), FireStraw, FireStrawLight);
            dummy.Box(Matrix4x4.Translate(new Vector3(0f, 1.25f, 0f)), new Vector3(1.1f, 0.08f, 0.08f), WoodA);
            foreach (float sx in new[] { -0.55f, 0.55f })
                dummy.Box(Matrix4x4.Translate(new Vector3(sx, 1.25f, 0f)), new Vector3(0.16f, 0.16f, 0.16f), FireStraw, FireStrawLight);
            Target("FireTarget_Dummy_FireField", dummy, -0.5f, 4.9f, new Vector3(0f, 0.88f, 0f), new Vector3(0.6f, 1.76f, 0.4f));

            // Leñera: dos tablas laterales y troncos apilados 4-3-2.
            var pile = new LowPolyMesh();
            foreach (float sx in new[] { -0.5f, 0.5f })
                pile.Box(Matrix4x4.Translate(new Vector3(sx, 0.25f, 0f)), new Vector3(0.08f, 0.5f, 0.8f), WoodB);
            int log = 0;
            for (int row = 0; row < 3; row++)
                for (int n = 0; n < 4 - row; n++, log++)
                {
                    float lx = (n - (3 - row) * 0.5f) * 0.2f;
                    Color bark = LowPolyMesh.Jitter((log & 1) == 0 ? WoodA : WoodB, 0.1f, 70 + log);
                    pile.Box(Matrix4x4.Translate(new Vector3(lx, 0.09f + row * 0.16f, 0f)), new Vector3(0.17f, 0.16f, 0.78f), bark, Plank);
                }
            Target("FireTarget_Woodpile_FireField", pile, 1.3f, 4.3f, new Vector3(0f, 0.25f, 0f), new Vector3(1.08f, 0.5f, 0.82f));

            // Dos cajas movibles entre la fogata y los blancos (MovablesSetup ya les pone el perfil Madera).
            MovablesSetup.Prop(t, lit, "FireCrate_A_FireField", MovablesSetup.Kind.SmallBox, t.TransformPoint(new Vector3(-1.5f, 0f, 3.3f)), yaw + 14f, true);
            MovablesSetup.Prop(t, lit, "FireCrate_B_FireField", MovablesSetup.Kind.SmallBox, t.TransformPoint(new Vector3(0.6f, 0f, 3.4f)), yaw - 20f, true);

            // 5. Mesa con papeles y tintero fijo (atras a la derecha).
            const float tableX = 3.0f, tableZ = -2.9f;
            HeightRange(t, tableX - 0.9f, tableX + 0.9f, tableZ - 0.4f, tableZ + 0.4f, out float tmin, out _);
            TowerBuilder.Table(t, lit, "FireFieldTable", t.rotation * new Vector3(tableX, tmin, tableZ), yaw, 1.6f, 0.7f);
            float desk = tmin + 0.82f;
            FixedInkwell(t, "Inkwell_FireField", t.TransformPoint(new Vector3(tableX - 0.55f, desk, tableZ - 0.1f)), yaw);
            var tray = Tray(t, "PaperTray_FireField", new Vector3(tableX + 0.45f, desk, tableZ));
            Supply(tray, PaperTemplate("PaperTemplate"), 0.08f, PaperCooldown, 2, "firefield-paper");

            // 6. Carteles: el primero en la entrada (se lee al llegar), los otros dos a la izquierda, de cara al centro.
            Vector2 s1 = gateDir * 4.7f + gatePerp * 1.5f;
            CanvasDesigns.Sign(t, lit, "Sign_FireField_1", t.TransformPoint(new Vector3(s1.x, 0f, s1.y)), t.TransformDirection(new Vector3(gateDir.x, 0f, gateDir.y)),
                $"<color={FireTitleColor}>Campo de fuego</color>", "Dibujá el círculo con el signo\nde <b>Fuego</b> en el piso (en el\ncentro, sin ceniza) o en un\npapel, y cerralo: se enciende.");
            Vector3 ToCenter(float x, float z) => t.TransformDirection(new Vector3(-x, 0f, -z).normalized);
            CanvasDesigns.Sign(t, lit, "Sign_FireField_2", t.TransformPoint(new Vector3(-4.5f, 0f, 1.7f)), ToCenter(-4.5f, 1.7f),
                $"<color={FireTitleColor}>Llama y brasas</color>", "Fuego + <b>columna</b>: una llama\ndirigida. Fuego + <b>dispersión</b>:\nun abanico de brasas.\nProbá con los blancos.");
            CanvasDesigns.Sign(t, lit, "Sign_FireField_3", t.TransformPoint(new Vector3(-3.0f, 0f, -3.9f)), ToCenter(-3.0f, -3.9f),
                $"<color={FireTitleColor}>Cuidado</color>", "El fuego prende el pasto y la\nmadera; el <b>agua</b> lo apaga.\nTinta más larga = fuego más\nfuerte. Hay agua en la entrada.");
        }
    }
}
