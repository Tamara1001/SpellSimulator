using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Parte de <see cref="PlayerInteractor"/>: manos de VR (Quest / Touch). Cada mano agarra con su grip y se queda con lo agarrado
    /// mientras lo aprieta; al soltar, la velocidad de la mano decide: rapida = lo arroja, casi quieta = lo deja flotando (si es
    /// fijable), si no cae. La mano del rayo es la ultima que apreto grip o gatillo (por defecto la principal). Las herramientas
    /// y estampas van a la mano que las agarro (esa pasa a ser la principal); lo demas, a la mano que agarro (o a la otra si esa
    /// ya lleva la herramienta). En PC nada de esto corre.
    /// </summary>
    public partial class PlayerInteractor
    {
        // Valores fijos (no serializados): el prefab ya guarda los viejos y los pisaria.
        private const float VrThrowMinSpeed = 2f;
        private const float VrPinMaxSpeed = 0.8f;
        private const float VrThrowScale = 1.3f;
        /// <summary>Un agarre mas corto que esto queda «pegado» a la mano: se suelta con otro apretón de grip.</summary>
        private const float VrTapSeconds = 0.35f;
        private float vrThrowMinSpeed => VrThrowMinSpeed;
        private float vrPinMaxSpeed => VrPinMaxSpeed;
        private float vrThrowScale => VrThrowScale;

        private bool vrMainRight = true;
        private bool rayRight = true;
        private bool secondaryRight;
        private int secondaryGrabFrame = -1;
        private float secondaryGrabTime;
        private bool secondarySticky;
        private Vector3 vrLastPosL, vrLastPosR, vrVelL, vrVelR;
        private bool vrTracking;
        private LineRenderer vrPointerLineL;

        private Transform VrController(bool right) => right ? vrRightController : vrLeftController;
        /// <summary>Controlador del que sale el rayo (null sin VR o sin referencias).</summary>
        private Transform VrRayController => PlatformDetector.IsVR ? VrController(rayRight) : null;
        /// <summary>True si la mano principal es la derecha (en VR la que agarro la herramienta; en PC la opcion del jugador).</summary>
        private bool MainRight => PlatformDetector.IsVR ? vrMainRight : PlayerSettings.MainHandRight;

        /// <summary>Clic del puntero: el gatillo de la mano principal, o el de la otra mano si es la del rayo (botones, perillas).</summary>
        private bool PointerHeld => inputReader.IsUsingTool ||
            (PlatformDetector.IsVR && rayRight != vrMainRight && inputReader.VrTrigger(rayRight) > 0.1f);

        /// <summary>[VR] True si esa mano lleva algo (la herramienta equipada o lo agarrado). La usa el visual de la mano para esconderse.</summary>
        public bool IsHandOccupied(bool right) =>
            (inventory.HeldMain != null && vrMainRight == right) || (inventory.HandSecondary != null && secondaryRight == right);

        /// <summary>[VR] True si lo agarrado en la mano tiene paginas (el golpe del stick las pasa en vez de girar).</summary>
        public bool HoldsPageable
        {
            get
            {
                GameObject hand = inventory.HandSecondary;
                return hand != null && hand.GetComponentInParent<IPageable>() != null;
            }
        }

        private void InitVr()
        {
            SetVrMainHand(PlayerSettings.MainHandRight);
            rayRight = vrMainRight;
            if (vrLeftController != null) vrPointerLineL = CreatePointer(vrLeftController.gameObject);
            if (vrPointerLineL != null) vrPointerLineL.enabled = false;
            // La HUD de pantalla no se ve en el visor: un panel junto a la mano izquierda con la barra, la tinta y los avisos.
            if (vrLeftController != null) gameObject.AddComponent<VrWristHud>().Init(this, vrLeftController);
            // Menu y ayuda de controles (clic del stick derecho).
            gameObject.AddComponent<VrMenu>().Init(this, inputReader, vrLeftController, vrRightController);
        }

        /// <summary>[VR] Cambia la mano principal desde el menu (la herramienta equipada pasa a esa mano).</summary>
        public void SetMainHandVr(bool right)
        {
            SetVrMainHand(right);
            rayRight = right;
            AttachToMain(inventory.HeldMain);
        }

        private void SetVrMainHand(bool right)
        {
            vrMainRight = right;
            inputReader.VrMainRight = right;
            Haptics.MainHand = right ? HandType.Right : HandType.Left;
        }

        /// <summary>Una vez por cuadro: velocidad de cada mano y cual es la mano del rayo.</summary>
        private void UpdateVrHands()
        {
            float dt = Time.deltaTime;
            if (dt > 0f && vrLeftController != null && vrRightController != null)
            {
                Vector3 pl = vrLeftController.position, pr = vrRightController.position;
                if (vrTracking)
                {
                    vrVelL = Vector3.Lerp(vrVelL, (pl - vrLastPosL) / dt, 0.5f);
                    vrVelR = Vector3.Lerp(vrVelR, (pr - vrLastPosR) / dt, 0.5f);
                }
                vrLastPosL = pl;
                vrLastPosR = pr;
                vrTracking = true;
            }
            if (inputReader.VrGripDownLeft || inputReader.VrTriggerDownLeft) rayRight = false;
            if (inputReader.VrGripDownRight || inputReader.VrTriggerDownRight) rayRight = true;
        }

        private Vector3 VrHandVelocity(bool right) => right ? vrVelR : vrVelL;

        /// <summary>
        /// Soltar el grip de la mano que lleva algo (mano secundaria): rapida = arroja con esa velocidad, casi quieta = lo deja
        /// flotando (si se puede fijar), si no lo suelta y cae. Tambien suelta si el grip ya no esta apretado (se perdio el evento).
        /// </summary>
        private void HandleVrRelease()
        {
            if (!PlatformDetector.IsVR || inventory.HandSecondary == null) return;
            bool up = secondaryRight ? inputReader.VrGripUpRight : inputReader.VrGripUpLeft;
            bool down = secondaryRight ? inputReader.VrGripDownRight : inputReader.VrGripDownLeft;
            if (secondarySticky)
            {
                // Agarre «pegado»: se suelta con otro apretón de grip (no el mismo que lo agarro).
                if (!down || secondaryGrabFrame == Time.frameCount) return;
            }
            else if (up && Time.unscaledTime - secondaryGrabTime < VrTapSeconds)
            {
                // Toque corto: queda en la mano sin tener que mantener el grip.
                secondarySticky = true;
                return;
            }
            else
            {
                bool lost = inputReader.Gate == InputGate.Full && !inputReader.VrGripHeld(secondaryRight) && secondaryGrabFrame != Time.frameCount;
                if (!up && !lost) return;
            }
            secondarySticky = false;

            Vector3 v = VrHandVelocity(secondaryRight);
            float speed = v.magnitude;
            if (speed >= vrThrowMinSpeed)
                Throw(LocalOwnerId, v * vrThrowScale + (body != null ? body.linearVelocity : Vector3.zero));
            else
                Release(LocalOwnerId, speed <= vrPinMaxSpeed);
        }

        /// <summary>Hacia donde sale lo que se arroja con B: el rayo de la mano principal.</summary>
        private Vector3 VrMainAim()
        {
            Transform hand = VrController(vrMainRight);
            if (hand == null) return cameraTransform.forward;
            return Quaternion.AngleAxis(vrRayPitchOffset, hand.right) * hand.forward;
        }

        /// <summary>Elige la mano de lo que se agarra: la que agarro, o la otra si esa lleva la herramienta. Devuelve el controlador.</summary>
        private Transform ChooseSecondaryHand()
        {
            bool right = rayRight;
            if (inventory.HeldMain != null && vrMainRight == right) right = !right;
            secondaryRight = right;
            secondaryGrabFrame = Time.frameCount;
            secondaryGrabTime = Time.unscaledTime;
            secondarySticky = false;
            return VrController(right) != null ? VrController(right) : (right ? rightHoldPoint : leftHoldPoint);
        }

        /// <summary>Una herramienta o estampa agarrada pasa a esa mano (la otra si ahi hay algo agarrado).</summary>
        private void ChooseMainHandForPickup()
        {
            bool right = rayRight;
            if (inventory.HandSecondary != null && secondaryRight == right) right = !right;
            SetVrMainHand(right);
        }

        /// <summary>
        /// Lo agarrado que va PEGADO a la mano (lienzo, cuaderno, grimorio) se endereza: la cara visible mira a la cabeza y queda
        /// derecho respecto del piso. Desde ahi sigue la muñeca. Sin esto salia boca abajo o de espaldas segun la mano. Lo que sigue
        /// a la mano por fisica (cajas) no se toca.
        /// </summary>
        private void OrientHeldTowardHead(GameObject item, Transform hand)
        {
            if (item == null || hand == null || item.transform.parent != hand) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 away = item.transform.position - cam.transform.position;
            if (away.sqrMagnitude < 0.0004f) return;
            item.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        /// <summary>Lo guardado en un slot vuelve a la mano principal actual (en VR esa mano puede haber cambiado).</summary>
        private void AttachToMain(GameObject item)
        {
            Transform hold = MainHold;
            if (item == null || hold == null || item.transform.parent == hold) return;
            item.transform.SetParent(hold, false);
            item.transform.localPosition = Vector3.zero;
        }
    }
}
