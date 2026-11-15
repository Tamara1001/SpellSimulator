using UnityEngine;

namespace SpellSimulator.Rendering
{
    /// <summary>
    /// Precalienta (una sola vez por sesion) los shaders que el primer uso del Laboratorio compilaria en pleno frame:
    /// TextMeshPro SDF, Sprites/Default (lineas y placas) y las particulas de URP. Arma en runtime un
    /// <see cref="ShaderVariantCollection"/> (sin tocar assets) y llama a WarmUp. Si algun shader no esta en el build,
    /// <see cref="Shader.Find"/> devuelve null y se omite sin error. Variantes con keywords no se cubren: el calentamiento por
    /// dibujo de <c>LabOverlay</c> (etiquetas y flechas casi invisibles) completa el resto.
    /// </summary>
    public static class ShaderWarmup
    {
        private static readonly string[] ShaderNames =
        {
            "TextMeshPro/Distance Field",
            "TextMeshPro/Mobile/Distance Field",
            "Sprites/Default",
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Particles/Simple Lit",
        };

        private static bool done;
        private static ShaderVariantCollection collection;

        // Con Enter Play Mode sin recarga de dominio el estado estatico sobreviviria entre sesiones de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            done = false;
            collection = null;
        }

        /// <summary>Precalienta los shaders comunes del Lab. Idempotente: solo trabaja la primera vez.</summary>
        public static void WarmCommon()
        {
            if (done) return;
            done = true;

            collection = new ShaderVariantCollection { name = "LabWarmup (runtime)" };
            for (int i = 0; i < ShaderNames.Length; i++)
            {
                Shader shader = Shader.Find(ShaderNames[i]);
                if (shader == null) continue;
                // Los shaders de URP usan el pase ScriptableRenderPipeline; los clasicos, Normal. El constructor lanza si no existe.
                try { collection.Add(new ShaderVariantCollection.ShaderVariant(shader, UnityEngine.Rendering.PassType.ScriptableRenderPipeline)); }
                catch (System.ArgumentException)
                {
                    try { collection.Add(new ShaderVariantCollection.ShaderVariant(shader, UnityEngine.Rendering.PassType.Normal)); }
                    catch (System.ArgumentException) { }
                }
            }
            if (collection.shaderCount > 0 && !collection.isWarmedUp) collection.WarmUp();
        }
    }
}
