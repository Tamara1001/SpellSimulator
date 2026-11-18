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
            // En red: lo que ya sostiene otro jugador se rechaza al instante; si no, el cuerpo queda listo para simularse aca.
            if (!Net.NetWorld.AllowLocalGrab(request.target)) return false;
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

        /// <summary>Suelta lo que sostiene el jugador si es ese objeto (el servidor lo rechazo o se lo dio a otro). Sin avisar a nadie.</summary>
        public void ForceRelease(GameObject go)
        {
            if (go == null) return;
            if (IsDragging && mover.HeldBody != null && mover.HeldBody.gameObject == go) { mover.End(false); return; }
            if (inventory.HeldMain == go) { DropMain(false, Vector3.zero); return; }
            if (inventory.HandSecondary == go) DropHandSecondary();
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
                else if (hand.TryGetComponent(out Rigidbody handBody) && !handBody.isKinematic) handBody.linearVelocity = velocity;
                return;
            }
            DropMain(true, velocity);
        }

        /// <summary>Velocidad con que sale lo de la mano principal (Q): hacia la mira, un poco arriba, mas la del jugador.</summary>
        private Vector3 MainThrowVelocity()
        {
            if (inventory.HeldMain == null) return Vector3.zero;
            Vector3 aim = PlatformDetector.IsVR ? VrMainAim() : cameraTransform.forward;
            return aim * throwSpeed + Vector3.up * 1.2f + (body != null ? body.linearVelocity : Vector3.zero);
        }

        // ---------------------------------------------------------------- mano principal (efectos de escena)

        /// <summary>Herramientas y estampas: al primer slot libre de la barra (o reemplazan a la equipada) y quedan en la mano.</summary>
        private bool PickUpToHotbar(IInteractable interactable)
        {
            if (!(interactable is MonoBehaviour mb)) return false;
            if (PlatformDetector.IsVR) ChooseMainHandForPickup();
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
                if (PlatformDetector.IsVR) AttachToMain(next);
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

        /// <summary>Deja las manos vacias (arrastre, agarre y herramienta equipada). Lo usa el inicio de partida: nadie empieza con algo en la mano.</summary>
        public void ClearHands()
        {
            if (IsDragging) mover.End(false);
            if (inventory.HandSecondary != null) DropHandSecondary();
            for (int i = 0; i < SlotCount; i++)
            {
                if (inventory.HotbarItem(i) == null) continue;
                SelectSlotInternal(i);
                DropMain(false, Vector3.zero);
            }
            StowMain();
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
            bool right = MainRight;
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
            // VR: una sola cosa agarrada a la vez (hay que soltar el grip de la otra primero).
            if (PlatformDetector.IsVR && (inventory.HandSecondary != null || IsDragging)) return false;

            // VR: lo grande (cajas, rocas) y el grimorio no se pegan a la mano (marea): se sostienen a distancia, como en PC.
            Rigidbody bodyOfItem = mb.GetComponentInParent<Rigidbody>();
            bool remote = PlatformDetector.IsVR && mover != null && ObjectMover.PrefersRemote(bodyOfItem);
            if ((PlatformDetector.IsVR && !remote) || mover == null)
            {
                // VR: se lleva en la mano que la agarro (o en la otra, si esa lleva la herramienta).
                GameObject item = mb.gameObject;
                Transform holdHand = PlatformDetector.IsVR ? ChooseSecondaryHand() : leftHoldPoint;
                inventory.SetHandSecondary(item);
                Holdable.Mark(item, LocalOwnerId);
                interactable.OnInteract(holdHand);
                if (PlatformDetector.IsVR) OrientHeldTowardHead(item, holdHand);
                currentHovered = null;
                return true;
            }
            var rb = bodyOfItem;
            if (rb == null) return false;
            if (remote)
            {
                // El grip de esta mano lo suelta (HandleVrRelease): mismo seguimiento que lo agarrado en la mano.
                secondaryRight = rayRight;
                secondaryGrabFrame = Time.frameCount;
                secondaryGrabTime = Time.unscaledTime;
                secondarySticky = false;
            }
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
