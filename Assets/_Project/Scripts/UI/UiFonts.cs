using TMPro;
using UnityEngine;

namespace SpellSimulator.UI
{
    /// <summary>
    /// Tipografias del proyecto (Docs/UI_ESTETICA.md), cargadas de Resources/Fonts: Nunito (HUD y lecturas; ademas es la fuente por defecto de TMP),
    /// Lora (libros y grimorio) y Caveat Brush (carteles). Se cachean al primer uso; si falta un asset devuelven null y TMP usa su fuente por defecto.
    /// </summary>
    public static class UiFonts
    {
        private static TMP_FontAsset body, book, sign;

        public static TMP_FontAsset Body => body != null ? body : body = Resources.Load<TMP_FontAsset>("Fonts/Nunito SDF");
        public static TMP_FontAsset Book => book != null ? book : book = Resources.Load<TMP_FontAsset>("Fonts/Lora SDF");
        public static TMP_FontAsset Sign => sign != null ? sign : sign = Resources.Load<TMP_FontAsset>("Fonts/CaveatBrush SDF");
    }
}
