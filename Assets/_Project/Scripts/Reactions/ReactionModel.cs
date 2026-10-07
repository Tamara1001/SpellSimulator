using System;
using UnityEngine;

namespace SpellSimulator.Reactions
{
    /// <summary>Propiedades fisicas de un material (las guarda MaterialProfile).</summary>
    [Serializable]
    public struct MaterialParams
    {
        [Tooltip("Puede prenderse fuego.")]
        public bool flammable;
        [Tooltip("Calor acumulado necesario para encenderse.")]
        public float ignitionHeat;
        [Tooltip("Segundos que tarda en consumirse ardiendo (sin viento).")]
        public float burnSeconds;
        [Tooltip("Al consumirse se rompe (si no, queda carbonizado).")]
        public bool burnDestroys;
        [Tooltip("Calor que pierde por segundo cuando no arde.")]
        public float coolingPerSecond;
        [Tooltip("Cuanta agua absorbe (0 = impermeable).")]
        public float wetAbsorb;
        [Tooltip("Humedad que pierde por segundo.")]
        public float dryingPerSecond;
        [Tooltip("Se puede romper por impacto.")]
        public bool fracturable;
        [Tooltip("Resistencia al impacto (mayor = aguanta mas).")]
        public float strength;
        [Tooltip("Calor por segundo que transmite a sus vecinos mientras arde.")]
        public float spreadHeatPerSecond;

        public static MaterialParams Wood => new MaterialParams
        {
            flammable = true, ignitionHeat = 1f, burnSeconds = 12f, burnDestroys = true, coolingPerSecond = 0.25f,
            wetAbsorb = 1f, dryingPerSecond = 0.04f, fracturable = true, strength = 1f, spreadHeatPerSecond = 0.6f
        };

        public static MaterialParams Stone => new MaterialParams
        {
            flammable = false, ignitionHeat = 1f, burnSeconds = 1f, coolingPerSecond = 0.5f,
            wetAbsorb = 0.2f, dryingPerSecond = 0.1f, fracturable = true, strength = 3f
        };
    }

    /// <summary>Lo que recibe un objeto en un tick, por segundo. Lo arma MagicWorld desde los hechizos y el fuego vecino.</summary>
    public struct Exposure
    {
        /// <summary>Calor (negativo = enfria, ej. agua).</summary>
        public float Heat;
        public float Wet;
        public float Impact;
        /// <summary>Viento: aviva lo que arde y enfria lo que no.</summary>
        public float Fan;

        public bool IsZero => Heat == 0f && Wet == 0f && Impact == 0f && Fan == 0f;

        public void Add(in Exposure other, float scale)
        {
            Heat += other.Heat * scale;
            Wet += other.Wet * scale;
            Impact += other.Impact * scale;
            Fan += other.Fan * scale;
        }
    }

    /// <summary>Estado reactivo de un objeto.</summary>
    [Serializable]
    public struct ReactiveState
    {
        public float Heat;
        public float Wetness;
        public bool Burning;
        /// <summary>0 = intacto, 1 = consumido. Tambien es cuanto se ve carbonizado.</summary>
        public float BurnProgress;
        /// <summary>1 = sano, 0 = roto.</summary>
        public float Integrity;
        public bool Broken;

        public static ReactiveState Fresh => new ReactiveState { Integrity = 1f };
    }

    [Flags]
    public enum ReactionEvents
    {
        None = 0,
        Ignited = 1 << 0,
        Extinguished = 1 << 1,
        BurnedOut = 1 << 2,
        Broken = 1 << 3,
        Soaked = 1 << 4
    }

    /// <summary>
    /// Reglas del mundo reactivo (MAGIC_SYSTEM 5): fuego, agua, viento e impacto sobre materiales. Logica pura y
    /// determinista; ElementReactive la llama en cada tick de MagicWorld.
    /// </summary>
    public static class ReactionModel
    {
        /// <summary>Con esta humedad no se enciende y lo que arde se apaga.</summary>
        public const float WetBlocksFire = 0.25f;
        /// <summary>Humedad a partir de la cual el objeto cuenta como empapado (corre la tinta de un sello).</summary>
        public const float SoakedThreshold = 0.5f;
        /// <summary>Cuanto acelera el viento la combustion por unidad de Fan.</summary>
        public const float FanBurnBoost = 1.5f;
        /// <summary>Cuanto enfria el viento a lo que no arde.</summary>
        public const float FanCooling = 0.5f;
        /// <summary>El calor evapora la humedad: agua perdida por unidad de calor y segundo.</summary>
        public const float Evaporation = 0.3f;
        /// <summary>Calor maximo acumulable, en multiplos del umbral de ignicion.</summary>
        public const float MaxHeatFactor = 2f;
        /// <summary>Lo carbonizado pierde resistencia (hasta este factor).</summary>
        public const float CharWeakening = 0.7f;

        public static ReactionEvents Step(ref ReactiveState s, in MaterialParams m, in Exposure e, float dt)
        {
            var events = ReactionEvents.None;
            if (s.Broken || dt <= 0f) return events;

            // Agua: moja segun la absorcion; se seca sola y mas rapido con calor.
            bool wasSoaked = s.Wetness >= SoakedThreshold;
            s.Wetness += Mathf.Max(e.Wet, 0f) * m.wetAbsorb * dt;
            // El calor recibido (y el propio) evapora el agua: el fuego primero seca y despues prende.
            s.Wetness -= (m.dryingPerSecond + Evaporation * (Mathf.Max(e.Heat, 0f) + s.Heat)) * dt;
            s.Wetness = Mathf.Clamp01(s.Wetness);
            if (!wasSoaked && s.Wetness >= SoakedThreshold) events |= ReactionEvents.Soaked;

            // Calor: lo mojado se calienta menos (el agua se lleva el calor).
            float gain = e.Heat > 0f ? e.Heat * (1f - s.Wetness) : e.Heat;
            s.Heat += gain * dt;
            if (!s.Burning) s.Heat -= (m.coolingPerSecond + FanCooling * Mathf.Max(e.Fan, 0f)) * dt;
            // Tope: el calor acumulado no crece sin limite (si no, lo que arde mucho tiempo se vuelve inapagable).
            s.Heat = Mathf.Clamp(s.Heat, 0f, MaxHeatFactor * Mathf.Max(m.ignitionHeat, 1f));

            if (s.Burning)
            {
                if (s.Wetness >= WetBlocksFire)
                {
                    s.Burning = false;
                    s.Heat = 0f;
                    events |= ReactionEvents.Extinguished;
                }
                else
                {
                    // Arder se sostiene solo: el calor no baja del umbral mientras haya combustible.
                    s.Heat = Mathf.Max(s.Heat, m.ignitionHeat);
                    s.BurnProgress += dt / Mathf.Max(m.burnSeconds, 0.01f) * (1f + FanBurnBoost * Mathf.Max(e.Fan, 0f));
                    if (s.BurnProgress >= 1f)
                    {
                        s.BurnProgress = 1f;
                        s.Burning = false;
                        s.Heat = 0f;
                        events |= ReactionEvents.BurnedOut;
                        if (m.burnDestroys) Break(ref s, ref events);
                    }
                }
            }
            else if (m.flammable && s.BurnProgress < 1f && s.Heat >= m.ignitionHeat && s.Wetness < WetBlocksFire)
            {
                s.Burning = true;
                events |= ReactionEvents.Ignited;
            }

            // Impacto (tierra, golpes): lo carbonizado aguanta menos.
            if (m.fracturable && e.Impact > 0f && !s.Broken)
            {
                float strength = Mathf.Max(m.strength, 0.01f) * (1f - CharWeakening * s.BurnProgress);
                s.Integrity -= e.Impact * dt / strength;
                if (s.Integrity <= 0f) Break(ref s, ref events);
            }
            return events;
        }

        private static void Break(ref ReactiveState s, ref ReactionEvents events)
        {
            if (s.Broken) return;
            s.Integrity = 0f;
            s.Broken = true;
            s.Burning = false;
            events |= ReactionEvents.Broken;
        }
    }
}
