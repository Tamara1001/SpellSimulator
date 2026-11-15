using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Learning;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Pose del papel del banco (<see cref="LabSealBenchMath.PaperPose"/>) en el marco local del laboratorio (+Z pista, +Y arriba).
    /// <see cref="Position"/> solo vale si <see cref="AtEasel"/> es false (si no, el papel se queda en el atril).
    /// </summary>
    public readonly struct LabPaperPose
    {
        /// <summary>Posicion local del centro del papel (ignorar si <see cref="AtEasel"/>).</summary>
        public readonly Vector3 Position;
        /// <summary>Direccion (unitaria) en que sale el hechizo, ya con la cara aplicada.</summary>
        public readonly Vector3 Emission;
        /// <summary>Hacia donde mira la cara visible del papel (paralela a la emision).</summary>
        public readonly Vector3 ViewNormal;
        /// <summary>Vector «arriba» del papel (perpendicular a la emision).</summary>
        public readonly Vector3 Up;
        /// <summary>True si el papel esta en el atril (presets 0–2 y 8).</summary>
        public readonly bool AtEasel;

        public LabPaperPose(Vector3 position, Vector3 emission, Vector3 viewNormal, Vector3 up, bool atEasel)
        {
            Position = position; Emission = emission; ViewNormal = viewNormal; Up = up; AtEasel = atEasel;
        }

        /// <summary>Signo de tinta para <c>DrawingCanvas.SetInkSign</c>: +1 si el hechizo sale por la cara visible, −1 si sale por detras.</summary>
        public int InkSign => Vector3.Dot(Emission, ViewNormal) >= 0f ? 1 : -1;
    }

    /// <summary>
    /// Logica pura del banco de sellos (<see cref="LabSealBench"/>): traduce cada <see cref="LabSealVarId"/> a un valor
    /// numerico del <see cref="LabSealSpec"/> y vuelta, carga recetas de Data/Spells, formatea lecturas en español y
    /// serializa el sello en una linea breve para el CSV. Las listas (recetas, sigilos, signos) las pasa el banco; las
    /// variables de lista valen el indice en esa lista (sigilo/signo -1 = ninguno). Crea strings: solo en eventos.
    /// </summary>
    public static class LabSealBenchMath
    {
        /// <summary>Id de la receta «Libre»: sello vacio (sin sigilo ni signos) para armarlo desde cero.</summary>
        public const string FreeRecipeId = "libre";

        /// <summary>Nombres de la tinta, indexados por <see cref="LabInkKind"/>.</summary>
        public static readonly string[] InkNames = { "Normal", "Poderosa", "Resistente" };

        // ---------------------------------------------------------------- spec <-> valor

        /// <summary>Valor actual de una variable. Listas: indice (-1 si no esta en la lista); papel: indice en <see cref="LabSealSpec.PaperSizes"/>.</summary>
        public static float Get(in LabSealSpec s, LabSealVarId id, IReadOnlyList<string> recipes,
            IReadOnlyList<string> sigils, IReadOnlyList<string> signs)
        {
            switch (id)
            {
                case LabSealVarId.BaseRecipe: return IndexOf(recipes, s.recipeId);
                case LabSealVarId.Sigil: return IndexOf(sigils, s.sigilId);
                case LabSealVarId.PaperSize: return PaperIndex(s.paperSize);
                case LabSealVarId.RingFill: return s.ringFill;
                case LabSealVarId.SigilScale: return s.sigilScale;
                case LabSealVarId.SignType: return IndexOf(signs, s.signId);
                case LabSealVarId.SignCount: return s.signCount;
                case LabSealVarId.BaseAngle: return s.baseAngleDeg;
                case LabSealVarId.TurnDeg: return s.turnDeg;
                case LabSealVarId.SignLength: return s.signLength;
                case LabSealVarId.Asymmetry: return s.asymmetry;
                case LabSealVarId.SignDistance: return s.signDistance;
                case LabSealVarId.RingGap: return s.ringGapDeg;
                case LabSealVarId.Tremor: return s.tremor;
                case LabSealVarId.Ink: return (int)s.ink;
                case LabSealVarId.PaperFace: return s.paperFace;
                case LabSealVarId.PaperTilt: return s.paperTiltDeg;
                case LabSealVarId.PenWidth: return s.penWidthIndex;
                case LabSealVarId.PaperPose: return s.paperPose;
                default: return 0f;
            }
        }

        /// <summary>
        /// Copia del spec con la variable fijada y todo recortado (<see cref="LabSealSpec.Clamp"/>). En la receta base solo
        /// cambia <c>recipeId</c>: cargar la receta es <see cref="FromRecipe"/>. Sigilo/signo con indice &lt; 0 = ninguno.
        /// </summary>
        public static LabSealSpec With(LabSealSpec s, LabSealVarId id, float value, IReadOnlyList<string> recipes,
            IReadOnlyList<string> sigils, IReadOnlyList<string> signs)
        {
            int index = Mathf.RoundToInt(value);
            switch (id)
            {
                case LabSealVarId.BaseRecipe:
                    if (recipes != null && recipes.Count > 0) s.recipeId = recipes[Mathf.Clamp(index, 0, recipes.Count - 1)];
                    break;
                case LabSealVarId.Sigil: s.sigilId = Pick(sigils, index); break;
                case LabSealVarId.PaperSize: s.paperSize = LabSealSpec.PaperSizes[Mathf.Clamp(index, 0, LabSealSpec.PaperSizes.Length - 1)]; break;
                case LabSealVarId.RingFill: s.ringFill = value; break;
                case LabSealVarId.SigilScale: s.sigilScale = value; break;
                case LabSealVarId.SignType: s.signId = Pick(signs, index); break;
                case LabSealVarId.SignCount: s.signCount = index; break;
                case LabSealVarId.BaseAngle: s.baseAngleDeg = value; break;
                case LabSealVarId.TurnDeg: s.turnDeg = value; break;
                case LabSealVarId.SignLength: s.signLength = value; break;
                case LabSealVarId.Asymmetry: s.asymmetry = value; break;
                case LabSealVarId.SignDistance: s.signDistance = value; break;
                case LabSealVarId.RingGap: s.ringGapDeg = value; break;
                case LabSealVarId.Tremor: s.tremor = value; break;
                case LabSealVarId.Ink: s.ink = (LabInkKind)Mathf.Clamp(index, 0, InkNames.Length - 1); break;
                case LabSealVarId.PaperFace: s.paperFace = value < 0f ? -1 : 1; break;
                case LabSealVarId.PaperTilt: s.paperTiltDeg = value; break;
                case LabSealVarId.PenWidth: s.penWidthIndex = index; break;
                case LabSealVarId.PaperPose: s.paperPose = index; break;
            }
            return LabSealSpec.Clamp(s);
        }

        /// <summary>
        /// Carga una receta sobre el spec actual: sigilo, tipo y cantidad de signos, angulo base y giro salen de la receta; la
        /// geometria del sello (tamaño del sigilo, largo, asimetria, distancia) vuelve al canon y el anillo se cierra (hueco 0,
        /// para que se lance). Papel, tinta, cara, inclinacion, pluma, temblor y semilla se conservan. <paramref name="def"/>
        /// null = «Libre» (sin sigilo ni signos). <paramref name="exact"/> es false si la receta no entra en el modelo del banco
        /// (signos distintos entre si, giros distintos o angulos no repartidos parejo): se toma el primer signo.
        /// </summary>
        public static LabSealSpec FromRecipe(in LabSealSpec current, SpellDef def, out bool exact)
        {
            LabSealSpec s = current;
            s.sigilScale = 1f;
            s.signLength = 1f;
            s.asymmetry = 1f;
            s.signDistance = SpellStrokeBuilder.SignDistance;
            s.ringGapDeg = 0f;
            exact = true;

            if (def == null)
            {
                s.recipeId = FreeRecipeId;
                s.sigilId = null;
                s.signId = null;
                s.signCount = 0;
                s.baseAngleDeg = 0f;
                s.turnDeg = 0f;
                return LabSealSpec.Clamp(s);
            }

            s.recipeId = def.Id;
            s.sigilId = def.Sigil;
            int count = def.Signs.Count;
            s.signCount = count;
            if (count == 0)
            {
                s.signId = null;
                s.baseAngleDeg = 0f;
                s.turnDeg = 0f;
            }
            else
            {
                SpellSign first = def.Signs[0];
                s.signId = first.Id;
                s.baseAngleDeg = SnapAngle(first.AngleDeg);
                s.turnDeg = FoldTurn(first.TurnDeg) >= 90f ? LabSealSpec.TurnOutDeg : 0f;
                for (int i = 0; i < count; i++)
                {
                    SpellSign sign = def.Signs[i];
                    float expected = first.AngleDeg + 360f * i / count;
                    if (sign.Id != first.Id || Mathf.Abs(FoldTurn(sign.TurnDeg) - FoldTurn(first.TurnDeg)) > 1f
                        || Mathf.Abs(Mathf.DeltaAngle(sign.AngleDeg, expected)) > 1f)
                        exact = false;
                }
                if (Mathf.Abs(Mathf.DeltaAngle(first.AngleDeg, s.baseAngleDeg)) > 1f) exact = false;
                if (count > LabSealSpec.MaxSignCount) exact = false;
            }
            return LabSealSpec.Clamp(s);
        }

        // ---------------------------------------------------------------- texto

        /// <summary>
        /// Lectura en español de las variables numericas (p. ej. «45°», «0,45 m», «×1,2», «80 %»). Devuelve null en las de
        /// lista que dependen de nombres externos (receta, sigilo, tipo de signo, pluma): esas las formatea el banco.
        /// </summary>
        public static string FormatNumeric(LabSealVarId id, float value)
        {
            switch (id)
            {
                case LabSealVarId.PaperSize:
                    return Num(LabSealSpec.PaperSizes[Mathf.Clamp(Mathf.RoundToInt(value), 0, LabSealSpec.PaperSizes.Length - 1)], "0.##") + " m";
                case LabSealVarId.RingFill: return Num(value * 100f, "0") + " % del papel";
                case LabSealVarId.SigilScale:
                case LabSealVarId.SignLength:
                case LabSealVarId.Asymmetry: return "×" + Num(value, "0.##");
                case LabSealVarId.SignCount: return Mathf.RoundToInt(value).ToString(LabCsv.NumberFormat);
                case LabSealVarId.BaseAngle:
                case LabSealVarId.PaperTilt: return Num(value, "0") + "°";
                case LabSealVarId.TurnDeg: return value >= LabSealSpec.TurnOutDeg * 0.5f ? "hacia afuera" : "hacia adentro";
                case LabSealVarId.SignDistance: return Num(value, "0.00") + " R";
                case LabSealVarId.RingGap:
                    return Num(value, "0") + "°" + (value >= StampPattern.RingGapDeg ? " (preparado)" : value <= 0f ? " (cerrado)" : "");
                case LabSealVarId.Tremor: return value <= 0f ? "prolijo" : Num(value, "0.000") + " R";
                case LabSealVarId.Ink: return InkNames[Mathf.Clamp(Mathf.RoundToInt(value), 0, InkNames.Length - 1)];
                case LabSealVarId.PaperFace: return value < 0f ? "abajo" : "arriba";
                case LabSealVarId.PaperPose: return PaperPoseName(Mathf.RoundToInt(value));
                default: return null;
            }
        }

        /// <summary>
        /// Sello en una linea breve para la columna spec_sello del CSV: pares clave=valor separados por espacios (sin ';', que es
        /// el separador del CSV), numeros con coma decimal. Ej.: «sigilo=fire signo=column×2 base=0 giro=0 ... semilla=1».
        /// </summary>
        public static string Serialize(in LabSealSpec spec)
        {
            LabSealSpec s = LabSealSpec.Clamp(spec);
            var sb = new StringBuilder(220);
            sb.Append("sigilo=").Append(string.IsNullOrEmpty(s.sigilId) ? "-" : s.sigilId);
            sb.Append(" signo=").Append(string.IsNullOrEmpty(s.signId) ? "-" : s.signId).Append('×').Append(s.signCount);
            sb.Append(" base=").Append(Num(s.baseAngleDeg, "0"));
            sb.Append(" giro=").Append(Num(s.turnDeg, "0"));
            sb.Append(" largo=").Append(Num(s.signLength, "0.##"));
            sb.Append(" asim=").Append(Num(s.asymmetry, "0.##"));
            sb.Append(" dist=").Append(Num(s.signDistance, "0.###"));
            sb.Append(" sigiloX=").Append(Num(s.sigilScale, "0.##"));
            sb.Append(" anillo=").Append(Num(s.ringFill, "0.##"));
            sb.Append(" hueco=").Append(Num(s.ringGapDeg, "0.#"));
            sb.Append(" temblor=").Append(Num(s.tremor, "0.###"));
            sb.Append(" papel=").Append(Num(s.paperSize, "0.##"));
            sb.Append(" tinta=").Append(InkNames[(int)s.ink]);
            sb.Append(" cara=").Append(s.paperFace < 0 ? "abajo" : "arriba");
            sb.Append(" incl=").Append(Num(s.paperTiltDeg, "0"));
            sb.Append(" pluma=").Append(s.penWidthIndex);
            sb.Append(" pose=").Append(s.paperPose);
            sb.Append(" semilla=").Append(s.seed);
            return sb.ToString();
        }

        /// <summary>
        /// Copia de <paramref name="current"/> con las variables de <paramref name="ids"/> tomadas de <paramref name="baseline"/>
        /// (reset por grupo). La receta base se ignora aqui: cargar una receta es <see cref="FromRecipe"/>.
        /// </summary>
        public static LabSealSpec ResetVars(in LabSealSpec current, in LabSealSpec baseline, IReadOnlyList<LabSealVarId> ids,
            IReadOnlyList<string> recipes, IReadOnlyList<string> sigils, IReadOnlyList<string> signs)
        {
            LabSealSpec s = current;
            if (ids == null) return s;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == LabSealVarId.BaseRecipe) continue;
                s = With(s, ids[i], Get(baseline, ids[i], recipes, sigils, signs), recipes, sigils, signs);
            }
            return s;
        }

        // ---------------------------------------------------------------- posicion del papel (Laboratorio v3)

        /// <summary>Nombres de los presets de posicion del papel, indexados por <see cref="LabSealSpec.paperPose"/>.</summary>
        public static readonly string[] PaperPoseNames =
        {
            "Atril → pista", "Atril → jugador", "Atril → arriba", "Piso", "Pista → izquierda",
            "Pista → derecha", "Pista → banco", "Pista → piso", "Libre"
        };

        /// <summary>Altura (m) del centro del papel de pie en la pista.</summary>
        public const float StandHeight = 1.2f;
        /// <summary>Altura (m) del papel acostado en el piso (levemente sobre el suelo).</summary>
        public const float FloorHeight = 0.02f;
        /// <summary>Altura (m) del papel «Pista → piso» (boca abajo).</summary>
        public const float HighHeight = 1.5f;

        // Hacia donde mira el jugador desde el banco: hacia atras (−Z) y un poco desde arriba.
        private static readonly Vector3 ViewerHint = new Vector3(0f, 0.5f, -1f);

        /// <summary>Nombre del preset (vacio fuera de rango).</summary>
        public static string PaperPoseName(int preset)
            => preset >= 0 && preset < PaperPoseNames.Length ? PaperPoseNames[preset] : string.Empty;

        /// <summary>
        /// Pose del papel para un preset (tabla de Docs/LABORATORIO_V3_PLAN.md, (b)), en el marco local del laboratorio:
        /// +Z a lo largo de la pista (desde el banco hacia el fondo), +Y arriba, +X a la derecha del jugador mirando la pista.
        /// <paramref name="trackStart"/> = Z (m) del inicio de la pista en ese marco. En 0–7 <paramref name="face"/> = −1 invierte la
        /// emision y <paramref name="tiltDeg"/> se ignora; el 8 (libre) usa cara e inclinacion como en el Laboratorio v2.
        /// </summary>
        public static LabPaperPose PaperPose(int preset, int face, float tiltDeg, float trackStart)
        {
            preset = Mathf.Clamp(preset, 0, LabSealSpec.PaperPoseCount - 1);
            int f = face < 0 ? -1 : 1;
            Vector3 dir, up, position = Vector3.zero;
            bool atEasel = true;
            switch (preset)
            {
                case 0: dir = Vector3.forward; up = Vector3.up; break;
                case 1: dir = Vector3.back; up = Vector3.up; break;
                case 2: dir = Vector3.up; up = Vector3.forward; break;
                case 3: dir = Vector3.up; up = Vector3.forward; position = new Vector3(0f, FloorHeight, trackStart + 2f); atEasel = false; break;
                case 4: dir = Vector3.left; up = Vector3.up; position = new Vector3(0f, StandHeight, trackStart + 3f); atEasel = false; break;
                case 5: dir = Vector3.right; up = Vector3.up; position = new Vector3(0f, StandHeight, trackStart + 3f); atEasel = false; break;
                case 6: dir = Vector3.back; up = Vector3.up; position = new Vector3(0f, StandHeight, trackStart + 6f); atEasel = false; break;
                case 7: dir = Vector3.down; up = Vector3.forward; position = new Vector3(0f, HighHeight, trackStart + 3f); atEasel = false; break;
                default:
                    // Libre: como el v2 (horizontal hacia el fondo de la pista, levantada tiltDeg). La cara no cambia la rotacion, solo el lado de la tinta.
                    dir = EmissionDirection(Vector3.zero, Vector3.forward, tiltDeg, Vector3.forward, out up);
                    return new LabPaperPose(position, dir * f, -dir, up, true);
            }
            // Cara visible: el lado del eje de emision que mira hacia el jugador (en los laterales, sin lado preferido: el opuesto a la emision base).
            float d = Vector3.Dot(dir, ViewerHint);
            Vector3 view = d > 1e-4f ? dir : -dir;
            return new LabPaperPose(position, dir * f, view, up, atEasel);
        }

        // ---------------------------------------------------------------- geometria del lienzo de prueba

        /// <summary>
        /// Direccion en que debe salir el hechizo: horizontal hacia el blanco y levantada <paramref name="tiltDeg"/> grados
        /// (0 = horizontal, 90 = vertical hacia arriba). <paramref name="upHint"/> queda perpendicular, para orientar el papel.
        /// </summary>
        public static Vector3 EmissionDirection(Vector3 from, Vector3 target, float tiltDeg, Vector3 fallbackForward, out Vector3 upHint)
        {
            Vector3 flat = target - from;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f)
            {
                flat = fallbackForward;
                flat.y = 0f;
                if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
            }
            flat.Normalize();
            float k = Mathf.Clamp(tiltDeg, 0f, LabSealSpec.MaxPaperTiltDeg) / 90f;
            // Slerp entre vectores perpendiculares: el angulo es exacto y los dos resultados siguen perpendiculares.
            upHint = Vector3.Slerp(Vector3.up, -flat, k);
            return Vector3.Slerp(flat, Vector3.up, k);
        }

        /// <summary>
        /// Rotacion de mundo que lleva la normal local de la cara visible (<paramref name="surfaceNormalLocal"/>) a
        /// <paramref name="worldNormal"/>, con el «arriba» del papel lo mas cerca posible de <paramref name="worldUpHint"/>.
        /// </summary>
        public static Quaternion FaceRotation(Vector3 surfaceNormalLocal, Vector3 worldNormal, Vector3 worldUpHint)
        {
            Vector3 n = surfaceNormalLocal.sqrMagnitude > 1e-6f ? surfaceNormalLocal.normalized : Vector3.back;
            Vector3 localUp = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            Quaternion basis = Quaternion.LookRotation(n, localUp);
            return Quaternion.LookRotation(worldNormal, worldUpHint) * Quaternion.Inverse(basis);
        }

        /// <summary>UV del sello sintetizado (cuadrado unidad) -> UV de un lienzo no cuadrado: el sello ocupa el cuadrado central (lado = lado menor), asi un circulo sigue siendo circulo.</summary>
        public static Vector2 SquareToCanvasUv(Vector2 p, Vector2 iso)
        {
            float side = Mathf.Min(iso.x, iso.y);
            if (iso.x <= 1e-6f || iso.y <= 1e-6f) return p;
            Vector2 q = iso * 0.5f + (p - new Vector2(0.5f, 0.5f)) * side;
            return new Vector2(q.x / iso.x, q.y / iso.y);
        }

        // ---------------------------------------------------------------- auxiliares

        /// <summary>Indice del tamaño de papel permitido mas cercano.</summary>
        public static int PaperIndex(float paperSize)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < LabSealSpec.PaperSizes.Length; i++)
            {
                float d = Mathf.Abs(paperSize - LabSealSpec.PaperSizes[i]);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>Angulo en [0, 360) a pasos de <see cref="LabSealSpec.BaseAngleStepDeg"/>.</summary>
        public static float SnapAngle(float deg)
        {
            float a = Mathf.Repeat(deg, 360f);
            a = Mathf.Round(a / LabSealSpec.BaseAngleStepDeg) * LabSealSpec.BaseAngleStepDeg;
            return a >= 360f ? 0f : a;
        }

        /// <summary>Giro absoluto en 0..180 (0 = adentro, 180 = afuera), como SpellCatalog.</summary>
        private static float FoldTurn(float deg)
        {
            deg = Math.Abs(deg % 360f);
            return deg > 180f ? 360f - deg : deg;
        }

        private static int IndexOf(IReadOnlyList<string> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], id, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static string Pick(IReadOnlyList<string> list, int index)
        {
            if (list == null || list.Count == 0 || index < 0) return null;
            return list[Mathf.Min(index, list.Count - 1)];
        }

        private static string Num(float v, string format) => v.ToString(format, LabCsv.NumberFormat);
    }
}
