using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Tintero: objeto fisico con tinta de conjuro. Se agarra con cualquier mano y se mueve o se tira.
    /// Recarga: en PC mirando el tintero (o teniendolo en la mano) y apretando R; en VR mojando la punta adentro.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Inkwell : MonoBehaviour, IInteractable, IInkSource
    {
        [SerializeField] private InkReservoir ink = new InkReservoir(500f, 500f);
        [SerializeField, Tooltip("Si es true, nunca se vacia (tinteros de la torre en modo Libre).")]
        private bool infinite = true;

        private Rigidbody rb;
        private Collider col;

        public InkReservoir Ink => ink;
        public HandType PreferredHand => HandType.Both;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        public string GetInteractionPrompt() => "Agarrar tintero";

        public void OnInteract(Transform holdPoint)
        {
            rb.isKinematic = true;
            col.enabled = false;
            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void OnDrop()
        {
            transform.SetParent(null);
            rb.isKinematic = false;
            col.enabled = true;
        }

        /// <summary>Llena el deposito de una herramienta. Devuelve la tinta transferida.</summary>
        public float Refill(InkReservoir target)
        {
            if (infinite)
            {
                float missing = target.Missing;
                target.FillFrom(new InkReservoir(missing, missing));
                return missing;
            }
            return target.FillFrom(ink);
        }
    }
}
