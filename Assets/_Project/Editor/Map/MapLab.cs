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
    /// - banco en U de 4,5 × 1,2 m: al centro el atril con el papel de prueba (DrawingCanvas + LabSealBench), LANZAR grande
    ///   delante, «Sello por defecto» al costado y la pizarra «Vas a lanzar» con la banda de resultado (LabBenchDisplay; espejo
    ///   y flecha de emision cuando el papel sale a la pista); ala izquierda con la consola del sello por grupos numerados
    ///   (LabSealCatalog: 1 Receta/Sigilo, 2 Signos, 3 Papel y posicion, 4 Tinta/Prolijidad; pestaña ↺ por grupo); ala derecha
    ///   con la consola de entorno (masa, viento, fuego) y los botones de materializar y «Todo por defecto». Cada consola tiene
    ///   una tira de ayuda en su tablero (LabHelpStrip) y muescas de rango y valor por defecto en perillas y deslizadores;
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

        // Banco en U (4,5 × 1,2 m): alas de 1,2 × 1,2 m a cada lado y mesa de atras de 2,1 × 0,5 m entre ellas.
        private const float DeskTop = 0.86f;
        private const float WingInner = 1.05f, WingWidth = 1.2f, WingDepth = 1.2f;
        private const float BackDeskZ = 0.35f, BackDeskDepth = 0.5f;
        /// <summary>Centro del papel de prueba sobre la mesa (m).</summary>
        private const float PaperHeight = 0.6f;

        // Consolas inclinadas (marco del panel: +X a lo ancho, +Z subiendo la pendiente, +Y la normal).
        // Texto legible (LABORATORIO_V3_PLAN (a)): nada coplanar, alturas y tamaños fijos en LabPanelLayout.
        private const float PanelTilt = LabPanelLayout.PanelTilt, PanelLength = 1.13f, PanelWidth = 1.24f, PanelFrontZ = 0.08f;
        /// <summary>Centro de cada fila sobre la pendiente (paso 0.185 m: plaquita arriba, control, ventanita abajo).</summary>
        private static readonly float[] RowZ = { 0.1f, 0.285f, 0.47f, 0.655f, 0.84f, 1.025f };
        /// <summary>Profundidad de la franja de color de una fila (m).</summary>
        private const float StripDepth = 0.18f;
        /// <summary>Ancho util de la fila (4 controles a paso ≥ 0.30 m) y corrimiento a la derecha (margen del titulo de grupo).</summary>
        private const float RowSpan = 1.2f, RowShiftX = 0.03f;
        /// <summary>
        /// Consola del sello: corrimiento de las filas para dejar a la izquierda la pestaña de cada grupo (numero + ↺, reinicia
        /// el grupo). Con 4 controles las plaquitas van de −0.50 a +0.60 y la pestaña de −0.60 a −0.52.
        /// </summary>
        private const float SealRowShiftX = 0.05f, TabWidth = 0.08f, TabLift = 0.012f;
        private const float TabX = -PanelWidth * 0.5f + 0.02f + TabWidth * 0.5f;
        /// <summary>Muescas: mas de esto se ve como una regla y no ayuda (queda solo min/max y default).</summary>
        private const int MaxTicks = 13;
        private const float KnobRadius = 0.045f;
        /// <summary>Medio recorrido del deslizador (m) y altura del riel.</summary>
        private const float RailHalf = 0.05f, RailY = 0.008f;
        /// <summary>Plaquita del rotulo (arriba) y ventanita de la lectura (abajo): centro en Z, ancho y profundidad (m).</summary>
        private const float LabelZ = 0.076f, ReadoutZ = -0.073f, PlaqueWidth = 0.2f, LabelDepth = 0.026f, ReadoutDepth = 0.03f;
        private const float ButtonPitch = 0.26f;
        /// <summary>LANZAR (35 × 25 cm, (c).4): delante del atril, desde el borde de la mesa (z = BackDeskZ − BackDeskDepth/2).</summary>
        private const float LaunchWidth = 0.35f, LaunchDepth = 0.25f, LaunchZ = BackDeskZ - BackDeskDepth * 0.5f + LaunchDepth * 0.5f;
        /// <summary>Pizarra «Vas a lanzar» a la izquierda del atril: centro en X, base en Z, ancho, alto de la pizarra, alto de las patas e inclinacion.</summary>
        private const float DisplayX = -0.63f, DisplayZ = BackDeskZ + 0.07f, DisplayWidth = 0.64f, DisplayHeight = 0.5f, DisplayLegs = 0.14f, DisplayLean = 12f;
        /// <summary>Largo de las flechas de emision (m).</summary>
        private const float ArrowLength = 0.32f;
        /// <summary>Tope de la tapa de los botones planos (sobre su base).</summary>
        private const float ButtonTop = 0.06f;
        /// <summary>Centro en Y del tablero de atras del ala: subido para que el panel a 35° no tape la leyenda.</summary>
        private const float BoardY = 0.85f;
        /// <summary>Cara del tablero de atras donde apoya el texto (mira a −Z).</summary>
        private const float BoardFaceZ = WingDepth - 0.09f;

        // Toldo: postes en las esquinas, mas alto del lado de la pista.
        private const float PostX = 2.45f, PostFrontZ = -1.15f, PostBackZ = 1.65f;
        private const float CanopyFrontY = 2.5f, CanopyBackY = 2.9f, CanopyOverhang = 0.2f;
        private const int CanopyStrips = 11;

        // Pista (desde el centro del sitio, igual que MapStructures.LaboratoryFront).
        private const float TrackStart = MapStructures.LaboratoryTrackStart;
        private const float TrackLength = MapStructures.LaboratoryTrackLength, TrackWidth = MapStructures.LaboratoryTrackWidth;
        private const float StakeEvery = 2f, FirstStake = 6f;
        /// <summary>Altura (m) del blanco del disparo sobre el centro de la pista.</summary>
        private const float AimHeight = 1.2f;
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
        private static readonly Vector3 ViewerLocal = new Vector3(0f, 0f, -0.3f);

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
        private static readonly Color DialColor = new Color(0.1f, 0.09f, 0.14f);
        private static readonly Color ChalkBoard = new Color(0.16f, 0.24f, 0.2f);
        private static readonly Color ButtonBase = new Color(0.3f, 0.2f, 0.13f);
        private static readonly Color PaperGroup = new Color(0.36f, 0.52f, 0.82f);
        private static readonly Color SigilGroup = new Color(0.9f, 0.45f, 0.25f);
        private static readonly Color SignGroup = new Color(0.42f, 0.7f, 0.36f);
        private static readonly Color InkGroup = new Color(0.62f, 0.44f, 0.82f);
        private static readonly Color BoxGroup = new Color(0.86f, 0.72f, 0.48f);
        private static readonly Color WindGroup = new Color(0.62f, 0.86f, 0.95f);
        private static readonly Color FireGroup = new Color(0.95f, 0.5f, 0.25f);
        private static readonly Color MaterializeGroup = new Color(0.75f, 0.7f, 0.62f);
        private static readonly Color ResetColor = new Color(0.62f, 0.75f, 0.55f);
        private static readonly Color LaunchColor = new Color(0.86f, 0.24f, 0.2f);
        private static readonly Color LensColor = new Color(0.25f, 0.55f, 0.75f);
        private static readonly Color Glass = new Color(0.55f, 0.8f, 0.85f);
        private static readonly Color TextLight = new Color(0.98f, 0.93f, 0.8f);
        private static readonly Color TextGold = new Color(1f, 0.82f, 0.45f);
        private static readonly Color TextDark = new Color(0.14f, 0.08f, 0.04f);
        private static readonly Color PlaqueCream = new Color(0.96f, 0.9f, 0.76f);
        private static readonly Color WindowDark = new Color(0.07f, 0.06f, 0.1f);
        /// <summary>Banda de resultado: casi blanca, la tiñe LabBenchDisplay (verde/rojo) con un MaterialPropertyBlock.</summary>
        private static readonly Color BandBase = new Color(0.95f, 0.95f, 0.95f);
        /// <summary>Flechas de emision: HDR (como TowerBuilder.LanternGlow) para que brillen con el bloom.</summary>
        private static readonly Color ArrowGlow = new Color(2.6f, 1.25f, 0.35f);
        /// <summary>Muesca del valor por defecto en perillas y deslizadores.</summary>
        private static readonly Color DefaultMark = new Color(1f, 0.78f, 0.25f);

        private const string BoxMaterialPath = "Assets/_Project/Materials/Crate.mat";
        private const string WindMaterialPath = "Assets/_Project/Materials/SpellStreak.mat";
        private const string WindLookPath = "Assets/_Project/Data/Elements/Wind.asset";
        private const string SpellsFolder = "Assets/_Project/Data/Spells";

        /// <summary>Texto acostado sobre una superficie de normal +Y, legible con la parte de arriba hacia +Z.</summary>
        private static readonly Quaternion Lying = Quaternion.Euler(90f, 0f, 0f);

        // ---------------------------------------------------------------- filas de controles

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

            public string Key => Source == LabControlSource.Seal ? "Seal_" + (LabSealVarId)Var : "Env_" + (LabVariableId)Var;
        }

        private struct Row
        {
            public string Title;
            public string Key;
            public Color Color;
            public Ctl[] Controls;
            /// <summary>Grupo de la consola del sello (1..4, LabSealCatalog); 0 en la consola de entorno.</summary>
            public int Group;

            public Row(string title, string key, Color color, params Ctl[] controls)
            {
                Title = title; Key = key; Color = color; Controls = controls; Group = 0;
            }
        }

        // Consola del sello por grupos numerados (LABORATORIO_V3_PLAN (c).5): el orden y la pertenencia salen de
        // LabSealCatalog.IdsOfGroup; aca solo el tipo de control, el rotulo corto (≤ 10 caracteres) y el color de cada grupo.
        // Las claves de grupo reusan las del v2 («Sigil», «Sign», «Paper», «Ink») para no dejar mallas huerfanas.
        private static readonly string[] GroupKeys = { "Sigil", "Sign", "Paper", "Ink" };
        private static Color GroupColor(int group)
        {
            switch (group)
            {
                case 1: return SigilGroup;
                case 2: return SignGroup;
                case 3: return PaperGroup;
                default: return InkGroup;
            }
        }

        /// <summary>Perillas para angulos, deslizadores para continuos y selectores para listas.</summary>
        private static LabControlKind SealKind(LabSealVarId id)
        {
            switch (id)
            {
                case LabSealVarId.RingGap:
                case LabSealVarId.BaseAngle:
                case LabSealVarId.PaperTilt:
                    return LabControlKind.Knob;
                case LabSealVarId.SigilScale:
                case LabSealVarId.RingFill:
                case LabSealVarId.SignLength:
                case LabSealVarId.Asymmetry:
                case LabSealVarId.SignDistance:
                case LabSealVarId.Tremor:
                    return LabControlKind.Slider;
                default:
                    return LabControlKind.Selector;
            }
        }

        /// <summary>Rotulo corto (≤ LabPanelLayout.MaxLabelChars) de cada variable del sello.</summary>
        private static string SealLabel(LabSealVarId id)
        {
            switch (id)
            {
                case LabSealVarId.BaseRecipe: return "Receta";
                case LabSealVarId.Sigil: return "Sigilo";
                case LabSealVarId.SigilScale: return "Tamaño";
                case LabSealVarId.RingFill: return "Llenado";
                case LabSealVarId.RingGap: return "Hueco";
                case LabSealVarId.SignType: return "Tipo";
                case LabSealVarId.SignCount: return "Cantidad";
                case LabSealVarId.BaseAngle: return "Áng. base";
                case LabSealVarId.TurnDeg: return "Sentido";
                case LabSealVarId.SignLength: return "Largo";
                case LabSealVarId.Asymmetry: return "Asimetría";
                case LabSealVarId.SignDistance: return "Distancia";
                case LabSealVarId.PaperSize: return "Tam. papel";
                case LabSealVarId.PaperPose: return "Posición";
                case LabSealVarId.PaperFace: return "Cara";
                case LabSealVarId.PaperTilt: return "Inclinac.";
                case LabSealVarId.Ink: return "Tinta";
                case LabSealVarId.PenWidth: return "Pluma";
                case LabSealVarId.Tremor: return "Temblor";
                default: return id.ToString();
            }
        }

        /// <summary>
        /// Filas de la consola del sello: cada grupo en sus propias filas (≤ 4 controles a paso ≥ 0.30 m, repartidos parejo:
        /// 5 → 3 + 2, 7 → 4 + 3), adelante el grupo 1 y atras el 4. Nunca se quitan variables: avisa si alguna quedo afuera.
        /// </summary>
        private static Row[] BuildSealRows()
        {
            var rows = new List<Row>(RowZ.Length);
            int total = 0;
            for (int g = 1; g <= LabSealCatalog.GroupCount; g++)
            {
                IReadOnlyList<LabSealVarId> ids = LabSealCatalog.IdsOfGroup(g);
                int n = ids.Count;
                if (n == 0) continue;
                int rowCount = LabPanelLayout.RowsNeeded(n, RowSpan);
                int perRow = (n + rowCount - 1) / rowCount;
                for (int start = 0; start < n; start += perRow)
                {
                    int count = Mathf.Min(perRow, n - start);
                    var controls = new Ctl[count];
                    for (int k = 0; k < count; k++)
                    {
                        LabSealVarId id = ids[start + k];
                        controls[k] = new Ctl(id, SealKind(id), SealLabel(id));
                    }
                    rows.Add(new Row(LabSealCatalog.GroupName(g), GroupKeys[g - 1], GroupColor(g), controls) { Group = g });
                    total += count;
                }
            }
            if (total != LabSealCatalog.Count)
                Debug.LogWarning($"[MapLab] la consola del sello muestra {total} de {LabSealCatalog.Count} variables: revisá LabSealCatalog.IdsOfGroup.");
            if (rows.Count > RowZ.Length)
                Debug.LogWarning($"[MapLab] la consola del sello necesita {rows.Count} filas y el panel tiene {RowZ.Length}: sobran controles.");
            return rows.ToArray();
        }

        private static readonly Row[] EnvRows =
        {
            new Row("Caja", "Box", BoxGroup,
                new Ctl(LabVariableId.Mass, LabControlKind.Selector, "Masa")),
            new Row("Viento", "Wind", WindGroup,
                new Ctl(LabVariableId.WindYaw, LabControlKind.Knob, "Dirección"),
                new Ctl(LabVariableId.WindSpeed, LabControlKind.Slider, "Velocidad")),
            new Row("Fuego", "Fire", FireGroup,
                new Ctl(LabVariableId.FireSeedRadius, LabControlKind.Slider, "Semilla"),
                new Ctl(LabVariableId.FireDecay, LabControlKind.Slider, "Merma"),
                new Ctl(LabVariableId.FireMaxGenerations, LabControlKind.Knob, "Generac."))
        };

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

            var caster = SceneCaster();
            if (caster == null) Debug.LogWarning("[MapLab] no hay SpellCaster en la escena: el banco, el overlay y el registro no veran los hechizos.");

            // Pista y blanco del disparo.
            var target = new GameObject("LabTrackTarget").transform;
            target.SetParent(root, false);
            target.SetPositionAndRotation(GroundAt(root, new Vector3(0f, 0f, TrackStart + TrackLength * 0.5f)) + Vector3.up * AimHeight, root.rotation);
            Track(root, material);

            // Banco en U: mesa de atras con atril y papel, botones del sello, consolas a los lados.
            LabSealBench bench = BenchCenter(root, material, floor, caster, target);
            Console(root, material, floor, -1f, "LabSealConsole", "Sello",
                "Doble clic: por defecto · Rueda: un paso · Pestaña: reinicia", BuildSealRows(), bench, true);
            Transform envPanel = Console(root, material, floor, 1f, "LabEnvConsole", "Entorno",
                "Ajustá y materializá: aparece en la pista", EnvRows, bench, false);
            MaterializeRow(envPanel, material, RowZ[EnvRows.Length]);
            // Sin franja debajo: apoya sobre la losa (y = 0).
            Text(envPanel, "Hint", new Vector3(0f, LabPanelLayout.TextY(0f), RowZ[EnvRows.Length + 1]), Lying, new Vector2(1.1f, 0.12f),
                LabPanelLayout.ReadoutFontSize, TextLight,
                "Los botones y controles se apuntan con el rayo.\nLas lentes muestran los parámetros en el mundo.");

            // Sistemas: parametros del entorno + materializador con sus anclas.
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

            Debug.Log($"[MapLab] laboratorio en {root.position} mirando {forward}; pinos/rocas despejados: {cleared}; " +
                      $"controles: {controlCount}; fuego en {fireAnchor.position}; blanco en {target.position}.");
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

        // ---------------------------------------------------------------- banco: centro

        /// <summary>Mesa de atras con el atril, el papel de prueba (LabSealBench), «Lanzar», «Sello por defecto» y frascos.</summary>
        private static LabSealBench BenchCenter(Transform root, Material material, float floor, SpellCaster caster, Transform target)
        {
            float top = floor + DeskTop;
            var b = new LowPolyMesh();
            float width = WingInner * 2f;
            b.Box(Matrix4x4.Translate(new Vector3(0f, (DeskTop - 0.05f) * 0.5f, BackDeskZ)), new Vector3(width - 0.06f, DeskTop - 0.05f, BackDeskDepth - 0.08f), WoodA);
            b.Box(Matrix4x4.Translate(new Vector3(0f, DeskTop - 0.025f, BackDeskZ)), new Vector3(width, 0.05f, BackDeskDepth), WoodB, Plank);
            foreach (float sx in new[] { -0.5f, 0f, 0.5f })
                b.Box(Matrix4x4.Translate(new Vector3(sx, DeskTop * 0.55f, BackDeskZ - BackDeskDepth * 0.5f + 0.035f)), new Vector3(0.36f, 0.22f, 0.01f), WoodB);
            // Atril: caballete detras del papel.
            float paperZ = BackDeskZ - 0.02f;
            foreach (float sx in new[] { -0.17f, 0.17f })
                Beam(b, new Vector3(sx, DeskTop, BackDeskZ + 0.16f), new Vector3(sx * 0.6f, DeskTop + PaperHeight + 0.2f, paperZ + 0.08f), 0.035f, WoodB);
            Beam(b, new Vector3(0f, DeskTop, BackDeskZ + 0.22f), new Vector3(0f, DeskTop + PaperHeight, paperZ + 0.1f), 0.03f, WoodA);
            b.Box(Matrix4x4.Translate(new Vector3(0f, DeskTop + PaperHeight - 0.27f, paperZ + 0.03f)), new Vector3(0.5f, 0.04f, 0.06f), WoodA, Brass);
            var desk = MapBuilder.Place("LabBenchDesk", root, Save("Lab_BenchDesk", b), material, root.TransformPoint(new Vector3(0f, floor, 0f)), root.rotation);
            var col = desk.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, DeskTop * 0.5f, BackDeskZ);
            col.size = new Vector3(width, DeskTop, BackDeskDepth);
            desk.isStatic = true;

            // Banco de sellos: el papel de prueba vive debajo (Map/Lab/LabBench/LabBenchPaper).
            var benchGo = Child(root, "LabBench", new Vector3(0f, top, BackDeskZ));
            var bench = benchGo.AddComponent<LabSealBench>();
            DrawingCanvas canvas = BenchPaper(benchGo.transform, root.TransformPoint(new Vector3(0f, top + PaperHeight, paperZ)), root.rotation);
            // Marco de la pista para los presets de posicion del papel (LABORATORIO_V3_PLAN (b)): mismo frente que el sitio y la
            // altura del suelo donde se acuesta el papel «Piso» (TrackStart + 2), para que no quede enterrado ni flotando.
            var trackOrigin = Child(root, "LabTrackOrigin", new Vector3(0f, Rel(root, 0f, TrackStart + 2f), 0f)).transform;
            var so = new SerializedObject(bench);
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("caster").objectReferenceValue = caster;
            so.FindProperty("aim").objectReferenceValue = target;
            so.FindProperty("trackOrigin").objectReferenceValue = trackOrigin;
            so.FindProperty("paperRoot").objectReferenceValue = canvas != null ? canvas.transform : null;
            so.FindProperty("trackStart").floatValue = TrackStart;
            var files = so.FindProperty("spellFiles");
            List<TextAsset> spells = SpellFiles();
            files.arraySize = spells.Count;
            for (int i = 0; i < spells.Count; i++) files.GetArrayElementAtIndex(i).objectReferenceValue = spells[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            if (canvas == null) Debug.LogWarning("[MapLab] falta PaperCanvas en la escena: el banco no tiene papel de prueba.");

            // LANZAR grande y rojo, centrado delante del atril ((c).4); «Sello por defecto» al costado, mas chico.
            var launch = Button(root, material, "LabLaunch", "Lab_ButtonLaunch", "LANZAR", LaunchColor,
                new Vector3(0f, top, LaunchZ), LaunchWidth, LaunchDepth, 0.55f);
            launch.AddComponent<LabBenchButton>().Configure(LabBenchAction.Launch, bench, launch.GetComponent<MeshRenderer>());
            var reset = Button(root, material, "LabSealDefault", "Lab_ButtonSealReset", "Sello por\ndefecto", ResetColor,
                new Vector3(0.62f, top, LaunchZ), 0.2f, 0.15f, 0.2f);
            reset.AddComponent<LabBenchButton>().Configure(LabBenchAction.ResetSeal, bench, reset.GetComponent<MeshRenderer>());
            EditorUtility.SetDirty(launch.GetComponent<LabBenchButton>());
            EditorUtility.SetDirty(reset.GetComponent<LabBenchButton>());

            BenchDisplay(root, material, top, paperZ, bench, canvas);

            Mesh jars = JarsMesh();
            // Detras de la pizarra (antes en −0.95, BackDeskZ + 0.12, donde ahora va la pizarra).
            MapBuilder.Place("LabJars_Desk", root, jars, material, root.TransformPoint(new Vector3(-0.98f, top, BackDeskZ + 0.13f)), root.rotation).isStatic = true;
            MapBuilder.Place("LabJars_Desk2", root, jars, material, root.TransformPoint(new Vector3(0.95f, top, BackDeskZ + 0.14f)), root.rotation * Quaternion.Euler(0f, 140f, 0f)).isStatic = true;
            return bench;
        }

        /// <summary>Lienzo de prueba: clon de PaperCanvas cuadrado de 45 cm, sin PhysicalCanvas ni Respawnable, kinematico, emite por la cara con tinta.</summary>
        private static DrawingCanvas BenchPaper(Transform parent, Vector3 position, Quaternion rotation)
        {
            var src = GameObject.Find("PaperCanvas");
            if (src == null) return null;
            var go = Object.Instantiate(src, position, rotation);
            go.name = "LabBenchPaper";
            go.transform.localScale = new Vector3(LabSealSpec.DefaultPaperSize, LabSealSpec.DefaultPaperSize, src.transform.localScale.z);
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
                so.FindProperty("emissionSide").enumValueIndex = (int)EmissionSide.InkSide;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            go.transform.SetParent(parent, true);
            return canvas;
        }

        /// <summary>
        /// Pizarra «Vas a lanzar» a la izquierda del atril (resumen del sello + banda de resultado, <see cref="LabBenchDisplay"/>),
        /// espejo del papel en el atril con su flecha de emision (se ven cuando el papel sale a la pista) y la flecha que acompaña
        /// al papel real. La pizarra mira al jugador (−Z) y se recuesta <see cref="DisplayLean"/>° para leerse desde arriba.
        /// </summary>
        private static void BenchDisplay(Transform root, Material material, float top, float paperZ, LabSealBench bench, DrawingCanvas canvas)
        {
            var go = Child(root, "LabBenchDisplay", new Vector3(DisplayX, top, DisplayZ));
            go.transform.localRotation = Quaternion.Euler(DisplayLean, 0f, 0f);
            const float legs = DisplayLegs, mid = DisplayLegs + DisplayHeight * 0.5f;
            var b = new LowPolyMesh();
            foreach (float sx in new[] { -1f, 1f })
                b.Box(Matrix4x4.Translate(new Vector3(sx * (DisplayWidth * 0.5f - 0.04f), legs * 0.5f + 0.01f, 0.02f)), new Vector3(0.04f, legs + 0.02f, 0.04f), WoodB);
            b.Box(Matrix4x4.Translate(new Vector3(0f, mid, 0.015f)), new Vector3(DisplayWidth + 0.05f, DisplayHeight + 0.05f, 0.03f), WoodA);
            b.Box(Matrix4x4.Translate(new Vector3(0f, mid, 0f)), new Vector3(DisplayWidth, DisplayHeight, 0.03f), ChalkBoard);
            b.Frustum(Matrix4x4.Translate(new Vector3(0f, legs + DisplayHeight + 0.025f, 0.015f)), 0.04f, 0f, 0f, 0.08f, 6, Brass, false);
            Part("Board", go.transform, Save("Lab_BenchDisplay", b), material, Vector3.zero, Quaternion.identity);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, mid, 0.01f);
            col.size = new Vector3(DisplayWidth + 0.05f, DisplayHeight + 0.05f, 0.05f);

            // La pizarra termina en z = −0.015; la banda de resultado sobresale 4 mm. Texto a TextLift de lo que tenga detras.
            const float face = -0.015f;
            float boardTop = legs + DisplayHeight;
            var title = Text(go.transform, "Title", new Vector3(0f, boardTop - 0.045f, face - LabPanelLayout.TextLift), Quaternion.identity,
                new Vector2(DisplayWidth - 0.04f, 0.06f), 0.42f, TextGold, "Vas a lanzar");
            title.fontStyle = FontStyles.Bold;
            var summary = Text(go.transform, "Summary", new Vector3(0f, boardTop - 0.19f, face - LabPanelLayout.TextLift), Quaternion.identity,
                new Vector2(DisplayWidth - 0.04f, 0.22f), 0.24f, TextLight, "-");
            summary.alignment = TextAlignmentOptions.TopLeft;

            const float bandDepth = 0.004f, bandHeight = 0.13f;
            float bandY = legs + 0.085f;
            var bandMesh = new LowPolyMesh();
            bandMesh.Box(Matrix4x4.Translate(new Vector3(0f, 0f, -bandDepth * 0.5f)), new Vector3(DisplayWidth - 0.04f, bandHeight, bandDepth), BandBase);
            var band = Part("ResultBand", go.transform, Save("Lab_ResultBand", bandMesh), material, new Vector3(0f, bandY, face), Quaternion.identity);
            band.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var result = Text(go.transform, "Result", new Vector3(0f, bandY, face - bandDepth - LabPanelLayout.TextLift), Quaternion.identity,
                new Vector2(DisplayWidth - 0.06f, bandHeight - 0.02f), 0.24f, TextLight, "Todavía no lanzaste\nApretá LANZAR");
            result.fontStyle = FontStyles.Bold;

            var display = go.AddComponent<LabBenchDisplay>();
            Assign(display, "bench", bench);
            Assign(display, "summaryText", summary);
            Assign(display, "resultText", result);
            Assign(display, "resultBand", band.GetComponent<MeshRenderer>());

            // Espejo del atril: misma malla y material que el papel (el material del lienzo se toma en runtime), un poco detras
            // del papel para que no se pisen; oculto hasta que el papel salga a la pista.
            var anchor = Child(root, "LabEaselMirror", new Vector3(0f, top + PaperHeight, paperZ + 0.015f)).transform;
            if (canvas != null && canvas.TryGetComponent(out MeshFilter paperFilter) && canvas.TryGetComponent(out MeshRenderer paperRenderer))
            {
                var mirror = Child(anchor, "Mirror", Vector3.zero);
                mirror.transform.rotation = LabSealBenchMath.FaceRotation(canvas.SurfaceNormalLocal, -anchor.forward, anchor.up);
                mirror.transform.localScale = canvas.transform.lossyScale;
                mirror.AddComponent<MeshFilter>().sharedMesh = paperFilter.sharedMesh;
                var mirrorRenderer = mirror.AddComponent<MeshRenderer>();
                mirrorRenderer.sharedMaterial = paperRenderer.sharedMaterial;
                mirrorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mirrorRenderer.enabled = false;
                Assign(display, "mirrorRenderer", mirrorRenderer);
            }
            else Debug.LogWarning("[MapLab] el papel del banco no tiene MeshFilter/MeshRenderer: el atril no tendra espejo.");
            Assign(display, "mirrorAnchor", anchor);
            var easelArrow = Part("Arrow", anchor, ArrowMesh(), material, Vector3.zero, Quaternion.identity);
            easelArrow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            easelArrow.SetActive(false);
            Assign(display, "easelArrow", easelArrow.transform);

            var paperArrow = Part("LabPaperArrow", root, ArrowMesh(), material, new Vector3(0f, top + PaperHeight, paperZ), Quaternion.identity);
            paperArrow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Assign(display, "paperArrow", paperArrow.transform);
            EditorUtility.SetDirty(display);
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

        // ---------------------------------------------------------------- consolas (alas)

        /// <summary>
        /// Ala del banco (side −1 izquierda, +1 derecha): mesa de 1,2 × 1,2 m, panel inclinado 35° con una fila por grupo de color y
        /// tablero de atras con el titulo. Marco del ala: +Z hacia afuera (lejos de quien usa el banco), +X a su derecha.
        /// Devuelve el panel (marco donde van los controles).
        /// </summary>
        private static Transform Console(Transform root, Material material, float floor, float side, string name, string title,
            string help, Row[] rows, LabSealBench bench, bool groupTabs)
        {
            var wing = Child(root, name, new Vector3(side * WingInner, floor + DeskTop, 0f)).transform;
            wing.localRotation = Quaternion.Euler(0f, side * 90f, 0f);

            var b = new LowPolyMesh();
            float half = WingWidth * 0.5f;
            b.Box(Matrix4x4.Translate(new Vector3(0f, -(DeskTop + 0.05f) * 0.5f, WingDepth * 0.5f)), new Vector3(WingWidth - 0.06f, DeskTop - 0.05f, WingDepth - 0.08f), WoodA);
            b.Box(Matrix4x4.Translate(new Vector3(0f, -0.025f, WingDepth * 0.5f)), new Vector3(WingWidth, 0.05f, WingDepth), WoodB, Plank);
            // Cuña bajo el panel inclinado.
            float cos = Mathf.Cos(PanelTilt * Mathf.Deg2Rad), sin = Mathf.Sin(PanelTilt * Mathf.Deg2Rad);
            float zb = PanelFrontZ + PanelLength * cos, yb = PanelLength * sin - 0.04f;
            foreach (float sx in new[] { -half + 0.02f, half - 0.02f })
            {
                Vector3 f = new Vector3(sx, 0f, PanelFrontZ), bk = new Vector3(sx, 0f, zb), t = new Vector3(sx, yb, zb);
                b.Tri(f, t, bk, WoodB);
                b.Tri(f, bk, t, WoodB);
            }
            b.Box(Matrix4x4.Translate(new Vector3(0f, yb * 0.5f, zb - 0.01f)), new Vector3(WingWidth - 0.04f, yb, 0.02f), WoodB);
            // Tablero de atras con el titulo (su cara oscura termina en BoardFaceZ).
            b.Box(Matrix4x4.Translate(new Vector3(0f, BoardY, WingDepth - 0.06f)), new Vector3(WingWidth, 0.5f, 0.04f), WoodB);
            b.Box(Matrix4x4.Translate(new Vector3(0f, BoardY, BoardFaceZ + 0.005f)), new Vector3(WingWidth - 0.06f, 0.44f, 0.01f), PanelColor);
            b.Box(Matrix4x4.Translate(new Vector3(0f, BoardY + 0.27f, WingDepth - 0.06f)), new Vector3(WingWidth + 0.04f, 0.04f, 0.08f), Brass);
            // Patas del tablero (ahora flota sobre la mesa).
            foreach (float sx in new[] { -half + 0.03f, half - 0.03f })
                b.Box(Matrix4x4.Translate(new Vector3(sx, (BoardY - 0.25f) * 0.5f, WingDepth - 0.06f)), new Vector3(0.05f, BoardY - 0.25f, 0.04f), WoodB);
            var wingMesh = Save("Lab_Console", b);
            wing.gameObject.AddComponent<MeshFilter>().sharedMesh = wingMesh;
            wing.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            var col = wing.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -DeskTop * 0.5f, WingDepth * 0.5f);
            col.size = new Vector3(WingWidth, DeskTop, WingDepth);
            var boardCol = wing.gameObject.AddComponent<BoxCollider>();
            boardCol.center = new Vector3(0f, BoardY, WingDepth - 0.06f);
            boardCol.size = new Vector3(WingWidth, 0.5f, 0.06f);

            // Texto del tablero: TextLift por delante de la cara (normal −Z), tamaños fijos.
            float boardTextZ = BoardFaceZ - LabPanelLayout.TextLift;
            var titleText = Text(wing, "Title", new Vector3(0f, BoardY + 0.16f, boardTextZ), Quaternion.identity, new Vector2(1.08f, 0.12f), 0.8f, TextGold, title);
            titleText.fontStyle = FontStyles.Bold;
            Text(wing, "Legend", new Vector3(0f, BoardY + 0.035f, boardTextZ), Quaternion.identity, new Vector2(1.08f, 0.12f), 0.28f, TextLight, Legend(rows) + "\n" + help);
            // Tira de ayuda ((c).6): dos renglones con la ayuda del control apuntado o agarrado de esta consola.
            var helpText = Text(wing, "Help", new Vector3(0f, BoardY - 0.125f, boardTextZ), Quaternion.identity, new Vector2(1.08f, 0.08f), 0.28f, TextLight,
                "Apuntá un control para ver qué hace.");
            wing.gameObject.AddComponent<LabHelpStrip>().Configure(helpText, wing, null);
            EditorUtility.SetDirty(wing.GetComponent<LabHelpStrip>());

            // Panel inclinado: losa oscura y una franja de color por fila (tope en LabPanelLayout.SurfaceTop).
            var panel = Child(wing, "Panel", new Vector3(0f, 0f, PanelFrontZ)).transform;
            panel.localRotation = Quaternion.Euler(-PanelTilt, 0f, 0f);
            var p = new LowPolyMesh();
            p.Box(Matrix4x4.Translate(new Vector3(0f, -0.02f, PanelLength * 0.5f)), new Vector3(PanelWidth, 0.04f, PanelLength), WoodB, PanelColor);
            p.Box(Matrix4x4.Translate(new Vector3(0f, 0.005f, -0.005f)), new Vector3(PanelWidth, 0.02f, 0.02f), Brass);
            const float strip = LabPanelLayout.SurfaceTop;
            for (int i = 0; i < rows.Length && i < RowZ.Length; i++)
                p.Box(Matrix4x4.Translate(new Vector3(0f, strip * 0.5f, RowZ[i])), new Vector3(PanelWidth - 0.04f, strip, StripDepth), Color.Lerp(PanelColor, rows[i].Color, 0.32f));
            if (rows.Length < RowZ.Length) // fila de botones (consola de entorno)
                p.Box(Matrix4x4.Translate(new Vector3(0f, strip * 0.5f, RowZ[rows.Length])), new Vector3(PanelWidth - 0.04f, strip, StripDepth), Color.Lerp(PanelColor, MaterializeGroup, 0.25f));
            Part("PanelBody", panel, Save(side < 0f ? "Lab_SealPanel" : "Lab_EnvPanel", p), material, Vector3.zero, Quaternion.identity);

            float shift = groupTabs ? SealRowShiftX : RowShiftX;
            for (int i = 0; i < rows.Length && i < RowZ.Length; i++) ControlRow(panel, material, rows[i], RowZ[i], bench, shift, !groupTabs);
            if (groupTabs)
                // Una pestaña por grupo a lo largo de sus filas (numero + ↺): titulo numerado y reset del grupo en un solo blanco.
                for (int g = 1; g <= LabSealCatalog.GroupCount; g++)
                {
                    int first = -1, last = -1;
                    for (int i = 0; i < rows.Length && i < RowZ.Length; i++)
                        if (rows[i].Group == g) { if (first < 0) first = i; last = i; }
                    if (first < 0) continue;
                    GroupTab(panel, material, g, RowZ[first] - StripDepth * 0.5f, RowZ[last] + StripDepth * 0.5f, bench);
                }
            return panel;
        }

        /// <summary>
        /// Pestaña de un grupo de la consola del sello, en el margen izquierdo: barra del color del grupo con el numero grande y
        /// el icono ↺ (malla: la fuente TMP no trae la flecha circular). Apretarla llama a <see cref="LabSealBench.ResetGroup"/>
        /// (<see cref="LabBenchButton"/>, accion ResetGroup). El titulo completo del grupo esta en la leyenda del tablero y en la
        /// tira de ayuda al apuntarla. Ancho ≥ 8 cm y collider ≥ 9 cm (rayo de VR).
        /// </summary>
        private static void GroupTab(Transform panel, Material material, int group, float zFrom, float zTo, LabSealBench bench)
        {
            Color color = GroupColor(group);
            float length = zTo - zFrom, zMid = (zFrom + zTo) * 0.5f;
            const float tabTop = LabPanelLayout.SurfaceTop + TabLift;
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, tabTop * 0.5f, 0f)), new Vector3(TabWidth, tabTop, length - 0.01f), Darken(color, 0.75f), color);
            // ↺: arco de 300° con la punta al final, acostado sobre la tapa, en la punta de la pestaña mas cerca del jugador.
            const float iconR = 0.021f, iconY = tabTop + 0.0015f;
            float iconZ = -length * 0.5f + 0.038f;
            const int segments = 9;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (50f + 300f * i / segments) * Mathf.Deg2Rad, a1 = (50f + 300f * (i + 1) / segments) * Mathf.Deg2Rad;
                Vector3 p0 = new Vector3(Mathf.Sin(a0) * iconR, iconY, iconZ + Mathf.Cos(a0) * iconR);
                Vector3 p1 = new Vector3(Mathf.Sin(a1) * iconR, iconY, iconZ + Mathf.Cos(a1) * iconR);
                Vector3 mid = (p0 + p1) * 0.5f;
                b.Box(Matrix4x4.TRS(mid, Quaternion.LookRotation(p1 - p0), Vector3.one), new Vector3(0.006f, 0.003f, (p1 - p0).magnitude + 0.002f), TextDark);
            }
            float end = 350f * Mathf.Deg2Rad;
            Vector3 tip = new Vector3(Mathf.Sin(end) * iconR, iconY, iconZ + Mathf.Cos(end) * iconR);
            Vector3 along = new Vector3(Mathf.Cos(end), 0f, -Mathf.Sin(end)), outward = new Vector3(Mathf.Sin(end), 0f, Mathf.Cos(end));
            Vector3 lift = Vector3.up * 0.0015f;
            b.Tri(tip + outward * 0.011f + lift, tip + along * 0.014f + lift, tip - outward * 0.011f + lift, TextDark);
            b.Tri(tip - outward * 0.011f + lift, tip + along * 0.014f + lift, tip + outward * 0.011f + lift, TextDark);

            var go = Part("LabGroupReset_" + group, panel, Save($"Lab_GroupTab_{group}_{Mathf.RoundToInt(length * 1000f)}", b), material,
                new Vector3(TabX, 0f, zMid), Quaternion.identity);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.03f, 0f);
            col.size = new Vector3(0.09f, 0.06f, Mathf.Max(0.09f, length - 0.01f));
            // Numero en la mitad de arriba (lejos del icono): nada debajo del texto salvo la tapa.
            var number = Text(go.transform, "Number", new Vector3(0f, LabPanelLayout.TextY(tabTop), length * 0.5f - 0.045f), Lying,
                new Vector2(TabWidth - 0.01f, 0.06f), 0.45f, TextDark, group.ToString());
            number.fontStyle = FontStyles.Bold;
            go.AddComponent<LabBenchButton>().Configure(LabBenchAction.ResetGroup, bench, go.GetComponent<MeshRenderer>(), group);
            EditorUtility.SetDirty(go.GetComponent<LabBenchButton>());
        }

        private static string Legend(Row[] rows)
        {
            var sb = new System.Text.StringBuilder();
            string last = null;
            int distinct = 0, count = 0;
            foreach (Row row in rows)
                if (row.Title != last) { distinct++; last = row.Title; }
            last = null;
            foreach (Row row in rows)
            {
                if (row.Title == last) continue;
                // Con mas de 3 grupos, de a dos por renglon (los titulos numerados del sello no entran en uno).
                if (last != null) sb.Append(distinct > 3 && count % 2 == 0 ? "\n" : "   ");
                count++;
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(row.Color)).Append('>').Append(row.Title).Append("</color>");
                last = row.Title;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Fila de controles centrada en el panel (corrida <paramref name="shiftX"/>). Con <paramref name="spineTitle"/> el titulo
        /// del grupo va acostado en el margen izquierdo (consola de entorno); la del sello usa la pestaña de grupo (GroupTab).
        /// </summary>
        private static void ControlRow(Transform panel, Material material, Row row, float z, LabSealBench bench, float shiftX, bool spineTitle)
        {
            int n = row.Controls.Length;
            if (n > LabPanelLayout.MaxPerRow(RowSpan))
                Debug.LogWarning($"[MapLab] la fila «{row.Title}» tiene {n} controles: no entran a paso {LabPanelLayout.MinControlPitch} m (partila en dos).");
            float pitch = LabPanelLayout.MinControlPitch;

            if (spineTitle)
            {
                // Titulo del grupo: se lee subiendo la pendiente, en el margen izquierdo, sobre la franja.
                Quaternion spine = Quaternion.AngleAxis(-90f, Vector3.up) * Lying;
                var title = Text(panel, "Group_" + row.Key + "_" + Mathf.RoundToInt(z * 1000f),
                    new Vector3(-PanelWidth * 0.5f + 0.03f, LabPanelLayout.TextY(LabPanelLayout.SurfaceTop), z), spine,
                    new Vector2(StripDepth, 0.045f), LabPanelLayout.TitleFontSize, row.Color, row.Title);
                title.fontStyle = FontStyles.Bold;
            }

            for (int i = 0; i < n; i++)
            {
                float x = shiftX + (i - (n - 1) * 0.5f) * pitch;
                Control(panel, material, row.Controls[i], row, new Vector3(x, LabPanelLayout.SurfaceTop, z), bench);
            }
        }

        /// <summary>
        /// Un control: collider ≥ 9 cm, pieza movil (tapa de perilla/selector o mango del deslizador; es el primer renderer, el
        /// que se resalta al apuntar), rotulo oscuro sobre plaquita crema arriba y lectura clara en ventanita oscura abajo.
        /// LabControl + LabControlBinding (enlace en runtime). <paramref name="at"/>.y es el tope de la franja.
        /// </summary>
        private static void Control(Transform panel, Material material, Ctl c, Row row, Vector3 at, LabSealBench bench)
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
                col.size = new Vector3(RailHalf * 2f + 0.04f, 0.06f, 0.09f);
                var handle = Part("Handle", go.transform, HandleMesh(row), material, new Vector3(0f, RailY, 0f), Quaternion.identity);
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
                Part("Cap", pivot, c.Kind == LabControlKind.Selector ? SelectorMesh(row) : KnobMesh(row), material, Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
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

        private static Mesh KnobMesh(Row row) => Cached("Lab_Knob_" + row.Key, () =>
        {
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.identity, KnobRadius, KnobRadius, 0f, 0.012f, 10, Darken(row.Color, 0.6f));
            b.Frustum(Matrix4x4.identity, KnobRadius - 0.005f, KnobRadius - 0.012f, 0.012f, 0.04f, 10, row.Color);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.041f, 0.018f)), new Vector3(0.01f, 0.004f, 0.026f), TextLight);
            return b;
        });

        private static Mesh SelectorMesh(Row row) => Cached("Lab_Selector_" + row.Key, () =>
        {
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.identity, 0.04f, 0.04f, 0f, 0.012f, 10, Darken(row.Color, 0.6f));
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.026f, 0.004f)), new Vector3(0.024f, 0.03f, 0.085f), Darken(row.Color, 0.85f), row.Color);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.043f, 0.03f)), new Vector3(0.012f, 0.004f, 0.022f), TextLight);
            return b;
        });

        private static Mesh HandleMesh(Row row) => Cached("Lab_Handle_" + row.Key, () =>
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.014f, 0f)), new Vector3(0.026f, 0.028f, 0.05f), Darken(row.Color, 0.8f), row.Color);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.029f, 0f)), new Vector3(0.006f, 0.003f, 0.04f), TextLight);
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
                var id = (LabSealVarId)c.Var;
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
        /// maximo, chicas en cada paso (selector de 9 presets, etc.) y una dorada mas alta en el valor por defecto. Todas terminan
        /// antes de r = 0.061 para no meterse bajo el rotulo (plaquita desde z = 0.063).
        /// </summary>
        private static Mesh DialMesh(int ticks, float defaultFraction) =>
            Cached($"Lab_Dial_{ticks}_{Mathf.RoundToInt(defaultFraction * 1000f)}", () =>
        {
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.identity, 0.05f, 0.05f, 0f, 0.004f, 12, DialColor);
            for (int i = 1; i < ticks - 1; i++)
                DialTick(b, Mathf.Lerp(-120f, 120f, i / (float)(ticks - 1)), 0.053f, new Vector3(0.004f, 0.005f, 0.01f), Brass);
            DialTick(b, -120f, 0.054f, new Vector3(0.007f, 0.006f, 0.012f), Brass);
            DialTick(b, 120f, 0.054f, new Vector3(0.007f, 0.006f, 0.012f), Brass);
            if (defaultFraction >= 0f)
                DialTick(b, Mathf.Lerp(-120f, 120f, defaultFraction), 0.055f, new Vector3(0.008f, 0.008f, 0.012f), DefaultMark);
            return b;
        });

        private static void DialTick(LowPolyMesh b, float angle, float radius, Vector3 size, Color color)
        {
            float r = angle * Mathf.Deg2Rad;
            b.Box(Matrix4x4.TRS(new Vector3(Mathf.Sin(r) * radius, size.y * 0.5f, Mathf.Cos(r) * radius), Quaternion.Euler(0f, angle, 0f), Vector3.one), size, color);
        }

        /// <summary>Plaquita crema del rotulo: apoya sobre la franja y sube LabPanelLayout.PlaqueHeight (3 mm).</summary>
        private static Mesh PlaqueMesh() => Cached("Lab_Plaque", () =>
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, LabPanelLayout.PlaqueHeight * 0.5f, 0f)), new Vector3(PlaqueWidth, LabPanelLayout.PlaqueHeight, LabelDepth), PlaqueCream);
            return b;
        });

        /// <summary>Ventanita oscura de la lectura, a la misma altura que la plaquita.</summary>
        private static Mesh WindowMesh() => Cached("Lab_ReadoutWindow", () =>
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, LabPanelLayout.PlaqueHeight * 0.5f, 0f)), new Vector3(PlaqueWidth, LabPanelLayout.PlaqueHeight, ReadoutDepth), WindowDark);
            return b;
        });

        /// <summary>
        /// Riel del deslizador: topes de bronce en el minimo y el maximo, muescas chicas en cada paso (si hay) del lado del jugador
        /// y una dorada mas alta en el valor por defecto.
        /// </summary>
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

        /// <summary>Fila de botones de la consola de entorno: materializar caja, viento y fuego, y «Todo por defecto» (LabReset).</summary>
        private static void MaterializeRow(Transform panel, Material material, float z)
        {
            MaterializeButton(panel, material, LabSpawnKind.Box, "Materializar\ncaja", BoxGroup, new Vector3(-1.5f * ButtonPitch, 0f, z));
            MaterializeButton(panel, material, LabSpawnKind.Wind, "Materializar\nviento", WindGroup, new Vector3(-0.5f * ButtonPitch, 0f, z));
            MaterializeButton(panel, material, LabSpawnKind.Fire, "Materializar\nfuego", FireGroup, new Vector3(0.5f * ButtonPitch, 0f, z));
            var go = Button(panel, material, "LabReset", "Lab_ButtonReset", "Todo por\ndefecto", ResetColor, new Vector3(1.5f * ButtonPitch, 0f, z), 0.18f, 0.13f, 0.24f);
            Assign(go.AddComponent<LabResetButton>(), "highlight", go.GetComponent<MeshRenderer>());
        }

        private static void MaterializeButton(Transform parent, Material material, LabSpawnKind kind, string text, Color color, Vector3 local)
        {
            var go = Button(parent, material, "LabMaterialize_" + kind, "Lab_Button" + kind, text, color, local, 0.18f, 0.13f, 0.18f);
            var button = go.AddComponent<LabMaterializeButton>();
            AssignInt(button, "kind", (int)kind);
            Assign(button, "highlight", go.GetComponent<MeshRenderer>());
        }

        /// <summary>Boton plano (base de madera + tapa de color) con el rotulo acostado encima (tamaño fijo) y collider disparador.</summary>
        private static GameObject Button(Transform parent, Material material, string name, string meshName, string text, Color color,
            Vector3 local, float width, float depth, float fontSize)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.015f, 0f)), new Vector3(width, 0.03f, depth), ButtonBase);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.045f, 0f)), new Vector3(width - 0.03f, ButtonTop - 0.03f, depth - 0.03f), Darken(color, 0.85f), color);
            var go = Part(name, parent, Save(meshName, b), material, local, Quaternion.identity);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.045f, 0f);
            col.size = new Vector3(width, 0.09f, depth);
            var label = Text(go.transform, "Label", new Vector3(0f, LabPanelLayout.TextY(ButtonTop), 0f), Lying, new Vector2(width - 0.035f, depth - 0.035f), fontSize, TextDark, text);
            label.fontStyle = FontStyles.Bold;
            return go;
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
            var help = Text(go.transform, "Instructions", new Vector3(0f, 1.57f, face), Quaternion.identity, new Vector2(0.94f, 0.2f), 0.3f, TextLight,
                "Izquierda: armá el sello y apretá LANZAR (dispara a la pista). Derecha: ajustá el entorno y materializalo. Las lentes muestran los parámetros.");
            AutoSize(help, 0.12f, 0.3f);

            var fire = Counter(go.transform, "FireArea", 1.4f, "Área quemada: -");
            var box = Counter(go.transform, "BoxDistance", 1.3f, "Distancia de la caja: -");
            var reach = Counter(go.transform, "CastReach", 1.2f, "Alcance del hechizo: -");
            var duration = Counter(go.transform, "CastDuration", 1.1f, "Duración del hechizo: -");

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
            float paperZ = BackDeskZ;
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
