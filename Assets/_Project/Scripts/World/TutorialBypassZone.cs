using UnityEngine;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// [VR] Pasillo para saltear la viga del tutorial (agacharse es dificil con el visor): una abertura en la viga; atravesarla
    /// da por hecho «agacharse». La reja y el resto de las barreras no se tocan. Lo arma <see cref="TowerTutorial"/> al empezar en VR.
    /// </summary>
    public class TutorialBypassZone : MonoBehaviour
    {
        private TowerTutorial tutorial;

        public void Init(TowerTutorial owner) => tutorial = owner;

        private void OnTriggerEnter(Collider other)
        {
            if (tutorial == null || tutorial.Crouched) return;
            if (other.GetComponentInParent<PlayerMovement>() != null) tutorial.MarkTerraceDone(true, false);
        }
    }
}
