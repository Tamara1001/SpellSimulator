using System.Collections.Generic;

namespace SpellSimulator.World
{
    /// <summary>
    /// Reglas puras del tope global de objetos sueltos (<see cref="WorldObjectBudget"/>): decide cuales sacar cuando se pasa
    /// el maximo total o el maximo por tipo. Siempre se van los mas viejos y nunca los protegidos (en una mano o cerca del jugador).
    /// </summary>
    public static class BudgetMath
    {
        public struct Entry
        {
            /// <summary>Instante de creacion (cuanto menor, mas viejo).</summary>
            public float birthTime;
            /// <summary>Tipo de suministro (limite por tipo).</summary>
            public string typeKey;
            /// <summary>En una mano o cerca del jugador: no se puede sacar.</summary>
            public bool isProtected;
        }

        /// <summary>
        /// Llena <paramref name="result"/> con los indices de <paramref name="entries"/> a sacar. Primero recorta el exceso
        /// de cada tipo y despues el exceso total, siempre del mas viejo al mas nuevo. Si todo lo que sobra esta protegido,
        /// el tope queda superado hasta que deje de estarlo.
        /// </summary>
        public static void SelectEvictions(IReadOnlyList<Entry> entries, int maxTotal, int maxPerType, List<int> result)
        {
            result.Clear();
            int n = entries.Count;
            for (int i = 0; i < n; i++)
            {
                string key = entries[i].typeKey;
                bool seen = false;
                for (int j = 0; j < i; j++)
                    if (entries[j].typeKey == key) { seen = true; break; }
                if (seen) continue;

                int count = 0;
                for (int j = 0; j < n; j++) if (entries[j].typeKey == key) count++;
                for (int excess = count - maxPerType; excess > 0; excess--)
                {
                    int oldest = OldestFree(entries, result, key);
                    if (oldest < 0) break;
                    result.Add(oldest);
                }
            }
            for (int remaining = n - result.Count; remaining > maxTotal; remaining--)
            {
                int oldest = OldestFree(entries, result, null);
                if (oldest < 0) break;
                result.Add(oldest);
            }
        }

        /// <summary>Indice del mas viejo no protegido ni elegido (de un tipo, o de cualquiera si <c>key</c> es null); -1 si no hay.</summary>
        private static int OldestFree(IReadOnlyList<Entry> entries, List<int> chosen, string key)
        {
            int best = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e.isProtected || (key != null && e.typeKey != key) || chosen.Contains(i)) continue;
                if (best < 0 || e.birthTime < entries[best].birthTime) best = i;
            }
            return best;
        }
    }
}
