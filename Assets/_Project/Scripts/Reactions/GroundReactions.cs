using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Output;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Busca donde los hechizos tocan el suelo (GroundSurface) y deja la marca de su elemento. Prueba unos rayos desde
    /// el sello (direccion del chorro, normal del sello y hacia abajo) y marca donde la exposicion es fuerte, con un
    /// enfriamiento por hechizo y distancia minima entre marcas para no llenar el piso.
    /// </summary>
    public class GroundReactions : MonoBehaviour
    {
        [SerializeField] private Material markMaterial;
        [SerializeField] private Material rockMaterial;
        [SerializeField, Tooltip("Material de las rocas que emergen (piedra).")]
        private MaterialProfile rockProfile;
        [SerializeField] private float probeInterval = 0.2f;
        [SerializeField, Tooltip("Exposicion minima en el punto para marcar.")]
        private float minExposure = 0.15f;
        [SerializeField, Tooltip("Segundos entre marcas del mismo hechizo.")]
        private float cooldown = 0.6f;
        [SerializeField, Tooltip("Distancia minima (m) entre marcas del mismo hechizo.")]
        private float minSpacing = 0.8f;

        private readonly Dictionary<MagicField, (float time, Vector3 point)> last = new Dictionary<MagicField, (float, Vector3)>();
        private readonly List<MagicField> stale = new List<MagicField>();
        private readonly Vector3[] directions = new Vector3[3];
        private float nextProbe;
        private ElementLook earthLook;

        private void Start()
        {
            var output = FindFirstObjectByType<SpellOutputController>();
            if (output != null) earthLook = output.LookFor(Compiler.ElementType.Earth);
        }

        private void Update()
        {
            if (Time.time < nextProbe) return;
            nextProbe = Time.time + probeInterval;

            var fields = MagicField.Active;
            for (int f = 0; f < fields.Count; f++) Probe(fields[f]);

            // Olvidar hechizos que ya terminaron.
            stale.Clear();
            foreach (var key in last.Keys) if (key == null) stale.Add(key);
            foreach (var key in stale) last.Remove(key);
        }

        private void Probe(MagicField field)
        {
            if (last.TryGetValue(field, out var previous) && Time.time - previous.time < cooldown) return;

            var frame = field.Frame;
            directions[0] = field.MainDirection;
            directions[1] = frame.Normal;
            directions[2] = Vector3.down;

            for (int i = 0; i < directions.Length; i++)
            {
                if (!Physics.Raycast(frame.Origin, directions[i], out RaycastHit hit, field.ReachRadius, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<GroundSurface>() == null) continue;
                float exposure = field.ExposureAt(hit.point + hit.normal * 0.1f);
                if (exposure < minExposure) continue;
                if (previous.time > 0f && Vector3.Distance(previous.point, hit.point) < minSpacing) continue;

                float radius = Mathf.Clamp(frame.Scale * (0.4f + 0.6f * exposure), 0.25f, 1.5f);
                if (GroundMarks.Mark(field.Element, hit.point, hit.normal, radius, markMaterial, rockMaterial, rockProfile, earthLook))
                    last[field] = (Time.time, hit.point);
                return;
            }
        }
    }
}
