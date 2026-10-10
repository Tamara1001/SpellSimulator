using System;

namespace SpellSimulator.Combat
{
    /// <summary>Reglas del Duelo (balance ajustable): rondas para ganar, duraciones y tope de rondas.</summary>
    [Serializable]
    public struct DuelRules
    {
        public int roundsToWin;
        public float countdownSeconds;
        public float roundSeconds;
        public float roundEndSeconds;
        public int maxRounds;

        public static DuelRules Default => new DuelRules { roundsToWin = 2, countdownSeconds = 3f, roundSeconds = 90f, roundEndSeconds = 4f, maxRounds = 5 };
    }

    public enum DuelPhase : byte { Countdown = 0, Round = 1, RoundEnd = 2, MatchEnd = 3 }

    /// <summary>
    /// Maquina de estados del Duelo 1 contra 1 al mejor de N rondas. Logica pura (sin Unity): la alimenta DuelGameMode con
    /// Tick y con la vida de cada jugador; en red solo la corre el anfitrion y los clientes reciben su estado.
    /// Un jugador es 0 o 1; Winner/RoundWinner = -1 si nadie (empate o aun sin resultado).
    /// </summary>
    public sealed class DuelMatch
    {
        public readonly DuelRules Rules;
        public DuelPhase Phase { get; private set; }
        public int RoundNumber { get; private set; }   // 1..N
        public float PhaseTime { get; private set; }   // segundos en la fase actual
        public int RoundWinner { get; private set; } = -1;
        public int Winner { get; private set; } = -1;
        public readonly int[] Wins = new int[2];

        /// <summary>Se dispara al empezar cada fase (para reaparecer, restaurar vida, HUD).</summary>
        public event Action<DuelPhase> PhaseChanged;

        public DuelMatch(DuelRules rules)
        {
            Rules = rules;
            Phase = DuelPhase.Countdown;
            RoundNumber = 1;
        }

        public float PhaseDuration
        {
            get
            {
                switch (Phase)
                {
                    case DuelPhase.Countdown: return Rules.countdownSeconds;
                    case DuelPhase.Round: return Rules.roundSeconds;
                    case DuelPhase.RoundEnd: return Rules.roundEndSeconds;
                    default: return 0f;
                }
            }
        }

        public float SecondsLeft => Phase == DuelPhase.MatchEnd ? 0f : Math.Max(0f, PhaseDuration - PhaseTime);
        public bool Over => Phase == DuelPhase.MatchEnd;

        /// <summary>Avanza el reloj. Vidas = fraccion 0-1 de cada jugador (para decidir al acabarse el tiempo).</summary>
        public void Tick(float dt, float life0, float life1)
        {
            if (Phase == DuelPhase.MatchEnd || dt <= 0f) return;
            PhaseTime += dt;
            switch (Phase)
            {
                case DuelPhase.Countdown:
                    if (PhaseTime >= Rules.countdownSeconds) Enter(DuelPhase.Round);
                    break;
                case DuelPhase.Round:
                    if (life0 <= 0f || life1 <= 0f) EndRound(life0 <= 0f && life1 <= 0f ? -1 : (life0 <= 0f ? 1 : 0));
                    else if (PhaseTime >= Rules.roundSeconds) EndRound(life0 > life1 ? 0 : (life1 > life0 ? 1 : -1));
                    break;
                case DuelPhase.RoundEnd:
                    if (PhaseTime >= Rules.roundEndSeconds) NextRoundOrFinish();
                    break;
            }
        }

        /// <summary>Un jugador se fue de la partida: el otro gana el match.</summary>
        public void Forfeit(int player)
        {
            if (Phase == DuelPhase.MatchEnd || player < 0 || player > 1) return;
            Winner = 1 - player;
            Enter(DuelPhase.MatchEnd);
        }

        /// <summary>Red: un cliente copia el estado que manda el anfitrion.</summary>
        public void Apply(DuelPhase phase, int round, float phaseTime, int wins0, int wins1, int roundWinner, int winner)
        {
            bool changed = phase != Phase;
            Phase = phase; RoundNumber = round; PhaseTime = phaseTime;
            Wins[0] = wins0; Wins[1] = wins1; RoundWinner = roundWinner; Winner = winner;
            if (changed) PhaseChanged?.Invoke(Phase);
        }

        private void EndRound(int winner)
        {
            RoundWinner = winner;
            if (winner >= 0) Wins[winner]++;
            Enter(DuelPhase.RoundEnd);
        }

        private void NextRoundOrFinish()
        {
            if (Wins[0] >= Rules.roundsToWin || Wins[1] >= Rules.roundsToWin || RoundNumber >= Rules.maxRounds)
            {
                Winner = Wins[0] > Wins[1] ? 0 : (Wins[1] > Wins[0] ? 1 : -1);
                Enter(DuelPhase.MatchEnd);
                return;
            }
            RoundNumber++;
            RoundWinner = -1;
            Enter(DuelPhase.Countdown);
        }

        private void Enter(DuelPhase phase)
        {
            Phase = phase;
            PhaseTime = 0f;
            PhaseChanged?.Invoke(phase);
        }
    }
}
