using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;
using SpellSimulator.Interfaces;
using SpellSimulator.World;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Hechizo de CRISTAL (tierra, sigilo "crystal"): una esfera centrada en el sello cristaliza todo lo que toca
    /// (cuerpos rigidos, indestructibles, tenidos de hielo), levanta una placa lisa de cristal sobre el piso
    /// y congela el agua (reaccion agua + cristal = hielo, via <see cref="IFreezable"/>). Todo el trabajo es una sola vez al lanzar.
    /// </summary>
    public static class CrystalSpell
    {
        private const int MaxHits = 128;
        private const float RingSeconds = 0.5f;
        private const float PlateThickness = 0.12f;
        private const float PlateRadiusFactor = 0.6f;

        private static readonly Collider[] hits = new Collider[MaxHits];
        private static readonly RaycastHit[] rays = new RaycastHit[16];
        private static Mesh hexMesh;
        private static Shader flatShader;

        public static void Spawn(SpellIR spell, SealFrame frame, ElementLook look, DrawingCanvas source, GameObject spellGo)
        {
            float radius = CrystalMath.Radius(frame.Radius, spell.inkPower);
            float seconds = CrystalMath.Seconds(radius);
            Vector3 center = frame.Origin;

            ShowRange(center, radius);

            // Cuerpos y aguas dentro de la esfera (incluye triggers para encontrar el agua).
            int count = Physics.OverlapSphereNonAlloc(center, radius, hits, ~0, QueryTriggerInteraction.Collide);
            Rigidbody sourceBody = source != null ? source.GetComponentInParent<Rigidbody>() : null;

            int before = Crystallized.ActiveCount;
            int bodies = 0, waters = 0;
            for (int i = 0; i < count; i++)
            {
                var col = hits[i];
                if (col == null) continue;

                var freezable = col.GetComponentInParent<IFreezable>();
                if (freezable != null && freezable.Freeze(center, radius, seconds)) waters++;

                if (col.isTrigger) continue;
                var rb = col.attachedRigidbody;
                if (rb == null || rb == sourceBody) continue;
                if (spellGo != null && rb.transform.IsChildOf(spellGo.transform)) continue;
                if (rb.isKinematic && rb.GetComponent<Crystallized>() == null) continue; // estatico, en mano o animado
                if (Player.PlayerHeld.IsAttachedToAnyPlayer(rb.transform)) continue; // jugadores y lo que llevan
                if (rb.GetComponentInParent<CharacterController>() != null) continue;
                Crystallized.Apply(rb, seconds, look);
            }
            bodies = Mathf.Max(0, Crystallized.ActiveCount - before);

            SpawnPlate(center, radius, seconds);
            Debug.Log($"[Cristal] radio {radius:0.0} m, {bodies} cuerpos cristalizados, {waters} aguas congeladas");
        }

        private static Material NewMaterial(Color color)
        {
            if (flatShader == null) flatShader = Shader.Find("Sprites/Default");
            return new Material(flatShader) { name = "Cristal (runtime)", color = color, hideFlags = HideFlags.DontSave };
        }

        /// <summary>Esfera translucida celeste que muestra el alcance y se desvanece en medio segundo.</summary>
        private static void ShowRange(Vector3 center, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CristalAlcance";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * radius * 2f;
            var mat = NewMaterial(new Color(0.65f, 0.9f, 1f, 0.35f));
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<CrystalTimedVisual>().Setup(CrystalTimedVisual.Mode.FadeOut, RingSeconds, mat);
        }

        /// <summary>Placa hexagonal lisa sobre el piso estatico mas cercano bajo el sello.</summary>
        private static void SpawnPlate(Vector3 center, float radius, float seconds)
        {
            int n = Physics.RaycastNonAlloc(center + Vector3.up * 0.05f, Vector3.down, rays, radius, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            RaycastHit best = default;
            for (int i = 0; i < n; i++)
            {
                var h = rays[i];
                if (h.collider.attachedRigidbody != null) continue;
                if (h.collider.GetComponentInParent<CrystalTimedVisual>() != null) continue;
                if (!found || h.distance < best.distance) { best = h; found = true; }
            }
            if (!found) return;

            Vector3 normal = best.normal.y > 0.5f ? best.normal : Vector3.up;
            float plateRadius = radius * PlateRadiusFactor;
            var go = new GameObject("CristalPlaca");
            go.transform.SetPositionAndRotation(best.point + normal * (PlateThickness * 0.5f), Quaternion.FromToRotation(Vector3.up, normal));
            go.transform.localScale = new Vector3(plateRadius, PlateThickness, plateRadius);
            var mesh = HexMesh();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = NewMaterial(new Color(0.65f, 0.9f, 1f, 0.9f));
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
            go.AddComponent<CrystalTimedVisual>().Setup(CrystalTimedVisual.Mode.ShrinkAtEnd, seconds, mat);
        }

        /// <summary>Prisma hexagonal de radio 1 y altura 1 centrado en el origen (se escala a la placa).</summary>
        private static Mesh HexMesh()
        {
            if (hexMesh != null) return hexMesh;
            const int Sides = 6;
            var verts = new Vector3[Sides * 2 + 2];
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                verts[i] = new Vector3(x, 0.5f, z);
                verts[Sides + i] = new Vector3(x, -0.5f, z);
            }
            verts[Sides * 2] = new Vector3(0f, 0.5f, 0f);
            verts[Sides * 2 + 1] = new Vector3(0f, -0.5f, 0f);
            var tris = new int[Sides * 12];
            int t = 0;
            for (int i = 0; i < Sides; i++)
            {
                int j = (i + 1) % Sides;
                // Tapa superior (hacia +Y) e inferior (hacia -Y), y el lado.
                tris[t++] = Sides * 2; tris[t++] = j; tris[t++] = i;
                tris[t++] = Sides * 2 + 1; tris[t++] = Sides + i; tris[t++] = Sides + j;
                tris[t++] = i; tris[t++] = j; tris[t++] = Sides + i;
                tris[t++] = j; tris[t++] = Sides + j; tris[t++] = Sides + i;
            }
            hexMesh = new Mesh { name = "CristalHexagono", hideFlags = HideFlags.DontSave };
            hexMesh.vertices = verts;
            hexMesh.triangles = tris;
            hexMesh.RecalculateNormals();
            hexMesh.RecalculateBounds();
            return hexMesh;
        }
    }
}
