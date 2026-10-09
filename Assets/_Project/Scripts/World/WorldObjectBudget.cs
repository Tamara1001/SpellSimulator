using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// Tope global de objetos "suministrados" sueltos por el mundo (clones de <see cref="SupplyPoint"/> y lo que entrega el
    /// reciclaje): maximo 40 en total y 12 por tipo. Al pasarse se achican y destruyen los mas viejos que no esten en una
    /// mano ni cerca del jugador. Las rocas del hechizo no entran (tienen su propio limite).
    /// </summary>
    public static class WorldObjectBudget
    {
        public const int MaxTotal = 40;
        public const int MaxPerType = 12;
        /// <summary>Distancia (m) a la camara dentro de la cual un objeto no se saca. Con varios jugadores: a cualquiera de ellos.</summary>
        public const float SafeRadius = 15f;
        private const float EvictShrinkSeconds = 0.8f;

        private struct Item
        {
            public GameObject go;
            public string typeKey;
            public int ownerId;
            public float birthTime;
        }

        private static readonly List<Item> items = new List<Item>(48);
        private static readonly List<BudgetMath.Entry> entries = new List<BudgetMath.Entry>(48);
        private static readonly List<int> evictions = new List<int>(8);

        private static ObjectMover mover;
        private static float nextMoverSearch;

        /// <summary>Objetos registrados que siguen vivos.</summary>
        public static int Count => items.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            items.Clear();
            entries.Clear();
            evictions.Clear();
            mover = null;
            nextMoverSearch = 0f;
        }

        /// <summary>Anota un objeto suministrado (<c>ownerId</c>: jugador que lo pidio, 0 = local) y aplica el tope.</summary>
        public static void Register(GameObject go, string supplyId, int ownerId = 0)
        {
            if (go == null) return;
            items.Add(new Item { go = go, typeKey = supplyId ?? "", ownerId = ownerId, birthTime = Time.time });
            Enforce();
        }

        /// <summary>Descarta lo destruido o ya achicandose y saca el exceso. Barato: se llama al registrar y cada tanto.</summary>
        public static void Enforce()
        {
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                GameObject go = items[i].go;
                if (go == null || (go.TryGetComponent(out ExpiringBody b) && b.IsExpiring)) items.RemoveAt(i);
            }
            entries.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                entries.Add(new BudgetMath.Entry
                {
                    birthTime = it.birthTime,
                    typeKey = it.typeKey,
                    isProtected = IsProtected(it.go, cam),
                });
            }
            BudgetMath.SelectEvictions(entries, MaxTotal, MaxPerType, evictions);
            if (evictions.Count == 0) return;
            for (int i = 0; i < evictions.Count; i++) Evict(items[evictions[i]].go);
            // Se borran de mayor a menor indice para no desfasar los demas.
            evictions.Sort();
            for (int i = evictions.Count - 1; i >= 0; i--) items.RemoveAt(evictions[i]);
        }

        private static void Evict(GameObject go)
        {
            if (!go.TryGetComponent(out ExpiringBody body)) body = go.AddComponent<ExpiringBody>();
            body.Init(float.MaxValue, EvictShrinkSeconds);
            body.BeginExpire();
            Debug.Log($"[Suministro] Tope de objetos sueltos: {go.name} se retira.");
        }

        private static bool IsProtected(GameObject go, Transform cam)
        {
            if (!go.activeInHierarchy) return true; // guardado en el inventario
            if (IsHeld(go)) return true;
            return cam != null && (go.transform.position - cam.position).sqrMagnitude <= SafeRadius * SafeRadius;
        }

        /// <summary>True si el objeto esta agarrado por el jugador o cuelga de su camara (mano, VR).</summary>
        public static bool IsHeld(GameObject go)
        {
            if (mover == null && Time.unscaledTime >= nextMoverSearch)
            {
                mover = Object.FindFirstObjectByType<ObjectMover>();
                nextMoverSearch = Time.unscaledTime + 5f;
            }
            if (mover != null && mover.HeldBody != null)
            {
                Transform held = mover.HeldBody.transform;
                if (go.transform == held || go.transform.IsChildOf(held)) return true;
            }
            Camera cam = Camera.main;
            return cam != null && go.transform.IsChildOf(cam.transform);
        }
    }
}
