using System.Collections;
using TMPro;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Output;
using SpellSimulator.Player;
using SpellSimulator.Reactions;
using SpellSimulator.Rendering;
using SpellSimulator.World;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Overlay de las lentes de tinta: etiquetas 3D (world-space, iguales en PC y VR) con los parametros de lo que pasa cerca
    /// de la camara: cada hechizo lanzado (elemento, fuerza, alcance, duracion, calidad), cada <see cref="Movable"/> (masa y
    /// velocidad), el pasto en llamas (generacion y probabilidad efectiva de contagio) y el viento (zonas del laboratorio y
    /// conos de hechizos: flecha con m/s). Solo se ve con <see cref="Visible"/> (lo ponen las <see cref="InkLenses"/>).
    /// Rendimiento (Quest): pool fijo de etiquetas y flechas creado en Awake, contenido a 5 Hz, texto en un char[] propio
    /// (sin strings por frame) que solo se reenvia a TMP si cambio, posicion/orientacion en LateUpdate, radio de 12 m y sin
    /// outline en VR (contraste con una placa oscura detras).
    /// </summary>
    public class LabOverlay : MonoBehaviour
    {
        private const int LabelCount = 32;
        private const int ArrowCount = 8;
        private const int MaxSpells = 8;
        private const int MaxMovableCandidates = 128;
        private const int MaxFireSamples = 128;
        private const int MaxArrowCandidates = 32;
        private const int TextCapacity = 192;
        /// <summary>Periodo del refresco de contenido (5 Hz).</summary>
        private const float RefreshPeriod = 0.2f;
        /// <summary>Radio alrededor de la camara dentro del que se etiqueta (m).</summary>
        private const float Range = 12f;
        /// <summary>Cuanto dura la etiqueta de un hechizo lanzado (s).</summary>
        private const float SpellLabelSeconds = 12f;
        /// <summary>Alto del texto (m) a la distancia de referencia; mas lejos crece (tamano angular parecido) dentro de un tope.</summary>
        private const float TextHeight = 0.075f;
        private const float ReferenceDistance = 2.5f;
        private const float MinScale = 0.7f, MaxScale = 3f;
        private const float MovableLabelHeight = 0.55f;
        private const float SpellLabelHeight = 0.32f, SpellStackStep = 0.24f;
        private const float FireLabelHeight = 0.6f;
        private const float WindZoneArrowHeight = 1f;
        private const float PlatePadding = 0.05f;
        private const float PcOutlineWidth = 0.18f;
        /// <summary>Etiquetas nuevas que un refresco puede encender; el resto se difiere al siguiente tick (evita 32 ForceMeshUpdate de golpe).</summary>
        private const int MaxNewLabelsPerRefresh = 8;
        /// <summary>Etiquetas que se precalientan por frame en el warmup de Start.</summary>
        private const int WarmupLabelsPerFrame = 4;
        /// <summary>
        /// Texto del precalentamiento: todos los caracteres que usa el overlay (letras, digitos, coma, punto, %, /, -, espacio y
        /// saltos) mas las etiquetas rich text que usa. Todo es ASCII a proposito: asi no se cae a la fuente de respaldo dinamica
        /// (° y × no se usan; si se agregan, hay que incluirlos aca y en el atlas de la fuente).
        /// TODO: asignar el campo font a una fuente de atlas estatico con estos caracteres (hoy usa la de TMP por defecto).
        /// </summary>
        private const string WarmupText = "Hechizo Fuego Agua Viento Tierra Luz\nfuerza alcance dur. calidad gen. contagio no contagia fuego modificado\n0123456789 , . % - / m/s kg s\n<size=80%>0123456789</size><size=70%>ABC</size>";

        // Defaults del fuego (constantes privadas de Reactions/GrassFire.cs: DefaultSpreadChance, DefaultGenerationDecay,
        // DefaultMaxGenerations). Si difieren de lo actual, la etiqueta avisa «fuego modificado».
        private const float DefaultFireSpread = 0.045f;
        private const float DefaultFireDecay = 0.78f;
        private const int DefaultFireMaxGenerations = 6;

        private static readonly string[] ElementNames = { "Hechizo", "Fuego", "Agua", "Viento", "Tierra", "Luz" };

        private static bool visible;

        /// <summary>Overlay visible (false por defecto). Las lentes lo encienden; el reinicio del mundo lo apaga.</summary>
        public static bool Visible
        {
            get => visible;
            set
            {
                if (visible == value) return;
                visible = value;
                VisibleChanged?.Invoke(value);
            }
        }

        /// <summary>Se emite cuando <see cref="Visible"/> cambia.</summary>
        public static event System.Action<bool> VisibleChanged;

        // Con Enter Play Mode sin recarga de dominio el estado estatico sobreviviria entre sesiones de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            visible = false;
            VisibleChanged = null;
        }

        [SerializeField, Tooltip("Lanzador cuyos hechizos se etiquetan (evento SpellCompiled).")]
        private SpellCaster caster;
        [SerializeField, Tooltip("Material de flechas y placas (con color por vertice, ej. Sprites/Default). Vacio = Sprites/Default.")]
        private Material lineMaterial;
        [SerializeField, Tooltip("Fuente de las etiquetas. Vacio = la fuente por defecto de TextMeshPro.")]
        private TMP_FontAsset font;

        [Header("Colores")]
        [SerializeField] private Color movableColor = new Color(0.95f, 0.93f, 0.85f);
        [SerializeField] private Color fireColor = new Color(1f, 0.62f, 0.25f);
        [SerializeField] private Color fireModifiedColor = new Color(1f, 0.35f, 0.3f);
        [SerializeField] private Color windColor = new Color(0.6f, 0.92f, 1f);
        [SerializeField] private Color plateColor = new Color(0.04f, 0.03f, 0.07f, 0.62f);

        private enum SlotKind { None, Fixed, Movable, Canvas }

        private struct SpellMark
        {
            public DrawingCanvas Canvas;
            public Vector3 Position;
            public ElementType Element;
            public float Force, Range, Duration, Quality, Time;
            public int Stack;
        }

        // Pool de etiquetas (todo preasignado en Awake).
        private Transform root;
        private readonly TextMeshPro[] labels = new TextMeshPro[LabelCount];
        private readonly Transform[] labelTransforms = new Transform[LabelCount];
        private readonly Transform[] plates = new Transform[LabelCount];
        private readonly SlotKind[] slotKind = new SlotKind[LabelCount];
        private readonly Movable[] slotMovable = new Movable[LabelCount];
        private readonly DrawingCanvas[] slotCanvas = new DrawingCanvas[LabelCount];
        private readonly Vector3[] slotAnchor = new Vector3[LabelCount];
        private readonly float[] slotHeight = new float[LabelCount];
        private readonly Color[] slotColor = new Color[LabelCount];
        private readonly char[][] slotText = new char[LabelCount][];
        private readonly int[] slotTextLength = new int[LabelCount];

        // Flechas.
        private readonly LineRenderer[] arrows = new LineRenderer[ArrowCount];
        private readonly Vector3[] arrowPoints = new Vector3[5];

        // Hechizos recientes (anillo fijo).
        private readonly SpellMark[] spells = new SpellMark[MaxSpells];
        private int spellNext;

        // Buffers de candidatos y seleccion (sin allocations en el refresco).
        private readonly Vector3[] spellPos = new Vector3[MaxSpells];
        private readonly int[] spellSlot = new int[MaxSpells];
        private readonly int[] spellPick = new int[MaxSpells];
        private readonly float[] spellPickDist = new float[MaxSpells];

        private readonly Vector3[] movablePos = new Vector3[MaxMovableCandidates];
        private readonly Movable[] movableRef = new Movable[MaxMovableCandidates];
        private readonly int[] movablePick = new int[LabelCount];
        private readonly float[] movablePickDist = new float[LabelCount];

        private readonly Vector3[] firePos = new Vector3[MaxFireSamples];
        private readonly byte[] fireGen = new byte[MaxFireSamples];
        private readonly int[] firePick = new int[LabelCount];
        private readonly float[] firePickDist = new float[LabelCount];

        private readonly Vector3[] arrowCandPos = new Vector3[MaxArrowCandidates];
        private readonly Vector3[] arrowCandDir = new Vector3[MaxArrowCandidates];
        private readonly float[] arrowCandLength = new float[MaxArrowCandidates];
        private readonly float[] arrowCandSpeed = new float[MaxArrowCandidates];
        private readonly int[] arrowPick = new int[ArrowCount];
        private readonly float[] arrowPickDist = new float[ArrowCount];

        // Texto en construccion (se copia al slot solo si cambio).
        private readonly char[] scratch = new char[TextCapacity];
        private readonly char[] digits = new char[24];
        private int scratchLength;

        private Material ownedMaterial, plateMaterial;
        private Camera cam;
        private float nextRefresh;
        private bool anyShown;
        private bool started, warming, warmDone;
        private int newLabelsThisRefresh;

        private void Awake()
        {
            root = new GameObject("LabOverlay_Pool").transform;
            root.SetParent(transform, false);
            var gazeTags = new GameObject("LabGazeTags");
            gazeTags.transform.SetParent(transform, false);
            gazeTags.AddComponent<LabGazeTagPresenter>();

            Material lines = lineMaterial;
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (lines == null && spriteShader != null) lines = ownedMaterial = new Material(spriteShader);
            if (spriteShader != null) plateMaterial = new Material(spriteShader) { color = plateColor };
            if (lines == null) UnityEngine.Debug.LogWarning("[Laboratorio] Sin material para las flechas del overlay (falta Sprites/Default).");

            bool vr = PlatformDetector.IsVR;
            for (int i = 0; i < LabelCount; i++)
            {
                var go = new GameObject("LabLabel");
                go.transform.SetParent(root, false);
                var text = go.AddComponent<TextMeshPro>();
                if (font != null) text.font = font;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.fontStyle = FontStyles.Bold;
                text.fontSize = TextHeight * 10f;
                text.richText = true;
                text.rectTransform.sizeDelta = new Vector2(1.5f, 0.5f);
                // Sin outline en VR: cada outline instancia material y encarece el fragment en la GPU por tiles del Quest.
                if (!vr)
                {
                    text.outlineWidth = PcOutlineWidth;
                    text.outlineColor = new Color32(0, 0, 0, 200);
                }
                var textRenderer = go.GetComponent<MeshRenderer>();
                if (textRenderer != null)
                {
                    textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    textRenderer.receiveShadows = false;
                }

                if (plateMaterial != null)
                {
                    var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    plate.name = "Placa";
                    Destroy(plate.GetComponent<Collider>());
                    plate.transform.SetParent(go.transform, false);
                    var r = plate.GetComponent<MeshRenderer>();
                    r.sharedMaterial = plateMaterial;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    plates[i] = plate.transform;
                }

                go.SetActive(false);
                labels[i] = text;
                labelTransforms[i] = go.transform;
                slotText[i] = new char[TextCapacity];
                slotTextLength[i] = -1;
            }

            for (int i = 0; i < ArrowCount; i++)
            {
                var go = new GameObject("LabArrow");
                go.transform.SetParent(root, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = arrowPoints.Length;
                if (lines != null) line.sharedMaterial = lines;
                line.numCapVertices = 2;
                line.numCornerVertices = 1;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.startColor = line.endColor = windColor;
                go.SetActive(false);
                arrows[i] = line;
            }
        }

        private void Start()
        {
            started = true;
            // Primer dibujo de TMP SDF / Sprites/Default / lineas: compila shaders y arma pipelines. Se hace aca, repartido en frames.
            ShaderWarmup.WarmCommon();
            if (!warmDone) StartCoroutine(WarmupRoutine());
        }

        /// <summary>
        /// Enciende cada etiqueta y flecha un frame (minuscula, alfa casi 0, frente a la camara) para que el primer dibujo real
        /// no ocurra al ponerse las lentes. Reparte <see cref="WarmupLabelsPerFrame"/> etiquetas por frame.
        /// </summary>
        private IEnumerator WarmupRoutine()
        {
            warming = true;
            // Esperar camara (hasta ~2 s); sin ella se calienta igual, en el origen.
            for (int i = 0; i < 120 && Camera.main == null; i++) yield return null;
            Camera warmCam = Camera.main;
            Vector3 at = warmCam != null ? warmCam.transform.position + warmCam.transform.forward * 2f : Vector3.zero;
            Quaternion rot = warmCam != null ? warmCam.transform.rotation : Quaternion.identity;
            var nearlyInvisible = new Color(1f, 1f, 1f, 0.004f);

            for (int start = 0; start < LabelCount; start += WarmupLabelsPerFrame)
            {
                int end = Mathf.Min(start + WarmupLabelsPerFrame, LabelCount);
                for (int i = start; i < end; i++)
                {
                    var text = labels[i];
                    var t = labelTransforms[i];
                    t.SetPositionAndRotation(at, rot);
                    t.localScale = new Vector3(0.001f, 0.001f, 0.001f);
                    text.color = nearlyInvisible;
                    text.text = WarmupText;
                    text.ForceMeshUpdate();
                    t.gameObject.SetActive(true);
                }
                if (start == 0) WarmArrows(true, at);
                yield return null;
                for (int i = start; i < end; i++)
                {
                    labels[i].gameObject.SetActive(false);
                    labels[i].text = string.Empty;
                    labels[i].color = Color.white;
                    slotColor[i] = Color.white;
                    slotTextLength[i] = -1; // fuerza SetCharArray en el primer uso real
                }
                if (start == 0) WarmArrows(false, at);
            }
            warming = false;
            warmDone = true;
            nextRefresh = 0f;
        }

        private void WarmArrows(bool on, Vector3 at)
        {
            for (int i = 0; i < ArrowCount; i++)
            {
                var line = arrows[i];
                if (line == null) continue;
                if (on)
                {
                    for (int p = 0; p < arrowPoints.Length; p++) arrowPoints[p] = at + new Vector3(0.0005f * p, 0f, 0f);
                    line.SetPositions(arrowPoints);
                    line.widthMultiplier = 0.0005f;
                    line.startColor = line.endColor = new Color(1f, 1f, 1f, 0.004f);
                    line.gameObject.SetActive(true);
                }
                else
                {
                    line.gameObject.SetActive(false);
                    line.startColor = line.endColor = windColor;
                }
            }
        }

        private void OnEnable()
        {
            if (started && !warmDone && !warming) StartCoroutine(WarmupRoutine());
            if (caster != null) caster.SpellCompiled += OnSpellCompiled;
            else UnityEngine.Debug.LogWarning("[Laboratorio] LabOverlay sin SpellCaster asignado: no etiqueta hechizos.");
            VisibleChanged += OnVisibleChanged;
            MagicWorld.WorldReset += OnWorldReset;
            nextRefresh = 0f;
        }

        private void OnDisable()
        {
            if (caster != null) caster.SpellCompiled -= OnSpellCompiled;
            VisibleChanged -= OnVisibleChanged;
            MagicWorld.WorldReset -= OnWorldReset;
            HideAll();
            if (warming)
            {
                // La corrutina se detiene con el componente: se apaga lo que dejo encendido y se reintenta al reactivar.
                warming = false;
                for (int i = 0; i < LabelCount; i++)
                {
                    labels[i].gameObject.SetActive(false);
                    labels[i].color = Color.white;
                    slotColor[i] = Color.white;
                    slotTextLength[i] = -1;
                }
                WarmArrows(false, Vector3.zero);
            }
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            if (ownedMaterial != null) Destroy(ownedMaterial);
            if (plateMaterial != null) Destroy(plateMaterial);
        }

        // ---------------------------------------------------------------- eventos

        private void OnSpellCompiled(SpellIR spell, DrawingCanvas canvas)
        {
            float now = Time.time;
            // Un sello compuesto publica varios SpellCompiled en el mismo lienzo: se apilan en vez de superponerse.
            int stack = 0;
            if (canvas != null)
            {
                for (int i = 0; i < MaxSpells; i++)
                    if (spells[i].Canvas == canvas && now - spells[i].Time < 0.5f) stack++;
            }
            spells[spellNext] = new SpellMark
            {
                Canvas = canvas,
                Position = canvas != null ? canvas.WorldCenter : (cam != null ? cam.transform.position + cam.transform.forward : Vector3.zero),
                Element = spell.element,
                Force = spell.force,
                Range = spell.range,
                Duration = spell.duration,
                Quality = spell.quality,
                Time = now,
                Stack = stack,
            };
            spellNext = (spellNext + 1) % MaxSpells;
            nextRefresh = 0f;
        }

        private void OnVisibleChanged(bool on)
        {
            if (on) nextRefresh = 0f;
            else HideAll();
        }

        private void OnWorldReset()
        {
            for (int i = 0; i < MaxSpells; i++) spells[i] = default;
            spellNext = 0;
            HideAll();
            Visible = false;
        }

        // ---------------------------------------------------------------- frame

        private void LateUpdate()
        {
            // Las etiquetas de datos del overlay v5 quedaron reemplazadas por LabGazeTagPresenter.
        }

        // ---------------------------------------------------------------- refresco (5 Hz)

        private void Refresh(Vector3 camPos)
        {
            int used = 0;
            newLabelsThisRefresh = 0;
            used = RefreshArrows(camPos, used);
            used = RefreshSpells(camPos, used);

            // Cajas y pasto se reparten lo que queda: el fuego tiene asegurado un tercio si hay llamas cerca.
            int movableFound = CollectMovables(camPos);
            int fireFound = CollectFire(camPos);
            int remaining = LabelCount - used;
            int fireShare = Mathf.Min(fireFound, Mathf.Max(remaining / 3, remaining - movableFound));
            int movableShare = Mathf.Min(movableFound, remaining - fireShare);
            fireShare = Mathf.Min(fireFound, remaining - movableShare);

            for (int k = 0; k < movableShare; k++) WriteMovable(used++, movableRef[movablePick[k]]);
            if (fireShare > 0) used = WriteFire(used, fireShare);

            for (int i = used; i < LabelCount; i++) HideSlot(i);
            anyShown = true;
        }

        private int RefreshArrows(Vector3 camPos, int used)
        {
            int count = 0;
            var zones = LabWindZone.All;
            if (zones != null)
            {
                for (int i = 0; i < zones.Count && count < MaxArrowCandidates; i++)
                {
                    var zone = zones[i];
                    if (zone == null) continue;
                    float speed = zone.Speed;
                    arrowCandPos[count] = zone.transform.position + Vector3.up * WindZoneArrowHeight;
                    arrowCandDir[count] = Quaternion.Euler(0f, zone.YawDeg, 0f) * Vector3.forward;
                    arrowCandLength[count] = Mathf.Clamp(1f + speed * 0.15f, 1f, 4f);
                    arrowCandSpeed[count] = speed;
                    count++;
                }
            }
            var fields = MagicField.Active;
            for (int i = 0; i < fields.Count && count < MaxArrowCandidates; i++)
            {
                var field = fields[i];
                if (field == null || field.Element != ElementType.Wind || field.WindSpeed <= 0.01f) continue;
                Vector3 dir = field.MainDirection;
                if (dir.sqrMagnitude < 1e-6f) continue;
                arrowCandPos[count] = field.Frame.Origin;
                arrowCandDir[count] = dir.normalized;
                arrowCandLength[count] = Mathf.Clamp(field.ConeLength, 0.8f, 8f);
                arrowCandSpeed[count] = field.WindSpeed;
                count++;
            }

            int picked = LabOverlayMath.SelectNearest(camPos, arrowCandPos, count, Range, arrowPick, arrowPickDist);
            for (int k = 0; k < picked; k++)
            {
                int c = arrowPick[k];
                Vector3 tail = arrowCandPos[c];
                Vector3 dir = arrowCandDir[c];
                Vector3 tip = tail + dir * arrowCandLength[c];
                DrawArrow(arrows[k], tail, tip, dir, arrowCandLength[c]);

                BeginText();
                AppendFixed(arrowCandSpeed[c], 1);
                Append(" m/s");
                CommitSlot(used++, SlotKind.Fixed, (tail + tip) * 0.5f, 0.3f, windColor, null, null);
            }
            for (int k = picked; k < ArrowCount; k++)
                if (arrows[k].gameObject.activeSelf) arrows[k].gameObject.SetActive(false);
            return used;
        }

        private void DrawArrow(LineRenderer arrow, Vector3 tail, Vector3 tip, Vector3 dir, float length)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            float head = Mathf.Min(0.6f, length * 0.3f);
            arrowPoints[0] = tail;
            arrowPoints[1] = tip;
            arrowPoints[2] = tip - dir * head + side * head * 0.6f;
            arrowPoints[3] = tip;
            arrowPoints[4] = tip - dir * head - side * head * 0.6f;
            arrow.widthMultiplier = 0.05f;
            arrow.SetPositions(arrowPoints);
            if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);
        }

        private int RefreshSpells(Vector3 camPos, int used)
        {
            float now = Time.time;
            int count = 0;
            for (int i = 0; i < MaxSpells; i++)
            {
                ref SpellMark s = ref spells[i];
                if (s.Time <= 0f) continue;
                if (now - s.Time > SpellLabelSeconds) { s = default; continue; }
                spellPos[count] = s.Canvas != null ? s.Canvas.WorldCenter : s.Position;
                spellSlot[count] = i;
                count++;
            }
            int capacity = Mathf.Min(MaxSpells, LabelCount - used);
            if (capacity <= 0 || count == 0) return used;
            int picked = LabOverlayMath.SelectNearest(camPos, spellPos, count, Range, spellPick, spellPickDist);
            if (picked > capacity) picked = capacity;
            for (int k = 0; k < picked; k++)
            {
                SpellMark s = spells[spellSlot[spellPick[k]]];
                int e = (int)s.Element;
                BeginText();
                Append(e >= 0 && e < ElementNames.Length ? ElementNames[e] : ElementNames[0]);
                Append("\n<size=80%>fuerza ");
                AppendFixed(s.Force, 2);
                Append("   alcance ");
                AppendFixed(s.Range, 2);
                Append("\ndur. ");
                AppendFixed(s.Duration, 1);
                Append(" s   calidad ");
                AppendFixed(s.Quality, 2);
                Append("</size>");
                float height = SpellLabelHeight + SpellStackStep * s.Stack;
                if (s.Canvas != null) CommitSlot(used++, SlotKind.Canvas, s.Position, height, ElementColor(s.Element), null, s.Canvas);
                else CommitSlot(used++, SlotKind.Fixed, s.Position, height, ElementColor(s.Element), null, null);
            }
            return used;
        }

        private int CollectMovables(Vector3 camPos)
        {
            var all = Movable.All;
            int count = 0;
            for (int i = 0; i < all.Count && count < MaxMovableCandidates; i++)
            {
                var m = all[i];
                if (m == null) continue;
                var body = m.Body;
                movablePos[count] = body != null ? body.worldCenterOfMass : m.transform.position;
                movableRef[count] = m;
                count++;
            }
            // Las referencias viejas al final del buffer no se usan (count manda); se limpian para no retener objetos.
            for (int i = count; i < MaxMovableCandidates && movableRef[i] != null; i++) movableRef[i] = null;
            return LabOverlayMath.SelectNearest(camPos, movablePos, count, Range, movablePick, movablePickDist);
        }

        private void WriteMovable(int slot, Movable m)
        {
            var body = m.Body;
            float mass = m.Mass;
            float speed = body != null ? body.linearVelocity.magnitude : 0f;
            BeginText();
            AppendFixed(mass, mass < 10f ? 1 : 0);
            Append(" kg\n<size=80%>");
            AppendFixed(speed < 0.05f ? 0f : speed, 1);
            Append(" m/s</size>");
            CommitSlot(slot, SlotKind.Movable, Vector3.zero, MovableLabelHeight, movableColor, m, null);
        }

        private int CollectFire(Vector3 camPos)
        {
            var fire = GrassFire.Current;
            if (fire == null || fire.BurningCells == 0) return 0;
            int n = fire.SampleBurning(camPos, Range, firePos, fireGen);
            return LabOverlayMath.SelectNearest(camPos, firePos, n, Range, firePick, firePickDist);
        }

        private int WriteFire(int used, int share)
        {
            var fire = GrassFire.Current;
            float spread = fire.SpreadChance;
            float decay = fire.GenerationDecay;
            int maxGen = fire.MaxGenerations;
            bool modified = Mathf.Abs(spread - DefaultFireSpread) > 1e-4f || Mathf.Abs(decay - DefaultFireDecay) > 1e-4f ||
                            maxGen != DefaultFireMaxGenerations;
            for (int k = 0; k < share; k++)
            {
                int c = firePick[k];
                int g = fireGen[c];
                BeginText();
                Append("gen. ");
                AppendInt(g);
                // Misma regla que GrassFire: las de generacion mayor que maxGenerations ya no propagan.
                if (g > maxGen) Append("\n<size=80%>no contagia</size>");
                else
                {
                    Append("\n<size=80%>contagio ");
                    AppendFixed(spread * Mathf.Pow(decay, g) * 100f, 2);
                    Append(" %</size>");
                }
                if (modified) Append("\n<size=70%>fuego modificado</size>");
                CommitSlot(used++, SlotKind.Fixed, firePos[c], FireLabelHeight, modified ? fireModifiedColor : fireColor, null, null);
            }
            return used;
        }

        private Color ElementColor(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fireColor;
                case ElementType.Water: return new Color(0.45f, 0.75f, 1f);
                case ElementType.Wind: return windColor;
                case ElementType.Earth: return new Color(0.85f, 0.7f, 0.45f);
                case ElementType.Light: return new Color(1f, 0.95f, 0.55f);
                default: return movableColor;
            }
        }

        // ---------------------------------------------------------------- slots

        /// <summary>
        /// Asigna el texto en construccion al slot. Solo reenvia a TMP (y recalcula la placa) si el texto o el color cambiaron.
        /// </summary>
        private void CommitSlot(int slot, SlotKind kind, Vector3 anchor, float height, Color color, Movable movable, DrawingCanvas canvas)
        {
            // Tope de etiquetas nuevas por refresco: las que no entran quedan apagadas y se encienden en el proximo tick.
            if (!labels[slot].gameObject.activeSelf)
            {
                if (newLabelsThisRefresh >= MaxNewLabelsPerRefresh) return;
                newLabelsThisRefresh++;
            }
            slotKind[slot] = kind;
            slotAnchor[slot] = anchor;
            slotHeight[slot] = height;
            slotMovable[slot] = movable;
            slotCanvas[slot] = canvas;

            var text = labels[slot];
            if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
            if (slotColor[slot] != color)
            {
                slotColor[slot] = color;
                text.color = color;
            }

            char[] stored = slotText[slot];
            bool same = slotTextLength[slot] == scratchLength;
            for (int i = 0; same && i < scratchLength; i++) same = stored[i] == scratch[i];
            if (same) return;

            System.Array.Copy(scratch, stored, scratchLength);
            slotTextLength[slot] = scratchLength;
            text.SetCharArray(stored, 0, scratchLength);

            var plate = plates[slot];
            if (plate != null)
            {
                text.ForceMeshUpdate();
                Bounds b = text.textBounds;
                plate.localPosition = new Vector3(b.center.x, b.center.y, 0.01f);
                plate.localScale = new Vector3(b.size.x + PlatePadding * 2f, b.size.y + PlatePadding, 1f);
            }
        }

        private void HideSlot(int slot)
        {
            if (slotKind[slot] == SlotKind.None) return;
            slotKind[slot] = SlotKind.None;
            slotMovable[slot] = null;
            slotCanvas[slot] = null;
            labels[slot].gameObject.SetActive(false);
        }

        private void HideAll()
        {
            if (!anyShown) return;
            for (int i = 0; i < LabelCount; i++) HideSlot(i);
            for (int i = 0; i < ArrowCount; i++)
                if (arrows[i] != null && arrows[i].gameObject.activeSelf) arrows[i].gameObject.SetActive(false);
            anyShown = false;
        }

        // ---------------------------------------------------------------- texto sin allocations

        private void BeginText() => scratchLength = 0;

        private void Append(string s)
        {
            for (int i = 0; i < s.Length && scratchLength < TextCapacity; i++) scratch[scratchLength++] = s[i];
        }

        private void Append(char c)
        {
            if (scratchLength < TextCapacity) scratch[scratchLength++] = c;
        }

        private void AppendInt(long value)
        {
            if (value < 0) { Append('-'); value = -value; }
            int n = 0;
            do
            {
                digits[n++] = (char)('0' + (int)(value % 10));
                value /= 10;
            } while (value > 0 && n < digits.Length);
            while (n > 0) Append(digits[--n]);
        }

        /// <summary>Numero con coma decimal (como el resto del laboratorio) y una cantidad fija de decimales.</summary>
        private void AppendFixed(float value, int decimals)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) { Append('-'); return; }
            long scale = 1;
            for (int i = 0; i < decimals; i++) scale *= 10;
            long scaled = (long)System.Math.Round(System.Math.Abs((double)value) * scale);
            if (value < 0f && scaled != 0) Append('-');
            AppendInt(scaled / scale);
            if (decimals <= 0) return;
            Append(',');
            long frac = scaled % scale;
            for (long p = scale / 10; p > 0; p /= 10)
            {
                Append((char)('0' + (int)(frac / p)));
                frac %= p;
            }
        }
    }
}
