using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.DebugTools;
using SpellSimulator.Core;

namespace SpellSimulator.World
{
    /// <summary>
    /// Union de runas (bloque 4): cuando dos runas ARMADAS quedan en contacto suave durante
    /// <see cref="RuneJoinMath.DefaultJoinSeconds"/>, sus dibujos se fusionan en un solo hechizo. La runa de mayor
    /// anillo manda el tamano, el origen y la direccion; la chica aporta su potencia y primitivas, y ademas lanza su
    /// propio efecto en el mismo marco (de ahi los combos: fuego + viento, agua + cristal). Cada runa gasta un uso.
    /// Va junto al SealTrigger y al Rigidbody de la runa.
    /// </summary>
    [RequireComponent(typeof(SealTrigger), typeof(Rigidbody))]
    public class RuneJoin : MonoBehaviour
    {
        [SerializeField, Tooltip("Segundos de contacto suave sostenido para fusionarse.")]
        private float joinSeconds = RuneJoinMath.DefaultJoinSeconds;

        private static readonly List<RuneJoin> all = new List<RuneJoin>(16);

        private SealTrigger trigger;
        private Rigidbody body;
        private RuneJoin partner;
        private float lastContactTime = -100f;
        private float held;

        private void Awake()
        {
            trigger = GetComponent<SealTrigger>();
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable() => all.Add(this);

        private void OnDisable()
        {
            all.Remove(this);
            partner = null;
            held = 0f;
        }

        private RuneJoin FindByBody(Rigidbody other)
        {
            if (other == null) return null;
            for (int i = 0; i < all.Count; i++)
                if (all[i].body == other && all[i] != this) return all[i];
            return null;
        }

        private void OnCollisionStay(Collision collision)
        {
            RuneJoin other = FindByBody(collision.rigidbody);
            if (other == null) return;
            // Contacto suave: por debajo del umbral de impacto (si no, ya disparo cada una por su cuenta).
            if (collision.relativeVelocity.magnitude >= trigger.ImpactSpeed) return;
            partner = other;
            lastContactTime = GameClock.Time;
        }

        private static bool Exhausted(SealTrigger t) => t.Rune != null && t.Rune.IsExhausted;

        private void FixedUpdate()
        {
            bool contact = partner != null && GameClock.Time - lastContactTime < GameClock.FixedDeltaTime * 2.5f;
            if (contact)
            {
                float fired = RuneJoinMath.RecentFireSeconds;
                // El contacto ya se filtro por velocidad en OnCollisionStay: aqui se pasa 0.
                contact = RuneJoinMath.CanJoin(
                    trigger.IsArmed, partner.trigger.IsArmed,
                    Exhausted(trigger), Exhausted(partner.trigger),
                    trigger.FiredRecently(fired), partner.trigger.FiredRecently(fired),
                    0f, trigger.ImpactSpeed);
            }
            held = RuneJoinMath.Advance(held, GameClock.FixedDeltaTime, contact);
            if (!contact)
            {
                if (partner != null && GameClock.Time - lastContactTime > 0.5f) partner = null;
                return;
            }
            // Solo una de las dos runas dirige la union.
            if (!RuneJoinMath.IsLeader(GetInstanceID(), partner.GetInstanceID())) return;
            if (RuneJoinMath.Completed(held, joinSeconds)) Join(partner);
        }

        private void Join(RuneJoin other)
        {
            held = 0f;
            other.held = 0f;
            SpellCaster caster = trigger.Caster;
            DrawingCanvas canvasA = trigger.Canvas, canvasB = other.trigger.Canvas;
            if (caster == null || canvasA == null || canvasB == null) return;

            // Compilar ambas ANTES de gastar nada: si una no se lee, no pasa nada.
            if (!caster.Compile(canvasA, out SpellIR irA, out Vector2 uvA) || !caster.Compile(canvasB, out SpellIR irB, out Vector2 uvB))
            {
                Debug.Log($"[Runa] Union de {name} y {other.name} cancelada: un sello no se pudo leer.", this);
                return;
            }

            bool aIsBig = RuneJoinMath.IsBig(irA.sealRadius, irB.sealRadius, GetInstanceID(), other.GetInstanceID());
            SpellIR big = aIsBig ? irA : irB;
            SpellIR small = aIsBig ? irB : irA;
            DrawingCanvas bigCanvas = aIsBig ? canvasA : canvasB;
            Vector2 bigUV = aIsBig ? uvA : uvB;

            SpellIR merged = SpellFusion.Merge(big, small);
            caster.Emit(merged, bigCanvas, bigUV);

            // La chica, con su propio elemento, en el mismo origen y marco de la grande.
            small.sealRadius = big.sealRadius;
            caster.Emit(small, bigCanvas, bigUV);

            caster.MarkSpent(canvasA);
            caster.MarkSpent(canvasB);
            trigger.RegisterFired();
            other.trigger.RegisterFired();

            Debug.Log($"[Runa] Union: {(aIsBig ? name : other.name)} ({big.element}) + {(aIsBig ? other.name : name)} ({small.element}), radio {big.sealRadius:F2} m.", this);
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke($"<b>[Runas] Unidas</b>\n{big.element} + {small.element}");
        }
    }
}
