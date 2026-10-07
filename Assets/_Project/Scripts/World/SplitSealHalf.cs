using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.DebugTools;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Mitad de un sello partido (SplitJoin). Dos objetos con el mismo `pairId` se dibujan cada uno con su parte del
    /// sello; al juntarlos lado a lado (cara con tinta hacia el mismo lado) durante <see cref="joinSeconds"/>, los trazos
    /// se fusionan en un lienzo temporal del tamaño de las dos mitades y el hechizo se lanza como siempre
    /// (<c>SpellCaster.Cast</c>). Una mitad sola nunca lanza nada: por eso es un <see cref="ISealTrigger"/>.
    /// </summary>
    [RequireComponent(typeof(DrawingCanvas))]
    public class SplitSealHalf : MonoBehaviour, ISealTrigger
    {
        [SerializeField, Tooltip("Las dos mitades de un sello comparten este id.")]
        private string pairId = "A";
        [SerializeField] private SpellCaster caster;
        [SerializeField, Tooltip("Segundos que deben estar pegadas para unirse.")]
        private float joinSeconds = 0.4f;
        [SerializeField, Tooltip("Segundos de espera tras unirse antes de poder volver a unirse.")]
        private float cooldownSeconds = 2f;
        [SerializeField, Tooltip("Segundos que sigue vivo el lienzo temporal del hechizo (es la fuente del campo magico).")]
        private float mergedLifetime = 10f;

        private static readonly List<SplitSealHalf> all = new List<SplitSealHalf>();

        private DrawingCanvas canvas;
        private float touching;
        private float cooldownUntil;

        public DrawingCanvas Canvas => canvas;
        public string PairId => pairId;

        public void Configure(string id, SpellCaster spellCaster)
        {
            pairId = id;
            caster = spellCaster;
        }

        private void Awake() => canvas = GetComponent<DrawingCanvas>();
        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        private void FixedUpdate()
        {
            if (caster == null || Time.time < cooldownUntil || canvas.Session.IsEmpty) { touching = 0f; return; }
            SplitSealHalf other = FindRightNeighbour();
            if (other == null) { touching = 0f; return; }
            touching += Time.fixedDeltaTime;
            if (touching >= joinSeconds) Join(other);
        }

        // Solo la mitad izquierda busca: la derecha es la que esta pegada a su lado derecho.
        private SplitSealHalf FindRightNeighbour()
        {
            Vector2 size = canvas.WorldSize;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == this || other.pairId != pairId || other.canvas.Session.IsEmpty) continue;
                if (SplitJoinMath.IsRightNeighbour(transform.position, transform.right, canvas.FaceNormal, size.x,
                        other.transform.position, other.canvas.FaceNormal, other.canvas.WorldSize.x)) return other;
            }
            return null;
        }

        private void Join(SplitSealHalf other)
        {
            touching = 0f;
            float cooldown = Time.time + cooldownSeconds;
            cooldownUntil = cooldown;
            other.cooldownUntil = cooldown;

            Vector2 sizeLeft = canvas.WorldSize, sizeRight = other.canvas.WorldSize;
            var merged = SplitJoinMath.Merge(canvas.Session, sizeLeft.x, other.canvas.Session, sizeRight.x);

            // Lienzo temporal con la misma malla y material que la mitad izquierda, del ancho de las dos.
            var go = new GameObject($"SplitSeal_{pairId}") { layer = gameObject.layer };
            go.SetActive(false);
            Vector3 center = (transform.position + other.transform.position) * 0.5f;
            go.transform.SetPositionAndRotation(center, transform.rotation);
            go.transform.localScale = new Vector3(sizeLeft.x + sizeRight.x, Mathf.Max(sizeLeft.y, sizeRight.y), 1f);
            var sourceFilter = GetComponent<MeshFilter>();
            go.AddComponent<MeshFilter>().sharedMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetComponent<Renderer>().sharedMaterial;
            renderer.enabled = false; // no se ve: solo existe para leer y lanzar
            var joined = go.AddComponent<DrawingCanvas>();
            joined.Prepare(512, canvas.BrushMaterial, new Color(0f, 0f, 0f, 0f), 0.02f, EmissionSide.InkSide);
            go.SetActive(true);
            foreach (var stroke in merged.Strokes) joined.Session.AddStroke(stroke);
            joined.SetInkSign(canvas.InkSign);

            DiagnosticBus.OnDiagnosticsUpdated?.Invoke($"<b>[Sello partido]</b> Las dos mitades se unieron ({pairId}).");
            SpellIR spell = caster.Cast(joined);
            Destroy(go, mergedLifetime);

            // Un sello unido queda gastado: las mitades se borran para volver a usarlas.
            if (spell.valid && spell.active)
            {
                canvas.Clear();
                other.canvas.Clear();
                caster.OnSealEdited(canvas);
                caster.OnSealEdited(other.canvas);
            }
        }
    }
}
