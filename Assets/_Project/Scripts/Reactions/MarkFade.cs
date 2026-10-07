using UnityEngine;

namespace SpellSimulator.Reactions
{
    /// <summary>Charco que se seca: se desvanece y se borra.</summary>
    public class MarkFade : MonoBehaviour
    {
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private float seconds, age;
        private Renderer rend;
        private MaterialPropertyBlock block;

        public void Init(float lifetime)
        {
            seconds = lifetime;
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            age += Time.deltaTime;
            rend.GetPropertyBlock(block);
            block.SetFloat(FadeId, 1f - Mathf.Clamp01((age - seconds * 0.5f) / (seconds * 0.5f)));
            rend.SetPropertyBlock(block);
            if (age >= seconds) Destroy(gameObject);
        }
    }
}
