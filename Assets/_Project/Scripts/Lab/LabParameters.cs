using System;
using UnityEngine;
using SpellSimulator.Net;
using SpellSimulator.Player;
using SpellSimulator.Reactions;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Valores actuales de la mesa de variables del laboratorio (uno por <see cref="LabVariableId"/>). Vive en la mesa
    /// (<see cref="Current"/>). Aplica en vivo las variables globales del fuego (<see cref="GrassFire.SetTuning"/>); las demas las
    /// leen el spawner y las otras piezas al usarlas. Los valores sobreviven al reinicio del mundo (no escucha WorldReset) y no se
    /// guardan entre sesiones. Tambien resalta la placa/boton del laboratorio que apunta el jugador (sondeo sin allocations).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LabParameters : MonoBehaviour
    {
        /// <summary>Pieza del laboratorio que se resalta mientras se la apunta (placas y botones).</summary>
        public interface IHoverTarget
        {
            void SetHovered(bool on);
        }

        /// <summary>Cada cuanto se vuelve a buscar el interactor del jugador si todavia no existe (s).</summary>
        private const float InteractorRetrySeconds = 1f;

        /// <summary>Mesa activa (null si no hay laboratorio en la escena).</summary>
        public static LabParameters Current { get; private set; }

        /// <summary>Cambio de una variable: (id, valor anterior, valor nuevo). Solo se emite si el valor cambio.</summary>
        public event Action<LabVariableId, float, float> ValueChanged;

        private readonly float[] values = new float[LabCatalog.Count];
        private PlayerInteractor interactor;
        private float nextInteractorSearch;
        private GameObject lastHoveredObject;
        private IHoverTarget hovered;

        /// <summary>True si el debilitamiento o las generaciones maximas del fuego difieren del default.</summary>
        public bool FireModified =>
            !LabParameterMath.IsDefault(values[(int)LabVariableId.FireDecay], LabCatalog.Get(LabVariableId.FireDecay)) ||
            !LabParameterMath.IsDefault(values[(int)LabVariableId.FireMaxGenerations], LabCatalog.Get(LabVariableId.FireMaxGenerations));

        private void Awake()
        {
            for (int i = 0; i < values.Length; i++) values[i] = LabCatalog.Defs[i].Default;
            Current = this;
        }

        private void Start()
        {
            // Si el fuego se habia tocado antes de que existiera la grilla, se aplica ahora.
            if (FireModified) ApplyFire();
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // ---------------------------------------------------------------- API

        /// <summary>Valor actual de una variable.</summary>
        public float Get(LabVariableId id) => values[(int)id];

        /// <summary>
        /// Fija una variable: se limita al rango y se ajusta a la grilla (el default, aunque quede fuera de la grilla como 0.78,
        /// se respeta tal cual). Emite <see cref="ValueChanged"/> solo si cambia.
        /// </summary>
        public void Set(LabVariableId id, float value)
        {
            LabVariableDef def = LabCatalog.Get(id);
            float next = LabParameterMath.IsDefault(value, def)
                ? def.Default
                : LabParameterMath.Snap(LabParameterMath.Clamp(value, def), def);
            Assign(id, next, true);
        }

        /// <summary>Un paso de la perilla (solo importa el signo de <paramref name="dir"/>).</summary>
        public void Step(LabVariableId id, int dir)
        {
            LabVariableDef def = LabCatalog.Get(id);
            Assign(id, LabParameterMath.Step(values[(int)id], def, dir), true);
        }

        /// <summary>Boton «valores por defecto»: todo vuelve al catalogo (un evento por variable que cambie) y el fuego a sus defaults.</summary>
        public void ResetAll()
        {
            for (int i = 0; i < values.Length; i++) Assign((LabVariableId)i, LabCatalog.Defs[i].Default, false);
            // Siempre, aunque los valores ya fueran los defaults: la grilla pudo cambiar por otro lado.
            ApplyFire();
        }

        // ---------------------------------------------------------------- interno

        private void Assign(LabVariableId id, float next, bool applyFire)
        {
            int i = (int)id;
            float previous = values[i];
            if (Mathf.Abs(previous - next) <= LabParameterMath.Epsilon * Mathf.Max(1f, Mathf.Abs(next))) return;
            values[i] = next;
            if (applyFire && (id == LabVariableId.FireDecay || id == LabVariableId.FireMaxGenerations)) ApplyFire();
            ValueChanged?.Invoke(id, previous, next);
        }

        /// <summary>
        /// Pasa decay y maxGen a la grilla del pasto conservando su spreadChance actual. En red no se toca (el cliente no simula
        /// el fuego y nada se replica, LABORATORIO_PLAN riesgo 11); sin grilla no hace nada.
        /// </summary>
        private void ApplyFire()
        {
            if (NetSession.Exists) return;
            GrassFire grass = GrassFire.Current;
            if (grass == null) return;
            grass.SetTuning(grass.SpreadChance, values[(int)LabVariableId.FireDecay],
                Mathf.RoundToInt(values[(int)LabVariableId.FireMaxGenerations]));
        }

        // ---------------------------------------------------------------- resaltado al apuntar

        private void Update()
        {
            if (interactor == null)
            {
                if (Time.unscaledTime < nextInteractorSearch) return;
                nextInteractorSearch = Time.unscaledTime + InteractorRetrySeconds;
                interactor = FindAnyObjectByType<PlayerInteractor>();
                if (interactor == null) return;
            }

            GameObject go = interactor.HoveredObject;
            if (go == lastHoveredObject) return;
            lastHoveredObject = go;
            if (hovered is UnityEngine.Object old && old != null) hovered.SetHovered(false);
            hovered = null;
            if (go != null && go.TryGetComponent(out IHoverTarget target))
            {
                hovered = target;
                hovered.SetHovered(true);
            }
        }
    }
}
