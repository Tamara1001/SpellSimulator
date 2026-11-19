using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Net;

namespace SpellSimulator.World
{
    /// <summary>
    /// Exhibidor de un puesto del Archivo: cada hueco (<see cref="slots"/>) muestra un objeto REAL (pluma, frasco de tinta, sello) clonado de
    /// <see cref="template"/>, quieto y agarrable. Cuando el jugador se lo lleva (agarrado, guardado como cartucho o arrojado) el hueco repone
    /// otro igual. Los llevados cuentan para un tope (se borra el mas viejo que nadie sostenga). Sin allocations en Update.
    /// </summary>
    public class DisplayStock : MonoBehaviour
    {
        [SerializeField, Tooltip("Plantilla desactivada (sin Respawnable) que se clona en cada hueco.")]
        private GameObject template;
        [SerializeField] private Transform[] slots;
        [SerializeField, Tooltip("Segundos hasta reponer lo que se llevaron.")]
        private float refillDelay = 0.6f;
        [SerializeField, Tooltip("Distancia (m) del hueco a partir de la cual se considera llevado.")]
        private float takenDistance = 0.12f;
        [SerializeField, Tooltip("Tope de cosas sacadas de este exhibidor que siguen sueltas en el mundo.")]
        private int maxLoose = 16;

        private GameObject[] items;
        private float[] refillAt;
        private bool[] spawned;
        private readonly List<GameObject> loose = new List<GameObject>();

        public void Configure(GameObject template, Transform[] slots)
        {
            this.template = template;
            this.slots = slots;
        }

        private void Awake()
        {
            int n = slots != null ? slots.Length : 0;
            items = new GameObject[n];
            refillAt = new float[n];
            spawned = new bool[n];
        }

        private void Update()
        {
            if (template == null || slots == null) return;
            float sqrTaken = takenDistance * takenDistance;
            for (int i = 0; i < slots.Length; i++)
            {
                if (spawned[i])
                {
                    GameObject item = items[i];
                    if (item != null)
                    {
                        Transform t = item.transform;
                        bool away = t.parent != slots[i] || (t.position - slots[i].position).sqrMagnitude > sqrTaken;
                        if (!away) continue;
                        TrackLoose(item);
                    }
                    items[i] = null;
                    spawned[i] = false;
                    refillAt[i] = Time.time + refillDelay;
                }
                if (Time.time >= refillAt[i]) Spawn(i);
            }
        }

        private void Spawn(int i)
        {
            if (NetSession.Exists) return; // en red no se saca nada (como el resto del restocker)
            GameObject go = Instantiate(template, slots[i].position, slots[i].rotation, slots[i]);
            go.name = template.name.Replace("Template", "Display");
            go.SetActive(true);
            if (go.TryGetComponent(out Rigidbody body)) body.isKinematic = true; // quieto en el hueco hasta que se lo lleven
            items[i] = go;
            spawned[i] = true;
        }

        private void TrackLoose(GameObject go)
        {
            loose.RemoveAll(x => x == null);
            loose.Add(go);
            while (loose.Count > maxLoose)
            {
                int victim = loose.FindIndex(x => !(x.TryGetComponent(out Holdable h) && h.IsHeld));
                if (victim < 0) break;
                Destroy(loose[victim]);
                loose.RemoveAt(victim);
            }
        }
    }
}
