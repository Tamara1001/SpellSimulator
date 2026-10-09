using System;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    /// <summary>Reloj manual para pruebas: el tiempo solo avanza con <see cref="Advance"/>.</summary>
    public sealed class FakeGameClock : IGameClock
    {
        public float Time { get; set; }
        public float DeltaTime { get; set; } = 0.02f;
        public float FixedDeltaTime { get; set; } = 0.02f;
        public float UnscaledTime => Time;

        public void Advance(float dt) { Time += dt; DeltaTime = dt; }
    }

    /// <summary>Instala un reloj y restaura el de Unity al salir: using (new ClockScope(fake)) { ... }.</summary>
    public readonly struct ClockScope : IDisposable
    {
        public ClockScope(IGameClock clock) => GameClock.Set(clock);
        public void Dispose() => GameClock.Set(null);
    }
}
