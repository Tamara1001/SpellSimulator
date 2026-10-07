using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    public enum AnnotationKind { Sigil, Sign, Ambiguous, Unknown }

    /// <summary>Una marca sobre el papel: que se leyo en ese lugar y, si es un signo, hacia donde apunta.</summary>
    public struct AnnotationItem
    {
        public AnnotationKind Kind;
        /// <summary>Posicion en el espacio isotropico del pipeline (lado mayor del lienzo = 1).</summary>
        public Vector2 Position;
        public string Label;
        /// <summary>Direccion del signo (unitario, Y arriba). Cero si no tiene o no es confiable.</summary>
        public Vector2 Facing;
    }

    /// <summary>
    /// Arma las anotaciones de un sello leido (que glifo es cada cosa, hacia donde apunta cada signo y las advertencias).
    /// Logica pura: SealAnnotations solo las dibuja.
    /// </summary>
    public static class SealAnnotationLayout
    {
        public static List<AnnotationItem> Build(GlyphAST ast, SpellIR spell)
        {
            var items = new List<AnnotationItem>();
            if (ast == null || !ast.Ring.Found) return items;

            if (ast.PrimarySigil != null)
                items.Add(Item(ast.PrimarySigil, AnnotationKind.Sigil));

            // Los signos salen de la lectura del plan: ya traen la orientacion final (pose + zona muerta).
            var plan = spell.plan;
            if (plan != null && plan.Signs.Count > 0)
            {
                foreach (var s in plan.Signs)
                {
                    var recognition = FindSign(ast, s);
                    bool ambiguous = recognition != null && recognition.IsAmbiguous;
                    items.Add(new AnnotationItem
                    {
                        Kind = ambiguous ? AnnotationKind.Ambiguous : AnnotationKind.Sign,
                        Position = ast.Ring.Center + s.At * ast.Ring.Radius,
                        Label = recognition != null ? $"{s.Id} {recognition.Confidence:F2}" : s.Id,
                        Facing = s.FacingTrust >= PlanResolver.FacingTrustFloor ? s.Facing : Vector2.zero
                    });
                }
            }
            else
            {
                foreach (var sign in ast.Signs) items.Add(Item(sign, sign.IsAmbiguous ? AnnotationKind.Ambiguous : AnnotationKind.Sign));
            }

            foreach (var extra in ast.ExtraSigils) items.Add(Item(extra, AnnotationKind.Ambiguous));
            foreach (var unknown in ast.Unknowns)
                items.Add(new AnnotationItem
                {
                    Kind = AnnotationKind.Unknown,
                    Position = unknown.Candidate.Center,
                    Label = $"{unknown.Id}? {unknown.Confidence:F2}"
                });
            return items;
        }

        /// <summary>Advertencias del hechizo en espanol, sin repetir.</summary>
        public static List<string> Warnings(SpellIR spell)
        {
            var lines = new List<string>();
            if (spell.warnings == null) return lines;
            foreach (var w in spell.warnings)
            {
                string text = GlyphWarnings.Describe(w);
                if (!lines.Contains(text)) lines.Add(text);
            }
            return lines;
        }

        private static AnnotationItem Item(GlyphRecognition r, AnnotationKind kind) => new AnnotationItem
        {
            Kind = kind,
            Position = r.Candidate.Center,
            Label = $"{r.Id} {r.Confidence:F2}"
        };

        /// <summary>El signo reconocido mas cercano a la posicion de la lectura (ReadSigns saltea los que no tienen semantica).</summary>
        private static GlyphRecognition FindSign(GlyphAST ast, SignReading reading)
        {
            Vector2 at = ast.Ring.Center + reading.At * ast.Ring.Radius;
            GlyphRecognition best = null;
            float bestDistance = float.MaxValue;
            foreach (var sign in ast.Signs)
            {
                if (sign.Id != reading.Id) continue;
                float d = (sign.Candidate.Center - at).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = sign; }
            }
            return best;
        }
    }
}
