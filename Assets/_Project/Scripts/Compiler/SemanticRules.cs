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
                    // Convierte el hechizo en un haz enfocado
                    spell.primaryManifestation = ManifestationType.Column;
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
                case SignType.Repetition:
                case SignType.Pull:
                case SignType.Collection:
                case SignType.Billowing:
                case SignType.Crush:
                    // Las matematicas puras de estos modificadores ahora son manejadas 
                    // de forma matricial (SignSemantics) en el SpellCompiler.
                    break;

            }
        }
    }
}
