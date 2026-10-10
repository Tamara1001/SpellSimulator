using System;
using System.Collections.Generic;
using SpellSimulator.Casting;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Que identidades de portal conoce el jugador. Es solo informativo (ADR-035): cualquiera puede escribir cualquier identidad en una placa;
    /// los libros de caminos solo anotan destinos en el grimorio. Logica pura.
    /// </summary>
    public static class PortalKnowledge
    {
        /// <summary>Catalogo compartido (lo carga el grimorio, como <see cref="SpellCatalog.Shared"/>).</summary>
        public static PortalRecipes Shared;

        /// <summary>Portales anotados por los libros leidos, en el orden de los libros. Sin repetidos.</summary>
        public static List<PortalDefinition> Known(PortalRecipes recipes, IEnumerable<BookInfo> books, Func<string, bool> bookRead)
        {
            var result = new List<PortalDefinition>();
            if (recipes == null || books == null || bookRead == null) return result;
            foreach (BookInfo book in books)
            {
                if (book.Portals.Count == 0 || !bookRead(book.Id)) continue;
                foreach (string id in book.Portals)
                {
                    PortalDefinition def = recipes.Find(id);
                    if (def != null && !result.Contains(def)) result.Add(def);
                }
            }
            return result;
        }

        /// <summary>Cantidad de portales distintos que enseñan todos los libros (el total que se puede llegar a conocer).</summary>
        public static int Total(PortalRecipes recipes, IEnumerable<BookInfo> books) => Known(recipes, books, _ => true).Count;
    }
}
