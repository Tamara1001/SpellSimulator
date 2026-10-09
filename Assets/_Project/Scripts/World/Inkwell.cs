using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Tintero con tinta de conjuro de una clase. FIJO (zonas del mundo): no se levanta, inagotable, recarga la pluma al
    /// apuntarlo y apretar R (PC) o al mojar la punta (VR). PORTATIL: con E pasa al inventario como un cartucho de 5 usos
    /// de su clase de tinta y el objeto se destruye.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Inkwell : MonoBehaviour, IInteractable, IInkSource
    {
        [SerializeField, Tooltip("Clase de tinta del tintero.")] private InkKind kind = InkKind.Normal;
        [SerializeField, Tooltip("Portatil: se guarda con E como cartucho. Fijo: no se levanta y es inagotable.")]
        private bool portable;

        private Rigidbody rb;
        private Collider col;

        public InkKind Kind => kind;
        public bool Portable => portable;
        /// <summary>Los fijos son inagotables; los portatiles no tienen deposito propio (son un cartucho).</summary>
        public bool Infinite => !portable;
        public HandType PreferredHand => HandType.Both;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        private string promptCache;

        public string GetInteractionPrompt()
            => promptCache ??= portable ? "Guardar tintero (" + InkKindInfo.DisplayName(kind) + ")" : "Tintero (fijo)";

        /// <summary>Portatil: lo guarda como cartucho en el stock del jugador y se destruye. False si el stock esta lleno o es fijo.</summary>
        public bool TryPocket(SpellSimulator.Player.InkSupply supply)
        {
            if (!portable || supply == null || !supply.Cartridges.TryAdd(kind)) return false;
            Destroy(gameObject);
            return true;
        }

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

        /// <summary>Fijo: llena el deposito de una herramienta (inagotable). Portatil: 0 (se guarda con E). Devuelve la tinta transferida.</summary>
        public float Refill(InkReservoir target)
        {
            if (portable) return 0f;
            float missing = target.Missing;
            target.FillFrom(new InkReservoir(missing, missing));
            return missing;
        }
    }
}
