using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>Niveles (0..1, salvo el pulso) de las partes de un portal en un instante: disco, grabados, borde y luz.</summary>
    public struct PortalLevels
    {
        /// <summary>Apertura del disco (_Open del shader): 0 oscuro, 1 remolino brillante.</summary>
        public float Disc;
        /// <summary>Brillo de los grabados de la identidad.</summary>
        public float Glyphs;
        /// <summary>Borde luminoso del disco.</summary>
        public float Rim;
        /// <summary>Intensidad relativa de la luz puntual.</summary>
        public float Light;
        /// <summary>Carga suavizada (0..1) mientras carga; 0 en los demas estados.</summary>
        public float Charge;
    }

    /// <summary>
    /// Curva pura de la animacion de los portales (la usa <see cref="PortalVisuals"/>; se prueba en PortalVisualCurveTests).
    /// Dormido: disco oscuro y grabados tenues. Cargando: grabados y borde crecen con la carga (monotono) y el pulso se
    /// acelera. Abierto: todo encendido; en los ultimos <see cref="WarnSeconds"/> s parpadea como aviso de cierre.
    /// Enfriando: se apaga en <see cref="CooldownFadeSeconds"/> s.
    /// </summary>
    public static class PortalVisualCurve
    {
        public const float MaxLightRange = 4f;
        public const float WarnSeconds = 3f;
        public const float WarnHz = 2.5f;
        public const float CooldownFadeSeconds = 1.5f;
        public const float DormantGlyphs = 0.12f;
        public const float DormantRim = 0.04f;

        private const float ChargeDisc = 0.3f;
        private const float ChargeLight = 0.25f;
        private const float CooldownStart = 0.6f;
        private const float WarnDepthMin = 0.35f, WarnDepthMax = 0.75f;

        /// <param name="progress01">Cargando: avance de la carga. Enfriando: avance del apagado (0 recien cerrado, 1 apagado).</param>
        /// <param name="openSecondsLeft">Segundos que le quedan abierto (solo cuenta en Open).</param>
        /// <param name="time">Tiempo (s) para el parpadeo final.</param>
        public static PortalLevels Evaluate(PortalState state, float progress01, float openSecondsLeft, float time)
        {
            var l = new PortalLevels();
            float p = Mathf.Clamp01(progress01);
            switch (state)
            {
                case PortalState.Charging:
                    float c = ChargeCurve(p);
                    l.Charge = c;
                    l.Glyphs = Mathf.Lerp(DormantGlyphs, 1f, c);
                    l.Rim = Mathf.Lerp(DormantRim, 1f, c);
                    l.Disc = ChargeDisc * c;
                    l.Light = ChargeLight * c;
                    break;
                case PortalState.Open:
                    float blink = Blink(openSecondsLeft, time);
                    l.Disc = blink;
                    l.Rim = blink;
                    l.Light = blink;
                    l.Glyphs = 0.7f + 0.3f * blink;
                    break;
                case PortalState.Cooldown:
                    float k = CooldownStart * (1f - p);
                    l.Disc = k;
                    l.Rim = Mathf.Max(DormantRim, k);
                    l.Light = k;
                    l.Glyphs = Mathf.Max(DormantGlyphs, k);
                    break;
                default:
                    l.Glyphs = DormantGlyphs;
                    l.Rim = DormantRim;
                    break;
            }
            return l;
        }

        /// <summary>Carga suavizada (smoothstep): monotona, 0 en 0 y 1 en 1.</summary>
        public static float ChargeCurve(float progress01)
        {
            float p = Mathf.Clamp01(progress01);
            return p * p * (3f - 2f * p);
        }

        /// <summary>Encendido escalonado de los grabados mientras carga: el grabado index se enciende en su tramo de la carga.</summary>
        public static float Staggered(float charge, int index, int count)
        {
            if (count <= 1) return Mathf.Lerp(DormantGlyphs, 1f, Mathf.Clamp01(charge));
            float local = Mathf.Clamp01(Mathf.Clamp01(charge) * count - index);
            return Mathf.Lerp(DormantGlyphs, 1f, local);
        }

        /// <summary>1 hasta los ultimos WarnSeconds; despues oscila entre 1 y un minimo que baja a medida que se acaba el tiempo.</summary>
        public static float Blink(float secondsLeft, float time)
        {
            if (secondsLeft >= WarnSeconds) return 1f;
            float k = 1f - Mathf.Clamp01(secondsLeft / WarnSeconds);
            float depth = Mathf.Lerp(WarnDepthMin, WarnDepthMax, k);
            float wave = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * WarnHz * time);
            return 1f - depth * (1f - wave);
        }

        /// <summary>Multiplicador de pulso (alrededor de 1): quieto dormido, rapido cargando, lento abierto.</summary>
        public static float Pulse(PortalState state, float charge, float time)
        {
            switch (state)
            {
                case PortalState.Charging:
                    float hz = Mathf.Lerp(1f, 5f, Mathf.Clamp01(charge));
                    return 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * hz * time);
                case PortalState.Open:
                    return 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 0.8f * time);
                default:
                    return 1f;
            }
        }

        /// <summary>Color propio de cada portal (estable): los cuatro del mapa tienen uno fijo; el resto sale de un hash del id.</summary>
        public static Color TintFor(string id)
        {
            switch (id)
            {
                case "tower-study": return new Color(0.68f, 0.5f, 1f);
                case "watchtower": return new Color(0.45f, 0.8f, 1f);
                case "field-circle": return new Color(0.5f, 1f, 0.62f);
                case "cave": return new Color(1f, 0.7f, 0.35f);
            }
            if (string.IsNullOrEmpty(id)) return new Color(0.75f, 0.65f, 1f);
            uint h = 2166136261u;
            for (int i = 0; i < id.Length; i++) h = (h ^ id[i]) * 16777619u;
            return Color.HSVToRGB((h % 360u) / 360f, 0.5f, 1f);
        }
    }
}
