using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Visual temporal del hechizo de cristal: la esfera de alcance (se desvanece) o la placa de piso (dura y se achica al final).
    /// Se destruye sola y tambien al reiniciar el mundo.
    /// </summary>
    public class CrystalTimedVisual : MonoBehaviour
    {
        public enum Mode { FadeOut, ShrinkAtEnd }

        private const float ShrinkSeconds = 0.6f;

        private Mode mode;
        private float life;
        private float elapsed;
        private Vector3 baseScale;
        private Material material;
        private Color baseColor;

        /// <summary>Configura el visual. <paramref name="mat"/> es una instancia propia (se destruye con el objeto) o null.</summary>
        public void Setup(Mode visualMode, float seconds, Material mat)
        {
            mode = visualMode;
            life = Mathf.Max(0.05f, seconds);
            baseScale = transform.localScale;
            material = mat;
            if (material != null) baseColor = material.color;
        }

        private void OnEnable() => Reactions.MagicWorld.WorldReset += OnWorldReset;
        private void OnDisable() => Reactions.MagicWorld.WorldReset -= OnWorldReset;
        private void OnWorldReset() => Destroy(gameObject);

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= life) { Destroy(gameObject); return; }
            if (mode == Mode.FadeOut)
            {
                if (material == null) return;
                Color c = baseColor;
                c.a = baseColor.a * (1f - elapsed / life);
                material.color = c;
            }
            else
            {
                float left = life - elapsed;
                if (left < ShrinkSeconds)
                {
                    float k = left / ShrinkSeconds;
                    transform.localScale = new Vector3(baseScale.x * k, baseScale.y, baseScale.z * k);
                }
            }
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
