using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Modulo 5: salida. Crea la manifestacion 3D (SpellManifestation) y su efecto fisico (MagicField) en el centro del
    /// anillo, con el tamanio real del sello y la cara de emision del papel. El look de cada elemento es un ElementLook.
    /// </summary>
    public class SpellOutputController : MonoBehaviour
    {
        [Header("Looks por elemento")]
        [SerializeField] private ElementLook fire;
        [SerializeField] private ElementLook water;
        [SerializeField] private ElementLook wind;
        [SerializeField] private ElementLook earth;
        [SerializeField] private ElementLook light;
        [SerializeField, Tooltip("Variante de Tierra para el sigilo crystal (mismo elemento, otro look). Vacio = usa Tierra.")]
        private ElementLook crystal;

        [Header("Escala y presupuesto")]
        [SerializeField, Tooltip("Distancia (m) delante del papel donde nace el efecto.")]
        private float surfaceOffset = 0.03f;
        [SerializeField, Tooltip("Hechizos activos simultaneos. Los mas viejos se apagan (provisorio hasta medir en Quest).")]
        private int maxActive = 6;

        private readonly List<SpellManifestation> active = new List<SpellManifestation>();
        private Camera cachedCamera;

        /// <param name="ringCenterUV">Centro del anillo en UV del lienzo.</param>
        public SpellManifestation SpawnSpellEffect(SpellIR spell, DrawingCanvas canvas, Vector2 ringCenterUV)
        {
            if (!spell.valid || !spell.active) return null;

            ElementLook look = LookFor(spell.element, spell.sigilId);
            if (look == null || look.material == null)
            {
                Debug.LogWarning($"[VFX Output] Sin ElementLook para {spell.element}.");
                return null;
            }

            if (cachedCamera == null) cachedCamera = Camera.main;
            Vector3 viewer = cachedCamera != null ? cachedCamera.transform.position : canvas.transform.position - canvas.transform.forward;
            Vector3 normal = canvas.EmissionNormal(viewer);
            Transform t = canvas.transform;
            // El borde derecho del dibujo es el mismo de ambos lados del papel: X e Y no se espejan al emitir por atras.
            var frame = new SealFrame
            {
                Origin = canvas.UVToWorld(ringCenterUV) + normal * surfaceOffset,
                Right = t.right,
                Up = t.up,
                Normal = normal,
                Radius = Mathf.Max(spell.sealRadius, 0.05f),
            };
            frame.Scale = frame.Radius * spell.effectScale;

            var go = new GameObject($"Spell_{spell.element}_{spell.primaryManifestation}");
            var manifestation = go.AddComponent<SpellManifestation>();
            manifestation.Init(spell, frame, look);
            go.AddComponent<MagicField>().Init(spell, frame, look, canvas.gameObject);
            SpellBodies.Spawn(spell, frame, look, canvas, go);

            Track(manifestation);
            Debug.Log($"[VFX Output] {spell.element} ({spell.sigilId}) | {spell.plan?.Primitives} | " +
                      $"radio {frame.Radius:F2} m | {manifestation.BodySeconds:F1} s");
            return manifestation;
        }

        private void Track(SpellManifestation manifestation)
        {
            active.RemoveAll(m => m == null);
            active.Add(manifestation);
            while (active.Count > maxActive)
            {
                if (active[0] != null) Destroy(active[0].gameObject);
                active.RemoveAt(0);
            }
        }

        /// <summary>Apaga todos los hechizos vivos (reinicio del mundo).</summary>
        public void ClearAll()
        {
            foreach (var m in active) if (m != null) Destroy(m.gameObject);
            active.Clear();
        }

        public const string CrystalSigil = "crystal";

        /// <summary>Look del hechizo: igual que LookFor(elemento) salvo variantes por sigilo (cristal es tierra celeste).</summary>
        public ElementLook LookFor(ElementType element, string sigilId)
        {
            if (element == ElementType.Earth && sigilId == CrystalSigil && crystal != null) return crystal;
            return LookFor(element);
        }

        /// <summary>Look de un elemento (tambien lo usan las reacciones: llamas de lo que arde).</summary>
        public ElementLook LookFor(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fire;
                case ElementType.Water: return water;
                case ElementType.Wind: return wind;
                case ElementType.Earth: return earth;
                case ElementType.Light: return light;
                default: return null;
            }
        }
    }
}
