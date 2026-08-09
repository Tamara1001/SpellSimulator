using UnityEngine;

namespace SpellSimulator.Compiler
{
    /// <summary>
    /// Motor Semantico puro.
    /// Define las matematicas que alteran el comportamiento del hechizo segun los modificadores.
    /// </summary>
    public static class SemanticRules
    {
        /// <summary>
        /// Traduce el nombre del glifo (del json) al enum interno.
        /// </summary>
        public static SignType ParseSignName(string name)
        {
            switch (name.ToLower())
            {
                case "column": return SignType.Column;
                case "levitation": return SignType.Levitation;
                case "pull": return SignType.Pull;
                case "dispersion": return SignType.Dispersion;
                case "collection": return SignType.Collection;
                case "convergence": return SignType.Convergence;
                case "region": return SignType.Region;
                case "billowing": return SignType.Billowing;
                case "float": return SignType.Float;
                case "crush": return SignType.Crush;
                case "repetition": return SignType.Repetition;
                case "weave": return SignType.Weave;
                default: return SignType.None;
            }
        }

        /// <summary>
        /// Aplica las reglas de un signo sobre la IR del hechizo (modifica valores por referencia).
        /// Cada signo puede sumarse iterativamente.
        /// </summary>
        public static void ApplySign(ref SpellIR spell, SignType sign)
        {
            switch (sign)
            {
                case SignType.Column:
                    // Convierte el hechizo en un haz enfocado y de gran alcance
                    spell.primaryManifestation = ManifestationType.Column;
                    spell.focus = Mathf.Clamp01(spell.focus + 0.35f);
                    spell.range = Mathf.Clamp01(spell.range + 0.45f);
                    spell.spread = Mathf.Clamp01(spell.spread - 0.25f);
                    spell.force = Mathf.Clamp01(spell.force + 0.2f);
                    break;
                    
                case SignType.Levitation:
                case SignType.Float:
                    // Reduce la gravedad acumulativamente.
                    // Si la gravedad se anula casi por completo, el hechizo se vuelve una orbe suspendida.
                    spell.gravity = Mathf.Clamp01(spell.gravity - 0.35f); 
                    if (spell.gravity <= 0.1f)
                    {
                        spell.gravity = 0f;
                        spell.primaryManifestation = ManifestationType.Levitation;
                    }
                    break;

                case SignType.Dispersion:
                    // Esparce el efecto a los lados
                    spell.spread = Mathf.Clamp01(spell.spread + 0.4f);
                    spell.focus = Mathf.Clamp01(spell.focus - 0.3f);
                    spell.force = Mathf.Clamp01(spell.force + 0.1f);
                    break;

                case SignType.Convergence:
                    // Comprime fuertemente el lateral del hechizo
                    spell.primaryManifestation = ManifestationType.Convergence;
                    spell.spread = Mathf.Clamp01(spell.spread - 0.4f);
                    spell.focus = Mathf.Clamp01(spell.focus + 0.3f);
                    break;

                case SignType.Repetition:
                    // Escala fuertemente la potencia y duracion
                    spell.duration += 4.5f;
                    spell.effectScale += 1.2f;
                    break;

                case SignType.Pull:
                case SignType.Collection:
                    spell.spread = Mathf.Clamp01(spell.spread - 0.15f);
                    spell.focus = Mathf.Clamp01(spell.focus + 0.2f);
                    break;

                case SignType.Billowing:
                    // Expande el tamano base
                    spell.effectScale += 0.8f;
                    spell.spread = Mathf.Clamp01(spell.spread + 0.2f);
                    break;

                case SignType.Crush:
                    spell.force = Mathf.Clamp01(spell.force + 0.4f);
                    spell.focus = Mathf.Clamp01(spell.focus + 0.2f);
                    break;
            }
        }
    }
}
