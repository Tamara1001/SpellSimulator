using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Lista de jugadores presentes. En solitario tiene uno (el local); en red, uno por conectado. La logica de mundo consulta
    /// aca en vez de buscar Camera.main o el PlayerMovement de la escena.
    /// </summary>
    public static class PlayerRegistry
    {
        private static readonly List<IPlayerContext> players = new List<IPlayerContext>(4);

        public static IReadOnlyList<IPlayerContext> Players => players;
        public static int Count => players.Count;
        /// <summary>Se emite al registrar o quitar un jugador.</summary>
        public static event Action Changed;

        /// <summary>Id del jugador local en la partida (0 en solitario; en red lo fija NetPlayer con el id de cliente de Netcode).</summary>
        public static int LocalOwnerId { get; set; }

        /// <summary>El jugador local (el primero marcado local; si no hay, el primero). Null si no hay ninguno.</summary>
        public static IPlayerContext Local
        {
            get
            {
                IPlayerContext first = null;
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (p == null || !p.IsAlive) continue;
                    if (p.IsLocal) return p;
                    if (first == null) first = p;
                }
                return first;
            }
        }

        public static void Register(IPlayerContext player)
        {
            if (player == null || players.Contains(player)) return;
            players.Add(player);
            Changed?.Invoke();
        }

        public static void Unregister(IPlayerContext player)
        {
            if (player == null) return;
            if (players.Remove(player)) Changed?.Invoke();
        }

        /// <summary>Jugador por id (null si no esta).</summary>
        public static IPlayerContext ById(int ownerId)
        {
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && players[i].IsAlive && players[i].OwnerId == ownerId) return players[i];
            return null;
        }

        /// <summary>Jugador vivo mas cercano a un punto (cabeza). Devuelve la distancia al cuadrado o -1 si no hay.</summary>
        public static IPlayerContext Closest(Vector3 point, out float sqrDistance)
        {
            IPlayerContext best = null;
            sqrDistance = -1f;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive || p.Head == null) continue;
                float d = (p.Head.position - point).sqrMagnitude;
                if (best == null || d < sqrDistance) { best = p; sqrDistance = d; }
            }
            return best;
        }

        /// <summary>True si algun jugador vivo tiene la cabeza a menos de radius del punto.</summary>
        public static bool AnyNear(Vector3 point, float radius)
        {
            Closest(point, out float sqr);
            return sqr >= 0f && sqr <= radius * radius;
        }

        /// <summary>Solo para pruebas y reinicio del dominio: vacia la lista.</summary>
        public static void Clear()
        {
            players.Clear();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() { players.Clear(); LocalOwnerId = 0; }
    }
}
