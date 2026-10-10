using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Reactions;
using SpellSimulator.Core;

namespace SpellSimulator.World
{
    /// <summary>
    /// Punto de reaparicion (mesa o cofre): clona una plantilla (un GameObject desactivado, hijo) en el ancla cuando faltan
    /// objetos vivos y paso el enfriamiento, para que nadie agote las runas y lienzos del mundo. Un objeto cuenta como vivo
    /// mientras exista, aunque el jugador se lo lleve lejos; deja de contar cuando se destruye, se recicla o lo saca el
    /// tope global (<see cref="WorldObjectBudget"/>). El limite <c>maxLive</c> es POR jugador (<c>ownerId</c>, hoy siempre 0).
    /// </summary>
    public class SupplyPoint : MonoBehaviour
    {
        /// <summary>Se emite al reponer un objeto (punto, clon). Para sonidos, destellos o el servidor.</summary>
        public static event System.Action<SupplyPoint, GameObject> Supplied;

        private const float PrunePeriod = 0.25f;
        private const float BudgetPeriod = 2f;
        private const float StackStep = 0.15f;

        [SerializeField, Tooltip("Plantilla desactivada que se clona.")] private GameObject template;
        [SerializeField, Tooltip("Donde aparece el clon.")] private Transform spawnAnchor;
        [SerializeField, Min(0.1f)] private float cooldownSeconds = 60f;
        [SerializeField, Min(1), Tooltip("Objetos vivos maximos por jugador.")] private int maxLive = 1;
        [SerializeField, Tooltip("Tipo de suministro (limite por tipo del tope global). Vacio = nombre de la plantilla.")]
        private string supplyId = "";

        private struct Live
        {
            public GameObject go;
            public int ownerId;
        }

        private readonly List<Live> live = new List<Live>(4);
        private float secondsSinceFree;
        private float pruneTimer, budgetTimer;
        private bool filled;

        /// <summary>Objetos de este punto que siguen vivos (de todos los jugadores).</summary>
        public int LiveCount
        {
            get { Prune(); return live.Count; }
        }

        /// <summary>Segundos hasta la proxima reposicion; -1 si el punto esta lleno.</summary>
        public float SecondsToNext
        {
            get { Prune(); return SupplyMath.SecondsRemaining(live.Count, maxLive, secondsSinceFree, cooldownSeconds); }
        }

        /// <summary>Lo llama el builder de estructuras.</summary>
        public void Configure(GameObject template, Transform spawnAnchor, float cooldownSeconds, int maxLive = 1, string supplyId = "")
        {
            this.template = template;
            this.spawnAnchor = spawnAnchor;
            this.cooldownSeconds = Mathf.Max(0.1f, cooldownSeconds);
            this.maxLive = Mathf.Max(1, maxLive);
            this.supplyId = supplyId ?? "";
        }

        private string SupplyKey => string.IsNullOrEmpty(supplyId) ? (template != null ? template.name : name) : supplyId;

        private void OnEnable()
        {
            MagicWorld.WorldReset += OnWorldReset;
            Net.NetTemplates.Register(template); // en red los clientes clonan la plantilla que el anfitrion les indica
        }
        private void OnDisable() => MagicWorld.WorldReset -= OnWorldReset;

        /// <summary>Repone ya, sin esperar el enfriamiento, para el jugador local (0).</summary>
        public bool TrySpawnNow() => TrySpawnNow(0);

        /// <summary>Repone ya para ese jugador si no llego a su limite de objetos vivos.</summary>
        public bool TrySpawnNow(int ownerId)
        {
            if (template == null || spawnAnchor == null) return false;
            Prune();
            if (CountOf(ownerId) >= maxLive) return false;
            Spawn(ownerId);
            return true;
        }

        private int CountOf(int ownerId)
        {
            int n = 0;
            for (int i = 0; i < live.Count; i++) if (live[i].ownerId == ownerId) n++;
            return n;
        }

        private void Spawn(int ownerId)
        {
            Vector3 pos = spawnAnchor.position + Vector3.up * (StackStep * live.Count);
            GameObject clone = Instantiate(template, pos, spawnAnchor.rotation);
            clone.name = template.name;
            clone.transform.localScale = template.transform.lossyScale;
            // La plantilla esta desactivada: el clon nace apagado y recien aca corre su Awake, ya en su pose final.
            clone.SetActive(true);
            live.Add(new Live { go = clone, ownerId = ownerId });
            secondsSinceFree = 0f;
            WorldObjectBudget.Register(clone, SupplyKey, ownerId);
            if (Net.NetWorld.Active != null) Net.NetWorld.Active.RegisterClone(clone, template, ownerId);
            Debug.Log($"[Suministro] {name} repuso {clone.name} ({SupplyKey}).", this);
            Supplied?.Invoke(this, clone);
        }

        /// <summary>Saca de la cuenta lo destruido o que ya se esta achicando (reciclado o retirado por el tope).</summary>
        private void Prune()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                GameObject go = live[i].go;
                if (go == null || (go.TryGetComponent(out ExpiringBody body) && body.IsExpiring)) live.RemoveAt(i);
            }
        }

        private void Update()
        {
            if (template == null || spawnAnchor == null) return;
            if (Net.NetCast.IsClientOnly) return; // en red solo el anfitrion repone; los clientes ven las copias
            if (!filled)
            {
                filled = true;
                FillUp();
                return;
            }
            pruneTimer += GameClock.DeltaTime;
            if (pruneTimer >= PrunePeriod) { pruneTimer = 0f; Prune(); }

            if (live.Count >= maxLive) secondsSinceFree = 0f;
            else secondsSinceFree += GameClock.DeltaTime;
            if (SupplyMath.ShouldSpawn(live.Count, maxLive, secondsSinceFree, cooldownSeconds)) Spawn(0);

            budgetTimer += GameClock.DeltaTime;
            if (budgetTimer >= BudgetPeriod) { budgetTimer = 0f; WorldObjectBudget.Enforce(); }
        }

        private void FillUp()
        {
            Prune();
            while (live.Count < maxLive) Spawn(0);
        }

        /// <summary>Al reiniciar el mundo se van los clones (menos el que el jugador lleva en la mano) y se repone al instante.</summary>
        private void OnWorldReset()
        {
            if (template == null || spawnAnchor == null) return;
            if (Net.NetCast.IsClientOnly) return; // las copias de un cliente las quita el anfitrion (mensaje de baja)
            for (int i = live.Count - 1; i >= 0; i--)
            {
                GameObject go = live[i].go;
                if (go != null && WorldObjectBudget.IsHeld(go)) continue;
                if (go != null) Destroy(go);
                live.RemoveAt(i);
            }
            FillUp();
        }
    }
}
