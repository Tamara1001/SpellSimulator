using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.World;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Parte de <see cref="PlayerInteractor"/>: PEDIDOS de agarrar / soltar / arrojar (F0 de MULTIPLAYER_PLAN, 2.6) y los
    /// efectos de ESCENA de las manos (emparentar, activar/desactivar, fisica). La entrada llama a <see cref="TryGrab"/>,
    /// <see cref="Release"/> y <see cref="Throw"/>; hoy se ejecutan local e inmediatamente, en red el servidor los valida.
    /// El estado (que hay en cada slot/mano) lo guarda <see cref="PlayerInventory"/>; quien sostiene cada objeto, <see cref="Holdable"/>.
    /// </summary>
    public partial class PlayerInteractor
    {
        /// <summary>Id del jugador de este interactor (el local; 0 en solitario).</summary>
        private static int LocalOwnerId => Holdable.LocalOwnerId;

        /// <summary>Lo que arrastra el movedor (consulta para <see cref="PlayerInventory.Dragged"/>).</summary>
        private GameObject DraggedObject() => mover != null && mover.HeldBody != null ? mover.HeldBody.gameObject : null;

        // ---------------------------------------------------------------- pedidos

        /// <summary>
        /// Pedido de agarre: herramientas y estampas van a la barra (mano principal); lo demas se arrastra (o va a la mano
        /// secundaria en VR). Devuelve true si se agarro. En red el servidor validaria ademas el alcance (distance), que nadie lo
        /// tenga (<see cref="Holdable.IsHeld"/>) y que no este cristalizado; hoy no se rechaza nada de eso (el harness agarra de lejos).
        /// </summary>
        public bool TryGrab(GrabRequest request)
        {
            if (request.ownerId != LocalOwnerId || request.target == null) return false;
            if (!request.target.TryGetComponent(out IInteractable interactable)) return false;
            if (interactable is PhysicalTool || interactable is Stamp) return PickUpToHotbar(interactable);
            return GrabSecondary(interactable, request.distance);
        }

        /// <summary>
        /// Pedido de soltar lo de la mano secundaria: lo arrastrado queda flotando si pin es true (o cae); lo agarrado en la mano
        /// (VR) se suelta y, con pin, se deja fijo en el aire si es <see cref="IPinnable"/>.
        /// </summary>
        public void Release(int ownerId, bool pin)
        {
            if (ownerId != LocalOwnerId) return;
            if (IsDragging) { mover.End(pin); return; }
            GameObject hand = inventory.HandSecondary;
            if (hand == null) return;
            IPinnable pinnable = null;
            if (pin) hand.TryGetComponent(out pinnable);
            DropHandSecondary();
            if (pinnable != null) pinnable.Pin();
        }

        /// <summary>Pedido de arrojar con esa velocidad: lo arrastrado, si no lo de la mano secundaria (VR), si no lo de la principal.</summary>
        public void Throw(int ownerId, Vector3 velocity)
        {
            if (ownerId != LocalOwnerId) return;
            if (IsDragging)
            {
                Rigidbody thrown = mover.HeldBody;
                mover.End(false);
                if (thrown != null && !thrown.isKinematic) thrown.linearVelocity = velocity;
                return;
            }
            GameObject hand = inventory.HandSecondary;
            if (hand != null)
            {
                DropHandSecondary();
                if (hand.TryGetComponent(out IThrowable t)) t.Throw(velocity);
                return;
            }
            DropMain(true, velocity);
        }

        /// <summary>Velocidad con que sale lo de la mano principal (Q): hacia la mira, un poco arriba, mas la del jugador.</summary>
        private Vector3 MainThrowVelocity()
        {
            if (inventory.HeldMain == null) return Vector3.zero;
            Transform view = PlatformDetector.IsVR && vrRightController != null ? vrRightController : cameraTransform;
            return view.forward * throwSpeed + Vector3.up * 1.2f + (body != null ? body.linearVelocity : Vector3.zero);
        }

        // ---------------------------------------------------------------- mano principal (efectos de escena)

        /// <summary>Herramientas y estampas: al primer slot libre de la barra (o reemplazan a la equipada) y quedan en la mano.</summary>
        private bool PickUpToHotbar(IInteractable interactable)
        {
            if (!(interactable is MonoBehaviour mb)) return false;
            int slot = inventory.FindPickupSlot();
            // Barra llena: se suelta lo del slot activo (sacandolo antes si estaba guardado).
            if (inventory.HotbarItem(slot) != null) { SelectSlotInternal(slot); DropMain(false, Vector3.zero); }

            StowMain();
            GameObject item = mb.gameObject;
            item.TryGetComponent(out mainUsable);
            inventory.Equip(slot, item);
            Holdable.Mark(item, LocalOwnerId);
            interactable.OnInteract(MainHold);
            currentHovered = null;
            return true;
        }

        private void SelectSlotInternal(int index)
        {
            if (!inventory.CanSelect(index)) return;
            StowMain();
            GameObject next = inventory.HotbarItem(index);
            if (next != null)
            {
                next.SetActive(true);
                next.TryGetComponent(out mainUsable);
            }
            inventory.Select(index);
        }

        /// <summary>Esconde lo que hay en la mano principal (sigue en su slot y sigue siendo del jugador).</summary>
        private void StowMain()
        {
            GameObject main = inventory.HeldMain;
            if (main == null) return;
            if (mainUsable != null && wasUsingTool) mainUsable.OnUseEnd();
            main.SetActive(false);
            inventory.Stow();
            mainUsable = null;
            wasUsingTool = false;
        }

        /// <summary>Suelta lo de la mano principal y libera su slot. Con arrojar = true sale con esa velocidad.</summary>
        private void DropMain(bool throwIt, Vector3 velocity)
        {
            GameObject obj = inventory.HeldMain;
            if (obj == null) return;
            if (mainUsable != null && wasUsingTool) mainUsable.OnUseEnd();
            if (obj.TryGetComponent(out IInteractable interactable)) interactable.OnDrop();
            if (throwIt)
            {
                if (obj.TryGetComponent(out IThrowable throwable)) throwable.Throw(velocity);
                else if (obj.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = velocity;
            }
            mainUsable = null;
            wasUsingTool = false;
            Holdable.Mark(obj, Holdable.NoOwner);
            inventory.RemoveMain();
        }

        private bool lastMainRight = true;

        /// <summary>Si cambia la mano principal en las opciones, lo equipado pasa a la otra mano.</summary>
        private void FollowMainHandSetting()
        {
            bool right = PlayerSettings.MainHandRight;
            if (right == lastMainRight) return;
            lastMainRight = right;
            GameObject main = inventory.HeldMain;
            if (main != null)
            {
                main.transform.SetParent(MainHold, false);
                main.transform.localPosition = Vector3.zero;
            }
        }

        // ---------------------------------------------------------------- mano secundaria (efectos de escena)

        /// <summary>Todo lo demas (lienzos, cajas, pesas, el grimorio): se arrastra con fisica (en VR va a la mano izquierda).</summary>
        private bool GrabSecondary(IInteractable interactable, float distance)
        {
            if (!(interactable is MonoBehaviour mb)) return false;
            if (interactable is Movable movable && !movable.CanLift) return false;

            if (PlatformDetector.IsVR || mover == null)
            {
                // VR: se lleva en la mano izquierda.
                GameObject item = mb.gameObject;
                inventory.SetHandSecondary(item);
                Holdable.Mark(item, LocalOwnerId);
                interactable.OnInteract(PlatformDetector.IsVR && vrLeftController != null ? vrLeftController : leftHoldPoint);
                currentHovered = null;
                return true;
            }
            var rb = mb.GetComponentInParent<Rigidbody>();
            if (rb == null) return false;
            // El movedor marca el Holdable del cuerpo (ObjectMover.Begin).
            return mover.TryBegin(rb, Mathf.Max(distance, 0.9f));
        }

        private void DropHandSecondary()
        {
            GameObject hand = inventory.HandSecondary;
            if (hand != null && hand.TryGetComponent(out IInteractable interactable)) interactable.OnDrop();
            if (hand != null) Holdable.Mark(hand, Holdable.NoOwner);
            inventory.ClearHandSecondary();
        }
    }
}
