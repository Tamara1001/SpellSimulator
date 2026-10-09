using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Descubrimiento de hechizos por experimentacion: escucha <see cref="SpellCaster.SealAnalyzed"/> y, cuando un hechizo
    /// valido y activo coincide con uno del catalogo que el jugador no conoce, se lo pasa a <see cref="Discovery.OnCast(string, IList{string}, IList{float})"/>.
    /// Se crea solo (sin tocar SpellCaster ni la escena) y vuelve a buscar el caster si cambia la escena.
    /// </summary>
    public class SpellDiscoveryTracker : MonoBehaviour
    {
        private const float SearchInterval = 1f;

        private SpellCaster caster;
        private float nextSearch;
        private readonly List<string> signIds = new List<string>(8);
        private readonly List<float> twists = new List<float>(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SpellDiscoveryTracker>() != null) return;
            var go = new GameObject("SpellDiscoveryTracker");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<SpellDiscoveryTracker>();
        }

        private void Update()
        {
            if (caster != null || Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + SearchInterval;
            caster = FindFirstObjectByType<SpellCaster>();
            if (caster != null) caster.SealAnalyzed += OnSealAnalyzed;
        }

        private void OnDestroy()
        {
            if (caster != null) caster.SealAnalyzed -= OnSealAnalyzed;
        }

        private void OnSealAnalyzed(GlyphAST ast, SpellIR spell, DrawingCanvas canvas)
        {
            if (ast == null || !spell.valid || !spell.active || ast.PrimarySigil == null) return;
            var discovery = Discovery.Instance;
            if (discovery == null) return;

            signIds.Clear();
            twists.Clear();
            foreach (var sign in ast.Signs) signIds.Add(sign.Id);
            // El giro de cada signo desempata hechizos con la misma composicion (Rafaga vs Abanico de viento).
            var readings = PlanResolver.ReadSigns(ast);
            if (readings.Count == signIds.Count)
                for (int i = 0; i < readings.Count; i++) twists.Add(Mathf.Abs(PlanResolver.TwistFromInwardDeg(readings[i])));

            discovery.OnCast(ast.PrimarySigil.Id, signIds, twists.Count == signIds.Count ? twists : null);
        }
    }
}
