using UnityEngine;

namespace SpellSimulator.Player
{
    /// <summary>
    /// [VR] Modelo visible de una mano (el control). Se esconde mientras esa mano lleva algo (lapiz, lienzo, libro...) para que no
    /// tape lo que se dibuja, y vuelve cuando queda libre. Sin colisionadores: la mano no empuja nada. Solo cambia la visibilidad
    /// cuando cambia el inventario (sin trabajo por cuadro).
    /// </summary>
    public class VrHandVisual : MonoBehaviour
    {
        [SerializeField, Tooltip("True para la mano derecha.")]
        private bool isRight;
        [SerializeField, Tooltip("Modelo del control que se muestra u oculta.")]
        private GameObject model;

        private PlayerInteractor interactor;

        private void Start()
        {
            interactor = GetComponentInParent<PlayerInteractor>();
            if (interactor == null) return;
            interactor.InventoryChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (interactor != null) interactor.InventoryChanged -= Refresh;
        }

        private void Refresh()
        {
            if (model != null && interactor != null) model.SetActive(!interactor.IsHandOccupied(isRight));
        }
    }
}
