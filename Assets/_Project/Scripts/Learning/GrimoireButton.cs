using System;
using TMPro;
using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Boton del grimorio: una placa de cuero con marco dorado y su rotulo. Se aprieta apuntando y haciendo clic
    /// (<see cref="PressWithClick"/>). El grimorio lo toma de un pool fijo y lo reubica al cambiar de pantalla
    /// (<see cref="Layout"/>); nunca se crea ni se destruye durante el juego.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class GrimoireButton : MonoBehaviour, IPressable
    {
        /// <summary>Grosor de la placa y ancho del marco dorado (m).</summary>
        private const float Depth = 0.005f, Border = 0.0026f;

        private static readonly Color LabelColor = new Color(0.97f, 0.91f, 0.76f);
        private static readonly Color LabelHover = new Color(1f, 0.84f, 0.42f);

        private string prompt;
        private Action action;
        private BoxCollider box;
        private Transform frame, plate;
        private Renderer plateRenderer;
        private TextMeshPro label;
        private Material normal, hover;

        public bool Hovered { get; private set; }

        /// <summary>Crea marco, placa y rotulo (una sola vez, al construir el libro).</summary>
        public void Build(Material gold, Material leather, Material leatherHover)
        {
            normal = leather;
            hover = leatherHover != null ? leatherHover : leather;
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            frame = Piece("Marco", gold);
            plate = Piece("Placa", leather);
            plateRenderer = plate.GetComponent<Renderer>();
            label = GrimoireTexts.MakeText("Rotulo", transform, new Vector2(0.1f, 0.03f), 0.08f, TextAlignmentOptions.Center, LabelColor);
            label.characterSpacing = 2f;
            label.overflowMode = TextOverflowModes.Overflow;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private Transform Piece(string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Tamano (m), rotulo y accion. Solo al cambiar de pantalla.</summary>
        public void Layout(float width, float height, string text, float fontSize, Action onPress)
        {
            prompt = text;
            action = onPress;
            box.size = new Vector3(width, height, Depth + 0.002f);
            box.center = new Vector3(0f, 0f, -Depth * 0.5f);
            frame.localScale = new Vector3(width, height, Depth * 0.6f);
            frame.localPosition = new Vector3(0f, 0f, -Depth * 0.3f);
            plate.localScale = new Vector3(width - 2f * Border, height - 2f * Border, Depth);
            plate.localPosition = new Vector3(0f, 0f, -Depth * 0.5f - 0.0006f);
            label.rectTransform.sizeDelta = new Vector2(width - 2f * Border - 0.006f, height);
            label.transform.localPosition = new Vector3(0f, -height * 0.04f, -Depth - 0.0014f);
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = fontSize * 0.55f;
            label.text = text;
            SetHovered(false);
        }

        /// <summary>Resalta la placa mientras se la apunta (cambia solo cuando cambia el estado).</summary>
        public void SetHovered(bool on)
        {
            Hovered = on;
            if (plateRenderer != null) plateRenderer.sharedMaterial = on ? hover : normal;
            if (label != null) label.color = on ? LabelHover : LabelColor;
        }

        public bool PressWithClick => true;

        public string GetPressPrompt() => prompt ?? "";

        public void Press() => action?.Invoke();
    }
}
