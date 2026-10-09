using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Consultas de "que tiene agarrado cada jugador" sobre todos los jugadores de <see cref="PlayerRegistry"/> (F0 de
    /// MULTIPLAYER_PLAN). Reemplaza buscar un unico ObjectMover con FindFirstObjectByType. Sin allocations.
    /// </summary>
    public static class PlayerHeld
    {
        /// <summary>Movedor a distancia del jugador (null si no tiene).</summary>
        public static ObjectMover MoverOf(IPlayerContext player)
        {
            if (player == null || !player.IsAlive || player.Root == null) return null;
            return player.Root.TryGetComponent(out ObjectMover mover) ? mover : null;
        }

        /// <summary>True si algun jugador sostiene a distancia ese cuerpo.</summary>
        public static bool IsHeldByMover(Rigidbody body)
        {
            if (body == null) return false;
            var players = PlayerRegistry.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var mover = MoverOf(players[i]);
                if (mover != null && mover.HeldBody == body) return true;
            }
            return false;
        }

        /// <summary>True si el objeto cuelga de la cabeza de algun jugador (mano, VR) o es/cuelga de lo que sostiene un movedor.</summary>
        public static bool IsHeld(Transform t)
        {
            if (t == null) return false;
            var players = PlayerRegistry.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive) continue;
                if (p.Head != null && t.IsChildOf(p.Head)) return true;
                var mover = MoverOf(p);
                if (mover != null && mover.HeldBody != null)
                {
                    Transform held = mover.HeldBody.transform;
                    if (t == held || t.IsChildOf(held)) return true;
                }
            }
            return false;
        }

        /// <summary>True si el objeto cuelga de la cabeza (camara, mano en VR) de algun jugador.</summary>
        public static bool IsOnAnyHead(Transform t)
        {
            if (t == null) return false;
            var players = PlayerRegistry.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && p.IsAlive && p.Head != null && t.IsChildOf(p.Head)) return true;
            }
            return false;
        }

        /// <summary>True si el objeto cuelga de la cabeza o de la raiz de algun jugador (lo que lleva puesto o en la mano).</summary>
        public static bool IsAttachedToAnyPlayer(Transform t)
        {
            if (t == null) return false;
            var players = PlayerRegistry.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive) continue;
                if ((p.Head != null && t.IsChildOf(p.Head)) || (p.Root != null && t.IsChildOf(p.Root))) return true;
            }
            return false;
        }

        /// <summary>Suelta lo que tenga arrastrado cada jugador.</summary>
        public static void ReleaseAll()
        {
            var players = PlayerRegistry.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var mover = MoverOf(players[i]);
                if (mover != null) mover.End(false);
            }
        }

        /// <summary>Suelta el cuerpo si algun jugador lo tiene arrastrado.</summary>
        public static void ReleaseBody(Rigidbody body)
        {
            if (body == null) return;
            var players = PlayerRegistry.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var mover = MoverOf(players[i]);
                if (mover != null && mover.HeldBody == body) mover.End(false);
            }
        }
    }
}
