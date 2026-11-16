using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Colores del Laboratorio compartidos por el generador (MapLab) y las piezas de runtime (cartel, lentes): el color de cada
    /// grupo se repite en franja, perilla, cartel y lentes (LABORATORIO_V3_PLAN (c).8, v4 punto 11).
    /// </summary>
    public static class LabPalette
    {
        public static readonly Color Recipe = new Color(0.9f, 0.45f, 0.25f);   // 1 Hechizo
        public static readonly Color Signs = new Color(0.42f, 0.7f, 0.36f);    // 2 Signos
        public static readonly Color Ink = new Color(0.62f, 0.44f, 0.82f);     // 3 Papel y tinta
        public static readonly Color Tuning = new Color(0.36f, 0.62f, 0.78f);  // 4 Afinado
        public static readonly Color Paper = new Color(0.36f, 0.52f, 0.82f);   // 5 Papel de prueba / objetos
        public static readonly Color Neutral = new Color(0.62f, 0.58f, 0.52f);

        /// <summary>Color de un grupo del sello (1..5); neutro fuera de rango.</summary>
        public static Color Group(int group)
        {
            switch (group)
            {
                case 1: return Recipe;
                case 2: return Signs;
                case 3: return Ink;
                case 4: return Tuning;
                case 5: return Paper;
                default: return Neutral;
            }
        }

        /// <summary>Version oscura (texto sobre pergamino): legible sobre crema.</summary>
        public static Color Dark(Color c, float k = 0.55f) => new Color(c.r * k, c.g * k, c.b * k, 1f);
    }
}
