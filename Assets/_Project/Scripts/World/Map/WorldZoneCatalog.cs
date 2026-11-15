using System;
using UnityEngine;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Catálogo de zonas del valle (Data/WorldZones.json, Docs/LABORATORIO_V3_PLAN.md (e)): una sola fuente de datos para el
    /// mapa de la torre. Cada zona dice su nombre, para qué sirve en una frase, su color y su origen (una constante de
    /// <see cref="ValleyShape"/> o un sitio de MapStructures). x/z son la posición de respaldo; el generador las reemplaza por
    /// la posición resuelta. Datos puros (JsonUtility): sin dependencias de escena.
    /// </summary>
    [Serializable]
    public class WorldZoneCatalog
    {
        /// <summary>Ruta del catálogo dentro del proyecto (la lee el generador del Editor y los tests).</summary>
        public const string AssetPath = "Assets/_Project/Data/WorldZones.json";

        /// <summary>Una zona del valle. Campos públicos en camelCase porque los llena JsonUtility.</summary>
        [Serializable]
        public class Zone
        {
            public string id = "";
            public string name = "";
            public string use = "";
            public string color = "#333333";
            public string origin = "";
            public float x;
            public float z;

            /// <summary>Posición en el plano XZ del mundo (m).</summary>
            public Vector2 Position => new Vector2(x, z);

            /// <summary>Color de la zona; gris oscuro si el texto no es un color HTML válido.</summary>
            public Color ParsedColor => ColorUtility.TryParseHtmlString(color, out Color c) ? c : new Color(0.2f, 0.2f, 0.2f);

            /// <summary>Color como «#RRGGBB» válido para el texto enriquecido de TMP.</summary>
            public string HtmlColor => "#" + ColorUtility.ToHtmlStringRGB(ParsedColor);
        }

        public Zone[] zones = Array.Empty<Zone>();

        /// <summary>Lee el catálogo; nunca devuelve null (un JSON vacío o roto da un catálogo sin zonas).</summary>
        public static WorldZoneCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new WorldZoneCatalog();
            WorldZoneCatalog catalog;
            try
            {
                catalog = JsonUtility.FromJson<WorldZoneCatalog>(json);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"[Mapa] WorldZones.json ilegible: {e.Message}");
                return new WorldZoneCatalog();
            }
            if (catalog == null) return new WorldZoneCatalog();
            if (catalog.zones == null) catalog.zones = Array.Empty<Zone>();
            return catalog;
        }

        /// <summary>
        /// Resuelve los orígenes que son constantes de <see cref="ValleyShape"/> (Tower, Training, Lake, Field, Spawn).
        /// False para los sitios de MapStructures (los resuelve el generador del Editor).
        /// </summary>
        public static bool TryResolveShape(string origin, out Vector2 position)
        {
            switch (origin)
            {
                case "Tower": position = ValleyShape.TowerCenter; return true;
                case "Training": position = ValleyShape.TrainingOrigin; return true;
                case "Lake": position = ValleyShape.LakeCenter; return true;
                case "Field": position = ValleyShape.FieldCenter; return true;
                case "Spawn": position = ValleyShape.Spawn; return true;
                default: position = Vector2.zero; return false;
            }
        }
    }
}
