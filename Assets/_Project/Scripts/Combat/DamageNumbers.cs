using TMPro;
using UnityEngine;

namespace SpellSimulator.Combat
{
    /// <summary>
    /// Numeros de daño flotantes (pool fijo de textos 3D que suben, miran a la camara y se desvanecen). Sin GC por frame:
    /// el texto se arma solo al mostrar un numero.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        private const int PoolSize = 24;
        private const float Lifetime = 1.1f;
        private const float RiseSpeed = 0.6f;
        private const float TextHeight = 0.18f;

        private static DamageNumbers instance;

        private readonly TextMeshPro[] texts = new TextMeshPro[PoolSize];
        private readonly float[] born = new float[PoolSize];
        private int next;
        private Camera cam;

        public static void Show(Vector3 at, float amount)
        {
            if (instance == null)
            {
                var go = new GameObject("DamageNumbers");
                instance = go.AddComponent<DamageNumbers>();
            }
            instance.Spawn(at, amount);
        }

        private void Awake()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Damage");
                go.transform.SetParent(transform, false);
                var t = go.AddComponent<TextMeshPro>();
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.fontStyle = FontStyles.Bold;
                t.fontSize = TextHeight * 10f;
                t.outlineWidth = 0.25f;
                t.outlineColor = new Color32(0, 0, 0, 220);
                go.SetActive(false);
                texts[i] = t;
                born[i] = -10f;
            }
        }

        private void Spawn(Vector3 at, float amount)
        {
            var t = texts[next];
            born[next] = Time.time;
            next = (next + 1) % PoolSize;
            t.text = Mathf.RoundToInt(amount).ToString();
            // Mas daño, mas grande y mas rojo.
            float k = Mathf.Clamp01(amount / 25f);
            t.color = Color.Lerp(new Color(1f, 0.9f, 0.4f), new Color(1f, 0.25f, 0.15f), k);
            t.transform.position = at + Random.insideUnitSphere * 0.1f;
            t.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.6f, k);
            t.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            for (int i = 0; i < PoolSize; i++)
            {
                var t = texts[i];
                if (!t.gameObject.activeSelf) continue;
                float age = Time.time - born[i];
                if (age > Lifetime)
                {
                    t.gameObject.SetActive(false);
                    continue;
                }
                t.transform.position += Vector3.up * RiseSpeed * Time.deltaTime;
                if (cam != null) t.transform.rotation = Quaternion.LookRotation(t.transform.position - cam.transform.position);
                Color c = t.color;
                c.a = 1f - Mathf.Clamp01((age - Lifetime * 0.6f) / (Lifetime * 0.4f));
                t.color = c;
            }
        }
    }
}
