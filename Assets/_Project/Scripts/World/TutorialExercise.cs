using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Ejercicio OPCIONAL del tutorial (no traba nada): calcar un glifo en un papel (Trace; lo detecta Lab.TutorialTraceStation y llama a Complete), dejar un lienzo flotando donde marca un papel fantasma
    /// (acostado o parado) o acertar un tiro a la diana. Al cumplirse enciende su farolito; el reinicio del tutorial lo apaga.
    /// En los de lienzo solo cuenta el papel de SU pedestal, ya soltado y flotando en el lugar del fantasma (no basta con pasar cerca).
    /// </summary>
    public class TutorialExercise : MonoBehaviour
    {
        public enum Kind { Float, Upright, Hit, Trace }

        [SerializeField] private Kind kind;
        [SerializeField] private GameObject glow;
        [SerializeField, Tooltip("Lienzo del pedestal (Float y Upright).")] private PhysicalCanvas paper;
        [SerializeField, Tooltip("Lado del papel fantasma (m) y giro (grados sobre Y) del fantasma parado.")] private float ghostSize = 0.5f, ghostYaw;

        private static readonly List<TutorialExercise> all = new List<TutorialExercise>(4);
        private static readonly Color GhostFill = new Color(0.98f, 0.94f, 0.82f, 0.3f), GhostEdge = new Color(1f, 0.82f, 0.35f, 0.9f);
        private const float CheckPeriod = 0.1f;

        private bool done;
        private float held, checkTimer;
        private GameObject ghost;
        private Material ghostMaterial;

        public bool Done => done;
        public Kind Mode => kind;

        public void Configure(Kind mode, GameObject light)
        {
            kind = mode;
            glow = light;
        }

        /// <summary>Lo llama el generador del mapa: el lienzo del pedestal y el tamaño / giro del papel fantasma.</summary>
        public void ConfigurePaper(PhysicalCanvas canvas, float size, float yaw)
        {
            paper = canvas;
            ghostSize = size;
            ghostYaw = yaw;
        }

        private void OnEnable() => all.Add(this);

        private void OnDisable() => all.Remove(this);

        private void Start() => BuildGhost();

        private void OnDestroy()
        {
            if (ghostMaterial != null) Destroy(ghostMaterial);
        }

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
            if (ghost != null) ghost.SetActive(true);
        }

        /// <summary>Papel fantasma semitransparente con borde, en el lugar y la postura que se piden (no es un lienzo: no tiene collider).</summary>
        private void BuildGhost()
        {
            if (kind == Kind.Hit || kind == Kind.Trace || ghost != null) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            ghostMaterial = new Material(shader);
            ghost = new GameObject("PaperGhost");
            ghost.transform.SetParent(transform, false);
            ghost.transform.localPosition = Vector3.zero;
            // El quad vive en el plano XY: acostado se gira 90 grados sobre X; parado queda de cara a ghostYaw.
            ghost.transform.rotation = kind == Kind.Upright ? Quaternion.Euler(0f, ghostYaw, 0f) : Quaternion.Euler(90f, 0f, 0f);

            float h = ghostSize * 0.5f;
            var mesh = new Mesh { name = "PaperGhost" };
            mesh.vertices = new[] { new Vector3(-h, -h, 0f), new Vector3(-h, h, 0f), new Vector3(h, h, 0f), new Vector3(h, -h, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.colors = new[] { GhostFill, GhostFill, GhostFill, GhostFill };
            mesh.RecalculateNormals();
            ghost.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = ghost.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ghostMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var edge = ghost.AddComponent<LineRenderer>();
            edge.useWorldSpace = false;
            edge.loop = true;
            edge.positionCount = 4;
            edge.SetPositions(new[] { new Vector3(-h, -h, 0f), new Vector3(-h, h, 0f), new Vector3(h, h, 0f), new Vector3(h, -h, 0f) });
            edge.widthMultiplier = 0.012f;
            edge.sharedMaterial = ghostMaterial;
            edge.startColor = edge.endColor = GhostEdge;
            edge.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            if (done || kind == Kind.Hit || kind == Kind.Trace || paper == null) return;
            checkTimer += Time.deltaTime;
            if (checkTimer < CheckPeriod) return;
            float dt = checkTimer;
            checkTimer = 0f;
            // Soltado y flotando (no en la mano ni apoyado en el pedestal) en el lugar del fantasma y con la postura pedida.
            bool ok = paper.IsFloating && !paper.IsHeld
                && TutorialExerciseRules.PaperAtSpot(paper.WorldCenter, paper.transform.forward, transform.position, kind == Kind.Upright);
            if (!ok) { held = 0f; return; }
            held += dt;
            if (held >= TutorialExerciseRules.HoldSeconds) Complete();
        }

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
            if (ghost != null) ghost.SetActive(false);
        }
    }
}
