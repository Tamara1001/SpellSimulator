namespace SpellSimulator.UI
{
    /// <summary>
    /// Una doble pagina del lector de libros (<see cref="BookReaderUI"/>). La izquierda muestra la ilustracion: el glifo
    /// <see cref="GlyphId"/> trazado desde el diccionario, o una portada con <see cref="CoverTitle"/>, o un ornamento.
    /// La derecha muestra <see cref="Kicker"/> (si falta y hay glifo: "Sigilo"/"Signo"), <see cref="Heading"/>,
    /// <see cref="Body"/> y una <see cref="Note"/> al pie (por ejemplo "Ya conocias este glifo").
    /// </summary>
    public class ReaderPage
    {
        public string Kicker;
        public string Heading;
        public string Body;
        public string Note;
        /// <summary>Glifo del diccionario a ilustrar en la pagina izquierda (null: portada u ornamento).</summary>
        public string GlyphId;
        /// <summary>Titulo grande de portada en la pagina izquierda (si no hay glifo).</summary>
        public string CoverTitle;
        public string CoverSubtitle;
    }
}
