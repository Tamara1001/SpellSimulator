using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Plantillas desactivadas de la escena que el juego clona durante la partida (suministros, reciclaje), por clave de ruta. El anfitrion avisa
    /// "cloné esta plantilla en esta pose" y cada cliente busca la misma por su clave y la clona igual (F4 de MULTIPLAYER_PLAN).
    /// </summary>
    public static class NetTemplates
    {
        private static readonly Dictionary<string, GameObject> byKey = new Dictionary<string, GameObject>(32);

        public static string KeyOf(GameObject template) => template != null ? NetBodyMath.PathKey(template.transform) : "";

        public static void Register(GameObject template)
        {
            if (template != null) byKey[KeyOf(template)] = template;
        }

        public static GameObject Find(string key) => key != null && byKey.TryGetValue(key, out GameObject go) && go != null ? go : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => byKey.Clear();
    }
}
