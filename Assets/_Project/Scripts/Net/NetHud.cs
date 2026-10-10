using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Cartel minimo de la sala (F1): "Sala CODIGO · N jugadores" arriba a la derecha mientras hay conexion. Es IMGUI (solo PC/WebGL);
    /// el HUD definitivo en world-space para VR llega con KI-095.
    /// </summary>
    public sealed class NetHud : MonoBehaviour
    {
        private GUIStyle style;
        private string cached = "";
        private int cachedPlayers = -1;
        private string cachedCode = "";

        private void OnGUI()
        {
            NetSession s = NetSession.Exists ? NetSession.Instance : null;
            if (s == null || !s.IsConnected) return;
            if (MainMenuActive()) return;
            NetDuel.Active?.DrawHud();
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 15, fontStyle = FontStyle.Bold };
                style.normal.textColor = new Color(1f, 0.95f, 0.8f);
            }
            int players = PlayerRegistry.Count;
            if (players != cachedPlayers || s.JoinCode != cachedCode)
            {
                cachedPlayers = players; cachedCode = s.JoinCode;
                cached = "Sala " + s.JoinCode + "  ·  " + players + (players == 1 ? " jugador" : " jugadores");
            }
            var rect = new Rect(Screen.width - 330f, 8f, 320f, 26f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), cached, style);
            GUI.color = Color.white;
            GUI.Label(rect, cached, style);
        }

        private static bool MainMenuActive() => UI.MainMenu.IsActive;
    }
}
