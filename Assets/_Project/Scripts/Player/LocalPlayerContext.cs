using System.Reflection;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Contexto del jugador local (F0 de MULTIPLAYER_PLAN): se registra en <see cref="PlayerRegistry"/> para que la logica de mundo
    /// no dependa de Camera.main ni de FindFirstObjectByType. Lo agrega PlayerMovement.Awake. Las manos se leen (solo lectura) de los
    /// puntos de agarre del <see cref="PlayerInteractor"/>, que son privados: se resuelven una vez por reflexion y se cachean.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalPlayerContext : MonoBehaviour, IPlayerContext
    {
        private static readonly FieldInfo RightField = typeof(PlayerInteractor).GetField("rightHoldPoint", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LeftField = typeof(PlayerInteractor).GetField("leftHoldPoint", BindingFlags.Instance | BindingFlags.NonPublic);

        private Camera headCamera;
        private PlayerInteractor interactor;

        public int OwnerId => 0;
        public bool IsLocal => true;
        public Transform Root => transform;
        public bool IsAlive => this != null;

        public Transform Head
        {
            get
            {
                if (headCamera == null) headCamera = GetComponentInChildren<Camera>(true);
                return headCamera != null ? headCamera.transform : transform;
            }
        }

        public Transform MainHand => PlayerSettings.MainHandRight ? Hold(RightField) : Hold(LeftField);
        public Transform SecondaryHand => PlayerSettings.MainHandRight ? Hold(LeftField) : Hold(RightField);

        private Transform Hold(FieldInfo field)
        {
            if (field == null) return null;
            if (interactor == null && !TryGetComponent(out interactor)) return null;
            return field.GetValue(interactor) as Transform;
        }

        private void OnEnable() => PlayerRegistry.Register(this);
        private void OnDisable() => PlayerRegistry.Unregister(this);
    }
}
