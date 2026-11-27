using System;
using System.Collections.Generic;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Etapa sugerida del recorrido del modo Libre según los libros leídos (Docs/MAPA_FLUJO_PLAN.md §9 y D32). Solo sugiere:
    /// el tablero del mapa hace latir los marcadores de la etapa, nada se bloquea. Adaptado al mapa actual: la torre enseña
    /// con los tomos I y II (no con «Los cuatro elementos») y no existe todavía el libro de sellos del entrenamiento, así
    /// que al salir de la torre se sugieren a la vez el entrenamiento y el Archivo.
    /// </summary>
    public static class LearningFlow
    {
        public enum Stage { Tower, TrainingAndArchive, ElementFields, LateGame }

        private static readonly string[] TowerBooks = { "wind-primer", "earth-primer" };
        private static readonly string[] ArchiveBooks = { "variants", "motion", "shape", "rare" };
        private static readonly string[] ElementSpellBooks = { "spells-water", "spells-earth", "spells-fire", "spells-wind" };

        // Ids de zona de Data/WorldZones.json (los cubre LearningFlowTests contra el catálogo real).
        private static readonly string[] TowerZones = { "tower" };
        private static readonly string[] TrainingArchiveZones = { "training", "archive" };
        private static readonly string[] FieldZones = { "water-lab", "earth-workshop", "fire-field", "wind-field" };
        private static readonly string[] LateZones = { "watchtower", "stone-circle", "shrine", "cave", "cabin-forest" };

        /// <summary>Etapa según los libros leídos: gana la más avanzada alcanzada (leer un libro de un campo sin pasar por el Archivo cuenta igual).</summary>
        public static Stage StageFor(Func<string, bool> hasRead)
        {
            if (hasRead == null) return Stage.Tower;
            if (Any(ElementSpellBooks, hasRead)) return Stage.LateGame;
            if (Any(ArchiveBooks, hasRead)) return Stage.ElementFields;
            if (Any(TowerBooks, hasRead)) return Stage.TrainingAndArchive;
            return Stage.Tower;
        }

        /// <summary>Zonas a resaltar en esa etapa (arreglos compartidos: no modificarlos).</summary>
        public static IReadOnlyList<string> ZonesFor(Stage stage)
        {
            switch (stage)
            {
                case Stage.TrainingAndArchive: return TrainingArchiveZones;
                case Stage.ElementFields: return FieldZones;
                case Stage.LateGame: return LateZones;
                default: return TowerZones;
            }
        }

        /// <summary>Todas las zonas que alguna etapa puede sugerir (para los tests).</summary>
        public static IEnumerable<string> AllSuggestedZones()
        {
            foreach (Stage s in Enum.GetValues(typeof(Stage)))
                foreach (string id in ZonesFor(s)) yield return id;
        }

        private static bool Any(string[] books, Func<string, bool> hasRead)
        {
            for (int i = 0; i < books.Length; i++)
                if (hasRead(books[i])) return true;
            return false;
        }
    }
}
