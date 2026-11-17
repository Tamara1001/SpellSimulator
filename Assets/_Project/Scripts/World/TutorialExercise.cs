using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Ejercicio OPCIONAL del piso 2 del tutorial (no traba nada): dejar un lienzo flotando en un marco, ponerlo de canto
    /// o acertar un tiro a la diana. Al cumplirse enciende su farolito; el reinicio del tutorial lo apaga.
    /// </summary>
    public class TutorialExercise : MonoBehaviour
    {
        public enum Kind { Float, Upright, Hit }

        [SerializeField] private Kind kind;
        [SerializeField] private GameObject glow;

        private static readonly List<TutorialExercise> all = new List<TutorialExercise>(4);
        private bool done;
        private float held;

        public bool Done => done;
        public Kind Mode => kind;

        public void Configure(Kind mode, GameObject light)
        {
            kind = mode;
            glow = light;
        }

        private void OnEnable() => all.Add(this);

        private void OnDisable() => all.Remove(this);

        /// <summary>Apaga todos los ejercicios (el tutorial arranca de nuevo).</summary>
        public static void ResetAll()
        {
            for (int i = all.Count - 1; i >= 0; i--) all[i].Rearm();
        }

        public void Rearm()
        {
            done = false;
            held = 0f;
            if (glow != null) glow.SetActive(false);
        }

        private void OnTriggerStay(Collider other)
        {
            if (done || kind == Kind.Hit) return;
            var canvas = other.GetComponentInParent<PhysicalCanvas>();
            bool ok = canvas != null && canvas.IsFloating && (kind != Kind.Upright || TutorialExerciseRules.Upright(canvas.transform.forward));
            if (!ok) { held = 0f; return; }
            held += Time.fixedDeltaTime;
            if (held >= TutorialExerciseRules.HoldSeconds) Complete();
        }

        private void OnTriggerExit(Collider other) => held = 0f;

        private void OnCollisionEnter(Collision collision)
        {
            if (done || kind != Kind.Hit) return;
            if (collision.rigidbody != null && collision.rigidbody.GetComponent<Movable>() != null
                && TutorialExerciseRules.HardEnough(collision.relativeVelocity.magnitude)) Complete();
        }

        /// <summary>Marca el ejercicio como cumplido (lo usan el juego y las pruebas).</summary>
        public void Complete()
        {
            done = true;
            if (glow != null) glow.SetActive(true);
        }
    }
}
