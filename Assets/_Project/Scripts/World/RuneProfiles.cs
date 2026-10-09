using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>Perfil de un material de runa: datos puros, sin dependencias de Unity salvo Mathf.</summary>
    public readonly struct RuneProfile
    {
        public readonly int Durability;
        public readonly float CooldownSeconds;
        public readonly bool Waterproof;
        public readonly bool FireProof;
        public readonly float Glow;

        public RuneProfile(int durability, float cooldownSeconds, bool waterproof, bool fireProof, float glow)
        {
            Durability = durability;
            CooldownSeconds = cooldownSeconds;
            Waterproof = waterproof;
            FireProof = fireProof;
            Glow = glow;
        }
    }

    /// <summary>
    /// Perfiles de las runas (bloque 4 del plan, PLAN_OCTAVA_TANDA). Todas las runas comparten la misma logica;
    /// solo cambia el perfil. Valores iniciales, a afinar probando a mano.
    /// </summary>
    public static class RuneProfiles
    {
        public const int StoneDurability = 2;
        public const float StoneCooldown = 5f;
        public const int MetalDurability = 5;
        public const float MetalCooldown = 4f;
        public const int GoldDurability = 10;
        public const float GoldCooldown = 1.5f;
        public const float GoldGlow = 1f;

        public static RuneProfile Get(RuneKind kind)
        {
            switch (kind)
            {
                case RuneKind.Metal: return new RuneProfile(MetalDurability, MetalCooldown, true, true, 0f);
                case RuneKind.Gold: return new RuneProfile(GoldDurability, GoldCooldown, true, true, GoldGlow);
                default: return new RuneProfile(StoneDurability, StoneCooldown, false, false, 0f);
            }
        }

        /// <summary>Desgaste tras gastar un uso (acotado a la durabilidad).</summary>
        public static int AddWear(int wear, int durability) => Mathf.Min(Mathf.Max(wear, 0) + 1, Mathf.Max(durability, 1));

        public static bool IsExhausted(int wear, int durability) => wear >= durability;

        /// <summary>Desgaste 0..1.</summary>
        public static float WearFraction(int wear, int durability)
            => durability <= 0 ? 1f : Mathf.Clamp01((float)wear / durability);
    }
}
