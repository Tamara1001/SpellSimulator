using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Combat
{
    /// <summary>
    /// Reglas de daño (GAME_DESIGN 5, Duelo). Logica pura: los numeros vienen de DamageTable.
    /// - Hechizo: daño por segundo del elemento x intensidad del campo en el blanco (0-1, ya incluye la fuerza).
    /// - Quemadura: daño fijo por segundo mientras el blanco arde.
    /// - Impacto: golpes fisicos (fragmentos, rocas, cajas) por encima de un umbral, segun energia y con tope.
    /// </summary>
    public static class DamageModel
    {
        // Referencia: DamageTable.Values.Default en DamageTable.cs (fuego 10/s, impacto maximo 25).
        public const float DuelImpactFactor = 0.2f;
        public const float DuelMinimumImpactSpeed = 4f;
        public const float DuelMaximumImpactDamage = 5f;
        public const float DuelBurnDamagePerSecond = 1f;

        /// <summary>Danio fisico reducido para duelo: energia sobre el umbral y tope por golpe.</summary>
        public static float DuelImpactDamage(float relativeSpeed, float otherMass)
        {
            float excess = relativeSpeed - DuelMinimumImpactSpeed;
            if (excess <= 0f) return 0f;
            float energy = 0.5f * Mathf.Clamp(otherMass, 0.05f, 50f) * excess * excess;
            return Mathf.Min(energy * DuelImpactFactor, DuelMaximumImpactDamage);
        }

        public static float SpellDamage(DamageTable.Values table, ElementType element, float intensity, float dt)
        {
            if (intensity <= 0f || dt <= 0f) return 0f;
            return table.PerSecond(element) * Mathf.Clamp01(intensity) * dt;
        }

        public static float BurnDamage(DamageTable.Values table, bool burning, float dt) => burning ? table.burnPerSecond * dt : 0f;

        /// <summary>Daño de un golpe: crece con la masa del otro y la velocidad relativa por encima del umbral.</summary>
        public static float ImpactDamage(DamageTable.Values table, float relativeSpeed, float otherMass)
        {
            float excess = relativeSpeed - table.impactMinSpeed;
            if (excess <= 0f) return 0f;
            float energy = 0.5f * Mathf.Clamp(otherMass, 0.05f, 50f) * excess * excess;
            return Mathf.Min(energy * table.impactPerJoule, table.impactMax);
        }
    }
}
