using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Cuerpo fisico del hechizo de VIENTO. El empuje (cono de arrastre cuadratico, mas potente con tinta poderosa) lo hace
    /// <see cref="MagicField"/> para el elemento Viento; aca se agregan: <see cref="WindCone"/> (sesgo del fuego del pasto y
    /// consulta para apagar llamas chicas) y, si el hechizo sale hacia arriba, <see cref="WindColumn"/> (corriente ascendente
    /// para elevarse y planear). Se destruyen con el hechizo.
    /// </summary>
    public static class WindSpell
    {
        /// <summary>Componente vertical (0-1) del chorro o la normal a partir del cual sale una columna ascendente.</summary>
        public const float UpwardThreshold = 0.6f;

        public static void Spawn(SpellIR spell, SealFrame frame, ElementLook look, DrawingCanvas source, GameObject spellGo)
        {
            var field = spellGo.GetComponent<MagicField>();
            if (field == null) return;
            float ink = spell.inkPower > 0f ? spell.inkPower : 1f;
            bool jet = spell.plan != null && spell.plan.Has(PlanPrimitive.Jet);
            Vector3 dir = field.MainDirection;

            var cone = spellGo.AddComponent<WindCone>();
            cone.Init(spell, jet, field.WindSpeed);

            bool upward = dir.y > UpwardThreshold || frame.Normal.y > UpwardThreshold;
            if (upward)
            {
                var column = spellGo.AddComponent<WindColumn>();
                column.Init(spell, frame, ink);
                Debug.Log($"[Viento] columna ascendente: altura {column.Height:0.0} m, radio {column.Radius:0.00} m, velocidad {column.MaxSpeed:0.0} m/s (tinta x{ink:0.0})");
            }
            else
            {
                Debug.Log($"[Viento] cono: longitud {field.ConeLength:0.0} m, viento {field.WindSpeed:0.0} m/s (tinta x{ink:0.0})");
            }
        }
    }
}
