using System;
using TMPro;
using UnityEngine;
using SpellSimulator.Audio;
using SpellSimulator.Interfaces;
using SpellSimulator.Net;
using SpellSimulator.Player;

namespace SpellSimulator.Lab
{
    /// <summary>Tipo de control manipulable del laboratorio.</summary>
    public enum LabControlKind { Knob, Slider, Selector }

    /// <summary>
    /// Control manipulable del laboratorio (perilla, deslizador o selector con muescas). No conoce a ningun otro modulo: se
    /// enlaza con <see cref="Setup"/> mediante delegados de lectura y escritura. Lo captura <c>PlayerInteractor</c> con clic o
    /// gatillo (grip tambien en VR) y le pasa un <see cref="ManipulationInput"/> por frame.
    /// - Perilla: PC arrastre horizontal del mouse; VR giro de la muñeca sobre el rayo.
    /// - Deslizador: PC arrastre horizontal del mouse; VR desplazamiento de la mano proyectado sobre el riel.
    /// - Selector: como la perilla pero el valor salta entre muescas.
    /// La rueda sobre el control sube o baja un paso. En cada cruce de muesca: haptica (VR) y clic de audio.
    /// Sin allocations por frame: la lectura solo se reescribe cuando cambia el valor.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LabControl : MonoBehaviour, IManipulable, LabParameters.IHoverTarget
    {
        /// <summary>Lado minimo (m) del collider: el rayo de VR tiembla, el blanco tiene que ser grande.</summary>
        private const float MinColliderSide = 0.09f;
        /// <summary>Recorrido visual de la perilla (grados, eje Z local) a cada lado del centro.</summary>
        private const float KnobHalfSweep = 120f;
        /// <summary>Pixeles de mouse que recorren todo el rango (PC).</summary>
        private const float PcFullRangeDrag = 320f;
        /// <summary>Grados de giro de muñeca que recorren todo el rango (VR).</summary>
        private const float VrFullRangeDegrees = 200f;
        /// <summary>Largo (m) del riel si no se asignaron extremos.</summary>
        private const float FallbackRailLength = 0.2f;
        /// <summary>Pasos de la rueda cuando el control es continuo (sin muescas).</summary>
        private const int WheelSteps = 20;
        /// <summary>Segundos entre lecturas de refresco (10 Hz): el valor puede cambiar por fuera (valores por defecto).</summary>
        private const float PollSeconds = 0.1f;
        /// <summary>Segundos maximos entre dos clics para contar como doble clic (reset).</summary>
        private const float DoubleClickSeconds = 0.3f;
        /// <summary>Sonido de la muesca (cue de la biblioteca de audio; si no existe, no suena).</summary>
        private const string ClickCue = "ui_select";

        private LabControlKind kind;
        private string label = "";
        private Func<float> get;
        private Action<float> set;
        private Func<float, string> format;
        private float min, max = 1f;
        private int detents;
        private Transform movable, railStart, railEnd;
        private TMP_Text readout;
        private Quaternion movableBaseRotation = Quaternion.identity;
        private bool ready;

        private string helpText = "";
        private bool hasReset;
        private float resetValue;
        private float lastClickTime = -10f;

        // Estado de la manipulacion en curso.
        private bool active;
        private float startValue, dragValue;
        private float startRoll, unwrappedRoll;
        private Vector3 startHand;
        private float lastNorm;

        // Estado mostrado (para escribir el texto y mover la pieza solo cuando cambia).
        private float shownValue = float.NaN;
        private float nextPoll;
        private string[] detentLabels;
        private HoverTint tint;

        /// <summary>Texto de la pista junto a la mira (constante cacheada: se compara por referencia).</summary>
        public string HelpText => helpText;

        /// <summary>
        /// Enlaza el control. <paramref name="get"/>/<paramref name="set"/> leen y escriben el valor; <paramref name="format"/> arma
        /// la lectura; <paramref name="detents"/> son las muescas (Selector: al menos 2; Knob/Slider: 0 = continuo);
        /// <paramref name="movable"/> es la pieza que gira (Knob/Selector) o se desliza (Slider) entre <paramref name="railStart"/> y
        /// <paramref name="railEnd"/>; <paramref name="readout"/> la lectura numerica.
        /// </summary>
        public void Setup(LabControlKind kind, string label, Func<float> get, Action<float> set, Func<float, string> format,
            float min, float max, int detents = 0, Transform movable = null, Transform railStart = null, Transform railEnd = null,
            TMP_Text readout = null)
        {
            this.kind = kind;
            this.label = label ?? "";
            this.get = get;
            this.set = set;
            this.format = format;
            this.min = min;
            this.max = max > min ? max : min + 1e-4f;
            this.detents = kind == LabControlKind.Selector ? Mathf.Max(2, detents) : (detents >= 2 ? detents : 0);
            this.movable = movable;
            this.railStart = railStart;
            this.railEnd = railEnd;
            this.readout = readout;
            if (movable != null) movableBaseRotation = movable.localRotation;
            detentLabels = this.detents > 0 ? new string[this.detents] : null;

            string verb = kind == LabControlKind.Slider ? "arrastrar" : kind == LabControlKind.Knob ? "girar" : "cambiar";
            helpText = this.label + " | Clic: " + verb + (PlatformDetector.IsVR ? " (giro de muñeca)" : " (mouse)") + " | Rueda: un paso";
            ready = get != null && set != null;
            shownValue = float.NaN;
            Refresh();
        }

        /// <summary>Activa el reset por doble clic: vuelve al valor dado.</summary>
        public void SetResetValue(float value)
        {
            hasReset = true;
            resetValue = value;
        }

        /// <summary>Relee el valor y actualiza pieza y lectura si cambio.</summary>
        public void Refresh()
        {
            if (!ready) return;
            Show(get());
        }

        private void Awake()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            Vector3 scale = transform.lossyScale;
            Vector3 size = box.size;
            size.x = Mathf.Max(size.x, MinColliderSide / Mathf.Max(1e-4f, Mathf.Abs(scale.x)));
            size.y = Mathf.Max(size.y, MinColliderSide / Mathf.Max(1e-4f, Mathf.Abs(scale.y)));
            size.z = Mathf.Max(size.z, MinColliderSide / Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
            box.size = size;
            tint.Init(GetComponentInChildren<Renderer>());
        }

        private void Update()
        {
            if (!ready || active || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + PollSeconds;
            Show(get());
        }

        private void OnDisable()
        {
            active = false;
            tint.Set(false);
        }

        public void SetHovered(bool on) => tint.Set(on);

        // ---------------------------------------------------------------- IManipulable

        public void BeginManipulation(RaycastHit hit)
        {
            if (!ready) return;
            if (NetSession.Exists)
            {
                Hud.Notify("lab", "Laboratorio: solo en modo Libre", 2.5f);
                return;
            }
            active = true;
            startValue = Mathf.Clamp(get(), min, max);
            dragValue = startValue;
            lastNorm = Normalized(startValue);
            startRoll = float.NaN; // se toma con el primer Update (hace falta el angulo de la muñeca)
            unwrappedRoll = 0f;

            // Doble clic: valor de reset (si se configuro).
            if (hasReset && Time.unscaledTime - lastClickTime <= DoubleClickSeconds)
            {
                lastClickTime = -10f;
                dragValue = resetValue;
                Apply(resetValue);
                return;
            }
            lastClickTime = Time.unscaledTime;
        }

        public void UpdateManipulation(in ManipulationInput input)
        {
            if (!ready || !active) return;

            if (input.Wheel != 0f)
            {
                StepBy(input.Wheel > 0f ? 1 : -1);
                return;
            }

            if (float.IsNaN(startRoll))
            {
                startRoll = input.WristRoll;
                unwrappedRoll = input.WristRoll;
                startHand = input.HandPosition;
            }

            float next = dragValue;
            switch (kind)
            {
                case LabControlKind.Slider:
                    next = SliderValue(in input);
                    break;
                default: // Knob y Selector
                    if (input.IsVR)
                    {
                        unwrappedRoll = LabControlMath.UnwrapAngle(unwrappedRoll, input.WristRoll);
                        next = LabControlMath.DeltaToValue(startValue, unwrappedRoll - startRoll, min, max, VrFullRangeDegrees);
                    }
                    else
                    {
                        // Cada frame suma el delta del mouse al valor continuo (mover a la derecha sube).
                        next = LabControlMath.DeltaToValue(dragValue, input.PointerDelta.x, min, max, PcFullRangeDrag);
                    }
                    break;
            }
            dragValue = next;
            Apply(next);
        }

        public void EndManipulation() => active = false;

        // ---------------------------------------------------------------- calculo

        private float SliderValue(in ManipulationInput input)
        {
            if (input.IsVR)
            {
                GetRail(out Vector3 dir, out float length);
                float along = Vector3.Dot(input.HandPosition - startHand, dir);
                return LabControlMath.DeltaToValue(startValue, along, min, max, length);
            }
            // PC: el mouse horizontal mueve el deslizador en el sentido en que el riel se ve en pantalla.
            GetRail(out Vector3 railDir, out _);
            float sign = Vector3.Dot(railDir, input.CameraRight) >= 0f ? 1f : -1f;
            return LabControlMath.DeltaToValue(dragValue, input.PointerDelta.x * sign, min, max, PcFullRangeDrag);
        }

        private void GetRail(out Vector3 direction, out float length)
        {
            if (railStart != null && railEnd != null)
            {
                Vector3 span = railEnd.position - railStart.position;
                length = span.magnitude;
                if (length > 1e-4f)
                {
                    direction = span / length;
                    return;
                }
            }
            direction = transform.right;
            length = FallbackRailLength;
        }

        private float Normalized(float value) => Mathf.InverseLerp(min, max, value);

        private void StepBy(int dir)
        {
            float v01 = Normalized(dragValue);
            float stepped;
            if (detents > 0)
            {
                int index = LabControlMath.DetentIndex(v01, detents) + dir;
                stepped = Mathf.Clamp01(index / (float)(detents - 1));
            }
            else stepped = Mathf.Clamp01(v01 + dir / (float)WheelSteps);
            dragValue = Mathf.Lerp(min, max, stepped);
            Apply(dragValue);
        }

        /// <summary>Escribe el valor (pegado a la muesca si hay) y avisa los cruces de muesca.</summary>
        private void Apply(float value)
        {
            float v01 = Normalized(Mathf.Clamp(value, min, max));
            if (detents > 0)
            {
                v01 = LabControlMath.SnapToDetent(v01, detents);
                if (LabControlMath.CrossedDetent(lastNorm, v01, detents)) DetentFeedback();
            }
            lastNorm = v01;
            float snapped = Mathf.Lerp(min, max, v01);
            set(snapped);
            Show(get());
        }

        private void DetentFeedback()
        {
            if (PlatformDetector.IsVR) Haptics.Pulse(HandType.Right, 0.35f, 0.03f);
            AudioDirector director = AudioDirector.Instance;
            if (director != null) director.Play(ClickCue, transform.position, 0.5f);
        }

        // ---------------------------------------------------------------- visual

        /// <summary>Mueve la pieza y reescribe la lectura SOLO si el valor mostrado cambio.</summary>
        private void Show(float value)
        {
            if (!float.IsNaN(shownValue) && Mathf.Abs(value - shownValue) <= 1e-5f * Mathf.Max(1f, Mathf.Abs(value))) return;
            shownValue = value;
            float v01 = Normalized(value);

            if (movable != null)
            {
                if (kind == LabControlKind.Slider)
                {
                    if (railStart != null && railEnd != null) movable.position = Vector3.Lerp(railStart.position, railEnd.position, v01);
                }
                else movable.localRotation = movableBaseRotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(-KnobHalfSweep, KnobHalfSweep, v01));
            }

            if (readout == null || format == null) return;
            string text;
            if (detentLabels != null)
            {
                int i = LabControlMath.DetentIndex(v01, detents);
                text = detentLabels[i] ?? (detentLabels[i] = format(Mathf.Lerp(min, max, i / (float)(detents - 1))));
            }
            else text = format(value);
            readout.SetText(text);
        }

        /// <summary>
        /// Resaltado al apuntar compartido por las piezas del laboratorio: aclara el color base del renderer con un
        /// MaterialPropertyBlock (sin instanciar materiales). Solo cambia cuando cambia el estado.
        /// </summary>
        internal struct HoverTint
        {
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int ColorId = Shader.PropertyToID("_Color");
            private static readonly Color HoverGlow = new Color(1f, 0.84f, 0.42f);

            private Renderer renderer;
            private MaterialPropertyBlock block;
            private int propertyId;
            private Color hoverColor;

            public void Init(Renderer target)
            {
                renderer = target;
                if (renderer == null) return;
                Material m = renderer.sharedMaterial;
                Color baseColor = Color.white;
                propertyId = BaseColorId;
                if (m != null)
                {
                    if (m.HasProperty(BaseColorId)) baseColor = m.GetColor(BaseColorId);
                    else if (m.HasProperty(ColorId)) { propertyId = ColorId; baseColor = m.GetColor(ColorId); }
                }
                hoverColor = Color.Lerp(baseColor, HoverGlow, 0.55f);
                block = new MaterialPropertyBlock();
            }

            public void Set(bool on)
            {
                if (renderer == null) return;
                if (!on)
                {
                    renderer.SetPropertyBlock(null);
                    return;
                }
                block.SetColor(propertyId, hoverColor);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
