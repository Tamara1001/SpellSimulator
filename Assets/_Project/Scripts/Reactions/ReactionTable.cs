using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Matriz elemento x material (MAGIC_SYSTEM 5) como datos. Cada elemento aporta una exposicion base por segundo a
    /// intensidad 1; un material puede multiplicarla (ej. el papel se moja el doble, la piedra no siente el viento).
    /// </summary>
    [CreateAssetMenu(menuName = "SpellSimulator/Reaction Table", fileName = "ReactionTable")]
    public class ReactionTable : ScriptableObject
    {
        [Serializable]
        public struct ElementRow
        {
            public ElementType element;
            public float heat;
            public float wet;
            public float impact;
            public float fan;
        }

        [Serializable]
        public struct MaterialOverride
        {
            public MaterialProfile material;
            public ElementType element;
            [Tooltip("Multiplica toda la exposicion de ese elemento sobre ese material.")]
            public float multiplier;
        }

        public List<ElementRow> elements = new List<ElementRow>
        {
            new ElementRow { element = ElementType.Fire, heat = 3f },
            new ElementRow { element = ElementType.Water, heat = -2f, wet = 1.2f, impact = 0.1f },
            new ElementRow { element = ElementType.Wind, fan = 1f, impact = 0.05f },
            new ElementRow { element = ElementType.Earth, impact = 1.2f },
            new ElementRow { element = ElementType.Light, heat = 0.4f }
        };

        public List<MaterialOverride> overrides = new List<MaterialOverride>();

        /// <summary>Exposicion por segundo de un elemento sobre un material, a intensidad 1.</summary>
        public Exposure For(ElementType element, MaterialProfile material)
        {
            var result = new Exposure();
            foreach (var row in elements)
            {
                if (row.element != element) continue;
                result = new Exposure { Heat = row.heat, Wet = row.wet, Impact = row.impact, Fan = row.fan };
                break;
            }
            foreach (var o in overrides)
            {
                if (o.element != element || o.material != material) continue;
                var scaled = new Exposure();
                scaled.Add(result, o.multiplier);
                return scaled;
            }
            return result;
        }
    }
}
