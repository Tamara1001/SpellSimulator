using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Runa: objeto con un sello inscripto que se dispara por su cuenta (SealTrigger). El material (piedra, metal,
    /// oro) fija su perfil: durabilidad de por vida, enfriamiento y resistencias. El desgaste NO se reinicia al
    /// borrar el dibujo: una runa agotada esta rota y hay que reciclarla (bloque 6). Va junto al SealTrigger.
    /// </summary>
    public class Rune : MonoBehaviour, ISealGate
    {
        /// <summary>Se emite al cambiar el desgaste de cualquier runa (para grietas, HUD, sonido).</summary>
        public static event System.Action<Rune> WearChanged;

        [SerializeField] private RuneKind kind = RuneKind.Stone;
        [SerializeField, Min(0), Tooltip("Usos gastados de por vida.")] private int wear;

        private RuneProfile profile;
        private bool profileReady;

        private RuneProfile Profile
        {
            get
            {
                if (!profileReady) { profile = RuneProfiles.Get(kind); profileReady = true; }
                return profile;
            }
        }

        public RuneKind Kind => kind;
        public int Durability => Profile.Durability;
        public int Wear => Mathf.Min(wear, Durability);
        public float WearFraction => RuneProfiles.WearFraction(wear, Durability);
        public bool IsExhausted => RuneProfiles.IsExhausted(wear, Durability);
        public bool Waterproof => Profile.Waterproof;
        public bool FireProof => Profile.FireProof;
        public float GlowIntensity => Profile.Glow;
        public float CooldownSeconds => Profile.CooldownSeconds;

        /// <summary>Una runa agotada no se arma ni dispara (SpellCaster la consulta por ISealGate).</summary>
        bool ISealGate.SealBlocked => IsExhausted;

        /// <summary>Fija el material y aplica su perfil. La masa la pone el builder.</summary>
        public void Configure(RuneKind newKind)
        {
            kind = newKind;
            profile = RuneProfiles.Get(kind);
            profileReady = true;
            wear = Mathf.Min(wear, profile.Durability);
        }

        /// <summary>Gasta un uso de por vida. Lo llama el disparador (SealTrigger / RuneJoin) tras un lanzamiento.</summary>
        public void RegisterUse()
        {
            if (IsExhausted) return;
            wear = RuneProfiles.AddWear(wear, Durability);
            Debug.Log(IsExhausted
                ? $"[Runa] {name} ({kind}) se agoto: {wear}/{Durability} usos. Hay que reciclarla."
                : $"[Runa] {name} ({kind}) gasto un uso: {wear}/{Durability}.", this);
            WearChanged?.Invoke(this);
        }

        /// <summary>Avisa que la runa gastada ya no sirve (al intentar usarla o golpearla).</summary>
        public void NotifyExhausted() => Player.Hud.Notify("rune", "Runa gastada: ya no sirve. Hay que reciclarla.", 3f);

        private void OnValidate()
        {
            profileReady = false;
        }
    }
}
