using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Compiler
{
    /// <summary>
    /// Lectura de un signo para el plan (SignReading de la referencia). Espacio-sello: origen en el centro del anillo,
    /// 1 unidad = radio del anillo, X derecha, Y arriba, Z afuera del papel.
    /// </summary>
    public struct SignReading
    {
        public string Id;
        public string Manifestation;
        /// <summary>Posicion del signo en el sello.</summary>
        public Vector2 At;
        /// <summary>Largo de tinta en radios del anillo.</summary>
        public float Length;
        /// <summary>Hacia donde apunta el signo (unitario).</summary>
        public Vector2 Facing;
        /// <summary>Confianza en la orientacion. Bajo el piso, el signo aporta potencia pero no direccion (R-06).</summary>
        public float FacingTrust;
        public bool FacingFromPose;
    }

    /// <summary>Plan de sostener (levitacion): resorte que mantiene la magia a una altura.</summary>
    public class HoldSpec
    {
        /// <summary>Posicion del orbe en espacio-sello (z = altura sobre el papel, en radios).</summary>
        public Vector3 At;
        public float Grip;
        /// <summary>Giro del orbe (positivo = antihorario visto desde el papel).</summary>
        public float Spin;
        public float Budget;
    }

    /// <summary>Plan de succion (pull): acople con la materia ambiente del mismo elemento.</summary>
    public class IntakeSpec
    {
        public float Budget;
        /// <summary>Atraccion hacia el sello (positivo = hacia adentro).</summary>
        public float Draw;
        public float Swirl;
        public Vector2 Lateral;
    }

    public enum PlanMode { Create, Manipulate }

    /// <summary>Primitivas que la manifestacion 3D tiene que ejecutar.</summary>
    [System.Flags]
    public enum PlanPrimitive { None = 0, Burst = 1, Jet = 2, Fan = 4, Vortex = 8, Hold = 16, Intake = 32 }

    /// <summary>
    /// Que hace el hechizo en el espacio (SpellPlan de la referencia, compiler/plan/). Cada familia de signos tiene su
    /// propio presupuesto y verbo (R-13): columna = motor, levitacion = resorte, pull = succion, convergencia = lente.
    /// </summary>
    public class SpellPlan
    {
        public PlanMode Mode;
        /// <summary>Direccion del chorro en espacio-sello: (P lateral, max(C, 0)). Cero = sin chorro.</summary>
        public Vector3 Aim;
        /// <summary>Abanico radial pegado al papel (columnas hacia afuera).</summary>
        public float Dispersion;
        /// <summary>Remolino alrededor del eje del sello (positivo = antihorario).</summary>
        public float Circulation;
        /// <summary>Presupuesto total de tinta de los signos (columnas + potencia).</summary>
        public float Budget;
        public HoldSpec Hold;
        public IntakeSpec Intake;
        /// <summary>Lente de convergencia: 1 = sin enfoque; mayor = mas angosto.</summary>
        public float Focus = 1f;
        /// <summary>Dispersion como fuga lenta (mas larga, menos intensa) en vez de rafaga (R-08).</summary>
        public bool Leak;
        public float Quality;
        public PlanPrimitive Primitives;
        public readonly List<string> Notes = new List<string>();
        /// <summary>Lectura de cada signo que entro al plan (para diagnostico y overlays).</summary>
        public readonly List<SignReading> Signs = new List<SignReading>();

        public static SpellPlan Inert() => new SpellPlan { Primitives = PlanPrimitive.Burst };

        public bool Has(PlanPrimitive primitive) => (Primitives & primitive) != 0;
    }
}
