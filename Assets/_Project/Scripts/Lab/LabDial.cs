using System.Collections;
using TMPro;
using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Perilla del laboratorio: una palanca fisica que marca la posicion del valor en su rango y una lectura numerica. Escucha
    /// <see cref="LabParameters.ValueChanged"/>; la palanca gira sobre su eje Z local de -60° (minimo) a +60° (maximo) partiendo
    /// de la rotacion que tiene en la escena (el generador la orienta). Solo anima con una corrutina mientras se mueve: en
    /// reposo no corre nada por frame. La lectura cambia de color cuando el valor no es el default.
    /// </summary>
    public class LabDial : MonoBehaviour
    {
        /// <summary>Angulo de la palanca en el minimo y el maximo del rango (grados, eje Z local).</summary>
        private const float MinAngle = -60f, MaxAngle = 60f;
        /// <summary>Rapidez de la interpolacion exponencial de la palanca (1/s).</summary>
        private const float Sharpness = 12f;
        /// <summary>Diferencia (grados) bajo la cual la palanca se considera llegada.</summary>
        private const float ArriveDegrees = 0.1f;

        private static readonly Color ModifiedColor = new Color(1f, 0.72f, 0.25f);

        [SerializeField, Tooltip("Variable que muestra esta perilla.")]
        private LabVariableId variable = LabVariableId.Mass;
        [SerializeField, Tooltip("Pivote de la palanca: gira sobre su eje Z local.")]
        private Transform lever;
        [SerializeField, Tooltip("Lectura numerica (TextMeshPro).")]
        private TMP_Text readout;

        private LabParameters bound;
        private Quaternion leverBase;
        private Color defaultColor = Color.white;
        private float angle, targetAngle;
        private Coroutine animating;

        private void Awake()
        {
            if (lever != null) leverBase = lever.localRotation;
            if (readout != null) defaultColor = readout.color;
        }

        private void OnEnable() => TryBind();

        // Por si la mesa se creo despues (orden de Awake/OnEnable entre objetos).
        private void Start() => TryBind();

        private void OnDisable()
        {
            if (bound != null) bound.ValueChanged -= OnValueChanged;
            bound = null;
            animating = null; // Unity detiene las corrutinas al desactivar.
        }

        private void TryBind()
        {
            LabParameters parameters = LabParameters.Current;
            if (parameters == null || parameters == bound) return;
            if (bound != null) bound.ValueChanged -= OnValueChanged;
            bound = parameters;
            bound.ValueChanged += OnValueChanged;
            Show(bound.Get(variable), false);
        }

        private void OnValueChanged(LabVariableId id, float previous, float current)
        {
            if (id == variable) Show(current, true);
        }

        private void Show(float value, bool animate)
        {
            LabVariableDef def = LabCatalog.Get(variable);
            if (readout != null)
            {
                readout.text = LabParameterMath.Format(value, def);
                readout.color = LabParameterMath.IsDefault(value, def) ? defaultColor : ModifiedColor;
            }

            targetAngle = Mathf.Lerp(MinAngle, MaxAngle, LabParameterMath.Normalized(value, def));
            if (lever == null) return;
            if (!animate || !isActiveAndEnabled)
            {
                angle = targetAngle;
                ApplyAngle();
                return;
            }
            if (animating == null) animating = StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            while (Mathf.Abs(angle - targetAngle) > ArriveDegrees)
            {
                angle = Mathf.Lerp(angle, targetAngle, 1f - Mathf.Exp(-Sharpness * Time.deltaTime));
                ApplyAngle();
                yield return null;
            }
            angle = targetAngle;
            ApplyAngle();
            animating = null;
        }

        private void ApplyAngle() => lever.localRotation = leverBase * Quaternion.Euler(0f, 0f, angle);
    }
}
