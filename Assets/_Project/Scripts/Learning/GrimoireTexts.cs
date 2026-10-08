using TMPro;
using UnityEngine;

namespace SpellSimulator.Learning
{
    /// <summary>Una hoja doble del grimorio: titulo y texto de la pagina izquierda y de la derecha.</summary>
    public readonly struct GrimoireSpread
    {
        public readonly string LeftTitle, LeftBody, RightTitle, RightBody;

        public GrimoireSpread(string leftTitle, string leftBody, string rightTitle, string rightBody)
        {
            LeftTitle = leftTitle;
            LeftBody = leftBody;
            RightTitle = rightTitle;
            RightBody = rightBody;
        }
    }

    /// <summary>
    /// Textos fijos del grimorio (Magia, Sellos, Controles) y el formato comun (titulos con inicial, filas de teclas).
    /// Breves a proposito: se leen en un libro flotando a ~0.8 m.
    /// </summary>
    public static class GrimoireTexts
    {
        /// <summary>Tinta de los textos y colores de acento (rubrica roja, oro viejo).</summary>
        public static readonly Color Ink = new Color(0.23f, 0.13f, 0.07f);
        public const string Rubric = "#7a1f14";
        public const string Faded = "#7a5a3a";
        public const string Gold = "#c9a24a";

        /// <summary>Titulo con inicial grande en rojo y el resto en versalitas.</summary>
        public static string Title(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return $"<cspace=0.04em><color={Rubric}><size=130%>{text.Substring(0, 1)}</size></color><smallcaps>{text.Substring(1)}</smallcaps></cspace>";
        }

        /// <summary>Encabezado chico de seccion dentro de una pagina.</summary>
        public static string Heading(string text) => $"<smallcaps><b><color={Rubric}>{text}</color></b></smallcaps>";

        /// <summary>Fila tecla / descripcion con sangria colgante (la descripcion envuelta queda alineada).</summary>
        public static string Row(string key, string description, int indentPercent = 34) =>
            $"<b><color=#5a2410>{key}</color></b><indent={indentPercent}%>{description}</indent>\n";

        private static string Intro(string text) => $"<i><color={Faded}>{text}</color></i>\n\n";

        // ---------------------------------------------------------------- Magia

        public static readonly GrimoireSpread[] Magic =
        {
            new GrimoireSpread(
                "La magia",
                "Para hacer magia sólo hacen falta dos cosas: <b>tinta de conjuro</b> y saber dibujar <b>sellos</b>. Cualquiera puede aprender.\n\n" +
                "Un sello tiene tres partes: el <b>sigilo</b> al centro, los <b>signos</b> alrededor y el <b>anillo</b> que lo encierra todo.\n\n" +
                "El hechizo se lanza en el momento en que se cierra el anillo.",
                "Tres principios",
                "<b>I.</b> El sigilo elige el elemento: fuego, agua, tierra, viento o luz.\n\n" +
                "<b>II.</b> Los signos le dan forma, tamaño y dirección: columna, levitación, atracción, dispersión...\n\n" +
                "<b>III.</b> La prolijidad importa: un sello torpe o chico es débil o falla.\n\n" +
                $"<i><color={Faded}>Borrar, mojar o romper un sello detiene su magia.</color></i>"),
            new GrimoireSpread(
                "Objetos",
                Row("Pluma", "dibuja con tinta", 30) +
                Row("Pincel", "trazo grueso, gasta más", 30) +
                Row("Goma", "borra trazo a trazo", 30) +
                Row("Trapo", "limpia toda la hoja", 30) +
                Row("Tintero", "recarga la pluma (R)", 30) +
                Row("Estampa", "copia un glifo del libro", 30) +
                "\nPapeles, cuadernos, pizarras y caballetes son superficies para dibujar. Cajas, pesas y piedras se mueven y se arrojan.",
                "Cómo se usan",
                "Con <b>E</b> tomás una herramienta y queda en tu barra (<b>1-2-3</b>): <b>clic izquierdo</b> la usa y <b>clic derecho</b> deshace.\n\n" +
                "Con <b>E</b> sobre una hoja, una caja o este libro, lo arrastrás con la otra mano: la <b>rueda</b> lo acerca, <b>F</b> da vuelta la hoja y <b>V</b> la fija a la pantalla para dibujar cómodo."),
            new GrimoireSpread(
                "El dibujo",
                "Dibujá el sigilo grande y centrado. Agregá los signos alrededor, sin tocarlo. Cerrá el anillo de un solo trazo: ahí se lanza.\n\n" +
                "El hechizo sale por la <b>cara con tinta</b> de la hoja: dala vuelta (<b>F</b>) para apuntarlo.\n\n" +
                "Cuanto más grande el anillo, más fuerte el hechizo.",
                "Este libro",
                "<b>Tab</b> lo abre y lo cierra (<b>ESC</b> si está a pantalla completa). Apuntá a un botón y hacé <b>clic</b>; <b>Z</b> y <b>X</b> pasan las páginas.\n\n" +
                "<b>E</b> sobre el libro lo agarra para moverlo y otra <b>E</b> lo suelta: queda flotando. <b>B</b> lo trae a pantalla completa.\n\n" +
                "Cada glifo que descubrís queda escrito en <b>Glifos</b>."),
        };

        // ---------------------------------------------------------------- Sellos

        public static readonly GrimoireSpread[] Seals =
        {
            new GrimoireSpread(
                "Sellos",
                "Un sello cerrado se lanza al instante por la cara con tinta. Si el anillo queda <b>abierto</b>, el sello está preparado: brilla y espera a que lo cierres.\n\n" +
                "También se dibuja sobre el piso, la torre y las cajas: el sello se pega ahí y sale hacia afuera. Un cráter, fuego o agua encima lo rompen.",
                "Estampas",
                "Hay estampas en la mesa del campo y en el escritorio. Tomá una (<b>E</b>), abrí un glifo en <b>Glifos</b> y apretá <b>Cargar en la estampa</b>.\n\n" +
                "Después, <b>clic izquierdo</b> sobre un papel o el piso la estampa (5 usos). Un sigilo sale con el anillo abierto: cerralo con la pluma."),
            new GrimoireSpread(
                "En objetos",
                "Las <b>piedras de impacto</b> y la <b>placa de presión</b> guardan el sello armado: dibujalo cerrado y se lanza con un golpe fuerte o con peso encima.\n\n" +
                "La placa se vuelve a armar sola y aguanta 5 usos.",
                "Sellos partidos",
                "Dibujá cada parte del sello en una <b>mitad de piedra</b> y juntalas lado a lado, con la tinta hacia el mismo lado: los trazos se unen y se lanza.\n\n" +
                $"<i><color={Faded}>Una mitad sola nunca lanza.</color></i>"),
        };

        // ---------------------------------------------------------------- Controles

        public static readonly GrimoireSpread[] Controls =
        {
            new GrimoireSpread(
                "Moverse",
                Row("W A S D", "caminar") +
                Row("Shift", "correr") +
                Row("C", "tumbarse si la mantenés; un toque, agacharse") +
                Row("Ctrl", "agacharse") +
                Row("Espacio", "saltar") +
                Row("Alt izq.", "zoom (mantenido)") +
                "\n" + Heading("Menú") + "\n" +
                Row("Tab", "abre o cierra el grimorio") +
                Row("Z / X", "página anterior / siguiente") +
                Row("B", "grimorio a pantalla") +
                Row("F3", "depuración") +
                Row("Retroceso", "reiniciar el mundo"),
                "Mano principal",
                Intro("La barra 1-2-3: el objeto equipado.") +
                Row("E", "agarra herramientas y estampas a la barra") +
                Row("Clic izq.", "usa") +
                Row("Clic der.", "deshace") +
                Row("Rueda", "grosor de la pluma o la goma") +
                Row("R", "recarga tinta") +
                Row("Q", "arroja")),
            new GrimoireSpread(
                "Mano secundaria",
                Intro("Lo agarrado con E se arrastra.") +
                Row("Rueda", "acerca o aleja lo agarrado", 44) +
                Row("Clic der.", "gira con el mouse", 44) +
                Row("Shift + rueda", "inclina", 44) +
                Row("F", "da vuelta la hoja", 44) +
                Row("V", "la fija a pantalla para dibujar", 44) +
                Row("Clic rueda", "lo deja flotando", 44) +
                Row("E", "suelta", 44),
                "Realidad virtual",
                Row("Gatillo", "dibuja", 44) +
                Row("Agarre", "agarra", 44) +
                Row("A", "deshace", 44) +
                Row("B", "arroja", 44) +
                Row("Stick izq.", "clic: corre", 44) +
                Row("Stick der.", "clic: da vuelta la hoja", 44) +
                Row("Y", "reinicia el mundo", 44)),
        };

        /// <summary>Texto de la pagina izquierda del indice de glifos.</summary>
        public static string GlyphIntro(int known, int total, int sigils, int signs) =>
            "Los glifos son los dibujos que forman un sello. Los <b>sigilos</b> van al centro y eligen el elemento; los <b>signos</b> van alrededor y le dan forma y rumbo.\n\n" +
            "Se aprenden leyendo los libros de la biblioteca o dibujando bien uno nuevo.\n\n" +
            $"<i><color={Faded}>Conocés {known} de {total}: {sigils} sigilos y {signs} signos.</color></i>";

        // ---------------------------------------------------------------- creacion de textos

        /// <summary>Crea un texto 3D (TextMeshPro) hijo de parent. Solo en la construccion del libro.</summary>
        public static TextMeshPro MakeText(string name, Transform parent, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = size;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            t.color = color;
            t.lineSpacing = 8f;
            t.paragraphSpacing = 0f;
            return t;
        }
    }
}
