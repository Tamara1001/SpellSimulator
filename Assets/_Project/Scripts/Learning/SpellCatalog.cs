using System;
using System.Collections.Generic;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Catalogo de hechizos del proyecto (Data/Spells/*.json): prearmados ("inicial") y de libros de hechizos.
    /// Un hechizo de libro esta desbloqueado cuando ese libro fue leido (el progreso ya vive en DiscoveryState).
    /// Logica pura. El guardado de hechizos del jugador es otra fase (ADR-034, H1).
    /// </summary>
    public class SpellCatalog
    {
        private readonly List<SpellDef> spells = new List<SpellDef>();
        private readonly Dictionary<string, SpellDef> byId = new Dictionary<string, SpellDef>();

        /// <summary>Catalogo cargado por el grimorio (null hasta que se carga). Lo leen los libros de hechizos.</summary>
        public static SpellCatalog Shared { get; set; }

        /// <summary>
        /// Lo instala Discovery: dice si un hechizo fue descubierto lanzandolo. Asi <see cref="IsUnlocked(SpellDef, Func{string, bool})"/>
        /// y <see cref="Unlocked(Func{string, bool}, List{SpellDef})"/> (que usa el grimorio) lo cuentan sin cambiar su firma.
        /// </summary>
        public static Func<string, bool> DiscoveredCheck { get; set; }

        public IReadOnlyList<SpellDef> All => spells;

        /// <summary>Lee varios JSON (uno por hechizo). Los invalidos se saltean y se avisan en onError. Ids repetidos: gana el primero.</summary>
        public static SpellCatalog FromJsons(IEnumerable<string> jsons, Action<string> onError = null)
        {
            var catalog = new SpellCatalog();
            foreach (var json in jsons)
            {
                try
                {
                    var def = SpellDef.FromJson(json);
                    if (catalog.byId.ContainsKey(def.Id)) { onError?.Invoke($"Hechizo repetido '{def.Id}'."); continue; }
                    catalog.byId[def.Id] = def;
                    catalog.spells.Add(def);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Hechizo invalido: {e.Message}");
                }
            }
            catalog.spells.Sort((a, b) => a.Difficulty != b.Difficulty ? a.Difficulty.CompareTo(b.Difficulty) : string.CompareOrdinal(a.Id, b.Id));
            return catalog;
        }

        /// <summary>Agrega un hechizo propio del jugador (al final; siempre desbloqueado). False si el id ya existe.</summary>
        public bool AddPlayer(SpellDef def)
        {
            if (def == null || byId.ContainsKey(def.Id)) return false;
            byId[def.Id] = def;
            spells.Add(def);
            return true;
        }

        /// <summary>Quita un hechizo propio (los del proyecto no se pueden quitar).</summary>
        public bool RemovePlayer(string id)
        {
            if (!byId.TryGetValue(id ?? "", out SpellDef def) || def.Origin != SpellLibrary.PlayerOrigin) return false;
            byId.Remove(id);
            spells.Remove(def);
            return true;
        }

        public SpellDef Get(string id) => id != null && byId.TryGetValue(id, out var s) ? s : null;

        /// <summary>True si es inicial o su libro fue leido. bookRead: id de libro -> leido.</summary>
        public static bool IsUnlocked(SpellDef def, Func<string, bool> bookRead) =>
            IsUnlocked(def, bookRead, DiscoveredCheck);

        /// <summary>True si es inicial, su libro fue leido o ya se descubrio lanzandolo (spellKnown: id de hechizo -> descubierto).</summary>
        public static bool IsUnlocked(SpellDef def, Func<string, bool> bookRead, Func<string, bool> spellKnown) =>
            def.IsInitial || (bookRead != null && bookRead(def.Book)) || (spellKnown != null && spellKnown(def.Id));

        /// <summary>Pista del hechizo («Se dice que...»), o "" si no existe o no tiene.</summary>
        public string Hint(string id) => Get(id)?.Hint ?? "";

        /// <summary>Hechizos desbloqueados (limpia y llena dst).</summary>
        public void Unlocked(Func<string, bool> bookRead, List<SpellDef> dst)
        {
            dst.Clear();
            for (int i = 0; i < spells.Count; i++)
                if (IsUnlocked(spells[i], bookRead)) dst.Add(spells[i]);
        }

        // ------------------------------------------------------------------ descubrimiento por experimentacion

        /// <summary>
        /// Hechizo del catalogo que coincide con lo lanzado: mismo sigilo y mismo multiconjunto de signos (el tamano y
        /// la posicion se ignoran). Si varios comparten composicion (Rafaga y Abanico de viento), desempata por el giro
        /// de los signos (twistsDeg, grados respecto de "hacia adentro", 180 = hacia afuera) y, sin giros, gana el primero
        /// del catalogo. Null si ninguno coincide. Funcion pura.
        /// </summary>
        public SpellDef MatchCast(string sigilId, IList<string> signIds, IList<float> twistsDeg = null)
        {
            if (string.IsNullOrEmpty(sigilId)) return null;
            int count = signIds != null ? signIds.Count : 0;
            SpellDef best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < spells.Count; i++)
            {
                SpellDef def = spells[i];
                if (def.Sigil != sigilId || def.Signs.Count != count || !SameSigns(def, signIds)) continue;
                float score = twistsDeg != null && twistsDeg.Count == count ? TwistScore(def, twistsDeg) : 0f;
                if (score < bestScore) { best = def; bestScore = score; }
            }
            return best;
        }

        private static bool SameSigns(SpellDef def, IList<string> signIds)
        {
            int count = def.Signs.Count;
            if (count == 0) return true;
            // Multiconjunto sin allocar: cada signo lanzado tacha uno igual de la receta (count es chico, <= ~4).
            ulong used = 0;
            for (int i = 0; i < count; i++)
            {
                bool found = false;
                for (int j = 0; j < count; j++)
                {
                    if ((used & (1UL << j)) != 0 || def.Signs[j].Id != signIds[i]) continue;
                    used |= 1UL << j;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }

        private static float TwistScore(SpellDef def, IList<float> twistsDeg)
        {
            int n = def.Signs.Count;
            var want = new float[n];
            var have = new float[n];
            for (int i = 0; i < n; i++)
            {
                want[i] = Fold(def.Signs[i].TurnDeg);
                have[i] = Fold(twistsDeg[i]);
            }
            Array.Sort(want);
            Array.Sort(have);
            float total = 0f;
            for (int i = 0; i < n; i++) total += Math.Abs(want[i] - have[i]);
            return total;
        }

        /// <summary>Giro absoluto en 0..180 (0 = adentro, 180 = afuera).</summary>
        private static float Fold(float deg)
        {
            deg = Math.Abs(deg % 360f);
            return deg > 180f ? 360f - deg : deg;
        }
    }
}
