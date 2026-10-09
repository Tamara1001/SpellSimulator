using System;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.World
{
    /// <summary>
    /// Quien sostiene este objeto, como DATO (F0 de MULTIPLAYER_PLAN, `heldBy`): reemplaza mirar la jerarquia o el
    /// <c>HeldBody</c> del movedor. Se agrega en runtime con <see cref="GetOrAdd"/> la primera vez que algo lo agarra (el
    /// movedor a distancia o la mano del interactor). En red, el servidor fija el id y lo replica.
    /// </summary>
    [DisallowMultipleComponent]
    public class Holdable : MonoBehaviour
    {
        /// <summary>Id de "nadie lo sostiene".</summary>
        public const int NoOwner = -1;

        private int heldByOwnerId = NoOwner;

        /// <summary>Id del jugador que lo sostiene (<see cref="NoOwner"/> = nadie).</summary>
        public int HeldByOwnerId => heldByOwnerId;
        /// <summary>True si algun jugador lo sostiene (en la mano, en la barra o arrastrado).</summary>
        public bool IsHeld => heldByOwnerId != NoOwner;

        /// <summary>Cambio quien lo sostiene; el argumento es el nuevo id (<see cref="NoOwner"/> al soltarlo).</summary>
        public event Action<int> HeldChanged;

        /// <summary>Id del jugador local (0 en solitario si todavia no hay ninguno registrado).</summary>
        public static int LocalOwnerId
        {
            get
            {
                IPlayerContext local = PlayerRegistry.Local;
                return local != null ? local.OwnerId : 0;
            }
        }

        /// <summary>Marca quien lo sostiene. Emite <see cref="HeldChanged"/> solo si cambia.</summary>
        public void SetHeldBy(int ownerId)
        {
            if (ownerId < NoOwner) ownerId = NoOwner;
            if (heldByOwnerId == ownerId) return;
            heldByOwnerId = ownerId;
            HeldChanged?.Invoke(ownerId);
        }

        /// <summary>Lo suelta (nadie lo sostiene).</summary>
        public void Release() => SetHeldBy(NoOwner);

        /// <summary>El Holdable del objeto, agregandolo si falta. Null si el objeto es null.</summary>
        public static Holdable GetOrAdd(GameObject go)
        {
            if (go == null) return null;
            return go.TryGetComponent(out Holdable holdable) ? holdable : go.AddComponent<Holdable>();
        }

        /// <summary>True si el objeto (o alguno de sus padres con Holdable) esta sostenido. Sin allocations.</summary>
        public static bool IsObjectHeld(Component c)
        {
            if (c == null) return false;
            Holdable holdable = c.GetComponentInParent<Holdable>();
            return holdable != null && holdable.IsHeld;
        }

        /// <summary>Marca o suelta el objeto: al marcar agrega el componente si falta; al soltar no agrega nada.</summary>
        public static void Mark(GameObject go, int ownerId)
        {
            if (go == null) return;
            if (ownerId == NoOwner)
            {
                if (go.TryGetComponent(out Holdable existing)) existing.Release();
                return;
            }
            GetOrAdd(go).SetHeldBy(ownerId);
        }
    }
}
