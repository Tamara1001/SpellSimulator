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

        public SpellDef Get(string id) => id != null && byId.TryGetValue(id, out var s) ? s : null;

        /// <summary>True si es inicial o su libro fue leido. bookRead: id de libro -> leido.</summary>
        public static bool IsUnlocked(SpellDef def, Func<string, bool> bookRead) =>
            def.IsInitial || (bookRead != null && bookRead(def.Book));

        /// <summary>Hechizos desbloqueados (limpia y llena dst).</summary>
        public void Unlocked(Func<string, bool> bookRead, List<SpellDef> dst)
        {
            dst.Clear();
            for (int i = 0; i < spells.Count; i++)
                if (IsUnlocked(spells[i], bookRead)) dst.Add(spells[i]);
        }
    }
}
