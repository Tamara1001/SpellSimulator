using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;
using SpellSimulator.Player;
using SpellSimulator.Reactions;

namespace SpellSimulator.World
{
    /// <summary>
    /// Estacion de reciclaje: lo que se deja en la zona de entrada (runas y papeles con tinta) se consume con una animacion
    /// corta (achicarse y subir) y, pasados <see cref="RecycleMath.RecycleSeconds"/>, entrega un reemplazo. Economia cerrada:
    /// 1 runa agotada = 1 runa nueva de su clase; 2 runas utiles de la misma clase = 1 nueva; 1 papel = 1 papel limpio.
    /// La zona es un trigger (lo crea el builder); aca se consulta por superposicion cada tanto, sin allocations.
    /// </summary>
    public class RecyclingStation : MonoBehaviour
    {
        private const float ScanPeriod = 0.25f;
        private const float RiseSpeed = 0.6f;
        private const float NoticeCooldown = 3f;
        private const int MaxPending = RecycleMath.MaxPending;
        private const int PaperSlot = -1;
        private const string SupplyKey = "reciclaje";

        [SerializeField, Tooltip("Zona de entrada: un Collider trigger (caja) en este Transform.")] private Transform dropZone;
        [SerializeField, Tooltip("Donde aparece lo reciclado.")] private Transform outputAnchor;
        [SerializeField, Tooltip("Plantilla (desactivada) de la runa nueva, indexada por RuneKind.")] private GameObject[] runeTemplates;
        [SerializeField, Tooltip("Plantilla (desactivada) del papel limpio.")] private GameObject paperTemplate;

        // Cola de entregas pendientes: sin listas dinamicas en el Update.
        private readonly int[] pendingKind = new int[MaxPending];
        private readonly float[] pendingReady = new float[MaxPending];
        private readonly int[] pendingOwner = new int[MaxPending]; // quien dejo el objeto (0 = jugador local)
        private int pendingCount;

        private readonly Collider[] hits = new Collider[48];
        private readonly List<GameObject> scanned = new List<GameObject>(16);
        private readonly Dictionary<GameObject, int> ownerOf = new Dictionary<GameObject, int>(16);
        private readonly List<GameObject> papers = new List<GameObject>(8);
        private readonly List<GameObject>[] exhaustedByKind = new List<GameObject>[8];
        private readonly List<GameObject>[] freshByKind = new List<GameObject>[8];
        private readonly List<Transform> rising = new List<Transform>(16);
        private float scanTimer, nextNoticeAt;

        public void Configure(Transform dropZone, Transform outputAnchor)
        {
            this.dropZone = dropZone;
            this.outputAnchor = outputAnchor;
        }

        /// <summary>Plantilla de la runa nueva de esa clase.</summary>
        public void AddRecipe(RuneKind kind, GameObject template)
        {
            int index = (int)kind;
            if (runeTemplates == null) runeTemplates = new GameObject[index + 1];
            else if (runeTemplates.Length <= index) System.Array.Resize(ref runeTemplates, index + 1);
            runeTemplates[index] = template;
        }

        public void SetPaperTemplate(GameObject template) => paperTemplate = template;

        private void Awake()
        {
            for (int i = 0; i < exhaustedByKind.Length; i++)
            {
                exhaustedByKind[i] = new List<GameObject>(4);
                freshByKind[i] = new List<GameObject>(4);
            }
        }

        private void OnEnable()
        {
            MagicWorld.WorldReset += OnWorldReset;
            Net.NetTemplates.Register(paperTemplate);
            if (runeTemplates != null) foreach (var template in runeTemplates) Net.NetTemplates.Register(template);
        }
        private void OnDisable() => MagicWorld.WorldReset -= OnWorldReset;

        // Lo pendiente se pierde al reiniciar el mundo (lo consumido ya no existe y el mundo vuelve a su estado inicial).
        private void OnWorldReset() => pendingCount = 0;

        private void Update()
        {
            if (Net.NetCast.IsClientOnly) return; // en red solo el anfitrion recicla
            float dt = Time.deltaTime;
            for (int i = rising.Count - 1; i >= 0; i--)
            {
                if (rising[i] == null) rising.RemoveAt(i);
                else rising[i].position += Vector3.up * (RiseSpeed * dt);
            }

            scanTimer += dt;
            if (scanTimer >= ScanPeriod) { scanTimer = 0f; Scan(); }

            DeliverReady();
        }

        // ---------------------------------------------------------------- entrada

        private void Scan()
        {
            if (dropZone == null) return;
            GetZone(out Vector3 center, out Vector3 half, out Quaternion rot);
            int n = Physics.OverlapBoxNonAlloc(center, half, hits, rot, ~0, QueryTriggerInteraction.Ignore);

            scanned.Clear();
            ownerOf.Clear();
            papers.Clear();
            for (int k = 0; k < exhaustedByKind.Length; k++) { exhaustedByKind[k].Clear(); freshByKind[k].Clear(); }

            for (int i = 0; i < n; i++)
            {
                PhysicalCanvas pc = hits[i].GetComponentInParent<PhysicalCanvas>();
                if (pc == null) continue;
                GameObject go = pc.gameObject;
                if (scanned.Contains(go)) continue;
                scanned.Add(go);
                if (WorldObjectBudget.IsHeld(go) || go.GetComponent<ExpiringBody>() != null || go.GetComponent<NotebookPages>() != null) continue;

                if (go.TryGetComponent(out Rune rune))
                {
                    // Las runas fijas (placas) no llevan RuneJoin: no son reciclables.
                    if (go.GetComponent<RuneJoin>() == null) continue;
                    int slot = (int)rune.Kind;
                    if (slot < 0 || slot >= exhaustedByKind.Length) continue;
                    ownerOf[go] = rune.OwnerId;
                    (rune.IsExhausted ? exhaustedByKind[slot] : freshByKind[slot]).Add(go);
                }
                else if (go.TryGetComponent(out DrawingCanvas dc) && !dc.Session.IsEmpty)
                {
                    // Dueno del papel: el de su ultimo trazo, si se conoce.
                    var strokes = dc.Session.Strokes;
                    ownerOf[go] = strokes.Count > 0 ? strokes[strokes.Count - 1].OwnerId : 0;
                    papers.Add(go);
                }
            }

            bool rejectedSingle = false, full = false;
            float now = GameClock.Time;
            bool limitPerOwner = PlayerRegistry.Count > 1;

            for (int i = 0; i < papers.Count; i++)
            {
                int owner = OwnerOf(papers[i]);
                if (!RecycleMath.CanQueue(pendingCount, PendingOf(owner), limitPerOwner)) { full = true; continue; }
                Consume(papers[i]);
                Enqueue(PaperSlot, now, owner);
            }

            for (int k = 0; k < exhaustedByKind.Length; k++)
            {
                RuneKind kind = (RuneKind)k;
                List<GameObject> spent = exhaustedByKind[k];
                for (int i = 0; i < spent.Count; i++)
                {
                    int owner = OwnerOf(spent[i]);
                    if (!RecycleMath.CanQueue(pendingCount, PendingOf(owner), limitPerOwner)) { full = true; continue; }
                    Consume(spent[i]);
                    Enqueue(k, now, owner);
                }

                List<GameObject> fresh = freshByKind[k];
                int toConsume = RecycleMath.FreshToConsume(kind, fresh.Count);
                int cost = RecycleMath.Cost(kind, false);
                for (int i = 0; i + cost <= toConsume; i += cost)
                {
                    int owner = OwnerOf(fresh[i]);
                    if (!RecycleMath.CanQueue(pendingCount, PendingOf(owner), limitPerOwner)) { full = true; break; }
                    for (int c = 0; c < cost; c++) Consume(fresh[i + c]);
                    Enqueue(k, now, owner);
                }
                if (fresh.Count > toConsume) rejectedSingle = true;
            }

            if ((rejectedSingle || full) && now >= nextNoticeAt)
            {
                nextNoticeAt = now + NoticeCooldown;
                if (rejectedSingle) Hud.Notify("rune", "Esa runa todavia sirve: usala hasta agotarla (o llevala de a dos)", 3f);
                else Hud.Notify("rune", "La estacion esta llena: espera a que entregue", 3f);
            }
        }

        private void GetZone(out Vector3 center, out Vector3 half, out Quaternion rot)
        {
            rot = dropZone.rotation;
            if (dropZone.TryGetComponent(out BoxCollider box))
            {
                center = dropZone.TransformPoint(box.center);
                Vector3 s = dropZone.lossyScale;
                half = new Vector3(Mathf.Abs(box.size.x * s.x), Mathf.Abs(box.size.y * s.y), Mathf.Abs(box.size.z * s.z)) * 0.5f;
            }
            else if (dropZone.TryGetComponent(out Collider col))
            {
                center = col.bounds.center;
                half = col.bounds.extents;
                rot = Quaternion.identity;
            }
            else
            {
                center = dropZone.position;
                half = Vector3.one * 0.5f;
            }
        }

        /// <summary>Lo deja sin colision ni disparo y lo achica subiendo; <see cref="ExpiringBody"/> lo suelta de la mano y lo destruye.</summary>
        private void Consume(GameObject go)
        {
            if (!go.TryGetComponent(out ExpiringBody body)) body = go.AddComponent<ExpiringBody>();
            body.Init(float.MaxValue, RecycleMath.AnimSeconds);
            body.BeginExpire();

            if (go.TryGetComponent(out SealTrigger trigger)) trigger.enabled = false;
            Collider[] colliders = go.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            if (go.TryGetComponent(out Rigidbody rb))
            {
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.useGravity = false;
                rb.isKinematic = true;
            }
            rising.Add(go.transform);
        }

        private int OwnerOf(GameObject go) => ownerOf.TryGetValue(go, out int owner) ? owner : 0;

        private int PendingOf(int owner)
        {
            int n = 0;
            for (int i = 0; i < pendingCount; i++) if (pendingOwner[i] == owner) n++;
            return n;
        }

        private void Enqueue(int slot, float now, int owner)
        {
            pendingOwner[pendingCount] = owner;
            pendingKind[pendingCount] = slot;
            pendingReady[pendingCount] = RecycleMath.ReadyAt(now);
            pendingCount++;
        }

        // ---------------------------------------------------------------- salida

        private void DeliverReady()
        {
            if (pendingCount == 0) return;
            float now = GameClock.Time;
            int delivered = 0;
            for (int i = 0; i < pendingCount;)
            {
                if (!RecycleMath.IsReady(now, pendingReady[i])) { i++; continue; }
                Deliver(pendingKind[i], pendingOwner[i], delivered++);
                pendingCount--;
                pendingKind[i] = pendingKind[pendingCount];
                pendingReady[i] = pendingReady[pendingCount];
                pendingOwner[i] = pendingOwner[pendingCount];
            }
        }

        private void Deliver(int slot, int owner, int stackIndex)
        {
            GameObject template = slot == PaperSlot ? paperTemplate
                : (runeTemplates != null && slot >= 0 && slot < runeTemplates.Length ? runeTemplates[slot] : null);
            if (template == null || outputAnchor == null)
            {
                Debug.LogWarning($"[Reciclaje] {name}: falta la plantilla de salida ({(slot == PaperSlot ? "papel" : ((RuneKind)slot).ToString())}).", this);
                return;
            }
            Vector3 pos = outputAnchor.position + Vector3.up * (0.12f * stackIndex);
            GameObject item = Instantiate(template, pos, outputAnchor.rotation);
            item.name = template.name;
            item.transform.localScale = template.transform.lossyScale;
            item.SetActive(true);
            if (item.TryGetComponent(out Rune newRune)) newRune.SetOwner(owner);
            WorldObjectBudget.Register(item, SupplyKey);
            if (Net.NetWorld.Active != null) Net.NetWorld.Active.RegisterClone(item, template, owner);
            Debug.Log($"[Reciclaje] {name} entrega {item.name}.", this);
        }
    }
}
