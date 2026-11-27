using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// Palanca del laboratorio (paso V, D33): la primera vez desbloquea el interruptor «sin daño» y lo enciende; después lo
    /// prende y apaga. El farolito (<see cref="glow"/>) muestra el estado. Sirve igual en PC (E) y en VR (gatillo).
    /// </summary>
    public class NoDamageLever : MonoBehaviour, IPressable
    {
        [SerializeField, Tooltip("Farolito encendido mientras el jugador no recibe daño.")]
        private GameObject glow;

        [SerializeField, Tooltip("Mango de la palanca: se inclina hacia un lado u otro segun el estado.")]
        private Transform handle;

        private const float HandleAngle = 28f;

        public string GetPressPrompt() => NoDamageSetting.On ? "Volver a recibir daño" : "Jugar sin daño (no morís)";

        public void Press()
        {
            NoDamageSetting.Toggle();
            Hud.Notify("vitality", NoDamageSetting.On ? "Sin daño: los hechizos ya no te lastiman." : "Daño activado: cuidado con el fuego.", 3f);
        }

        private void OnEnable()
        {
            NoDamageSetting.Changed += Show;
            Show();
        }

        private void OnDisable() => NoDamageSetting.Changed -= Show;

        private void Show()
        {
            bool on = NoDamageSetting.On;
            if (glow != null && glow.activeSelf != on) glow.SetActive(on);
            if (handle != null) handle.localRotation = Quaternion.Euler(on ? HandleAngle : -HandleAngle, 0f, 0f);
        }
    }
}
