using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Combat
{
    /// <summary>
    /// Tinta del Duelo, autoritativa en el servidor: cada jugador tiene un saldo que se recarga solo y cada hechizo cuesta lo que pesan los trazos
    /// del lienzo (lo que el servidor ve, no lo que diga el cliente). Logica pura.
    /// </summary>
    public sealed class InkLedger
    {
        public readonly float Max;
        public readonly float RegenPerSecond;
        public float Current { get; private set; }
        public float Fraction => Max > 0f ? Current / Max : 0f;

        public InkLedger(float max, float regenPerSecond)
        {
            Max = Mathf.Max(0f, max);
            RegenPerSecond = Mathf.Max(0f, regenPerSecond);
            Current = Max;
        }

        public void Refill() => Current = Max;

        public void Tick(float dt) => Current = Mathf.Min(Max, Current + RegenPerSecond * Mathf.Max(0f, dt));

        /// <summary>Descuenta el costo completo o nada: false si no alcanza.</summary>
        public bool TryCharge(float cost)
        {
            if (cost < 0f || cost > Current) return false;
            Current -= cost;
            return true;
        }
    }

    public static class DuelInk
    {
        /// <summary>Costo de tinta de todo lo dibujado en un lienzo: metros reales de cada trazo (UV x tamano del lienzo) con el costo por clase de tinta.</summary>
        public static float CanvasCost(IReadOnlyList<Stroke> strokes, Vector2 canvasWorldSize, float inkPerMeter)
        {
            if (strokes == null) return 0f;
            float total = 0f;
            for (int i = 0; i < strokes.Count; i++)
            {
                Stroke s = strokes[i];
                float meters = 0f;
                for (int j = 1; j < s.Points.Count; j++)
                {
                    Vector2 d = s.Points[j] - s.Points[j - 1];
                    meters += new Vector2(d.x * canvasWorldSize.x, d.y * canvasWorldSize.y).magnitude;
                }
                total += InkMath.StrokeCost(meters, canvasWorldSize, inkPerMeter, s.Ink);
            }
            return total;
        }
    }
}
