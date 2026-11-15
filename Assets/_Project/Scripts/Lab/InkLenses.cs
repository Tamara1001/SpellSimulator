using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Reactions;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Lentes de tinta sobre su soporte en la mesa del laboratorio. Apretar (E / grip, como agarrar) sobre el soporte las
    /// «pone»: <see cref="LabOverlay.Visible"/> = true y las lentes desaparecen del soporte. Apretar otra vez sobre el soporte
    /// las saca y vuelven a verse. El estado vive en <see cref="LabOverlay.Visible"/> (esta clase solo lo refleja), asi que es
    /// idempotente y el reinicio del mundo (que apaga el overlay) las devuelve solas al soporte.
    /// </summary>
    [DisallowMultipleComponent]
    public class InkLenses : MonoBehaviour, IPressable
    {
        private const string PutOnPrompt = "Ponerse las lentes de tinta";
        private const string TakeOffPrompt = "Quitarse las lentes de tinta";

        [SerializeField, Tooltip("Modelo de las lentes sobre el soporte: se oculta mientras estan puestas. No debe contener el collider del soporte.")]
        private GameObject lensesVisual;
        [SerializeField, Tooltip("Collider (o trigger) del soporte que recibe el rayo del interactor. Vacio = el collider de este objeto.")]
        private Collider standCollider;

        private void Awake()
        {
            if (standCollider == null) standCollider = GetComponent<Collider>();
            if (standCollider == null)
                UnityEngine.Debug.LogWarning("[Laboratorio] InkLenses sin collider: el interactor no puede apuntarlas.", this);
            else if (lensesVisual != null && standCollider.transform.IsChildOf(lensesVisual.transform))
                UnityEngine.Debug.LogWarning("[Laboratorio] El collider de InkLenses esta dentro del modelo de las lentes: al ponerlas no se podran sacar.", this);
        }

        private void OnEnable()
        {
            LabOverlay.VisibleChanged += Sync;
            MagicWorld.WorldReset += OnWorldReset;
            Sync(LabOverlay.Visible);
        }

        private void OnDisable()
        {
            LabOverlay.VisibleChanged -= Sync;
            MagicWorld.WorldReset -= OnWorldReset;
        }

        public bool PressWithClick => false;

        public string GetPressPrompt() => LabOverlay.Visible ? TakeOffPrompt : PutOnPrompt;

        public void Press()
        {
            LabOverlay.Visible = !LabOverlay.Visible;
            Sync(LabOverlay.Visible);
        }

        /// <summary>Por si no hay LabOverlay en escena: el reinicio igual saca las lentes.</summary>
        private void OnWorldReset() => LabOverlay.Visible = false;

        private void Sync(bool worn)
        {
            if (lensesVisual != null && lensesVisual.activeSelf == worn) lensesVisual.SetActive(!worn);
        }
    }
}
