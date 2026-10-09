using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.Glyphs;
using SpellSimulator.Parsing;
using SpellSimulator.Recognition;

namespace SpellSimulator.Casting
{
    /// <summary>Un circulo interior de un sello compuesto: su anillo y los indices de los trazos que le pertenecen.</summary>
    public sealed class NestedInner
    {
        public RingInfo Ring;
        /// <summary>Indices en la sesion original (mismo orden en la cruda y en la limpia: StrokeCleaner conserva 1:1).</summary>
        public readonly List<int> StrokeIndices = new List<int>();
    }

    /// <summary>Particion de un sello compuesto: el anillo exterior y 2 a 4 interiores cerrados.</summary>
    public sealed class NestedLayout
    {
        public RingInfo Outer;
        public readonly List<NestedInner> Inners = new List<NestedInner>();
    }

    /// <summary>Lectura de un circulo interior. Las coordenadas son del espacio isotropico del lienzo (lado mayor = 1).</summary>
    public sealed class NestedSubReading
    {
        public NestedInner Inner;
        public SealReading Reading;
        public Vector2 CenterIso;
        public float RadiusIso;
        /// <summary>Transformacion isotropico -> sub-sesion (q = Pivot + (p - Pivot) * Boost), como SpellIR.analysisBoost.</summary>
        public float Boost;
        public Vector2 Pivot;

        // Los completa SpellCaster (metros, UV, tinta y amplificacion).
        public SpellIR Spell;
        public Vector2 CenterUV;

        public bool Launches => Spell.valid && Spell.active;
    }

    /// <summary>Lectura completa de un sello compuesto.</summary>
    public sealed class NestedReading
    {
        public NestedLayout Layout;
        public readonly List<NestedSubReading> Subs = new List<NestedSubReading>();
        public float Amplification = 1f;

        /// <summary>Indice del interior mas grande que se lanza (o el mas grande a secas si ninguno se lanza). -1 si no hay.</summary>
        public int LargestIndex()
        {
            int best = -1;
            for (int i = 0; i < Subs.Count; i++)
            {
                if (best < 0) { best = i; continue; }
                bool launches = Subs[i].Launches, bestLaunches = Subs[best].Launches;
                if (launches != bestLaunches) { if (launches) best = i; continue; }
                if (Subs[i].RadiusIso > Subs[best].RadiusIso) best = i;
            }
            return best;
        }
    }

    /// <summary>
    /// Sellos compuestos (canon de fans de Witch Hat Atelier: hechizos complejos con varios circulos; regla del proyecto
    /// aprobada por Ale): un anillo EXTERIOR cerrado contiene 2 a 4 anillos INTERIORES cerrados, cada uno con su sigilo y
    /// signos. Cada interior se lee como un sello completo (re-normalizado al tamanio habitual) y el exterior amplifica
    /// (<see cref="NestedMath.Amplification"/>). Maximo 2 niveles. Si algo no cuadra (un solo interior, alguno abierto,
    /// solapes, ninguno valido) TryRead devuelve false y el sello se lee como siempre (un solo circulo).
    /// La deteccion reusa RingDetector sin tocar sus constantes: el exterior es el mejor anillo de todos los trazos; los
    /// interiores se buscan iterando la deteccion sobre los trazos que quedan.
    /// </summary>
    public static class NestedSealReader
    {
        /// <summary>Radio al que se lleva cada interior para leerlo (el de las plantillas: HarnessSeals.RingRadius).</summary>
        public const float TargetRadius = 0.42f;
        /// <summary>Intentos de deteccion de interiores (descartando circulos que no cuadran).</summary>
        public const int MaxAttempts = 8;
        /// <summary>Agrandado minimo de un interior (evita un pivote degenerado si el interior ya es del tamanio habitual).</summary>
        public const float MinBoost = 1.05f;
        private static readonly Vector2 SubCenter = new Vector2(0.5f, 0.5f);

        /// <summary>
        /// Busca un sello compuesto en una sesion LIMPIA (espacio isotropico). True si hay un exterior cerrado con 2 a 4
        /// interiores cerrados, contenidos con margen, chicos y sin solaparse.
        /// </summary>
        public static bool TryLayout(DrawingSession cleaned, out NestedLayout layout)
        {
            layout = null;
            if (cleaned == null || cleaned.Strokes.Count < 2) return false;

            RingInfo outer = RingDetector.Detect(cleaned);
            if (!outer.Found || !outer.Complete) return false;

            var remaining = new DrawingSession();
            foreach (var s in cleaned.Strokes) if (!outer.Contains(s)) remaining.AddStroke(s);

            var rings = new List<RingInfo>(NestedMath.MaxInner);
            for (int attempt = 0; attempt < MaxAttempts && rings.Count < NestedMath.MaxInner && !remaining.IsEmpty; attempt++)
            {
                RingInfo ring = RingDetector.Detect(remaining);
                if (!ring.Found) break;
                foreach (var s in ring.Strokes) remaining.Strokes.Remove(s);

                // Un circulo que no cabe como interior (parte de un glifo, demasiado grande) se ignora.
                if (!NestedMath.FitsAsInner(ring.Center, ring.Radius, outer.Center, outer.Radius)) continue;
                // Interiores solapados o abiertos: no es un compuesto (se lee como hoy).
                foreach (var other in rings)
                    if (NestedMath.Overlaps(ring.Center, ring.Radius, other.Center, other.Radius)) return false;
                if (!ring.Complete) return false;

                rings.Add(ring);
                // Lo que esta adentro de este interior no se vuelve a buscar (maximo 2 niveles).
                for (int i = remaining.Strokes.Count - 1; i >= 0; i--)
                    if (NestedMath.BelongsTo(NestedMath.Centroid(remaining.Strokes[i].Points), ring.Center, ring.Radius))
                        remaining.Strokes.RemoveAt(i);
            }
            if (rings.Count < NestedMath.MinInner) return false;

            layout = new NestedLayout { Outer = outer };
            foreach (var ring in rings) layout.Inners.Add(new NestedInner { Ring = ring });
            Partition(cleaned, layout);
            return true;
        }

        /// <summary>
        /// Asigna cada trazo a un interior: los trazos de su anillo, y los que tienen el centroide dentro (con tolerancia;
        /// si cae en varios, el de centro mas cercano relativo al radio). El exterior y los trazos sueltos se ignoran (v1).
        /// </summary>
        public static void Partition(DrawingSession cleaned, NestedLayout layout)
        {
            for (int i = 0; i < cleaned.Strokes.Count; i++)
            {
                Stroke stroke = cleaned.Strokes[i];
                if (layout.Outer.Contains(stroke)) continue;

                NestedInner owner = null;
                foreach (var inner in layout.Inners)
                    if (inner.Ring.Contains(stroke)) { owner = inner; break; }

                if (owner == null)
                {
                    Vector2 c = NestedMath.Centroid(stroke.Points);
                    float bestRatio = float.MaxValue;
                    foreach (var inner in layout.Inners)
                    {
                        if (!NestedMath.BelongsTo(c, inner.Ring.Center, inner.Ring.Radius)) continue;
                        float ratio = Vector2.Distance(c, inner.Ring.Center) / inner.Ring.Radius;
                        if (ratio < bestRatio) { bestRatio = ratio; owner = inner; }
                    }
                }
                owner?.StrokeIndices.Add(i);
            }
        }

        /// <summary>
        /// Copia los trazos de un interior y los re-normaliza: el anillo interior queda centrado en (0,5; 0,5) con radio
        /// <see cref="TargetRadius"/>. Devuelve la transformacion como agrandado alrededor de un pivote
        /// (q = pivot + (p - pivot) * boost), igual que el agrandado de sellos chicos de SpellCaster.
        /// </summary>
        public static DrawingSession SubSession(DrawingSession source, NestedInner inner, out float boost, out Vector2 pivot)
        {
            Vector2 center = inner.Ring.Center;
            boost = Mathf.Max(TargetRadius / Mathf.Max(inner.Ring.Radius, 1e-4f), MinBoost);
            // q = p * k + (S - c * k)  ==  pivot + (p - pivot) * k  con  pivot = (S - c * k) / (1 - k).
            pivot = (SubCenter - center * boost) / (1f - boost);

            var sub = new DrawingSession();
            foreach (int index in inner.StrokeIndices)
            {
                Stroke original = source.Strokes[index];
                var points = new List<Vector2>(original.Points.Count);
                foreach (var p in original.Points) points.Add(SubCenter + (p - center) * boost);
                sub.AddStroke(new Stroke(points) { Width = original.Width, Ink = original.Ink });
            }
            return sub;
        }

        /// <summary>
        /// Lee un sello compuesto. <paramref name="rawIso"/>: trazos crudos ya pasados al espacio isotropico. Cada interior se
        /// limpia por separado (desde los trazos crudos, ya agrandados) y se lee con <see cref="SealReader.Read"/>.
        /// False si no hay compuesto o si ningun interior es valido y activo (el llamador sigue con la lectura de siempre).
        /// SpellIR de cada interior sale tal cual del compilador: metros, UV, tinta y amplificacion los completa SpellCaster.
        /// </summary>
        public static bool TryRead(DrawingSession rawIso, float tolerance, int splineResolution,
            IGlyphClassifier classifier, GlyphDictionary dictionary, float time, out NestedReading reading)
        {
            reading = null;
            if (rawIso == null || classifier == null || dictionary == null) return false;

            DrawingSession cleaned = StrokeCleaner.CleanSession(rawIso, tolerance, splineResolution);
            if (!TryLayout(cleaned, out NestedLayout layout)) return false;

            var result = new NestedReading { Layout = layout };
            var radii = new List<float>(layout.Inners.Count);
            bool anyActive = false;
            foreach (var inner in layout.Inners)
            {
                DrawingSession sub = SubSession(rawIso, inner, out float boost, out Vector2 pivot);
                SealReading subReading = SealReader.Read(StrokeCleaner.CleanSession(sub, tolerance, splineResolution), classifier, dictionary, time);
                anyActive |= subReading.Spell.valid && subReading.Spell.active;
                radii.Add(inner.Ring.Radius);
                result.Subs.Add(new NestedSubReading
                {
                    Inner = inner,
                    Reading = subReading,
                    CenterIso = inner.Ring.Center,
                    RadiusIso = inner.Ring.Radius,
                    Boost = boost,
                    Pivot = pivot,
                    Spell = subReading.Spell,
                });
            }
            if (!anyActive) return false;

            result.Amplification = NestedMath.Amplification(layout.Outer.Radius, radii);
            reading = result;
            return true;
        }
    }
}
