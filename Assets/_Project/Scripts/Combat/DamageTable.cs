using System;
using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Combat
{
    /// <summary>Balance de daño (SO): se ajusta jugando sin tocar codigo. Valores de referencia en GAME_DESIGN 5.</summary>
    [CreateAssetMenu(menuName = "SpellSimulator/Damage Table", fileName = "DamageTable")]
    public class DamageTable : ScriptableObject
    {
        [Serializable]
        public struct Values
        {
            [Header("Daño por segundo a intensidad 1")]
            public float fire;
            public float water;
            public float wind;
            public float earth;
            public float light;
            [Header("Quemadura")]
            public float burnPerSecond;
            [Header("Impacto")]
            [Tooltip("Velocidad relativa (m/s) a partir de la cual un golpe hace daño.")]
            public float impactMinSpeed;
            public float impactPerJoule;
            public float impactMax;

            public float PerSecond(ElementType element)
            {
                switch (element)
                {
                    case ElementType.Fire: return fire;
                    case ElementType.Water: return water;
                    case ElementType.Wind: return wind;
                    case ElementType.Earth: return earth;
                    case ElementType.Light: return light;
                    default: return 0f;
                }
            }

            /// <summary>Valores iniciales: impacto directo 8-30 por hechizo, quemadura 4/s, aplastamiento hasta 25.</summary>
            public static Values Default => new Values
            {
                fire = 10f, water = 1.5f, wind = 1f, earth = 18f, light = 6f,
                burnPerSecond = 4f, impactMinSpeed = 3f, impactPerJoule = 0.8f, impactMax = 25f
            };
        }

        public Values values = Values.Default;
    }
}
