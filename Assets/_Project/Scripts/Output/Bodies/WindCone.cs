using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Reactions;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Extras del cono de viento que NO son empuje fisico (ese lo hace <see cref="MagicField"/> con arrastre cuadratico para el
    /// elemento Viento, para no duplicar la geometria del chorro): sesgo de propagacion del fuego del pasto hacia donde sopla
    /// y consulta para apagar llamas chicas. Se agrega desde <see cref="WindSpell.Spawn"/>.
    /// Apagado de llamas: ElementReactive no expone un metodo para apagar, asi que MagicWorld debe llamar a
    /// <see cref="ShouldSnuff"/> / <see cref="IsSmallFlame"/> (ver nota en WindSpell).
    /// </summary>
    [RequireComponent(typeof(MagicField))]
    public class WindCone : MonoBehaviour
    {
        /// <summary>Cada cuanto se informa el sesgo al pasto (s). El sesgo expira solo en GrassFire.</summary>
        private const float BiasInterval = 0.1f;
        /// <summary>Exposicion al viento (0-1) a partir de la cual se apaga una llama chica.</summary>
        public const float SnuffThreshold = 0.5f;
        /// <summary>Diagonal maxima de las bounds (m) para considerar una llama "chica" (vela, antorcha).</summary>
        public const float SmallFlameSize = 0.8f;
        /// <summary>Velocidad del viento (m/s) para sesgo maximo del fuego.</summary>
        private const float FullBiasSpeed = 10f;

        private MagicField field;
        private SpellManifestation manifestation;
        private bool hasJet;
        private float windSpeed;
        private float nextBias;

        public void Init(SpellIR spell, bool jet, float speed)
        {
            field = GetComponent<MagicField>();
            manifestation = GetComponent<SpellManifestation>();
            hasJet = jet;
            windSpeed = speed;
        }

        private void FixedUpdate()
        {
            if (!hasJet || field == null || manifestation == null || Time.time < nextBias) return;
            float t = manifestation.SinceStrike;
            if (t < 0f || !manifestation.InBody) return;
            nextBias = Time.time + BiasInterval;
            var grass = GrassFire.Current;
            if (grass == null) return;

            Vector3 dir = field.MainDirection;
            float length = field.ConeLength;
            float strength = Mathf.Clamp01(windSpeed / FullBiasSpeed) * manifestation.Envelope(t);
            // Centro y radio del cono (el sesgo se aplica en el plano del suelo, GrassFire ignora la componente vertical).
            Vector3 center = field.Frame.Origin + dir * (length * 0.5f);
            grass.AddWindBias(center, dir, strength, length * 0.5f + field.Frame.Scale);
        }

        /// <summary>Exposicion (0-1) al viento de todos los conos vivos en un punto. Sin allocations.</summary>
        public static float SnuffExposureAt(Vector3 point)
        {
            var fields = MagicField.Active;
            float best = 0f;
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].Element != ElementType.Wind) continue;
                float e = fields[i].ExposureAt(point);
                if (e > best) best = e;
            }
            return best;
        }

        /// <summary>True si el viento en el punto supera el umbral para apagar una llama chica.</summary>
        public static bool ShouldSnuff(Vector3 point) => SnuffExposureAt(point) >= SnuffThreshold;

        /// <summary>Llama chica: objeto ardiendo cuyo tamano total es menor que SmallFlameSize (velas, antorchas, fuegos pequenos).</summary>
        public static bool IsSmallFlame(ElementReactive reactive)
        {
            return reactive != null && reactive.State.Burning && reactive.WorldBounds.size.magnitude < SmallFlameSize;
        }
    }
}
