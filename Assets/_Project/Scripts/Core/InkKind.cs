using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>Clase de tinta: normal, poderosa (hechizo mas potente) o resistente (funciona en el agua).</summary>
    public enum InkKind { Normal = 0, Powerful = 1, Resistant = 2 }

    /// <summary>Datos de cada clase de tinta (logica pura).</summary>
    public static class InkKindInfo
    {
        /// <summary>Cuanto mas gasta por metro respecto de la tinta normal.</summary>
        public static float CostMultiplier(InkKind kind) => kind == InkKind.Normal ? 1f : 2f;

        /// <summary>Multiplicador de la potencia del hechizo.</summary>
        public static float PowerMultiplier(InkKind kind) => kind == InkKind.Powerful ? 2.5f : 1f;

        /// <summary>True si el hechizo sigue funcionando bajo el agua.</summary>
        public static bool IsWaterproof(InkKind kind) => kind == InkKind.Resistant;

        public static Color Color(InkKind kind)
        {
            switch (kind)
            {
                case InkKind.Powerful: return new Color(0.847f, 0.106f, 0.376f); // carmesi #D81B60
                case InkKind.Resistant: return new Color(0f, 0.659f, 0.553f);    // verde azulado #00A896
                default: return new Color(0.12f, 0.14f, 0.30f);                  // azul oscuro
            }
        }

        public static string DisplayName(InkKind kind)
        {
            switch (kind)
            {
                case InkKind.Powerful: return "Tinta poderosa";
                case InkKind.Resistant: return "Tinta resistente";
                default: return "Tinta";
            }
        }
    }
}
