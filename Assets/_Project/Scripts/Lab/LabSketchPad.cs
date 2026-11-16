using System;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Input.Tools;
using SpellSimulator.Interfaces;
using SpellSimulator.Net;
using SpellSimulator.Player;

namespace SpellSimulator.Lab
{
    /// <summary>Pieza del laboratorio con una ayuda propia para la tira de ayuda (LabHelpStrip): titulo y explicacion en dos renglones.</summary>
    public interface ILabHelp
    {
        /// <summary>Texto ya armado (LabHelpStrip.Line), cacheado por la pieza.</summary>
        string HelpLine { get; }
    }

    /// <summary>
    /// Atril de dibujo del banco (Laboratorio v4, punto 8): un <see cref="DrawingCanvas"/> real donde el jugador dibuja a mano
    /// el hechizo con el rayo (clic o gatillo sostenido), con el trazo del juego (<see cref="PenTool"/>: estabilizador y
    /// Session.AddStroke). Es un <see cref="IManipulable"/>: mientras se dibuja, el clic es del atril y la herramienta en mano
    /// no se usa, asi que no gasta tinta. Nunca lanza solo: es un <see cref="ISealGate"/> bloqueado (SpellCaster no lo
    /// auto-lanza) y no llama a OnStrokeFinished; lo lanza LANZAR copiando los trazos al papel del campo
    /// (<see cref="LabSealBench.Launch"/>). No se agarra (sin PhysicalCanvas), el collider va en un hijo sin DrawingCanvas
    /// (la lapicera no lo ve) y en red no dibuja.
    /// PC: la vista queda congelada al manipular, asi que el cursor se mueve con el mouse sobre el plano del atril.
    /// VR: el rayo de la mano corta el plano del atril.
    /// </summary>
    public class LabSketchPad : MonoBehaviour, IManipulable, ISealGate, LabParameters.IHoverTarget, ILabHelp
    {
        /// <summary>Metros que recorre el cursor por pixel de mouse (PC): el atril de 30 cm son ~330 px.</summary>
        private const float PcMetersPerPixel = 0.0009f;
        /// <summary>Ancho del trazo (m) y velocidad del estabilizador (como la lapicera).</summary>
        private const float StrokeWidth = 0.01f, Stabilizer = 14f;

        [SerializeField, Tooltip("Lienzo del atril (DrawingCanvas, sin PhysicalCanvas).")]
        private DrawingCanvas canvas;
        [SerializeField, Tooltip("Renderer que se resalta al apuntar (marco del atril).")]
        private Renderer highlight;

        /// <summary>El dibujo cambio (trazo terminado o borrado).</summary>
        public event Action Changed;

        private PenTool pen;
        private LabControl.HoverTint tint;
        private bool active, started;
        private Vector3 cursorWorld;
        private string helpLine;

        /// <summary>Lienzo del atril.</summary>
        public DrawingCanvas Canvas => canvas;
        /// <summary>True si hay al menos un trazo.</summary>
        public bool HasDrawing => canvas != null && !canvas.Session.IsEmpty;
        /// <summary>Siempre bloqueado: el atril nunca se lanza solo.</summary>
        public bool SealBlocked => true;

        /// <summary>Pista junto a la mira (IManipulable).</summary>
        public string HelpText => "Atril de dibujo | Clic sostenido: dibujar | LANZAR: lanzar";

        public string HelpLine => helpLine ??= LabHelpStrip.Line("Atril de dibujo",
            "Dibujá con clic sostenido. LANZAR lo copia al papel. Vacío: usa los controles.");

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(DrawingCanvas canvas, Renderer highlight)
        {
            this.canvas = canvas;
            this.highlight = highlight;
        }

        private void Awake()
        {
            if (canvas == null) canvas = GetComponentInChildren<DrawingCanvas>(true);
            pen = new PenTool(Stabilizer, StrokeWidth);
            tint.Init(highlight);
        }

        private void OnDisable()
        {
            if (active) EndManipulation();
            tint.Set(false);
            LabHelpStrip.Unfocus(this);
        }

        /// <summary>Borra el dibujo (boton «Borrar dibujo»).</summary>
        public void Clear()
        {
            if (canvas == null || canvas.Session.IsEmpty) return;
            canvas.Clear();
            Changed?.Invoke();
        }

        public void SetHovered(bool on)
        {
            tint.Set(on);
            if (on) LabHelpStrip.Focus(this);
            else if (!active) LabHelpStrip.Unfocus(this);
        }

        // ---------------------------------------------------------------- IManipulable

        public void BeginManipulation(RaycastHit hit)
        {
            if (canvas == null) return;
            if (NetSession.Exists)
            {
                Hud.Notify("lab", "Laboratorio: solo en modo Libre", 2.5f);
                return;
            }
            active = true;
            started = false;
            cursorWorld = hit.point;
            LabHelpStrip.Focus(this);
        }

        public void UpdateManipulation(in ManipulationInput input)
        {
            if (!active) return;
            // La rueda sin captura llega como Begin + Update + End en el mismo frame: no es un trazo.
            if (input.Wheel != 0f) return;

            Vector2 uv;
            if (!started)
            {
                uv = Clamp(canvas.GetUVFromRay(new Ray(input.RayOrigin, input.RayDirection), cursorWorld));
                cursorWorld = canvas.UVToWorld(uv);
                pen.OnPointerDown(uv, canvas);
                started = true;
                return;
            }

            if (input.IsVR) uv = Clamp(canvas.GetUVFromRay(new Ray(input.RayOrigin, input.RayDirection), cursorWorld));
            else
            {
                // Vista congelada: el mouse empuja un cursor sobre el plano (derecha/arriba de la camara).
                Vector3 up = Vector3.Cross(input.RayDirection, input.CameraRight);
                Vector3 delta = (input.CameraRight * input.PointerDelta.x + up * input.PointerDelta.y) * PcMetersPerPixel;
                uv = Clamp(canvas.GetUVFromWorldPosition(cursorWorld + delta));
            }
            cursorWorld = canvas.UVToWorld(uv);
            pen.OnPointerDrag(uv, canvas);
        }

        public void EndManipulation()
        {
            if (!active) return;
            active = false;
            if (started)
            {
                pen.OnPointerUp();
                started = false;
                Changed?.Invoke();
            }
            LabHelpStrip.Unfocus(this);
        }

        private static Vector2 Clamp(Vector2 uv) => new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
    }
}
