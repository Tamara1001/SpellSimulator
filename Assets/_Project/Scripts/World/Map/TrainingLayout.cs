using UnityEngine;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Distribucion del entrenamiento base (blancos, munecos, atril de papel, mesas y artilugios) relativa al atril.
    /// Logica pura: convierte un desplazamiento local (x = derecha, y = adelante, en metros) a coordenadas del mundo (XZ)
    /// segun el origen y el frente del entrenamiento (<see cref="ValleyShape.TrainingOrigin"/> / <see cref="ValleyShape.TrainingForward"/>).
    /// Los desplazamientos reproducen exactamente la disposicion que tenia el campo viejo (atril en <see cref="LegacyStation"/>
    /// mirando a +Z), solo rotada y trasladada al sitio nuevo.
    /// </summary>
    public static class TrainingLayout
    {
        /// <summary>Atril del campo viejo (FieldCenter + (0, -8)) y mesa de estampas vieja: las rutas de los senderos siguen apuntando ahi.</summary>
        public static readonly Vector2 LegacyStation = new Vector2(28f, -14f);
        public static readonly Vector2 LegacyFieldPoint = new Vector2(30f, -16.5f);

        // Desplazamientos locales (derecha, adelante) desde el atril.
        /// <summary>Origen del grupo TestProps (blancos, cajas y plataforma): 2 m detras del atril.</summary>
        public static readonly Vector2 TestProps = new Vector2(0f, -2f);
        public static readonly Vector2 DummyA = new Vector2(0f, 9f);
        public static readonly Vector2 DummyB = new Vector2(-3.5f, 3f);
        public static readonly Vector2 ToolTable = new Vector2(-2.2f, -2.5f);
        public static readonly Vector2 StampTable = new Vector2(2f, -2.5f);
        /// <summary>Artilugios (piedras de impacto, placa de presion y pesa): a la izquierda de la mesa de estampas.</summary>
        public static readonly Vector2 Contraptions = new Vector2(-3f, -2.5f);
        public static readonly Vector2 GiantStand = new Vector2(-6f, 0.5f);
        public static readonly Vector2 GiantPaper = new Vector2(-4f, 2.5f);
        public static readonly Vector2 Easel = new Vector2(-7f, -4.5f);
        /// <summary>Segunda mesa de herramientas, detras de la primera: lo que sobro de las mesas del piso 1 de la torre (cinturon, pincel, trapo, cuadernos).</summary>
        public static readonly Vector2 ExtraToolTable = new Vector2(-2.2f, -3.7f);

        /// <summary>Rumbo (grados, 0 = +Z, 90 = +X) del frente del entrenamiento.</summary>
        public static float YawDegrees => YawOf(ValleyShape.TrainingForward);

        public static float YawOf(Vector2 forward) => Mathf.Atan2(forward.x, forward.y) * Mathf.Rad2Deg;

        /// <summary>Local (derecha, adelante) a mundo XZ con el origen y el frente dados.</summary>
        public static Vector2 ToWorld(Vector2 origin, Vector2 forward, Vector2 local)
        {
            Vector2 f = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector2.up;
            Vector2 right = new Vector2(f.y, -f.x);
            return origin + right * local.x + f * local.y;
        }

        /// <summary>Local a mundo XZ en el entrenamiento actual.</summary>
        public static Vector2 ToWorld(Vector2 local) => ToWorld(ValleyShape.TrainingOrigin, ValleyShape.TrainingForward, local);

        /// <summary>Desplazamiento local (x derecha, y alto, z adelante) rotado al mundo; la altura no cambia.</summary>
        public static Vector3 Offset(Vector3 local)
        {
            Vector2 w = ToWorld(Vector2.zero, ValleyShape.TrainingForward, new Vector2(local.x, local.z));
            return new Vector3(w.x, local.y, w.y);
        }
    }
}
