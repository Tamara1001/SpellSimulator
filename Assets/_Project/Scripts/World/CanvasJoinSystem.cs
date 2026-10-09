using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.DebugTools;

namespace SpellSimulator.World
{
    /// <summary>
    /// Sello partido (canon Sylph Shoes) para cualquier lienzo: si dos lienzos con el anillo ABIERTO quedan pegados borde con
    /// borde (cualquier borde, mismo plano, tinta hacia el mismo lado) durante <see cref="joinSeconds"/>, sus trazos se
    /// fusionan en un lienzo temporal; si el anillo unido queda cerrado, el hechizo se lanza con <see cref="SpellCaster.Cast"/>
    /// y las dos mitades se gastan (se borran) con un enfriamiento. Las runas armadas (anillo cerrado) NO pasan por aca:
    /// se fusionan por contacto con <see cref="RuneJoin"/>. La crea SpellCaster.Start.
    /// </summary>
    [DisallowMultipleComponent]
    public class CanvasJoinSystem : MonoBehaviour
    {
        private const int MaxCandidates = 12;
        private const int MaxPairs = MaxCandidates * (MaxCandidates - 1) / 2;

        [SerializeField, Tooltip("Segundos entre revisiones de los pares de candidatos.")]
        private float checkInterval = 0.25f;
        [SerializeField, Tooltip("Segundos que deben estar pegadas las mitades para unirse.")]
        private float joinSeconds = 0.25f;
        [SerializeField, Tooltip("Segundos de enfriamiento de las mitades tras unirse.")]
        private float cooldownSeconds = 2f;
        [SerializeField, Tooltip("Segundos que sigue vivo el lienzo temporal del hechizo (es la fuente del campo magico).")]
        private float mergedLifetime = 10f;
        private struct Pair
        {
            public DrawingCanvas a;
            public DrawingCanvas b;
            public float held;
            /// <summary>Ya se evaluo y no se pudo unir: no reintenta hasta que se separen o se edite alguna.</summary>
            public bool blocked;
            public bool seen;
            /// <summary>Ya se aviso "las mitades encajan" a este par.</summary>
            public bool announced;
        }

        private struct Cooldown
        {
            public DrawingCanvas canvas;
            public float until;
        }

        private readonly List<DrawingCanvas> candidates = new List<DrawingCanvas>(MaxCandidates);
        private readonly CanvasRect[] rects = new CanvasRect[MaxCandidates];
        private readonly bool[] usable = new bool[MaxCandidates];
        private readonly List<Pair> pairs = new List<Pair>(MaxPairs);
        private readonly List<Cooldown> cooldowns = new List<Cooldown>(MaxCandidates);

        private SpellCaster caster;
        private float timer;

        private void Awake() => caster = GetComponent<SpellCaster>();

        private void OnEnable()
        {
            if (caster != null) caster.CanvasEdited += OnCanvasEdited;
        }

        private void OnDisable()
        {
            if (caster != null) caster.CanvasEdited -= OnCanvasEdited;
            candidates.Clear();
            pairs.Clear();
            cooldowns.Clear();
        }

        // ---------------------------------------------------------------- candidatos

        /// <summary>
        /// Candidato: cualquier lienzo con trazos cuyo anillo propio NO esta cerrado (fase Drawing, aunque tenga un arco de menos
        /// del 52 %, o Prepared). Armed/Spent/Charging tienen el anillo cerrado: esos no (las runas se fusionan con RuneJoin).
        /// </summary>
        private bool Qualifies(DrawingCanvas canvas)
        {
            if (canvas == null || !canvas.isActiveAndEnabled || canvas.Session.IsEmpty) return false;
            SealPhase phase = caster.GetPhase(canvas);
            return phase == SealPhase.Prepared || phase == SealPhase.Drawing;
        }

        private void OnCanvasEdited(DrawingCanvas canvas)
        {
            if (canvas == null) return;
            // Un lienzo editado vuelve a ser evaluable con cualquiera.
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].a != canvas && pairs[i].b != canvas) continue;
                Pair p = pairs[i];
                p.blocked = false;
                pairs[i] = p;
            }

            int index = candidates.IndexOf(canvas);
            if (!Qualifies(canvas))
            {
                if (index >= 0) candidates.RemoveAt(index);
                return;
            }
            if (index >= 0) return;
            if (candidates.Count >= MaxCandidates) candidates.RemoveAt(0); // el mas viejo sale
            candidates.Add(canvas);
        }

        // ---------------------------------------------------------------- revision periodica

        private void Update()
        {
            if (caster == null) return;
            timer += Time.deltaTime;
            if (timer < checkInterval) return;
            float elapsed = timer;
            timer = 0f;
            Check(elapsed);
        }

        private void Check(float elapsed)
        {
            // Depurar candidatos (destruidos, vacios, armados, gastados...) y vencer enfriamientos.
            for (int i = candidates.Count - 1; i >= 0; i--)
                if (!Qualifies(candidates[i])) candidates.RemoveAt(i);
            for (int i = cooldowns.Count - 1; i >= 0; i--)
                if (cooldowns[i].canvas == null || Time.time >= cooldowns[i].until) cooldowns.RemoveAt(i);

            int count = candidates.Count;
            for (int i = 0; i < pairs.Count; i++)
            {
                Pair p = pairs[i];
                p.seen = false;
                pairs[i] = p;
            }
            for (int i = 0; i < count; i++)
            {
                usable[i] = !InCooldown(candidates[i]);
                if (usable[i]) rects[i] = RectOf(candidates[i]);
            }

            for (int i = 0; i < count; i++)
            {
                if (!usable[i]) continue;
                for (int j = i + 1; j < count; j++)
                {
                    if (!usable[j] || !CanvasJoinMath.AreAdjacent(rects[i], rects[j])) continue;
                    int k = FindOrAddPair(candidates[i], candidates[j]);
                    if (k < 0) continue;
                    Pair p = pairs[k];
                    p.seen = true;
                    if (!p.blocked)
                    {
                        p.held += elapsed;
                        if (!p.announced)
                        {
                            p.announced = true;
                            Player.Hud.Notify("join", "Las mitades encajan...", 1f);
                        }
                    }
                    pairs[k] = p;
                }
            }

            // Los pares que dejaron de estar pegados se reinician.
            for (int i = pairs.Count - 1; i >= 0; i--)
                if (!pairs[i].seen || pairs[i].a == null || pairs[i].b == null) pairs.RemoveAt(i);

            for (int i = 0; i < pairs.Count; i++)
            {
                Pair p = pairs[i];
                if (p.blocked || p.held < joinSeconds) continue;
                bool joined = TryJoin(p.a, p.b);
                // Si no se unio (anillo abierto o hechizo invalido) queda bloqueado hasta que algo cambie.
                p.blocked = !joined;
                p.held = 0f;
                pairs[i] = p;
                if (joined) return; // la lista de candidatos cambio
            }
        }

        private int FindOrAddPair(DrawingCanvas a, DrawingCanvas b)
        {
            for (int i = 0; i < pairs.Count; i++)
                if ((pairs[i].a == a && pairs[i].b == b) || (pairs[i].a == b && pairs[i].b == a)) return i;
            if (pairs.Count >= MaxPairs) return -1;
            pairs.Add(new Pair { a = a, b = b });
            return pairs.Count - 1;
        }

        private bool InCooldown(DrawingCanvas canvas)
        {
            for (int i = 0; i < cooldowns.Count; i++)
                if (cooldowns[i].canvas == canvas) return Time.time < cooldowns[i].until;
            return false;
        }

        private static CanvasRect RectOf(DrawingCanvas canvas)
        {
            Transform t = canvas.transform;
            Vector2 size = canvas.WorldSize;
            // La cara "con tinta": dos lienzos pegados deben tener la tinta hacia el mismo lado.
            return new CanvasRect(canvas.WorldCenter, t.right, t.up, canvas.FaceNormal * canvas.InkSign, size.x, size.y);
        }

        // ---------------------------------------------------------------- union

        /// <summary>Devuelve true si las mitades se unieron y se lanzo un hechizo valido.</summary>
        private bool TryJoin(DrawingCanvas a, DrawingCanvas b)
        {
            CanvasRect ra = RectOf(a), rb = RectOf(b);
            // Lo que pasa una vez por union (no por frame): la fusion reserva memoria.
            DrawingSession merged = CanvasJoinMath.Merge(ra, a.Session, rb, b.Session, out MergedLayout layout);
            // Cada mitad debe aportar >= 25 % del anillo; si la costura quedo abierta (>= 85 % de cobertura) se cierra con un puente.
            if (!caster.TryCloseRing(ref merged, new Vector2(layout.width, layout.height), a.Session.Strokes.Count)) return false;

            DrawingCanvas joined = CreateJoinedCanvas(a, layout, merged, out GameObject go);
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke("<b>[Sello partido]</b> Las dos mitades se unieron.");
            Debug.Log($"[Mitades] {a.name} + {b.name} se unieron (envolvente {layout.width:F2} x {layout.height:F2} m).", this);

            SpellIR spell = caster.Cast(joined);
            Destroy(go, mergedLifetime);
            if (!(spell.valid && spell.active)) return false;
            Player.Hud.Notify("join", "Sello completado al unir las mitades", 3f);

            // Las mitades se gastan: se borran y descansan un rato.
            a.Clear();
            b.Clear();
            caster.OnSealEdited(a);
            caster.OnSealEdited(b);
            float until = Time.time + cooldownSeconds;
            cooldowns.Add(new Cooldown { canvas = a, until = until });
            cooldowns.Add(new Cooldown { canvas = b, until = until });
            return true;
        }

        /// <summary>
        /// Lienzo temporal oculto con la malla y el material de A, del tamano del envolvente y en su marco: solo existe para
        /// leer y lanzar el hechizo (y como fuente del campo magico).
        /// </summary>
        private static DrawingCanvas CreateJoinedCanvas(DrawingCanvas a, MergedLayout layout, DrawingSession merged, out GameObject go)
        {
            go = new GameObject("CanvasJoin") { layer = a.gameObject.layer };
            go.SetActive(false); // los componentes se despiertan al activarlo, con todo ya configurado

            Vector2 sizeA = a.WorldSize;
            float ratioX = layout.width / Mathf.Max(sizeA.x, 1e-6f);
            float ratioY = layout.height / Mathf.Max(sizeA.y, 1e-6f);
            Vector3 scale = a.transform.lossyScale;
            Vector3 offset = a.CenterOffsetLocalScaled;
            Quaternion rotation = a.transform.rotation;
            // El centro de la superficie (no el pivote) debe caer en el centro del envolvente.
            Vector3 scaledOffset = new Vector3(offset.x * ratioX, offset.y * ratioY, offset.z);
            go.transform.SetPositionAndRotation(layout.center - rotation * scaledOffset, rotation);
            go.transform.localScale = new Vector3(scale.x * ratioX, scale.y * ratioY, scale.z);

            var sourceFilter = a.GetComponent<MeshFilter>();
            go.AddComponent<MeshFilter>().sharedMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = a.GetComponent<Renderer>().sharedMaterial;
            renderer.enabled = false;
            var joined = go.AddComponent<DrawingCanvas>();
            joined.Prepare(512, a.BrushMaterial, new Color(0f, 0f, 0f, 0f), 0.02f, EmissionSide.InkSide);
            go.SetActive(true);
            foreach (var stroke in merged.Strokes) joined.Session.AddStroke(stroke);
            joined.SetInkSign(a.InkSign);
            return joined;
        }
    }
}
