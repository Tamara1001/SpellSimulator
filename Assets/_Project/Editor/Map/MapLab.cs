using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Lab;
using SpellSimulator.Output;
using SpellSimulator.Reactions;
using SpellSimulator.World;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Genera el area del Laboratorio v2 (Docs/LABORATORIO_V2_PLAN.md, «Área») en el sitio «Laboratory» de MapStructures
    /// (pradera sureste), estilo low-poly cartoon con la paleta de MapStructures (tela, revoque, laton):
    /// - plataforma de lajas (r ≈ 5 m) con toldo a rayas, faroles y guirnaldas;
    /// - banco compacto v4 (Docs/LABORATORIO_V4_PLAN.md, LabBenchLayout): todo control a ≤ 0,7 m de donde se para el jugador.
    ///   Adelante una mesa baja con el atril de dibujo (LabSketchPad), LANZAR, «Borrar dibujo» y «Reiniciar sello»; a los lados
    ///   cuatro paneles empinados en arco: izquierda el sello (1 Hechizo con grilla de sigilos, 2 Signos con grilla de signos,
    ///   3 Papel y tinta, 4 Afinado), derecha los objetos del campo (+ Caja, borrar, casillas P/1–4 y controles del elegido) y
    ///   viento/fuego. Cada panel tiene titulo y tira de ayuda arriba (LabHelpStrip) y muescas de rango y valor por defecto;
    /// - papel de prueba en la pista (con poste) y aro del objeto elegido (LabField); cartel «Vas a lanzar» (LabBenchDisplay)
    ///   y datos de reconocimiento que solo se ven con lentes (LabLensGate); tres luces reales sin sombras y faroles emisivos;
    /// - repisa con las lentes de tinta (InkLenses + LabOverlay) y pizarron con los contadores (LabCounters);
    /// - pista de pasto de 12 × 10 m al frente con estacas cada 2 m, manga de viento con banderines (LabWindVisual) y fogon de
    ///   piedras a ≥ 8 m (ancla del fuego).
    /// Marco local: +Z hacia la pista (frente del sitio), +X a la derecha de quien usa el banco. Todo cuelga de <c>Map/Lab</c> con
    /// nombres estables (<c>LabCtl_Seal_RingFill</c>, <c>LabMaterialize_Box</c>...) porque MapLayout conserva los ajustes
    /// manuales por ruta. Controles de 9 cm separados ≥ 6 cm (rayo de VR con temblor). Los delegados de los controles se
    /// enlazan en runtime (<see cref="LabControlBinding"/>).
    /// </summary>
    public static class MapLab
    {
        public const string RootName = "Lab";

        // ---------------------------------------------------------------- medidas

        private const float PlatformRadius = MapStructures.LaboratoryPlatformRadius;
        private const int PlatformSides = 14;
        /// <summary>Altura de la plataforma sobre el punto mas alto del terreno que cubre (m).</summary>
        private const float PlatformLift = 0.12f;
        private const float SlabCell = 0.8f;

        // Banco compacto v4 (LabBenchLayout): el jugador se para en StandZ y todo control queda a ≤ 0,7 m.
        private const float StandZ = LabBenchLayout.StandZ, DeskTop = LabBenchLayout.DeskTop;
        private const float FacetTilt = LabBenchLayout.FacetTilt, FacetSlope = LabBenchLayout.FacetSlope, CrestSlope = LabBenchLayout.CrestSlope;
        private const float FacetWidth = LabBenchLayout.FacetWidth, FacetNear = LabBenchLayout.FacetNear, FacetBottom = LabBenchLayout.FacetBottom;
        /// <summary>Muescas: mas de esto se ve como una regla y no ayuda (queda solo min/max y default).</summary>
        private const int MaxTicks = 13;
        private const float KnobRadius = 0.04f, DialRadius = 0.045f;
        /// <summary>Medio recorrido del deslizador (m) y altura del riel.</summary>
        private const float RailHalf = 0.05f, RailY = 0.008f;
        /// <summary>Plaquita del rotulo (arriba) y ventanita de la lectura (abajo): centro sobre la pendiente, ancho y profundidad (m).</summary>
        private const float LabelZ = 0.066f, ReadoutZ = -0.066f, PlaqueWidth = LabPanelLayout.PlaqueWidth, LabelDepth = 0.024f, ReadoutDepth = 0.028f;
        /// <summary>Largo de las flechas de emision (m).</summary>
        private const float ArrowLength = 0.32f;
        /// <summary>Tope de la tapa de los botones planos (sobre su base).</summary>
        private const float ButtonTop = 0.05f;

        // Cartel «Vas a lanzar» (relativo al jugador): adelante a la izquierda, fuera de la vista de la pista.
        private static readonly Vector3 CartelLocal = new Vector3(-1.25f, 0f, 0.8f);
        private const float CartelWidth = 0.8f, CartelHeight = 0.64f, CartelBottom = 1.12f;
        /// <summary>Banda de resultado y lectura del atril (solo con lentes), relativas al jugador.</summary>
        private static readonly Vector3 ResultLocal = new Vector3(-0.78f, 1.08f, 0.95f);
        private static readonly Vector3 PadInfoLocal = new Vector3(0f, 1.47f, 0.74f);

        // Toldo: postes en las esquinas, mas alto del lado de la pista.
        private const float PostX = 2.45f, PostFrontZ = -1.15f, PostBackZ = 1.65f;
        private const float CanopyFrontY = 2.5f, CanopyBackY = 2.9f, CanopyOverhang = 0.2f;
        private const int CanopyStrips = 11;

        // Pista (desde el centro del sitio, igual que MapStructures.LaboratoryFront).
        private const float TrackStart = MapStructures.LaboratoryTrackStart;
        private const float TrackLength = MapStructures.LaboratoryTrackLength, TrackWidth = MapStructures.LaboratoryTrackWidth;
        private const float StakeEvery = 2f, FirstStake = 6f;
        /// <summary>Distancia minima (m) del parche de fuego al banco (riesgo 12: que el banco no se queme).</summary>
        private const float MinFireDistance = 8f;

        // Anclas en el marco local (verificar con captura tras regenerar: el sitio lo elige MapStructures).
        private static readonly Vector3 BoxAnchorLocal = new Vector3(-2.5f, 0f, 8.4f);
        private static readonly Vector3 WindAnchorLocal = new Vector3(-2.5f, 0f, 9.2f);    // la caja queda dentro de la zona (4 × 3 × 4 m)
        private static readonly Vector3 FireAnchorLocal = new Vector3(3f, 0f, 13f);        // ~13 m del banco
        private static readonly Vector3 FountainAnchorLocal = new Vector3(0f, 0f, 7.5f);   // fase 3
        private static readonly Vector3 SockLocal = new Vector3(-6.3f, 0f, 6.5f);
        private static readonly Vector3 SignLocal = new Vector3(-3.25f, 0f, 0.9f);
        private static readonly Vector3 ShelfLocal = new Vector3(1.75f, 0f, -1.6f);
        private static readonly Vector3 ViewerLocal = new Vector3(0f, 0f, StandZ);

        // Manga de viento (misma altura de mastil que LabWindVisual).
        private const float MastHeight = 3f;
        private const int SockSegments = 4;
        private const float SockSegmentLength = 0.32f, SockRadius = 0.15f, SockTaper = 0.8f;
        private static readonly float[] PennantHeights = { 1.7f, 2.1f, 2.5f };

        // ---------------------------------------------------------------- colores

        private static Color WoodA => MapStructures.WoodA;
        private static Color WoodB => MapStructures.WoodB;
        private static Color Plank => MapStructures.Plank;
        private static Color StoneA => MapStructures.StoneA;
        private static Color StoneB => MapStructures.StoneB;
        private static Color Plaster => MapStructures.Plaster;
        private static Color Cloth => MapStructures.Cloth;
        private static Color ClothLight => MapStructures.ClothLight;
        private static Color Brass => MapStructures.Brass;
        private static Color Iron => MapStructures.Iron;

        private static readonly Color PanelColor = new Color(0.17f, 0.15f, 0.24f);
        private static readonly Color CrestColor = new Color(0.11f, 0.1f, 0.16f);
        private static readonly Color DialColor = new Color(0.1f, 0.09f, 0.14f);
        private static readonly Color ChalkBoard = new Color(0.16f, 0.24f, 0.2f);
        private static readonly Color ButtonBase = new Color(0.3f, 0.2f, 0.13f);
        private static readonly Color BoxGroup = new Color(0.86f, 0.72f, 0.48f);
        private static readonly Color WindGroup = new Color(0.62f, 0.86f, 0.95f);
        private static readonly Color FireGroup = new Color(0.95f, 0.5f, 0.25f);
        /// <summary>Botones de reinicio y borrado: neutros (no compiten con LANZAR).</summary>
        private static readonly Color NeutralButton = new Color(0.78f, 0.74f, 0.66f);
        private static readonly Color DeleteButton = new Color(0.72f, 0.58f, 0.55f);
        private static readonly Color LaunchColor = new Color(0.86f, 0.24f, 0.2f);
        private static readonly Color LensColor = new Color(0.25f, 0.55f, 0.75f);
        private static readonly Color Glass = new Color(0.55f, 0.8f, 0.85f);
        private static readonly Color TextLight = new Color(0.98f, 0.93f, 0.8f);
        private static readonly Color TextGold = new Color(1f, 0.82f, 0.45f);
        private static readonly Color TextDark = new Color(0.14f, 0.08f, 0.04f);
        private static readonly Color PlaqueCream = new Color(0.96f, 0.9f, 0.76f);
        private static readonly Color Parchment = new Color(0.95f, 0.88f, 0.7f);
        private static readonly Color ParchmentEdge = new Color(0.82f, 0.7f, 0.5f);
        private static readonly Color WindowDark = new Color(0.07f, 0.06f, 0.1f);
        /// <summary>Banda de resultado: casi blanca, la tiñe LabBenchDisplay (verde/rojo) con un MaterialPropertyBlock.</summary>
        private static readonly Color BandBase = new Color(0.95f, 0.95f, 0.95f);
        /// <summary>Flechas de emision y aro de seleccion: HDR (como TowerBuilder.LanternGlow) para que brillen con el bloom.</summary>
        private static readonly Color ArrowGlow = new Color(2.6f, 1.25f, 0.35f);
        private static readonly Color RingGlow = new Color(2.4f, 1.9f, 0.6f);
        /// <summary>Muesca del valor por defecto en perillas y deslizadores.</summary>
        private static readonly Color DefaultMark = new Color(1f, 0.78f, 0.25f);
        /// <summary>Luz calida de faroles y focos.</summary>
        private static readonly Color WarmLight = new Color(1f, 0.8f, 0.55f);

        private const string BoxMaterialPath = "Assets/_Project/Materials/Crate.mat";
        private const string WindMaterialPath = "Assets/_Project/Materials/SpellStreak.mat";
        private const string WindLookPath = "Assets/_Project/Data/Elements/Wind.asset";
        private const string IconMaterialPath = "Assets/_Project/Materials/Canvas.mat";
        private const string SpellsFolder = "Assets/_Project/Data/Spells";

        /// <summary>Texto acostado sobre una superficie de normal +Y, legible con la parte de arriba hacia +Z.</summary>
        private static readonly Quaternion Lying = Quaternion.Euler(90f, 0f, 0f);
        /// <summary>Texto acostado que se lee subiendo la pendiente (titulos del margen izquierdo).</summary>
        private static readonly Quaternion Spine = Quaternion.AngleAxis(-90f, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

        // ---------------------------------------------------------------- controles

        private struct Ctl
        {
            public LabControlSource Source;
            public int Var;
            public LabControlKind Kind;
            public string Label;

            public Ctl(LabSealVarId id, LabControlKind kind, string label)
            {
                Source = LabControlSource.Seal; Var = (int)id; Kind = kind; Label = label;
            }

            public Ctl(LabVariableId id, LabControlKind kind, string label)
            {
                Source = LabControlSource.Environment; Var = (int)id; Kind = kind; Label = label;
            }

            public Ctl(LabFieldVar id, LabControlKind kind, string label)
            {
                Source = LabControlSource.Field; Var = (int)id; Kind = kind; Label = label;
            }

            public string Key => Source == LabControlSource.Seal ? "Seal_" + (LabSealVarId)Var
                : Source == LabControlSource.Field ? "Field_" + (LabFieldVar)Var : "Env_" + (LabVariableId)Var;
        }

        /// <summary>Color y clave (para cachear mallas) de una fila o grupo.</summary>
        private struct Tone
        {
            public Color Color;
            public string Key;
            public Tone(Color color, string key) { Color = color; Key = key; }
        }

        private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        private static int controlCount;

        // ---------------------------------------------------------------- entrada

        /// <summary>
        /// Genera el laboratorio en <paramref name="site"/> (centro en XZ) mirando hacia <paramref name="front"/> (la pista).
        /// Lo llama MapBuilder.Build con el sitio «Laboratory» de MapStructures.
        /// </summary>
        public static void Build(Transform map, Material material, Vector2 site, Vector2 front)
        {
            meshes.Clear();
            controlCount = 0;
            var root = new GameObject(RootName).transform;
            root.SetParent(map, false);
            Vector3 forward = new Vector3(front.x, 0f, front.y);
            root.SetPositionAndRotation(MapBuilder.Ground(site.x, site.y),
                Quaternion.LookRotation(forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward));

            int cleared = ClearVegetation(map, root);
            float floor = Platform(root, material);
            Awning(root, material, floor);
            Track(root, material);
            CheckTrackBounds();

            var caster = SceneCaster();
            if (caster == null) Debug.LogWarning("[MapLab] no hay SpellCaster en la escena: el banco, el overlay y el registro no veran los hechizos.");

            // Sistemas: parametros del entorno + materializador con sus anclas (el campo y el banco los usan).
            var systems = Child(root, "LabSystems", Vector3.zero);
            systems.AddComponent<LabParameters>();
            var spawner = systems.AddComponent<LabSpawner>();
            Transform boxAnchor = Anchor(root, systems.transform, "LabAnchor_Box", BoxAnchorLocal);
            Transform windAnchor = Anchor(root, systems.transform, "LabAnchor_Wind", WindAnchorLocal);
            Transform fireAnchor = Anchor(root, systems.transform, "LabAnchor_Fire", FireAnchorLocal);
            Transform fountainAnchor = Anchor(root, systems.transform, "LabAnchor_Fountain", FountainAnchorLocal);
            Assign(spawner, "boxAnchor", boxAnchor);
            Assign(spawner, "windAnchor", windAnchor);
            Assign(spawner, "fireAnchor", fireAnchor);
            Assign(spawner, "fountainAnchor", fountainAnchor);
            Assign(spawner, "boxMaterial", AssetDatabase.LoadAssetAtPath<Material>(BoxMaterialPath));
            Assign(spawner, "windParticleMaterial", AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath));
            CheckFireAnchor(fireAnchor.position, root.position);

            // Marco de la pista: X y distancia del papel y de las cajas se miden aca (suelo a la altura de TrackStart + 2).
            var trackOrigin = Child(root, "LabTrackOrigin", new Vector3(0f, Rel(root, 0f, TrackStart + 2f), 0f)).transform;

            // Banco compacto: mesa de adelante (atril de dibujo y LANZAR) y cuatro paneles en arco; papel de prueba en el campo.
            LabSealBench bench = Bench(root, material, floor, caster, trackOrigin, spawner);

            WindSock(root, material, windAnchor);
            FirePit(root, material, fireAnchor.position);

            // Overlay de las lentes, registro y mediciones (como en v1).
            var overlay = Child(root, "LabOverlay", Vector3.zero).AddComponent<LabOverlay>();
            Assign(overlay, "caster", caster);
            var recorder = Child(root, "LabRecorder", Vector3.zero);
            Assign(recorder.AddComponent<LabMeasurements>(), "caster", caster);
            Assign(recorder.AddComponent<LabSessionLog>(), "caster", caster);

            Shelf(root, material, floor);
            Sign(root, material, floor);
            Lights(root, material, floor);

            Debug.Log($"[MapLab] laboratorio en {root.position} mirando {forward}; pinos/rocas despejados: {cleared}; " +
                      $"controles: {controlCount}; fuego en {fireAnchor.position}; banco: {(bench != null ? "ok" : "sin banco")}.");
        }

        /// <summary>Los limites del papel/cajas (LabSealSpec) tienen que caber en la pista que genera MapStructures.</summary>
        private static void CheckTrackBounds()
        {
            if (LabSealSpec.MaxPaperX > TrackWidth * 0.5f - 0.3f || LabSealSpec.MaxPaperDistance > TrackLength - 0.3f)
                Debug.LogWarning($"[MapLab] LabSealSpec.MaxPaperX/MaxPaperDistance no caben en la pista ({TrackWidth} × {TrackLength} m): ajustalos.");
        }

        // ---------------------------------------------------------------- plataforma y toldo

        /// <summary>Plataforma de lajas de 14 lados con escalones al frente y atras. Devuelve la altura del piso (local).</summary>
        private static float Platform(Transform root, Material material)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i <= 10; i++)
                for (int j = 0; j <= 10; j++)
                {
                    float x = Mathf.Lerp(-PlatformRadius, PlatformRadius, i / 10f), z = Mathf.Lerp(-PlatformRadius, PlatformRadius, j / 10f);
                    if (x * x + z * z > PlatformRadius * PlatformRadius) continue;
                    float h = Rel(root, x, z);
                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                }
            float top = max + PlatformLift, bottom = min - 0.4f;

            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.Translate(new Vector3(0f, bottom, 0f)), PlatformRadius, PlatformRadius, 0f, top - bottom, PlatformSides, StoneB);
            b.Frustum(Matrix4x4.Translate(new Vector3(0f, top - 0.06f, 0f)), PlatformRadius + 0.05f, PlatformRadius + 0.05f, 0f, 0.07f, PlatformSides, StoneA, false);
            // Lajas: losas apenas sobresalientes, giradas y con tono propio.
            int seed = 0;
            for (float x = -PlatformRadius; x <= PlatformRadius; x += SlabCell)
                for (float z = -PlatformRadius; z <= PlatformRadius; z += SlabCell)
                {
                    seed++;
                    float cx = x + SlabCell * 0.5f, cz = z + SlabCell * 0.5f;
                    if (cx * cx + cz * cz > (PlatformRadius - 0.5f) * (PlatformRadius - 0.5f)) continue;
                    float yaw = ((seed * 37) % 13 - 6) * 1.2f;
                    b.Box(Matrix4x4.TRS(new Vector3(cx, top + 0.012f, cz), Quaternion.Euler(0f, yaw, 0f), Vector3.one),
                        new Vector3(SlabCell - 0.07f, 0.012f, SlabCell - 0.07f), LowPolyMesh.Jitter(StoneA, 0.08f, seed));
                }
            float apothem = PlatformRadius * Mathf.Cos(Mathf.PI / PlatformSides);
            Steps(b, root, 0f, -apothem, -1f, 1.6f, top);
            Steps(b, root, 0f, apothem, 1f, 1.6f, top);

            var mesh = Save("Lab_Platform", b);
            var go = MapBuilder.Place("LabPlatform", root, mesh, material, root.position, root.rotation);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            return top + 0.018f;
        }

        /// <summary>Escalones de piedra (≤ 18 cm) desde el borde de la plataforma hacia afuera (dir = ±1 en Z local).</summary>
        private static void Steps(LowPolyMesh b, Transform root, float x, float edgeZ, float dir, float width, float top)
        {
            float g = Rel(root, x, edgeZ + dir * 0.6f);
            float rise = top - g;
            if (rise <= 0.22f) return;
            int n = Mathf.CeilToInt(rise / 0.18f);
            float h = rise / n;
            for (int k = 1; k < n; k++)
            {
                float stepTop = top - h * k;
                float z = edgeZ + dir * (0.15f + 0.3f * (k - 1));
                float stepBottom = Mathf.Min(g, Rel(root, x, z)) - 0.3f;
                b.Box(Matrix4x4.Translate(new Vector3(x, (stepTop + stepBottom) * 0.5f, z)), new Vector3(width, stepTop - stepBottom, 0.3f), StoneB, StoneA);
            }
        }

        private static float CanopyY(float z) => Mathf.Lerp(CanopyFrontY, CanopyBackY, Mathf.InverseLerp(PostFrontZ, PostBackZ, z));

        /// <summary>Toldo a rayas sobre cuatro postes, con volados, faroles colgados (sin luz: presupuesto Quest) y guirnaldas.</summary>
        private static void Awning(Transform root, Material material, float floor)
        {
            var b = new LowPolyMesh();
            foreach (float sx in new[] { -1f, 1f })
                foreach (float z in new[] { PostFrontZ, PostBackZ })
                {
                    float h = CanopyY(z);
                    b.Box(Matrix4x4.Translate(new Vector3(sx * PostX, 0.1f, z)), new Vector3(0.3f, 0.2f, 0.3f), StoneB, StoneA);
                    b.Frustum(Matrix4x4.Translate(new Vector3(sx * PostX, 0f, z)), 0.07f, 0.06f, 0.2f, h, 6, WoodA);
                    b.Frustum(Matrix4x4.Translate(new Vector3(sx * PostX, h + 0.03f, z)), 0.06f, 0f, 0f, 0.14f, 6, Brass, false);
                }
            // Vigas de adelante y de atras.
            b.Box(Matrix4x4.Translate(new Vector3(0f, CanopyFrontY - 0.06f, PostFrontZ)), new Vector3(PostX * 2f + 0.16f, 0.1f, 0.1f), WoodB);
            b.Box(Matrix4x4.Translate(new Vector3(0f, CanopyBackY - 0.06f, PostBackZ)), new Vector3(PostX * 2f + 0.16f, 0.1f, 0.1f), WoodB);

            // Lona a rayas (tela / tela clara) con pendiente hacia la pista.
            float z0 = PostFrontZ - CanopyOverhang, z1 = PostBackZ + CanopyOverhang;
            float slope = (CanopyBackY - CanopyFrontY) / (PostBackZ - PostFrontZ);
            float pitch = Mathf.Atan(slope) * Mathf.Rad2Deg;
            float length = (z1 - z0) / Mathf.Cos(pitch * Mathf.Deg2Rad);
            float width = (PostX + CanopyOverhang + 0.1f) * 2f, strip = width / CanopyStrips;
            float zc = (z0 + z1) * 0.5f;
            for (int i = 0; i < CanopyStrips; i++)
            {
                float x = -width * 0.5f + strip * (i + 0.5f);
                Color c = i % 2 == 0 ? Cloth : ClothLight;
                b.Box(Matrix4x4.TRS(new Vector3(x, CanopyY(zc) + 0.03f, zc), Quaternion.Euler(-pitch, 0f, 0f), Vector3.one), new Vector3(strip, 0.03f, length), c);
                // Volados en punta en los dos bordes.
                foreach (float ez in new[] { z0, z1 })
                {
                    float ey = CanopyY(ez) + 0.02f;
                    Vector3 l = new Vector3(x - strip * 0.5f, ey, ez), r = new Vector3(x + strip * 0.5f, ey, ez), tip = new Vector3(x, ey - 0.2f, ez);
                    b.Tri(l, r, tip, c);
                    b.Tri(r, l, tip, c);
                }
            }
            var go = MapBuilder.Place("LabAwning", root, Save("Lab_Awning", b), material, root.TransformPoint(new Vector3(0f, floor, 0f)), root.rotation);
            go.isStatic = true;

            // Faroles colgados de las vigas.
            Mesh front = LanternMesh("Lab_LanternFront", CanopyFrontY - 0.1f - 1.95f);
            Mesh back = LanternMesh("Lab_LanternBack", CanopyBackY - 0.1f - 2.2f);
            int n = 0;
            foreach (float sx in new[] { -1.4f, 1.4f })
            {
                MapBuilder.Place("LabLantern_" + ++n, root, front, material, root.TransformPoint(new Vector3(sx, floor + 1.95f, PostFrontZ)), root.rotation).isStatic = true;
                MapBuilder.Place("LabLantern_" + ++n, root, back, material, root.TransformPoint(new Vector3(sx, floor + 2.2f, PostBackZ)), root.rotation).isStatic = true;
            }

            // Guirnaldas de banderines entre los postes (adelante y atras).
            var g = new LowPolyMesh();
            Garland(g, new Vector3(-PostX, CanopyFrontY - 0.15f, PostFrontZ - 0.07f), new Vector3(PostX, CanopyFrontY - 0.15f, PostFrontZ - 0.07f), 0.22f);
            Garland(g, new Vector3(-PostX, CanopyBackY - 0.15f, PostBackZ + 0.07f), new Vector3(PostX, CanopyBackY - 0.15f, PostBackZ + 0.07f), 0.22f);
            MapBuilder.Place("LabGarlands", root, Save("Lab_Garlands", g), material, root.TransformPoint(new Vector3(0f, floor, 0f)), root.rotation).isStatic = true;
        }

        /// <summary>Farol de hierro con vidrio encendido (color HDR de la torre), origen en la base y cadena de <paramref name="chain"/> m.</summary>
        private static Mesh LanternMesh(string name, float chain)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.02f, 0f)), new Vector3(0.2f, 0.04f, 0.2f), Iron);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.15f, 0f)), new Vector3(0.13f, 0.22f, 0.13f), TowerBuilder.LanternGlow);
            for (int i = 0; i < 4; i++)
                b.Box(Matrix4x4.Translate(new Vector3((i & 1) == 0 ? -0.075f : 0.075f, 0.15f, i < 2 ? -0.075f : 0.075f)), new Vector3(0.022f, 0.24f, 0.022f), Iron);
            b.Frustum(Matrix4x4.Translate(new Vector3(0f, 0.26f, 0f)), 0.15f, 0.03f, 0f, 0.11f, 4, Iron, true, true);
            if (chain > 0.4f) b.Box(Matrix4x4.Translate(new Vector3(0f, 0.37f + (chain - 0.37f) * 0.5f, 0f)), new Vector3(0.02f, chain - 0.37f, 0.02f), Iron);
            return Save(name, b);
        }

        /// <summary>Cuerda con banderines triangulares de colores, con comba.</summary>
        private static void Garland(LowPolyMesh b, Vector3 from, Vector3 to, float sag)
        {
            Color[] colors = { Cloth, ClothLight, Brass, MapStructures.Shutter, MapStructures.Moss };
            const int segments = 14;
            Vector3 Point(float t) => Vector3.Lerp(from, to, t) + Vector3.down * sag * Mathf.Sin(t * Mathf.PI);
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = Point(i / (float)segments), c = Point((i + 1) / (float)segments);
                Beam(b, a, c, 0.012f, WoodB);
                if (i == 0) continue;
                Vector3 side = (c - a).normalized * 0.09f;
                Vector3 tip = a + Vector3.down * 0.2f;
                Color col = colors[i % colors.Length];
                b.Tri(a - side, a + side, tip, col);
                b.Tri(a + side, a - side, tip, col);
            }
        }

        // ---------------------------------------------------------------- banco compacto (Laboratorio v4)

        /// <summary>
        /// Banco v4 (Docs/LABORATORIO_V4_PLAN.md): cuelga de <c>Map/Lab/LabBench</c>, con el origen donde se para el jugador.
        /// Adelante la mesa baja con el atril de dibujo, LANZAR, «Borrar dibujo» y «Reiniciar sello»; a los lados cuatro paneles
        /// empinados en arco (izquierda: sello; derecha: objetos del campo y entorno); el cartel «Vas a lanzar» adelante a la
        /// izquierda. El papel de prueba (con su poste) y el aro de seleccion viven en el campo.
        /// </summary>
        private static LabSealBench Bench(Transform root, Material material, float floor, SpellCaster caster, Transform trackOrigin, LabSpawner spawner)
        {
            var benchGo = Child(root, "LabBench", new Vector3(0f, floor, StandZ));
            Transform bench = benchGo.transform;
            var sealBench = benchGo.AddComponent<LabSealBench>();
            var iconMaterial = AssetDatabase.LoadAssetAtPath<Material>(IconMaterialPath);
            if (iconMaterial == null) Debug.LogWarning($"[MapLab] no se encontro {IconMaterialPath}: las grillas de iconos no se veran.");

            // Papel de prueba en el campo («Frente», a 3 m del inicio de la pista; el banco lo reubica en runtime).
            var fieldPaper = Child(root, "LabFieldPaper", Vector3.zero).transform;
            Vector3 paperAt = trackOrigin.TransformPoint(new Vector3(0f, LabSealSpec.DefaultPaperHeight, TrackStart + LabSealSpec.DefaultPaperDistance));
            DrawingCanvas paper = ClonePaper(fieldPaper, "LabBenchPaper", LabSealSpec.DefaultPaperSize, paperAt, root.rotation, true);
            if (paper == null) Debug.LogWarning("[MapLab] falta PaperCanvas en la escena: el banco no tiene papel de prueba.");
            var stand = Part("LabPaperStand", fieldPaper, StandMesh(), material, Vector3.zero, Quaternion.identity);
            stand.transform.position = GroundAt(root, new Vector3(0f, 0f, TrackStart + LabSealSpec.DefaultPaperDistance));
            stand.transform.localScale = new Vector3(1f, LabSealSpec.DefaultPaperHeight, 1f);
            var ring = Part("LabFieldRing", root, SelectionRingMesh(), material, Vector3.zero, Quaternion.identity);
            ring.transform.position = stand.transform.position + Vector3.up * 0.03f;
            ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            LabSketchPad pad = Front(bench, material, sealBench, out Transform front);

            var so = new SerializedObject(sealBench);
            so.FindProperty("canvas").objectReferenceValue = paper;
            so.FindProperty("caster").objectReferenceValue = caster;
            so.FindProperty("trackOrigin").objectReferenceValue = trackOrigin;
            so.FindProperty("trackStart").floatValue = TrackStart;
            so.FindProperty("sketchPad").objectReferenceValue = pad;
            so.FindProperty("paperStand").objectReferenceValue = stand.transform;
            var files = so.FindProperty("spellFiles");
            List<TextAsset> spells = SpellFiles();
            files.arraySize = spells.Count;
            for (int i = 0; i < spells.Count; i++) files.GetArrayElementAtIndex(i).objectReferenceValue = spells[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Campo: papel elegido de entrada; las cajas las suma el spawner.
            var field = Child(root, "LabField", Vector3.zero).AddComponent<LabField>();
            Assign(field, "bench", sealBench);
            Assign(field, "spawner", spawner);
            Assign(field, "selectionRing", ring.transform);

            SealFacetInner(bench, material, sealBench, iconMaterial);
            SealFacetOuter(bench, material, sealBench);
            ObjectsFacet(bench, material, sealBench);
            EnvFacet(bench, material, sealBench);
            Cartel(bench, material, sealBench, caster, front);
            BenchLamps(bench, material);
            return sealBench;
        }

        /// <summary>
        /// Clon de PaperCanvas cuadrado de <paramref name="size"/> m, sin PhysicalCanvas, Respawnable ni componentes de red,
        /// kinematico y con los colliders apagados (no se agarra ni lo ve la lapicera). <paramref name="inkSide"/>: el hechizo
        /// sale por la cara con tinta (papel de prueba).
        /// </summary>
        private static DrawingCanvas ClonePaper(Transform parent, string name, float size, Vector3 position, Quaternion rotation, bool inkSide)
        {
            var src = GameObject.Find("PaperCanvas");
            if (src == null) return null;
            var go = Object.Instantiate(src, position, rotation);
            go.name = name;
            go.transform.localScale = new Vector3(size, size, src.transform.localScale.z);
            var grab = go.GetComponent<PhysicalCanvas>();
            if (grab != null) Object.DestroyImmediate(grab);
            var respawn = go.GetComponent<Respawnable>();
            if (respawn != null) Object.DestroyImmediate(respawn);
            StripNetwork(go);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            var canvas = go.GetComponent<DrawingCanvas>();
            if (canvas != null && inkSide)
            {
                var so = new SerializedObject(canvas);
                so.FindProperty("emissionSide").enumValueIndex = (int)EmissionSide.InkSide;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            go.transform.SetParent(parent, true);
            return canvas;
        }

        /// <summary>Saca los componentes de red (Netcode y SpellSimulator.Net) de un clon: el laboratorio no se replica.</summary>
        private static void StripNetwork(GameObject go)
        {
            for (int pass = 0; pass < 2; pass++)
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform) continue;
                    string ns = c.GetType().Namespace ?? string.Empty;
                    bool ours = ns.StartsWith("SpellSimulator.Net"), netcode = ns.StartsWith("Unity.Netcode");
                    if ((pass == 0 && ours) || (pass == 1 && netcode)) Object.DestroyImmediate(c);
                }
        }

        // ---------------------------------------------------------------- mesa de adelante

        /// <summary>Mesa baja de adelante: atril de dibujo (LabSketchPad), LANZAR y a sus costados «Borrar dibujo» y «Reiniciar sello».</summary>
        private static LabSketchPad Front(Transform bench, Material material, LabSealBench sealBench, out Transform front)
        {
            front = Child(bench, "LabFront", Vector3.zero).transform;
            float w = LabBenchLayout.FrontWidth, near = LabBenchLayout.FrontNear, depth = LabBenchLayout.FrontDepth;
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, (DeskTop - 0.05f) * 0.5f, near + depth * 0.5f + 0.02f)), new Vector3(w - 0.06f, DeskTop - 0.05f, depth - 0.06f), WoodA);
            b.Box(Matrix4x4.Translate(new Vector3(0f, DeskTop - 0.025f, near + depth * 0.5f)), new Vector3(w, 0.05f, depth), WoodB, Plank);
            b.Box(Matrix4x4.Translate(new Vector3(0f, DeskTop - 0.04f, near + 0.005f)), new Vector3(w + 0.02f, 0.03f, 0.02f), Brass);
            var desk = Part("Desk", front, Save("Lab_FrontDesk", b), material, Vector3.zero, Quaternion.identity);
            var col = desk.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, DeskTop * 0.5f, near + depth * 0.5f);
            col.size = new Vector3(w, DeskTop, depth);
            desk.isStatic = true;

            LabSketchPad pad = SketchPad(front, material);

            Vector3 launchAt = LabBenchLayout.LaunchCenter;
            var launch = Button(front, material, "LabLaunch", "Lab_ButtonLaunch", "LANZAR", LaunchColor, launchAt,
                LabBenchLayout.LaunchWidth, LabBenchLayout.LaunchDepth, 0.42f, ButtonIcon.None);
            launch.AddComponent<LabBenchButton>().Configure(LabBenchAction.Launch, sealBench, launch.GetComponent<MeshRenderer>());
            float bw = LabBenchLayout.FrontButtonWidth;
            var clear = Button(front, material, "LabClearDrawing", "Lab_ButtonClearDrawing", "Borrar\ndibujo", NeutralButton,
                launchAt + new Vector3(-LabBenchLayout.FrontButtonX, 0f, 0f), bw, LabBenchLayout.LaunchDepth, 0.15f, ButtonIcon.Eraser, true);
            clear.AddComponent<LabBenchButton>().Configure(LabBenchAction.ClearDrawing, sealBench, clear.GetComponent<MeshRenderer>());
            var reset = Button(front, material, "LabSealDefault", "Lab_ButtonSealReset", "Reiniciar\nsello", NeutralButton,
                launchAt + new Vector3(LabBenchLayout.FrontButtonX, 0f, 0f), bw, LabBenchLayout.LaunchDepth, 0.15f, ButtonIcon.Reset, true);
            reset.AddComponent<LabBenchButton>().Configure(LabBenchAction.ResetSeal, sealBench, reset.GetComponent<MeshRenderer>());
            EditorUtility.SetDirty(launch.GetComponent<LabBenchButton>());
            EditorUtility.SetDirty(clear.GetComponent<LabBenchButton>());
            EditorUtility.SetDirty(reset.GetComponent<LabBenchButton>());
            return pad;
        }

        /// <summary>
        /// Atril de dibujo: caballete inclinado <see cref="LabBenchLayout.PadTilt"/>° hacia el jugador con un DrawingCanvas real
        /// (clon de PaperCanvas sin colliders) y un collider disparador en un hijo SIN DrawingCanvas (la lapicera no lo ve; el
        /// interactor encuentra el LabSketchPad por GetComponentInParent).
        /// </summary>
        private static LabSketchPad SketchPad(Transform front, Material material)
        {
            float tilt = LabBenchLayout.PadTilt * Mathf.Deg2Rad, size = LabBenchLayout.PadSize;
            var normal = new Vector3(0f, Mathf.Cos(tilt), -Mathf.Sin(tilt));   // hacia el jugador y arriba
            var slopeUp = new Vector3(0f, Mathf.Sin(tilt), Mathf.Cos(tilt));
            var padGo = Child(front, "LabSketchPad", LabBenchLayout.PadCenter);
            var board = Child(padGo.transform, "Board", Vector3.zero).transform;
            board.localRotation = Quaternion.LookRotation(-normal, slopeUp); // +Z hacia adentro del atril, +Y subiendo

            // Caballete (marco de madera, respaldo y patas hasta la mesa) en el marco del tablero.
            var e = new LowPolyMesh();
            float frame = size + 0.05f;
            e.Box(Matrix4x4.Translate(new Vector3(0f, 0f, 0.02f)), new Vector3(frame, frame, 0.02f), WoodB);
            foreach (float sx in new[] { -1f, 1f })
                e.Box(Matrix4x4.Translate(new Vector3(sx * (frame * 0.5f - 0.0125f), 0f, 0f)), new Vector3(0.025f, frame, 0.022f), WoodA, Brass);
            e.Box(Matrix4x4.Translate(new Vector3(0f, -(frame * 0.5f - 0.0125f), -0.004f)), new Vector3(frame, 0.025f, 0.03f), WoodA, Brass);
            // Copete con el titulo, arriba del papel.
            e.Box(Matrix4x4.Translate(new Vector3(0f, size * 0.5f + 0.05f, 0.006f)), new Vector3(frame, 0.06f, 0.02f), WoodA);
            var easel = Part("Easel", board, Save("Lab_SketchEasel", e), material, Vector3.zero, Quaternion.identity);
            // Patas: del borde de abajo del atril a la mesa (en el marco del frente).
            var legs = new LowPolyMesh();
            Vector3 bottom = LabBenchLayout.PadCenter - slopeUp * (size * 0.5f) + new Vector3(0f, 0f, 0.03f);
            Vector3 top = LabBenchLayout.PadCenter + slopeUp * (size * 0.3f) + new Vector3(0f, 0f, 0.05f);
            foreach (float sx in new[] { -0.13f, 0.13f })
            {
                Beam(legs, new Vector3(sx, DeskTop, bottom.z - 0.02f), new Vector3(sx, bottom.y, bottom.z), 0.025f, WoodB);
                Beam(legs, new Vector3(sx * 0.6f, DeskTop, top.z + 0.1f), new Vector3(sx * 0.6f, top.y, top.z), 0.025f, WoodB);
            }
            Part("Legs", front, Save("Lab_SketchLegs", legs), material, Vector3.zero, Quaternion.identity);

            // Collider del rayo en un hijo sin DrawingCanvas: ahi va el LabSketchPad (lo resalta el interactor) y el lienzo cuelga
            // de el (SpellCaster encuentra el ISealGate por GetComponentInParent y no lo auto-lanza).
            var surfaceGo = Child(board, "Surface", Vector3.zero);
            var box = surfaceGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0f, 0.005f);
            box.size = new Vector3(size, size, 0.03f);

            // Lienzo real: centro de la malla sobre el centro del atril, un poco adelante del marco.
            Vector3 center = front.TransformPoint(LabBenchLayout.PadCenter + normal * 0.004f);
            DrawingCanvas canvas = ClonePaper(surfaceGo.transform, "LabSketchCanvas", size, center, Quaternion.identity, false);
            if (canvas != null)
            {
                MeshFilter mf = canvas.GetComponent<MeshFilter>();
                Vector3 surface = SurfaceNormal(mf);
                Quaternion rot = LabSealBenchMath.FaceRotation(surface, front.TransformDirection(normal), front.TransformDirection(slopeUp));
                canvas.transform.rotation = rot;
                Vector3 offset = mf != null && mf.sharedMesh != null ? Vector3.Scale(mf.sharedMesh.bounds.center, canvas.transform.lossyScale) : Vector3.zero;
                canvas.transform.position = center - rot * offset;
            }
            else Debug.LogWarning("[MapLab] falta PaperCanvas: el atril de dibujo no tiene lienzo.");

            // Titulo del atril (mira al jugador: −Z del tablero).
            var title = Text(board, "Title", new Vector3(0f, size * 0.5f + 0.05f, -0.004f - LabPanelLayout.TextLift), Quaternion.identity,
                new Vector2(frame - 0.03f, 0.05f), 0.2f, TextLight, "Dibujá acá (opcional)");
            title.fontStyle = FontStyles.Bold;

            var pad = surfaceGo.AddComponent<LabSketchPad>();
            pad.Configure(canvas, easel.GetComponent<MeshRenderer>());
            EditorUtility.SetDirty(pad);
            return pad;
        }

        /// <summary>Normal de la cara del lienzo segun su malla (primera normal), como DrawingCanvas.Awake.</summary>
        private static Vector3 SurfaceNormal(MeshFilter mf)
        {
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.normals.Length == 0) return Vector3.back;
            return mf.sharedMesh.normals[0];
        }

        // ---------------------------------------------------------------- paneles en arco

        /// <summary>
        /// Panel empinado del arco (LabBenchLayout): gabinete, losa inclinada con rieles de laton y un copete arriba con el
        /// titulo y la tira de ayuda (LabHelpStrip del panel). Devuelve el marco de la losa (+X ancho, +Z subiendo, +Y normal);
        /// el que llama agrega franjas a <paramref name="body"/> y termina con <see cref="FinishFacet"/>.
        /// </summary>
        private static Transform Facet(Transform bench, Material material, int index, string name, string title, string idle, out LowPolyMesh body)
        {
            var wing = Child(bench, name, Vector3.zero).transform;
            wing.localRotation = Quaternion.Euler(0f, LabBenchLayout.FacetYaw(index), 0f);
            float depth = LabBenchLayout.FacetDepth, length = FacetSlope + CrestSlope;
            float t = FacetTilt * Mathf.Deg2Rad;

            var w = new LowPolyMesh();
            w.Box(Matrix4x4.Translate(new Vector3(0f, (FacetBottom - 0.03f) * 0.5f, FacetNear + 0.04f + depth * 0.5f)),
                new Vector3(FacetWidth - 0.06f, FacetBottom - 0.03f, depth), WoodA, Plank);
            Vector3 topEdge = new Vector3(0f, FacetBottom + length * Mathf.Sin(t), FacetNear + length * Mathf.Cos(t)) + new Vector3(0f, -0.02f, 0.03f);
            foreach (float sx in new[] { -FacetWidth * 0.5f + 0.05f, FacetWidth * 0.5f - 0.05f })
                Beam(w, new Vector3(sx, FacetBottom - 0.03f, FacetNear + depth + 0.03f), new Vector3(sx, topEdge.y, topEdge.z), 0.03f, WoodB);
            Part("Body", wing, Save("Lab_FacetBody", w), material, Vector3.zero, Quaternion.identity);

            var panel = Child(wing, "Panel", new Vector3(0f, FacetBottom, FacetNear)).transform;
            panel.localRotation = Quaternion.Euler(-FacetTilt, 0f, 0f);
            body = new LowPolyMesh();
            body.Box(Matrix4x4.Translate(new Vector3(0f, -0.02f, length * 0.5f)), new Vector3(FacetWidth, 0.04f, length), WoodB, PanelColor);
            // Rieles de laton a los lados (marco biselado) y bordes de abajo y arriba.
            foreach (float sx in new[] { -1f, 1f })
                body.Box(Matrix4x4.Translate(new Vector3(sx * (FacetWidth * 0.5f - 0.004f), 0.004f, length * 0.5f)), new Vector3(0.008f, 0.016f, length), Brass);
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0.004f, -0.006f)), new Vector3(FacetWidth + 0.01f, 0.024f, 0.016f), Brass);
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0.004f, length + 0.006f)), new Vector3(FacetWidth + 0.01f, 0.024f, 0.016f), Brass);
            // Copete oscuro para el titulo y la ayuda.
            const float strip = LabPanelLayout.SurfaceTop;
            body.Box(Matrix4x4.Translate(new Vector3(0f, strip * 0.5f, FacetSlope + CrestSlope * 0.5f)), new Vector3(FacetWidth - 0.02f, strip, CrestSlope - 0.01f), CrestColor);

            float textY = LabPanelLayout.TextY(LabPanelLayout.SurfaceTop);
            var titleText = Text(panel, "Title", new Vector3(0f, textY, FacetSlope + CrestSlope - 0.028f), Lying, new Vector2(FacetWidth - 0.04f, 0.035f),
                LabPanelLayout.TitleFontSize, TextGold, title);
            titleText.fontStyle = FontStyles.Bold;
            var help = Text(panel, "Help", new Vector3(0f, textY, FacetSlope + 0.05f), Lying, new Vector2(FacetWidth - 0.04f, 0.085f), 0.15f, TextLight, idle);
            help.textWrappingMode = TextWrappingModes.Normal;
            help.alignment = TextAlignmentOptions.Top;
            var strip2 = wing.gameObject.AddComponent<LabHelpStrip>();
            strip2.Configure(help, wing, idle);
            EditorUtility.SetDirty(strip2);
            return panel;
        }

        private static void FinishFacet(Transform panel, Material material, LowPolyMesh body, string meshName)
            => Part("PanelBody", panel, Save(meshName, body), material, Vector3.zero, Quaternion.identity);

        /// <summary>Franja de color de un grupo entre <paramref name="sFrom"/> y <paramref name="sTo"/> y su titulo en el margen izquierdo.</summary>
        private static TextMeshPro Band(Transform panel, LowPolyMesh body, float sFrom, float sTo, Color color, string title)
        {
            const float strip = LabPanelLayout.SurfaceTop;
            float length = sTo - sFrom;
            body.Box(Matrix4x4.Translate(new Vector3(0f, strip * 0.5f, (sFrom + sTo) * 0.5f)), new Vector3(FacetWidth - 0.02f, strip, length - 0.006f),
                Color.Lerp(PanelColor, color, 0.32f));
            // Filete del color del grupo junto al margen (ayuda a ver donde empieza cada grupo).
            body.Box(Matrix4x4.Translate(new Vector3(LabBenchLayout.SpineX + 0.017f, strip + 0.001f, (sFrom + sTo) * 0.5f)), new Vector3(0.004f, 0.002f, length - 0.02f), color);
            if (string.IsNullOrEmpty(title)) return null;
            var text = Text(panel, "Group_" + Mathf.RoundToInt(sFrom * 1000f), new Vector3(LabBenchLayout.SpineX, LabPanelLayout.TextY(strip), (sFrom + sTo) * 0.5f),
                Spine, new Vector2(length - 0.01f, 0.03f), 0.18f, color, title);
            text.fontStyle = FontStyles.Bold;
            return text;
        }

        private static Vector3 At(float x, float s) => new Vector3(x, LabPanelLayout.SurfaceTop, s);

        private static float Col(int i) => LabBenchLayout.ColumnX[i];
        private static float Row(int i) => LabBenchLayout.RowS[i];

        /// <summary>Izquierda adentro: «1 Hechizo» (receta y grilla de sigilos) y «2 Signos» (grilla de signos, cantidad, angulo, sentido).</summary>
        private static void SealFacetInner(Transform bench, Material material, LabSealBench sealBench, Material iconMaterial)
        {
            Transform p = Facet(bench, material, LabBenchLayout.LeftInner, "LabFacet_Seal", "Armá el sello",
                "1 Elegí un hechizo · 2 Ajustá lo que quieras · 3 Tocá LANZAR. Apuntá un control para ver qué hace.", out LowPolyMesh body);
            var g1 = new Tone(LabPalette.Recipe, "Recipe");
            var g2 = new Tone(LabPalette.Signs, "Signs");
            float cell = LabBenchLayout.CellPitch;
            // Grupo 1: receta arriba y grilla de sigilos (2 filas) debajo.
            const float recipeS = 0.9f, sigilS = 0.7225f, signS = 0.5f, countS = 0.28f, turnS = 0.11f;
            Band(p, body, recipeS - 0.08f, recipeS + 0.08f, g1.Color, "1 Hechizo");
            Control(p, material, new Ctl(LabSealVarId.BaseRecipe, LabControlKind.Selector, "Receta"), g1, At(0f, recipeS), sealBench);
            TextMeshPro sigilName = Band(p, body, sigilS - cell - 0.003f, sigilS + cell + 0.003f, g1.Color, "Sigilo");
            Grid(p, material, LabSealVarId.Sigil, sigilS, 2, "Sigilo", sigilName, sealBench, iconMaterial);
            // Grupo 2: grilla de signos (3 filas), cantidad y angulo, sentido.
            TextMeshPro signName = Band(p, body, signS - 1.5f * cell - 0.003f, signS + 1.5f * cell + 0.003f, g2.Color, "2 Signos");
            Grid(p, material, LabSealVarId.SignType, signS, 3, "Signo", signName, sealBench, iconMaterial);
            Band(p, body, turnS - 0.08f, countS + 0.08f, g2.Color, null);
            Control(p, material, new Ctl(LabSealVarId.SignCount, LabControlKind.Selector, "Cantidad"), g2, At(Col(0), countS), sealBench);
            Control(p, material, new Ctl(LabSealVarId.BaseAngle, LabControlKind.Knob, "Ángulo"), g2, At(Col(1), countS), sealBench);
            Control(p, material, new Ctl(LabSealVarId.TurnDeg, LabControlKind.Selector, "Sentido"), g2, At(0f, turnS), sealBench);
            FinishFacet(p, material, body, "Lab_FacetSealInner");
        }

        /// <summary>Izquierda afuera: «3 Papel y tinta» (tinta, pluma, sentido del disparo) y «4 Afinado» (geometria y prolijidad).</summary>
        private static void SealFacetOuter(Transform bench, Material material, LabSealBench sealBench)
        {
            Transform p = Facet(bench, material, LabBenchLayout.LeftOuter, "LabFacet_Tuning", "Tinta y afinado",
                "Opcional: cambiá tinta y pluma; abajo, el afinado fino del sello. Doble clic: valor inicial.", out LowPolyMesh body);
            var g3 = new Tone(LabPalette.Ink, "Ink");
            var g4 = new Tone(LabPalette.Tuning, "Tuning");
            Band(p, body, Row(1) - 0.08f, Row(0) + 0.08f, g3.Color, "3 Papel y tinta");
            Control(p, material, new Ctl(LabSealVarId.Ink, LabControlKind.Selector, "Tinta"), g3, At(Col(0), Row(0)), sealBench);
            Control(p, material, new Ctl(LabSealVarId.PenWidth, LabControlKind.Selector, "Pluma"), g3, At(Col(1), Row(0)), sealBench);
            Control(p, material, new Ctl(LabSealVarId.PaperFace, LabControlKind.Selector, "Disparo"), g3, At(0f, Row(1)), sealBench);
            Band(p, body, Row(5) - 0.08f, Row(2) + 0.08f, g4.Color, "4 Afinado");
            Control(p, material, new Ctl(LabSealVarId.SigilScale, LabControlKind.Slider, "Tamaño"), g4, At(Col(0), Row(2)), sealBench);
            Control(p, material, new Ctl(LabSealVarId.RingFill, LabControlKind.Slider, "Llenado"), g4, At(Col(1), Row(2)), sealBench);
            Control(p, material, new Ctl(LabSealVarId.RingGap, LabControlKind.Knob, "Hueco"), g4, At(Col(0), Row(3)), sealBench);
            Control(p, material, new Ctl(LabSealVarId.SignLength, LabControlKind.Slider, "Largo"), g4, At(Col(1), Row(3)), sealBench);
            Control(p, material, new Ctl(LabSealVarId.Asymmetry, LabControlKind.Slider, "Asimetría"), g4, At(Col(0), Row(4)), sealBench);
            Control(p, material, new Ctl(LabSealVarId.SignDistance, LabControlKind.Slider, "Distancia"), g4, At(Col(1), Row(4)), sealBench);
            Control(p, material, new Ctl(LabSealVarId.Tremor, LabControlKind.Slider, "Temblor"), g4, At(0f, Row(5)), sealBench);
            FinishFacet(p, material, body, "Lab_FacetSealOuter");
        }

        /// <summary>Derecha adentro: «Objetos del campo»: + Caja / Borrar elegido / Borrar todo, lista de casillas y controles del elegido.</summary>
        private static void ObjectsFacet(Transform bench, Material material, LabSealBench sealBench)
        {
            Transform p = Facet(bench, material, LabBenchLayout.RightInner, "LabFacet_Objects", "Objetos del campo",
                "1 Agregá una caja o elegí una casilla (P = papel) · 2 Movela con los controles de abajo.", out LowPolyMesh body);
            var tone = new Tone(LabPalette.Paper, "Field");
            float bp = LabBenchLayout.ButtonPitch, bw = LabBenchLayout.ButtonWidth, bd = LabBenchLayout.ButtonDepth;
            Band(p, body, Row(0) - 0.07f, Row(0) + 0.07f, LabPalette.Neutral, null);
            MaterializeButton(p, material, LabSpawnKind.Box, "+ Caja", BoxGroup, At(-bp, Row(0)), ButtonIcon.Box);
            var del = Button(p, material, "LabDeleteSelected", "Lab_ButtonDeleteSelected", "Borrar\nelegido", DeleteButton, At(0f, Row(0)), bw, bd, 0.15f, ButtonIcon.Cross);
            del.AddComponent<LabBenchButton>().Configure(LabBenchAction.DeleteSelected, sealBench, del.GetComponent<MeshRenderer>());
            var all = Button(p, material, "LabDeleteAll", "Lab_ButtonDeleteAll", "Borrar\ntodo", DeleteButton, At(bp, Row(0)), bw, bd, 0.15f, ButtonIcon.Cross);
            all.AddComponent<LabBenchButton>().Configure(LabBenchAction.DeleteAll, sealBench, all.GetComponent<MeshRenderer>());
            EditorUtility.SetDirty(del.GetComponent<LabBenchButton>());
            EditorUtility.SetDirty(all.GetComponent<LabBenchButton>());

            const float slotS = 0.765f, r0 = 0.62f, r1 = 0.46f, r2 = 0.3f, r3 = 0.14f;
            Band(p, body, slotS - 0.05f, slotS + 0.05f, tone.Color, "Elegí");
            FieldSlots(p, material, slotS);
            Band(p, body, r3 - 0.08f, r0 + 0.08f, tone.Color, "El elegido");
            Control(p, material, new Ctl(LabFieldVar.Preset, LabControlKind.Selector, "Atajo"), tone, At(Col(0), r0), sealBench);
            Control(p, material, new Ctl(LabFieldVar.Size, LabControlKind.Selector, "Tam. papel"), tone, At(Col(1), r0), sealBench);
            Control(p, material, new Ctl(LabFieldVar.X, LabControlKind.Slider, "Lado"), tone, At(Col(0), r1), sealBench);
            Control(p, material, new Ctl(LabFieldVar.Distance, LabControlKind.Slider, "Distancia"), tone, At(Col(1), r1), sealBench);
            Control(p, material, new Ctl(LabFieldVar.Yaw, LabControlKind.Knob, "Giro"), tone, At(Col(0), r2), sealBench);
            Control(p, material, new Ctl(LabFieldVar.Height, LabControlKind.Slider, "Altura"), tone, At(Col(1), r2), sealBench);
            Control(p, material, new Ctl(LabFieldVar.Tilt, LabControlKind.Knob, "Inclinac."), tone, At(0f, r3), sealBench);
            FinishFacet(p, material, body, "Lab_FacetObjects");
        }

        /// <summary>Derecha afuera: «Viento y fuego»: + Viento / + Fuego / Reiniciar entorno, perillas del viento, del fuego y la masa de la caja.</summary>
        private static void EnvFacet(Transform bench, Material material, LabSealBench sealBench)
        {
            Transform p = Facet(bench, material, LabBenchLayout.RightOuter, "LabFacet_Env", "Viento y fuego",
                "Agregá viento o fuego y ajustalos en vivo. La masa vale para la caja elegida y las nuevas.", out LowPolyMesh body);
            var wind = new Tone(WindGroup, "Wind");
            var fire = new Tone(FireGroup, "Fire");
            var box = new Tone(BoxGroup, "Box");
            float bp = LabBenchLayout.ButtonPitch, bw = LabBenchLayout.ButtonWidth, bd = LabBenchLayout.ButtonDepth;
            Band(p, body, Row(0) - 0.07f, Row(0) + 0.07f, LabPalette.Neutral, null);
            MaterializeButton(p, material, LabSpawnKind.Wind, "+ Viento", WindGroup, At(-bp, Row(0)), ButtonIcon.Wind);
            MaterializeButton(p, material, LabSpawnKind.Fire, "+ Fuego", FireGroup, At(0f, Row(0)), ButtonIcon.Fire);
            var reset = Button(p, material, "LabReset", "Lab_ButtonReset", "Reiniciar\nentorno", NeutralButton, At(bp, Row(0)), bw, bd, 0.15f, ButtonIcon.Reset);
            Assign(reset.AddComponent<LabResetButton>(), "highlight", reset.GetComponent<MeshRenderer>());

            Band(p, body, Row(1) - 0.08f, Row(1) + 0.08f, wind.Color, "Viento");
            Control(p, material, new Ctl(LabVariableId.WindYaw, LabControlKind.Knob, "Dirección"), wind, At(Col(0), Row(1)), sealBench);
            Control(p, material, new Ctl(LabVariableId.WindSpeed, LabControlKind.Slider, "Velocidad"), wind, At(Col(1), Row(1)), sealBench);
            Band(p, body, Row(3) - 0.08f, Row(2) + 0.08f, fire.Color, "Fuego");
            Control(p, material, new Ctl(LabVariableId.FireSeedRadius, LabControlKind.Slider, "Semilla"), fire, At(Col(0), Row(2)), sealBench);
            Control(p, material, new Ctl(LabVariableId.FireDecay, LabControlKind.Slider, "Merma"), fire, At(Col(1), Row(2)), sealBench);
            Control(p, material, new Ctl(LabVariableId.FireMaxGenerations, LabControlKind.Knob, "Generac."), fire, At(Col(0), Row(3)), sealBench);
            Band(p, body, Row(4) - 0.08f, Row(4) + 0.08f, box.Color, "Caja");
            Control(p, material, new Ctl(LabVariableId.Mass, LabControlKind.Selector, "Masa caja"), box, At(0f, Row(4)), sealBench);
            FinishFacet(p, material, body, "Lab_FacetEnv");
        }

        /// <summary>
        /// Grilla de iconos (LabGlyphGrid): las celdas las arma el runtime con el diccionario; aca el marco dorado de la elegida
        /// y la configuracion. <paramref name="nameText"/> = titulo del margen, que pasa a mostrar «Sigilo: Fuego».
        /// </summary>
        private static void Grid(Transform panel, Material material, LabSealVarId variable, float sCenter, int rows, string prefix,
            TextMeshPro nameText, LabSealBench bench, Material iconMaterial)
        {
            var go = Child(panel, "LabGrid_" + variable, At(0f, sCenter));
            var frame = Part("Selection", go.transform, SelectionFrameMesh(), material, new Vector3(0f, 0.0005f, 0f), Quaternion.identity);
            frame.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var grid = go.AddComponent<LabGlyphGrid>();
            grid.Configure(variable, bench, iconMaterial, frame.transform, nameText, rows, prefix);
            EditorUtility.SetDirty(grid);
        }

        /// <summary>Lista de objetos del campo: casilla «P» (papel) y 1..4 (cajas), contiguas, ≥ 8,5 cm cada una.</summary>
        private static void FieldSlots(Transform panel, Material material, float s)
        {
            float cell = LabBenchLayout.CellPitch;
            for (int i = 0; i < LabField.SlotCount; i++)
            {
                var go = Part("LabFieldSlot_" + i, panel, SlotMesh(i == 0), material,
                    At((i - (LabField.SlotCount - 1) * 0.5f) * cell, s), Quaternion.identity);
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.015f, 0f);
                col.size = new Vector3(cell, 0.03f, cell);
                var label = Text(go.transform, "Label", new Vector3(0f, LabPanelLayout.TextY(0.01f), 0f), Lying, new Vector2(cell - 0.01f, cell - 0.01f),
                    0.34f, TextDark, i == 0 ? "P" : i.ToString());
                label.fontStyle = FontStyles.Bold;
                go.AddComponent<LabFieldSlot>().Configure(i, go.GetComponent<MeshRenderer>());
                EditorUtility.SetDirty(go.GetComponent<LabFieldSlot>());
            }
        }

        /// <summary>
        /// Un control: collider ≥ 9 cm, pieza movil (tapa de perilla/selector o mango del deslizador; es el primer renderer, el
        /// que se resalta al apuntar), rotulo oscuro sobre plaquita crema arriba y lectura clara en ventanita oscura abajo.
        /// LabControl + LabControlBinding (enlace en runtime). <paramref name="at"/>.y es el tope de la franja.
        /// </summary>
        private static void Control(Transform panel, Material material, Ctl c, Tone tone, Vector3 at, LabSealBench bench)
        {
            if (!LabPanelLayout.LabelFits(c.Label))
                Debug.LogWarning($"[MapLab] el rotulo «{c.Label}» pasa de {LabPanelLayout.MaxLabelChars} caracteres: se corta con «…».");
            var go = Child(panel, "LabCtl_" + c.Key, at);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.03f, 0f);

            Marks(c, out int ticks, out float defaultFraction);
            Transform movable, railStart = null, railEnd = null;
            if (c.Kind == LabControlKind.Slider)
            {
                col.size = new Vector3(RailHalf * 2f + 0.04f, 0.06f, 0.095f);
                var handle = Part("Handle", go.transform, HandleMesh(tone), material, new Vector3(0f, RailY, 0f), Quaternion.identity);
                Part("Rail", go.transform, RailMesh(ticks, defaultFraction), material, Vector3.zero, Quaternion.identity);
                railStart = Child(go.transform, "RailStart", new Vector3(-RailHalf, RailY, 0f)).transform;
                railEnd = Child(go.transform, "RailEnd", new Vector3(RailHalf, RailY, 0f)).transform;
                movable = handle.transform;
            }
            else
            {
                col.size = new Vector3(0.095f, 0.06f, 0.095f);
                // LabControl gira la pieza sobre su Z local: el pivote apunta su Z a la normal del panel; la tapa se gira de vuelta.
                var pivot = Child(go.transform, "Pivot", Vector3.zero).transform;
                pivot.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                Part("Cap", pivot, c.Kind == LabControlKind.Selector ? SelectorMesh(tone) : KnobMesh(tone), material, Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
                Part("Dial", go.transform, DialMesh(ticks, defaultFraction), material, Vector3.zero, Quaternion.identity);
                movable = pivot;
            }

            // Plaquita y ventanita despues de la pieza movil (LabControl resalta el primer renderer). Alturas relativas a la franja.
            Part("Plaque", go.transform, PlaqueMesh(), material, new Vector3(0f, 0f, LabelZ), Quaternion.identity);
            Part("Window", go.transform, WindowMesh(), material, new Vector3(0f, 0f, ReadoutZ), Quaternion.identity);
            float textY = LabPanelLayout.TextY(LabPanelLayout.PlaqueTop) - LabPanelLayout.SurfaceTop;
            var label = Text(go.transform, "Label", new Vector3(0f, textY, LabelZ), Lying, new Vector2(PlaqueWidth - 0.01f, LabelDepth),
                LabPanelLayout.LabelFontSize, TextDark, c.Label);
            label.fontStyle = FontStyles.Bold;
            var readout = Text(go.transform, "Readout", new Vector3(0f, textY, ReadoutZ), Lying, new Vector2(PlaqueWidth - 0.01f, ReadoutDepth),
                LabPanelLayout.ReadoutFontSize, TextLight, "-");

            go.AddComponent<LabControl>();
            var binding = go.AddComponent<LabControlBinding>();
            binding.Configure(c.Source, c.Var, c.Kind, movable, railStart, railEnd, readout, bench);
            EditorUtility.SetDirty(binding);
            controlCount++;
        }

        private static Mesh KnobMesh(Tone tone) => Cached("Lab_Knob_" + tone.Key, () =>
        {
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.identity, KnobRadius, KnobRadius, 0f, 0.012f, 10, Darken(tone.Color, 0.6f));
            b.Frustum(Matrix4x4.identity, KnobRadius - 0.005f, KnobRadius - 0.012f, 0.012f, 0.038f, 10, tone.Color);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.039f, 0.015f)), new Vector3(0.009f, 0.004f, 0.022f), TextLight);
            return b;
        });

        private static Mesh SelectorMesh(Tone tone) => Cached("Lab_Selector_" + tone.Key, () =>
        {
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.identity, 0.036f, 0.036f, 0f, 0.012f, 10, Darken(tone.Color, 0.6f));
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.026f, 0.003f)), new Vector3(0.024f, 0.03f, 0.07f), Darken(tone.Color, 0.85f), tone.Color);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.043f, 0.024f)), new Vector3(0.012f, 0.004f, 0.018f), TextLight);
            return b;
        });

        private static Mesh HandleMesh(Tone tone) => Cached("Lab_Handle_" + tone.Key, () =>
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.014f, 0f)), new Vector3(0.026f, 0.028f, 0.046f), Darken(tone.Color, 0.8f), tone.Color);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.029f, 0f)), new Vector3(0.006f, 0.003f, 0.036f), TextLight);
            return b;
        });

        /// <summary>
        /// Muescas de un control ((c).7): <paramref name="ticks"/> = cantidad de muescas a dibujar (0 = solo min/max; listas que
        /// dependen del diccionario o de las recetas no tienen tamaño fijo) y <paramref name="defaultFraction"/> = posicion del
        /// valor por defecto en 0..1 (−1 = sin muesca dorada). Mismos rangos que LabControlBinding.
        /// </summary>
        private static void Marks(Ctl c, out int ticks, out float defaultFraction)
        {
            ticks = 0;
            defaultFraction = -1f;
            if (c.Source == LabControlSource.Environment)
            {
                LabVariableDef env = LabCatalog.Get((LabVariableId)c.Var);
                if (env.HasSteps)
                {
                    ticks = env.Steps.Length;
                    defaultFraction = Nearest(env.Steps, env.Default) / (float)Mathf.Max(1, env.Steps.Length - 1);
                }
                else
                {
                    defaultFraction = Mathf.InverseLerp(env.Min, env.Max, env.Default);
                    if (env.Step > 0f)
                    {
                        float k = (env.Default - env.Min) / env.Step;
                        if (Mathf.Abs(k - Mathf.Round(k)) < 1e-3f) ticks = Mathf.RoundToInt((env.Max - env.Min) / env.Step) + 1;
                    }
                }
            }
            else
            {
                var id = c.Source == LabControlSource.Field ? LabField.SealVar((LabFieldVar)c.Var) : (LabSealVarId)c.Var;
                LabSealVarDef def = LabSealCatalog.Get(id);
                switch (id)
                {
                    case LabSealVarId.BaseRecipe:
                    case LabSealVarId.Sigil:
                    case LabSealVarId.SignType:
                        break; // rango del diccionario/recetas: se conoce en runtime
                    case LabSealVarId.PaperSize:
                        ticks = LabSealSpec.PaperSizes.Length;
                        defaultFraction = Nearest(LabSealSpec.PaperSizes, def.Default) / (float)(ticks - 1);
                        break;
                    case LabSealVarId.Ink:
                        ticks = LabSealBenchMath.InkNames.Length;
                        defaultFraction = def.Default / Mathf.Max(1, ticks - 1);
                        break;
                    default:
                        defaultFraction = Mathf.InverseLerp(def.Min, def.Max, def.Default);
                        if (def.Kind != LabSealVarKind.Continuous && def.Step > 0f) ticks = Mathf.RoundToInt((def.Max - def.Min) / def.Step) + 1;
                        break;
                }
            }
            if (ticks > MaxTicks || ticks < 2) ticks = 0;
        }

        private static int Nearest(float[] values, float value)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
                if (Mathf.Abs(values[i] - value) < Mathf.Abs(values[best] - value)) best = i;
            return best;
        }

        /// <summary>
        /// Base de la perilla/selector sobre el recorrido de LabControl (−120°..+120°): muescas grandes de bronce en el minimo y el
        /// maximo, chicas en cada paso y una dorada en el valor por defecto. Todas terminan antes de r = 0.053 para no meterse
        /// bajo el rotulo (plaquita desde z = 0.054).
        /// </summary>
        private static Mesh DialMesh(int ticks, float defaultFraction) =>
            Cached($"Lab_Dial_{ticks}_{Mathf.RoundToInt(defaultFraction * 1000f)}", () =>
        {
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.identity, DialRadius, DialRadius, 0f, 0.004f, 12, DialColor);
            for (int i = 1; i < ticks - 1; i++)
                DialTick(b, Mathf.Lerp(-120f, 120f, i / (float)(ticks - 1)), 0.047f, new Vector3(0.004f, 0.005f, 0.008f), Brass);
            DialTick(b, -120f, 0.048f, new Vector3(0.007f, 0.006f, 0.008f), Brass);
            DialTick(b, 120f, 0.048f, new Vector3(0.007f, 0.006f, 0.008f), Brass);
            if (defaultFraction >= 0f)
                DialTick(b, Mathf.Lerp(-120f, 120f, defaultFraction), 0.048f, new Vector3(0.008f, 0.008f, 0.008f), DefaultMark);
            return b;
        });

        private static void DialTick(LowPolyMesh b, float angle, float radius, Vector3 size, Color color)
        {
            float r = angle * Mathf.Deg2Rad;
            b.Box(Matrix4x4.TRS(new Vector3(Mathf.Sin(r) * radius, size.y * 0.5f, Mathf.Cos(r) * radius), Quaternion.Euler(0f, angle, 0f), Vector3.one), size, color);
        }

        /// <summary>Plaquita crema del rotulo: apoya sobre la franja y sube LabPanelLayout.PlaqueHeight (3 mm).</summary>
        private static Mesh PlaqueMesh() => Cached("Lab_Plaque_v4", () =>
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, LabPanelLayout.PlaqueHeight * 0.5f, 0f)), new Vector3(PlaqueWidth, LabPanelLayout.PlaqueHeight, LabelDepth), PlaqueCream);
            return b;
        });

        /// <summary>Ventanita oscura de la lectura, a la misma altura que la plaquita.</summary>
        private static Mesh WindowMesh() => Cached("Lab_ReadoutWindow_v4", () =>
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, LabPanelLayout.PlaqueHeight * 0.5f, 0f)), new Vector3(PlaqueWidth, LabPanelLayout.PlaqueHeight, ReadoutDepth), WindowDark);
            return b;
        });

        /// <summary>Riel del deslizador: topes de bronce en el minimo y el maximo, muescas en cada paso (si hay) y una dorada en el valor por defecto.</summary>
        private static Mesh RailMesh(int ticks, float defaultFraction) =>
            Cached($"Lab_Rail_{ticks}_{Mathf.RoundToInt(defaultFraction * 1000f)}", () =>
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.004f, 0f)), new Vector3(RailHalf * 2f + 0.03f, 0.008f, 0.02f), DialColor);
            foreach (float sx in new[] { -1f, 1f })
                b.Box(Matrix4x4.Translate(new Vector3(sx * (RailHalf + 0.012f), 0.006f, 0f)), new Vector3(0.008f, 0.012f, 0.026f), Brass);
            for (int i = 0; i < ticks; i++)
                b.Box(Matrix4x4.Translate(new Vector3(Mathf.Lerp(-RailHalf, RailHalf, i / (float)(ticks - 1)), 0.004f, -0.016f)), new Vector3(0.003f, 0.008f, 0.008f), Brass);
            if (defaultFraction >= 0f)
                b.Box(Matrix4x4.Translate(new Vector3(Mathf.Lerp(-RailHalf, RailHalf, defaultFraction), 0.005f, -0.019f)), new Vector3(0.006f, 0.01f, 0.014f), DefaultMark);
            return b;
        });

        /// <summary>Marco dorado (brilla con el bloom) de la celda elegida de una grilla: un poco mas grande que el icono y debajo de el.</summary>
        private static Mesh SelectionFrameMesh() => Cached("Lab_GridSelection", () =>
        {
            var b = new LowPolyMesh();
            float c = LabBenchLayout.CellPitch, bar = 0.008f, h = 0.004f;
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(Matrix4x4.Translate(new Vector3(0f, h * 0.5f, s * (c * 0.5f - bar * 0.5f))), new Vector3(c, h, bar), RingGlow);
                b.Box(Matrix4x4.Translate(new Vector3(s * (c * 0.5f - bar * 0.5f), h * 0.5f, 0f)), new Vector3(bar, h, c), RingGlow);
            }
            return b;
        });

        /// <summary>Casilla de la lista de objetos (placa con borde; la tiñe LabFieldSlot).</summary>
        private static Mesh SlotMesh(bool paper) => Cached(paper ? "Lab_SlotPaper" : "Lab_SlotBox", () =>
        {
            var b = new LowPolyMesh();
            float c = LabBenchLayout.CellPitch - 0.008f;
            Color face = paper ? Color.Lerp(LabPalette.Paper, Color.white, 0.55f) : BoxGroup;
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.005f, 0f)), new Vector3(c, 0.01f, c), Darken(face, 0.7f), face);
            return b;
        });

        /// <summary>Poste del papel del campo: alto 1 m en Y con el pivote abajo (el banco lo estira hasta el papel).</summary>
        private static Mesh StandMesh() => Cached("Lab_PaperStand", () =>
        {
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.identity, 0.03f, 0.022f, 0f, 1f, 6, WoodB);
            b.Frustum(Matrix4x4.identity, 0.12f, 0.1f, 0f, 0.03f, 8, StoneB, true);
            return b;
        });

        /// <summary>Aro luminoso del objeto elegido (radio 0,6 m) con cuatro marcas hacia adentro.</summary>
        private static Mesh SelectionRingMesh() => Cached("Lab_SelectionRing", () =>
        {
            var b = new LowPolyMesh();
            const int segments = 24;
            const float r = 0.6f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
                b.Box(Matrix4x4.TRS((p0 + p1) * 0.5f, Quaternion.LookRotation(p1 - p0), Vector3.one), new Vector3(0.04f, 0.02f, (p1 - p0).magnitude + 0.01f), RingGlow);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.Box(Matrix4x4.TRS(dir * (r - 0.09f), Quaternion.LookRotation(dir), Vector3.one), new Vector3(0.03f, 0.02f, 0.12f), RingGlow);
            }
            return b;
        });

        // ---------------------------------------------------------------- botones

        private enum ButtonIcon { None, Box, Wind, Fire, Reset, Cross, Eraser }

        private static void MaterializeButton(Transform parent, Material material, LabSpawnKind kind, string text, Color color, Vector3 local, ButtonIcon icon)
        {
            var go = Button(parent, material, "LabMaterialize_" + kind, "Lab_Button" + kind, text, color, local,
                LabBenchLayout.ButtonWidth, LabBenchLayout.ButtonDepth, 0.16f, icon);
            var button = go.AddComponent<LabMaterializeButton>();
            AssignInt(button, "kind", (int)kind);
            Assign(button, "highlight", go.GetComponent<MeshRenderer>());
        }

        /// <summary>
        /// Boton plano (base de madera + tapa de color) con icono grande opcional y rotulo acostado (tamaño fijo, ≥ 1,5 cm) y
        /// collider disparador. <paramref name="iconAbove"/>: icono arriba y texto abajo (botones angostos); si no, icono a la izquierda.
        /// </summary>
        private static GameObject Button(Transform parent, Material material, string name, string meshName, string text, Color color,
            Vector3 local, float width, float depth, float fontSize, ButtonIcon icon, bool iconAbove = false)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.012f, 0f)), new Vector3(width, 0.024f, depth), ButtonBase);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.037f, 0f)), new Vector3(width - 0.02f, ButtonTop - 0.024f, depth - 0.02f), Darken(color, 0.85f), color);
            Vector3 iconAt = Vector3.zero;
            Vector2 textSize = new Vector2(width - 0.03f, depth - 0.025f);
            Vector3 textAt = Vector3.zero;
            if (icon != ButtonIcon.None)
            {
                if (iconAbove)
                {
                    iconAt = new Vector3(0f, ButtonTop, depth * 0.5f - 0.032f);
                    textAt = new Vector3(0f, 0f, -0.014f);
                    textSize = new Vector2(width - 0.012f, depth - 0.06f);
                }
                else
                {
                    iconAt = new Vector3(-width * 0.5f + 0.03f, ButtonTop, 0f);
                    textAt = new Vector3(0.022f, 0f, 0f);
                    textSize = new Vector2(width - 0.055f, depth - 0.02f);
                }
                Icon(b, icon, iconAt);
            }
            var go = Part(name, parent, Save(meshName, b), material, local, Quaternion.identity);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.04f, 0f);
            col.size = new Vector3(Mathf.Max(width, 0.09f), 0.08f, Mathf.Max(depth, 0.09f));
            var label = Text(go.transform, "Label", new Vector3(textAt.x, LabPanelLayout.TextY(ButtonTop), textAt.z), Lying, textSize, fontSize, TextDark, text);
            label.fontStyle = FontStyles.Bold;
            return go;
        }

        /// <summary>Icono acostado sobre la tapa (≈ 4 cm), en tinta oscura (o en color para fuego/caja).</summary>
        private static void Icon(LowPolyMesh b, ButtonIcon icon, Vector3 at)
        {
            Vector3 up = Vector3.up * 0.0015f;
            switch (icon)
            {
                case ButtonIcon.Reset:
                    ResetArc(b, at + up, 0.018f);
                    break;
                case ButtonIcon.Box:
                    b.Box(Matrix4x4.Translate(at + new Vector3(0f, 0.008f, 0f)), new Vector3(0.03f, 0.016f, 0.03f), Darken(BoxGroup, 0.55f), Darken(BoxGroup, 0.8f));
                    b.Box(Matrix4x4.Translate(at + new Vector3(0f, 0.0165f, 0f)), new Vector3(0.032f, 0.002f, 0.004f), TextDark);
                    break;
                case ButtonIcon.Wind:
                    for (int i = 0; i < 3; i++)
                        b.Box(Matrix4x4.Translate(at + new Vector3(-0.004f * i, 0.002f, 0.012f - 0.012f * i)), new Vector3(0.034f - 0.008f * i, 0.003f, 0.005f), TextDark);
                    break;
                case ButtonIcon.Fire:
                    b.Tri(at + new Vector3(-0.015f, 0.003f, -0.016f), at + new Vector3(0f, 0.003f, 0.02f), at + new Vector3(0.015f, 0.003f, -0.016f), new Color(0.8f, 0.2f, 0.08f));
                    b.Tri(at + new Vector3(-0.008f, 0.004f, -0.014f), at + new Vector3(0f, 0.004f, 0.006f), at + new Vector3(0.008f, 0.004f, -0.014f), new Color(1f, 0.85f, 0.3f));
                    break;
                case ButtonIcon.Cross:
                    foreach (float yaw in new[] { 45f, -45f })
                        b.Box(Matrix4x4.TRS(at + new Vector3(0f, 0.002f, 0f), Quaternion.Euler(0f, yaw, 0f), Vector3.one), new Vector3(0.006f, 0.003f, 0.036f), TextDark);
                    break;
                case ButtonIcon.Eraser:
                    b.Box(Matrix4x4.TRS(at + new Vector3(0f, 0.006f, 0f), Quaternion.Euler(0f, 30f, 0f), Vector3.one), new Vector3(0.034f, 0.012f, 0.016f), new Color(0.93f, 0.62f, 0.66f));
                    break;
            }
        }

        /// <summary>↺: arco de 300° con la punta al final (malla: la fuente TMP no trae la flecha circular).</summary>
        private static void ResetArc(LowPolyMesh b, Vector3 center, float radius)
        {
            const int segments = 9;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (50f + 300f * i / segments) * Mathf.Deg2Rad, a1 = (50f + 300f * (i + 1) / segments) * Mathf.Deg2Rad;
                Vector3 p0 = center + new Vector3(Mathf.Sin(a0) * radius, 0f, Mathf.Cos(a0) * radius);
                Vector3 p1 = center + new Vector3(Mathf.Sin(a1) * radius, 0f, Mathf.Cos(a1) * radius);
                b.Box(Matrix4x4.TRS((p0 + p1) * 0.5f, Quaternion.LookRotation(p1 - p0), Vector3.one), new Vector3(0.006f, 0.003f, (p1 - p0).magnitude + 0.002f), TextDark);
            }
            float end = 350f * Mathf.Deg2Rad;
            Vector3 tip = center + new Vector3(Mathf.Sin(end) * radius, 0f, Mathf.Cos(end) * radius);
            Vector3 along = new Vector3(Mathf.Cos(end), 0f, -Mathf.Sin(end)), outward = new Vector3(Mathf.Sin(end), 0f, Mathf.Cos(end));
            Vector3 lift = Vector3.up * 0.0015f;
            b.Tri(tip + outward * 0.011f + lift, tip + along * 0.014f + lift, tip - outward * 0.011f + lift, TextDark);
            b.Tri(tip - outward * 0.011f + lift, tip + along * 0.014f + lift, tip + outward * 0.011f + lift, TextDark);
        }

        // ---------------------------------------------------------------- cartel «Vas a lanzar» y datos con lentes

        /// <summary>
        /// Cartel fisico «Vas a lanzar» (se ve siempre): madera con marco biselado, pergamino, titulo con ornamentos, una fila por
        /// dato con un rombo del color de su grupo, y abajo la tira de ayuda de la mesa de adelante. Al lado flotan (solo con
        /// lentes, LabLensGate) la banda de resultado y la lectura del atril.
        /// </summary>
        private static void Cartel(Transform bench, Material material, LabSealBench sealBench, SpellCaster caster, Transform front)
        {
            Quaternion facing = Quaternion.LookRotation(Flat(CartelLocal));
            var go = Child(bench, "LabCartel", CartelLocal);
            go.transform.localRotation = facing;
            const float W = CartelWidth, H = CartelHeight, bottom = CartelBottom;
            float mid = bottom + H * 0.5f, top = bottom + H;
            var b = new LowPolyMesh();
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box(Matrix4x4.Translate(new Vector3(sx * (W * 0.5f + 0.05f), (top + 0.08f) * 0.5f, 0.03f)), new Vector3(0.06f, top + 0.08f, 0.06f), WoodB);
                b.Frustum(Matrix4x4.Translate(new Vector3(sx * (W * 0.5f + 0.05f), top + 0.08f, 0.03f)), 0.045f, 0f, 0f, 0.07f, 6, Brass, false);
            }
            // Marco biselado: tabla oscura, filete claro y pergamino (cara en z = −0.013).
            b.Box(Matrix4x4.Translate(new Vector3(0f, mid, 0.025f)), new Vector3(W + 0.1f, H + 0.1f, 0.05f), WoodB);
            b.Box(Matrix4x4.Translate(new Vector3(0f, mid, -0.002f)), new Vector3(W + 0.04f, H + 0.04f, 0.01f), WoodA);
            b.Box(Matrix4x4.Translate(new Vector3(0f, mid, -0.008f)), new Vector3(W + 0.01f, H + 0.01f, 0.004f), ParchmentEdge);
            b.Box(Matrix4x4.Translate(new Vector3(0f, mid, -0.011f)), new Vector3(W, H, 0.004f), Parchment);
            const float face = -0.013f;
            // Titulo con ornamentos (rombos de laton y filete).
            float titleY = top - 0.055f;
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box(Matrix4x4.TRS(new Vector3(sx * 0.27f, titleY, face - 0.002f), Quaternion.Euler(0f, 0f, 45f), Vector3.one), new Vector3(0.022f, 0.022f, 0.004f), Brass);
                b.Box(Matrix4x4.Translate(new Vector3(sx * 0.33f, titleY, face - 0.001f)), new Vector3(0.07f, 0.004f, 0.002f), Brass);
            }
            b.Box(Matrix4x4.Translate(new Vector3(0f, titleY - 0.045f, face - 0.001f)), new Vector3(W - 0.12f, 0.004f, 0.002f), ParchmentEdge);
            // Filas: rombo del color del grupo a la izquierda.
            const float rowTop = 0.11f, rowPitch = 0.064f;
            for (int i = 0; i < LabSealSummary.RowCount; i++)
            {
                float y = top - rowTop - 0.03f - i * rowPitch;
                b.Box(Matrix4x4.TRS(new Vector3(-W * 0.5f + 0.045f, y, face - 0.002f), Quaternion.Euler(0f, 0f, 45f), Vector3.one),
                    new Vector3(0.022f, 0.022f, 0.004f), LabPalette.Group(LabSealSummary.RowGroup(i)));
            }
            // Tira de ayuda (placa oscura) abajo.
            const float helpH = 0.1f;
            float helpY = bottom + helpH * 0.5f + 0.02f;
            b.Box(Matrix4x4.Translate(new Vector3(0f, helpY, face - 0.002f)), new Vector3(W - 0.04f, helpH, 0.004f), WindowDark);
            var board = Part("Board", go.transform, Save("Lab_Cartel", b), material, Vector3.zero, Quaternion.identity);
            board.isStatic = true;

            float textZ = face - LabPanelLayout.TextLift;
            var title = Text(go.transform, "Title", new Vector3(0f, titleY, textZ), Quaternion.identity, new Vector2(0.46f, 0.07f), 0.5f,
                new Color(0.35f, 0.17f, 0.06f), "Vas a lanzar");
            title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            var rows = new TextMeshPro[LabSealSummary.RowCount];
            for (int i = 0; i < rows.Length; i++)
            {
                float y = top - rowTop - 0.03f - i * rowPitch;
                rows[i] = Text(go.transform, "Row_" + i, new Vector3(0.025f, y, textZ), Quaternion.identity, new Vector2(W - 0.11f, 0.055f), 0.27f, TextDark,
                    LabSealSummary.RowLabel(i) + "   -");
                rows[i].alignment = TextAlignmentOptions.Left;
            }
            const string idle = "Dibujá en el atril o usá los controles de la izquierda.\nDespués tocá LANZAR: sale del papel del campo.";
            var help = Text(go.transform, "Help", new Vector3(0f, helpY, face - 0.004f - LabPanelLayout.TextLift), Quaternion.identity,
                new Vector2(W - 0.08f, helpH - 0.015f), 0.18f, TextLight, idle);
            help.textWrappingMode = TextWrappingModes.Normal;
            var strip = go.AddComponent<LabHelpStrip>();
            strip.Configure(help, front, idle);
            EditorUtility.SetDirty(strip);

            // Solo con lentes: banda de resultado y lectura del atril.
            TextMeshPro result = LensBoard(bench, material, "LabLensResult", ResultLocal, new Vector2(0.62f, 0.15f), 0.19f, "Todavía no lanzaste", out Renderer band);
            TextMeshPro padInfo = LensBoard(bench, material, "LabLensPad", PadInfoLocal, new Vector2(0.42f, 0.06f), 0.17f, "Atril vacío", out _);

            var display = go.AddComponent<LabBenchDisplay>();
            var so = new SerializedObject(display);
            so.FindProperty("bench").objectReferenceValue = sealBench;
            var rowsProp = so.FindProperty("rowTexts");
            rowsProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++) rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            so.FindProperty("resultText").objectReferenceValue = result;
            so.FindProperty("resultBand").objectReferenceValue = band;
            so.FindProperty("padInfoText").objectReferenceValue = padInfo;
            so.FindProperty("caster").objectReferenceValue = caster;
            so.ApplyModifiedPropertiesWithoutUndo();

            var arrow = Part("LabPaperArrow", bench.parent, ArrowMesh(), material, Vector3.zero, Quaternion.identity);
            arrow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Assign(display, "paperArrow", arrow.transform);
            EditorUtility.SetDirty(display);
        }

        /// <summary>
        /// Placa flotante de datos que solo se ve con lentes, cerca y mirandola (LabLensGate sobre la raiz; el contenido es el hijo
        /// «Content»): placa oscura, banda clara (la tiñe LabBenchDisplay) y texto 6 mm por delante. Mira al jugador.
        /// </summary>
        private static TextMeshPro LensBoard(Transform bench, Material material, string name, Vector3 local, Vector2 size, float fontSize, string text, out Renderer band)
        {
            var go = Child(bench, name, local);
            go.transform.localRotation = Quaternion.LookRotation(Flat(local).sqrMagnitude > 1e-4f ? Flat(local) : Vector3.forward);
            var content = Child(go.transform, "Content", Vector3.zero);
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0f, 0.006f)), new Vector3(size.x + 0.03f, size.y + 0.03f, 0.006f), WindowDark);
            Part("Plate", content.transform, Save("Lab_LensPlate_" + name, b), material, Vector3.zero, Quaternion.identity)
                .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var bandMesh = new LowPolyMesh();
            bandMesh.Box(Matrix4x4.Translate(new Vector3(0f, 0f, 0.001f)), new Vector3(size.x, size.y, 0.002f), BandBase);
            var bandGo = Part("Band", content.transform, Save("Lab_LensBand_" + name, bandMesh), material, Vector3.zero, Quaternion.identity);
            band = bandGo.GetComponent<MeshRenderer>();
            band.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var label = Text(content.transform, "Text", new Vector3(0f, 0f, -LabPanelLayout.TextLift), Quaternion.identity,
                new Vector2(size.x - 0.02f, size.y - 0.01f), fontSize, TextLight, text);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.Normal;
            var gate = go.AddComponent<LabLensGate>();
            gate.Configure(content, go.transform, LabLensVisibility.ShowDistance);
            content.SetActive(false);
            EditorUtility.SetDirty(gate);
            return label;
        }

        /// <summary>Flecha de emision (+Z = punta), color HDR que brilla con el bloom como los faroles.</summary>
        private static Mesh ArrowMesh() => Cached("Lab_EmissionArrow", () =>
        {
            var b = new LowPolyMesh();
            const float head = 0.1f, shaft = ArrowLength - head;
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0f, shaft * 0.5f)), new Vector3(0.024f, 0.024f, shaft), ArrowGlow);
            b.Frustum(Matrix4x4.TRS(new Vector3(0f, 0f, shaft), Quaternion.Euler(90f, 0f, 0f), Vector3.one), 0.055f, 0f, 0f, head, 6, ArrowGlow, false, true);
            return b;
        });

        // ---------------------------------------------------------------- luz (presupuesto Quest: 3 luces reales)

        /// <summary>Faroles del banco: dos postes con farol en las juntas de atras y una lampara sobre el cartel (solo emisivo + bloom).</summary>
        private static void BenchLamps(Transform bench, Material material)
        {
            Mesh lamp = Cached("Lab_BenchLamp", () =>
            {
                var b = new LowPolyMesh();
                b.Frustum(Matrix4x4.identity, 0.03f, 0.025f, 0f, 1.85f, 6, WoodB);
                b.Box(Matrix4x4.Translate(new Vector3(0f, 1.87f, 0f)), new Vector3(0.12f, 0.03f, 0.12f), Iron);
                b.Box(Matrix4x4.Translate(new Vector3(0f, 1.97f, 0f)), new Vector3(0.09f, 0.17f, 0.09f), TowerBuilder.LanternGlow);
                b.Frustum(Matrix4x4.Translate(new Vector3(0f, 2.05f, 0f)), 0.1f, 0.02f, 0f, 0.08f, 4, Iron, true, true);
                // Halo falso: brillo mas tenue alrededor del vidrio (lo agranda el bloom).
                b.Frustum(Matrix4x4.Translate(new Vector3(0f, 1.9f, 0f)), 0.075f, 0.075f, 0f, 0.14f, 8, TowerBuilder.LanternGlow * 0.45f, false);
                return b;
            });
            float yaw = LabBenchLayout.FrontHalfAngle + 2f * LabBenchLayout.FacetHalfAngle;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 at = Quaternion.Euler(0f, s * yaw, 0f) * new Vector3(0f, 0f, 0.8f);
                Part(s < 0f ? "LabLamp_L" : "LabLamp_R", bench, lamp, material, at, Quaternion.identity).isStatic = true;
            }
        }

        /// <summary>
        /// Tres luces reales calidas, sin sombras y de rango corto: una sobre el banco (bajo el toldo), un foco al cartel y un foco
        /// a la pista (donde esta el papel). El resto de la iluminacion son faroles emisivos con bloom.
        /// </summary>
        private static void Lights(Transform root, Material material, float floor)
        {
            Vector3 stand = new Vector3(0f, floor, StandZ);
            NewLight(root, "LabLight_Bench", LightType.Point, stand + new Vector3(0f, 2.15f, 0.35f), Vector3.down, 3.6f, 1.8f, 0f);
            Vector3 cartel = stand + CartelLocal + new Vector3(0f, CartelBottom + CartelHeight * 0.5f, 0f);
            Vector3 spotAt = stand + new Vector3(-0.55f, 2.35f, 0.15f);
            NewLight(root, "LabLight_Cartel", LightType.Spot, spotAt, cartel - spotAt, 3.2f, 2.4f, 55f);
            Vector3 beam = new Vector3(0f, CanopyY(PostBackZ) - 0.25f + floor, PostBackZ - 0.1f);
            Vector3 aim = new Vector3(0f, Rel(root, 0f, TrackStart + LabSealSpec.DefaultPaperDistance) + 0.8f, TrackStart + LabSealSpec.DefaultPaperDistance);
            NewLight(root, "LabLight_Track", LightType.Spot, beam, aim - beam, 12f, 5f, 48f);
            // Farol colgado que «justifica» el foco de la pista.
            MapBuilder.Place("LabLantern_Spot", root, LanternMesh("Lab_LanternSpot", 0.25f), material, root.TransformPoint(beam + Vector3.down * 0.3f), root.rotation).isStatic = true;
        }

        private static void NewLight(Transform root, string name, LightType type, Vector3 local, Vector3 directionLocal, float range, float intensity, float spotAngle)
        {
            var go = Child(root, name, local);
            if (directionLocal.sqrMagnitude > 1e-6f) go.transform.localRotation = Quaternion.LookRotation(directionLocal.normalized);
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = WarmLight;
            light.range = range;
            light.intensity = intensity;
            if (type == LightType.Spot)
            {
                light.spotAngle = spotAngle;
                light.innerSpotAngle = spotAngle * 0.6f;
            }
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private static List<TextAsset> SpellFiles()
        {
            var list = new List<TextAsset>();
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { SpellsFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            list.Sort((a, c) => string.CompareOrdinal(a.name, c.name));
            return list;
        }

        private static Mesh JarsMesh()
        {
            var b = new LowPolyMesh();
            Color[] liquids = { new Color(0.35f, 0.6f, 0.9f), new Color(0.45f, 0.8f, 0.4f), new Color(0.95f, 0.65f, 0.25f) };
            Vector3[] at = { new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0f, 0.05f), new Vector3(0.03f, 0f, 0.11f) };
            float[] heights = { 0.16f, 0.11f, 0.13f };
            for (int i = 0; i < 3; i++)
            {
                float h = heights[i];
                b.Frustum(Matrix4x4.Translate(at[i]), 0.04f, 0.04f, 0f, h * 0.6f, 8, liquids[i]);
                b.Frustum(Matrix4x4.Translate(at[i]), 0.04f, 0.025f, h * 0.6f, h, 8, Glass);
                b.Frustum(Matrix4x4.Translate(at[i]), 0.02f, 0.02f, h, h + 0.03f, 6, WoodA);
            }
            return Save("Lab_Jars", b);
        }

        // ---------------------------------------------------------------- repisa de lentes y pizarron

        /// <summary>Repisa junto a la entrada del banco con las lentes de tinta arriba (InkLenses) y frascos abajo.</summary>
        private static void Shelf(Transform root, Material material, float floor)
        {
            Vector3 local = new Vector3(ShelfLocal.x, floor, ShelfLocal.z);
            Quaternion rotation = root.rotation * Quaternion.LookRotation(Flat(ShelfLocal - ViewerLocal));
            var b = new LowPolyMesh();
            foreach (float sx in new[] { -0.3f, 0.3f }) b.Box(Matrix4x4.Translate(new Vector3(sx, 0.55f, 0f)), new Vector3(0.05f, 1.1f, 0.26f), WoodB);
            foreach (float y in new[] { 0.06f, 0.45f, 0.93f }) b.Box(Matrix4x4.Translate(new Vector3(0f, y, 0f)), new Vector3(0.66f, 0.04f, 0.28f), WoodA, Plank);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.55f, 0.125f)), new Vector3(0.56f, 1.0f, 0.02f), Plaster);
            foreach (float sx in new[] { -0.3f, 0.3f }) b.Box(Matrix4x4.Translate(new Vector3(sx, 1.12f, 0f)), new Vector3(0.07f, 0.04f, 0.07f), Brass);
            var go = MapBuilder.Place("LabShelf", root, Save("Lab_Shelf", b), material, root.TransformPoint(local), rotation);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.475f, 0f);
            col.size = new Vector3(0.66f, 0.95f, 0.28f);
            go.isStatic = true;
            MapBuilder.Place("LabJars_Shelf", go.transform, JarsMesh(), material, go.transform.TransformPoint(new Vector3(-0.08f, 0.47f, -0.04f)), rotation).isStatic = true;
            Lenses(go.transform, material, new Vector3(0f, 0.95f, -0.02f));
        }

        /// <summary>
        /// Soporte con las lentes de tinta (InkLenses, E/grip). El collider del soporte va en la raiz (fuera de
        /// <c>LensesVisual</c>): al ponerse las lentes el modelo se oculta pero el soporte sigue recibiendo el rayo.
        /// </summary>
        private static void Lenses(Transform parent, Material material, Vector3 local)
        {
            var go = Child(parent, "LabLenses", local);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.11f, 0f);
            col.size = new Vector3(0.22f, 0.22f, 0.18f);

            var stand = new LowPolyMesh();
            stand.Box(Matrix4x4.Translate(new Vector3(0f, 0.01f, 0f)), new Vector3(0.16f, 0.02f, 0.12f), WoodB);
            stand.Box(Matrix4x4.Translate(new Vector3(0f, 0.08f, 0f)), new Vector3(0.03f, 0.12f, 0.03f), WoodB);
            stand.Box(Matrix4x4.Translate(new Vector3(0f, 0.145f, 0f)), new Vector3(0.15f, 0.015f, 0.03f), Brass);
            Part("Stand", go.transform, Save("Lab_LensStand", stand), material, Vector3.zero, Quaternion.identity);

            var lenses = new LowPolyMesh();
            Quaternion facing = Quaternion.Euler(90f, 0f, 0f);
            foreach (float s in new[] { -0.042f, 0.042f })
            {
                lenses.Frustum(Matrix4x4.TRS(new Vector3(s, 0.035f, 0f), facing, Vector3.one), 0.036f, 0.036f, -0.004f, 0.004f, 12, Brass, true, true);
                lenses.Frustum(Matrix4x4.TRS(new Vector3(s, 0.035f, -0.001f), facing, Vector3.one), 0.03f, 0.03f, -0.005f, 0.005f, 12, LensColor, true, true);
            }
            lenses.Box(Matrix4x4.Translate(new Vector3(0f, 0.045f, 0f)), new Vector3(0.02f, 0.008f, 0.008f), Brass);
            var visual = Part("LensesVisual", go.transform, Save("Lab_Lenses", lenses), material, new Vector3(0f, 0.15f, 0f), Quaternion.identity);

            Text(go.transform, "Label", new Vector3(0f, 0.3f, 0.05f), Quaternion.identity, new Vector2(0.34f, 0.07f), 0.3f, TextGold, "Lentes de tinta");

            var inkLenses = go.AddComponent<InkLenses>();
            Assign(inkLenses, "lensesVisual", visual);
            Assign(inkLenses, "standCollider", col);
        }

        /// <summary>Pizarron a la izquierda del banco, de cara a quien lo usa: titulo, instrucciones y contadores (LabCounters).</summary>
        private static void Sign(Transform root, Material material, float floor)
        {
            Vector3 at = root.TransformPoint(new Vector3(SignLocal.x, floor, SignLocal.z));
            Quaternion rotation = root.rotation * Quaternion.LookRotation(Flat(SignLocal - ViewerLocal));

            var b = new LowPolyMesh();
            foreach (float sx in new[] { -0.5f, 0.5f }) b.Box(Matrix4x4.Translate(new Vector3(sx, 0.95f, 0.05f)), new Vector3(0.07f, 1.9f, 0.07f), WoodB);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.4f, 0.015f)), new Vector3(1.06f, 0.86f, 0.03f), WoodA);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.4f, 0f)), new Vector3(1f, 0.8f, 0.03f), ChalkBoard);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.97f, -0.03f)), new Vector3(0.9f, 0.025f, 0.06f), WoodA);
            b.Frustum(Matrix4x4.Translate(new Vector3(0f, 1.9f, 0.05f)), 0.05f, 0f, 0f, 0.12f, 6, Brass, false);
            var go = MapBuilder.Place("LabSign", root, Save("Lab_Sign", b), material, at, rotation);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.95f, 0.02f);
            col.size = new Vector3(1.1f, 1.9f, 0.1f);
            go.isStatic = true;

            // El texto se lee desde -Z (el pizarron mira lejos de quien lo lee); la pizarra termina en z = −0.015.
            const float face = SignTextZ;
            var title = Text(go.transform, "Title", new Vector3(0f, 1.73f, face), Quaternion.identity, new Vector2(0.94f, 0.1f), 0.55f, TextGold, "Laboratorio");
            title.fontStyle = FontStyles.Bold;
            var help = Text(go.transform, "Instructions", new Vector3(0f, 1.56f, face), Quaternion.identity, new Vector2(0.94f, 0.22f), 0.24f, TextLight,
                "Parate en el centro del banco. Izquierda: armá el sello o dibujalo en el atril y tocá LANZAR. Derecha: cajas, viento y fuego. Con las lentes ves las mediciones.");
            help.textWrappingMode = TextWrappingModes.Normal;

            // Mediciones: solo con las lentes puestas, cerca y mirando el pizarron (v4, punto 10; se leen hasta 6 m).
            var content = Child(go.transform, "Counters", Vector3.zero);
            var fire = Counter(content.transform, "FireArea", 1.4f, "Área quemada: -");
            var box = Counter(content.transform, "BoxDistance", 1.3f, "Distancia de la caja: -");
            var reach = Counter(content.transform, "CastReach", 1.2f, "Alcance del hechizo: -");
            var duration = Counter(content.transform, "CastDuration", 1.1f, "Duración del hechizo: -");
            var gate = go.AddComponent<LabLensGate>();
            gate.Configure(content, Child(go.transform, "LensAnchor", new Vector3(0f, 1.3f, 0f)).transform, 6f);
            EditorUtility.SetDirty(gate);

            var counters = go.AddComponent<LabCounters>();
            Assign(counters, "fireAreaText", fire);
            Assign(counters, "boxDistanceText", box);
            Assign(counters, "castReachText", reach);
            Assign(counters, "castDurationText", duration);
        }

        /// <summary>Z del texto del pizarron: TextLift por delante de la pizarra (cara en z = −0.015).</summary>
        private const float SignTextZ = -0.015f - LabPanelLayout.TextLift;

        private static TextMeshPro Counter(Transform parent, string name, float y, string text)
        {
            var t = Text(parent, name, new Vector3(0f, y, SignTextZ), Quaternion.identity, new Vector2(0.94f, 0.09f), 0.34f, TextLight, text);
            t.alignment = TextAlignmentOptions.Left;
            return t;
        }

        // ---------------------------------------------------------------- pista, manga y fogon

        /// <summary>Estacas cada 2 m a los dos lados de la pista (distancia desde el papel) y postes con banderin en las esquinas.</summary>
        private static void Track(Transform root, Material material)
        {
            var s = new LowPolyMesh();
            s.Box(Matrix4x4.Translate(new Vector3(0f, 0.22f, 0f)), new Vector3(0.06f, 0.6f, 0.06f), WoodA);
            s.Box(Matrix4x4.Translate(new Vector3(0f, 0.45f, 0f)), new Vector3(0.24f, 0.12f, 0.03f), WoodB, ClothLight);
            s.Box(Matrix4x4.Translate(new Vector3(0f, 0.45f, -0.012f)), new Vector3(0.21f, 0.1f, 0.01f), ClothLight);
            s.Box(Matrix4x4.Translate(new Vector3(0f, 0.53f, 0f)), new Vector3(0.07f, 0.04f, 0.07f), Cloth);
            Mesh stake = Save("Lab_Stake", s);
            float paperZ = StandZ; // distancias desde donde se para el jugador
            for (float d = FirstStake; paperZ + d <= TrackStart + TrackLength + 0.01f; d += StakeEvery)
                foreach (float side in new[] { -1f, 1f })
                {
                    var local = new Vector3(side * TrackWidth * 0.5f, 0f, paperZ + d);
                    var go = MapBuilder.Place($"LabStake_{(side < 0f ? "L" : "R")}{d:00}", root, stake, material, GroundAt(root, local) + Vector3.down * 0.08f, root.rotation);
                    go.isStatic = true;
                    // La chapa de la estaca termina en z = −0.017.
                    var t = Text(go.transform, "Label", new Vector3(0f, 0.45f, -0.017f - LabPanelLayout.TextLift), Quaternion.identity, new Vector2(0.2f, 0.09f), 0.6f, TextDark, $"{d:0} m");
                    t.fontStyle = FontStyles.Bold;
                }

            var c = new LowPolyMesh();
            c.Frustum(Matrix4x4.identity, 0.05f, 0.04f, -0.1f, 1.2f, 6, WoodB);
            Vector3 top = new Vector3(0f, 1.15f, 0f);
            c.Tri(top, top + new Vector3(0f, -0.2f, 0f), top + new Vector3(0f, -0.1f, 0.3f), Cloth);
            c.Tri(top + new Vector3(0f, -0.2f, 0f), top, top + new Vector3(0f, -0.1f, 0.3f), Cloth);
            Mesh corner = Save("Lab_TrackCorner", c);
            foreach (float side in new[] { -1f, 1f })
                foreach (float z in new[] { TrackStart, TrackStart + TrackLength })
                {
                    var local = new Vector3(side * TrackWidth * 0.5f, 0f, z);
                    string name = $"LabTrackCorner_{(z > TrackStart ? "B" : "F")}{(side < 0f ? "L" : "R")}";
                    MapBuilder.Place(name, root, corner, material, GroundAt(root, local), root.rotation * Quaternion.Euler(0f, side * 90f, 0f)).isStatic = true;
                }
        }

        /// <summary>
        /// Manga de viento low-poly (mastil, cuatro segmentos encadenados por +Z que se doblan) y tres banderines con el pivote
        /// en el mastil y la tela por +Z. La anima LabWindVisual con el look del viento de los hechizos.
        /// </summary>
        private static void WindSock(Transform root, Material material, Transform windAnchor)
        {
            Vector3 at = GroundAt(root, SockLocal);
            var m = new LowPolyMesh();
            m.Box(Matrix4x4.Translate(new Vector3(0f, 0.08f, 0f)), new Vector3(0.45f, 0.26f, 0.45f), StoneB, StoneA);
            m.Frustum(Matrix4x4.identity, 0.06f, 0.04f, 0.2f, MastHeight + 0.05f, 6, WoodA);
            m.Frustum(Matrix4x4.Translate(new Vector3(0f, MastHeight + 0.05f, 0f)), 0.05f, 0f, 0f, 0.15f, 6, Brass, false);
            var mast = MapBuilder.Place("LabWindSock", root, Save("Lab_SockMast", m), material, at, root.rotation);
            var col = mast.AddComponent<CapsuleCollider>();
            col.radius = 0.12f;
            col.height = MastHeight;
            col.center = new Vector3(0f, MastHeight * 0.5f, 0f);

            var pivot = Child(mast.transform, "Manga", new Vector3(0f, MastHeight, 0f)).transform;
            var segments = new Transform[SockSegments];
            Transform parent = pivot;
            float radius = SockRadius;
            Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f); // el tronco va en Y: se acuesta sobre +Z
            for (int i = 0; i < SockSegments; i++)
            {
                var seg = Child(parent, "Segmento_" + i, new Vector3(0f, 0f, i == 0 ? 0.06f : SockSegmentLength)).transform;
                var sb = new LowPolyMesh();
                sb.Frustum(Matrix4x4.Rotate(alongZ), radius, radius * SockTaper, 0f, SockSegmentLength, 8, i % 2 == 0 ? Cloth : ClothLight, false, false);
                sb.Frustum(Matrix4x4.Rotate(alongZ), radius * SockTaper * 0.97f, radius * 0.97f, SockSegmentLength, 0f, 8, i % 2 == 0 ? Cloth : ClothLight, false, false);
                Part("Tela", seg, Save("Lab_SockSeg" + i, sb), material, Vector3.zero, Quaternion.identity);
                segments[i] = seg;
                parent = seg;
                radius *= SockTaper;
            }
            Part("Aro", pivot, Save("Lab_SockRing", RingMesh()), material, Vector3.zero, Quaternion.identity);

            Color[] flagColors = { Cloth, MapStructures.Shutter, Brass };
            var flags = new Transform[PennantHeights.Length];
            for (int i = 0; i < PennantHeights.Length; i++)
            {
                var flag = Child(mast.transform, "Banderin_" + (i + 1), new Vector3(0f, PennantHeights[i], 0f)).transform;
                var fb = new LowPolyMesh();
                Vector3 up = new Vector3(0f, 0.1f, 0.04f), down = new Vector3(0f, -0.1f, 0.04f), tip = new Vector3(0f, 0f, 0.5f);
                fb.Tri(up, tip, down, flagColors[i]);
                fb.Tri(down, tip, up, flagColors[i]);
                Part("Tela", flag, Save("Lab_Pennant" + (i + 1), fb), material, Vector3.zero, Quaternion.identity);
                flags[i] = flag;
            }

            var look = AssetDatabase.LoadAssetAtPath<ElementLook>(WindLookPath);
            if (look == null) Debug.LogWarning($"[MapLab] no se encontro {WindLookPath}: LabWindVisual usara el look de SpellOutputController.");
            var visual = mast.AddComponent<LabWindVisual>();
            visual.Setup(look, material, windAnchor, pivot, segments, flags);
            EditorUtility.SetDirty(visual);
        }

        private static LowPolyMesh RingMesh()
        {
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f)), SockRadius + 0.012f, SockRadius + 0.012f, -0.02f, 0.06f, 8, Brass, false, false);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0f, -0.03f)), new Vector3(0.03f, 0.03f, 0.08f), Iron);
            return b;
        }

        /// <summary>Fogon: anillo de piedras alrededor del ancla del fuego y troncos al centro (solo decorado: Map/Lab no arde).</summary>
        private static void FirePit(Transform root, Material material, Vector3 at)
        {
            var b = new LowPolyMesh();
            const int stones = 10;
            for (int i = 0; i < stones; i++)
            {
                float a = i * Mathf.PI * 2f / stones;
                float size = 0.22f + 0.06f * ((i * 7) % 3);
                b.Box(Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.95f, size * 0.3f, Mathf.Sin(a) * 0.95f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg + (i % 3) * 11f, 0f), Vector3.one),
                    new Vector3(size, size * 0.75f, size * 0.8f), LowPolyMesh.Jitter(StoneB, 0.1f, i + 3), LowPolyMesh.Jitter(StoneA, 0.08f, i + 17));
            }
            foreach (float yaw in new[] { 0f, 60f, 120f })
                b.Box(Matrix4x4.TRS(new Vector3(0f, 0.08f, 0f), Quaternion.Euler(0f, yaw, 12f), Vector3.one), new Vector3(0.8f, 0.09f, 0.09f), WoodB, new Color(0.18f, 0.12f, 0.1f));
            MapBuilder.Place("LabFirePit", root, Save("Lab_FirePit", b), material, at, root.rotation).isStatic = true;
        }

        // ---------------------------------------------------------------- utilidades

        private static Mesh Save(string name, LowPolyMesh b)
        {
            if (meshes.TryGetValue(name, out Mesh mesh)) return mesh;
            mesh = MapBuilder.SaveMesh(b.Build(name));
            meshes[name] = mesh;
            return mesh;
        }

        private static Mesh Cached(string name, System.Func<LowPolyMesh> build)
        {
            if (meshes.TryGetValue(name, out Mesh mesh)) return mesh;
            return Save(name, build());
        }

        private static Color Darken(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static void Beam(LowPolyMesh b, Vector3 from, Vector3 to, float thickness, Color color)
        {
            Vector3 axis = to - from;
            if (axis.sqrMagnitude < 1e-8f) return;
            b.Box(Matrix4x4.TRS((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, axis), Vector3.one), new Vector3(thickness, axis.magnitude, thickness), color);
        }

        private static GameObject Child(Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 local, Quaternion localRotation)
        {
            var go = Child(parent, name, local);
            go.transform.localRotation = localRotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static TextMeshPro Text(Transform parent, string name, Vector3 local, Quaternion localRotation, Vector2 size, float fontSize, Color color, string text)
        {
            var go = Child(parent, name, local);
            go.transform.localRotation = localRotation;
            var label = go.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = size;
            // Tamaño fijo, una linea por renglon y «…» si no entra (LABORATORIO_V3_PLAN (a).4).
            label.enableAutoSizing = false;
            label.fontSize = fontSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.text = text;
            // El texto no proyecta sombra (a pocos mm de la superficie la sombra lo ensucia).
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return label;
        }

        /// <summary>Solo para el pizarron (parrafo largo): reactiva el ajuste de linea y el autosize. Las consolas usan tamaños fijos.</summary>
        private static void AutoSize(TextMeshPro text, float min, float max)
        {
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
        }

        /// <summary>Ancla (Transform) en el piso bajo un punto del marco local, con la orientacion del laboratorio.</summary>
        private static Transform Anchor(Transform root, Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(GroundAt(root, local), root.rotation);
            return go.transform;
        }

        private static Vector3 GroundAt(Transform root, Vector3 local)
        {
            Vector3 p = root.TransformPoint(local);
            return MapBuilder.Ground(p.x, p.z);
        }

        /// <summary>Altura del terreno relativa a la raiz, en coordenadas locales de la raiz.</summary>
        private static float Rel(Transform root, float x, float z)
        {
            Vector3 w = root.TransformPoint(new Vector3(x, 0f, z));
            return ValleyShape.Height(w.x, w.z) - root.position.y;
        }

        /// <summary>El parche de fuego tiene que caer sobre pasto (no camino ni agua) y lejos del banco (riesgo 12).</summary>
        private static void CheckFireAnchor(Vector3 fire, Vector3 bench)
        {
            if (GrassFire.IsFireproofAt(fire))
                Debug.LogWarning($"[MapLab] el ancla del fuego {fire} cae en un camino o en el agua: el parche no prendera.");
            Vector3 d = fire - bench;
            d.y = 0f;
            if (d.magnitude < MinFireDistance)
                Debug.LogWarning($"[MapLab] el ancla del fuego esta a {d.magnitude:0.0} m del banco (minimo {MinFireDistance} m).");
        }

        /// <summary>Saca pinos y rocas (ya generados) de la plataforma, la pista y la manga. Devuelve cuantos saco.</summary>
        private static int ClearVegetation(Transform map, Transform root)
        {
            int removed = 0;
            float halfWidth = TrackWidth * 0.5f + 1.5f;
            foreach (string groupName in new[] { "Trees", "Rocks" })
            {
                Transform group = map.Find(groupName);
                if (group == null) continue;
                for (int i = group.childCount - 1; i >= 0; i--)
                {
                    Transform t = group.GetChild(i);
                    Vector3 p = root.InverseTransformPoint(t.position);
                    float margin = groupName == "Trees" ? 2f : t.localScale.x;
                    bool hit = new Vector2(p.x, p.z).magnitude < PlatformRadius + margin
                        || (Mathf.Abs(p.x) < halfWidth + margin && p.z > TrackStart - 1f - margin && p.z < TrackStart + TrackLength + 1.5f + margin)
                        || Flat(p - SockLocal).magnitude < 1.5f + margin;
                    if (!hit) continue;
                    Object.DestroyImmediate(t.gameObject);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>El lanzador que ya usan las anotaciones de sellos (SealAnnotations); si no hay, el primero de la escena.</summary>
        private static SpellCaster SceneCaster()
        {
            var annotations = Object.FindFirstObjectByType<SealAnnotations>(FindObjectsInactive.Include);
            if (annotations != null)
            {
                var caster = new SerializedObject(annotations).FindProperty("caster").objectReferenceValue as SpellCaster;
                if (caster != null) return caster;
            }
            return Object.FindFirstObjectByType<SpellCaster>(FindObjectsInactive.Include);
        }

        private static void Assign(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[MapLab] {target.GetType().Name} no tiene el campo serializado «{property}».");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignInt(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[MapLab] {target.GetType().Name} no tiene el campo serializado «{property}».");
                return;
            }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
