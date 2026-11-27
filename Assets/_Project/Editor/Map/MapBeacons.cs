using UnityEditor;
using UnityEngine;
using SpellSimulator.Learning;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// «El mundo responde» (paso 11, D27): farolito al costado de cada atril de libro y estandarte de color en cada campo de
    /// elemento. Los dos llevan un <see cref="ReadBeacon"/>: la parte clara (color HDR, la levanta el bloom; sin luz real por
    /// Quest) se prende al leer el libro. Nada se bloquea.
    /// </summary>
    public static class MapBeacons
    {
        private static readonly Color Iron = new Color(0.2f, 0.2f, 0.23f);
        private static readonly Color GlassOff = new Color(0.28f, 0.26f, 0.24f);
        private static readonly Color LampLit = new Color(3.8f, 2.6f, 1.1f);
        private static readonly Color Mast = new Color(0.33f, 0.21f, 0.13f);

        private const float LanternSide = 0.42f, LanternHeight = 1.55f;

        /// <summary>
        /// Campos de elemento: sitio, centro y radio del campo (ValleyShape.Paths), punta del camino que llega, libro de hechizos
        /// y color (WorldZones.json). El estandarte va en el borde del campo, del lado por donde se llega.
        /// </summary>
        private static readonly (string site, Vector2 center, float radius, Vector2 pathEnd, string book, string html)[] Fields =
        {
            ("WaterLab", Signposts.WaterFieldCenter, 5f, new Vector2(-21.5f, -38.5f), "spells-water", "#3A9BB5"),
            ("FireField", ValleyShape.FireFieldCenter, 8f, new Vector2(-12f, -55.5f), "spells-fire", "#C8432B"),
            ("EarthWorkshop", Signposts.EarthFieldCenter, 6f, new Vector2(10.8f, -48.1f), "spells-earth", "#9C6B3C"),
            ("WindField", ValleyShape.WindFieldCenter, 8f, new Vector2(18.7f, -37.4f), "spells-wind", "#6FB7D1"),
        };

        /// <summary>Farolito sobre un poste al costado derecho del atril (marco local del atril, frente = −Z).</summary>
        public static void BookLantern(Transform parent, Material material, string bookId, Transform lectern)
        {
            Vector3 at = lectern.TransformPoint(new Vector3(LanternSide, 0f, 0f));
            var body = new LowPolyMesh();
            body.Box(Matrix4x4.Translate(new Vector3(0f, LanternHeight * 0.5f, 0f)), new Vector3(0.045f, LanternHeight, 0.045f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0.02f, 0f)), new Vector3(0.16f, 0.04f, 0.16f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, LanternHeight + 0.02f, 0f)), new Vector3(0.15f, 0.025f, 0.15f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, LanternHeight + 0.13f, 0f)), new Vector3(0.11f, 0.19f, 0.11f), GlassOff);
            body.Box(Matrix4x4.Translate(new Vector3(0f, LanternHeight + 0.25f, 0f)), new Vector3(0.15f, 0.03f, 0.15f), Iron);
            var go = MapBuilder.Place("BookLantern_" + bookId, parent, MapBuilder.SaveMesh(body.Build("BookLantern")), material, at, lectern.rotation);

            var glow = new LowPolyMesh();
            glow.Box(Matrix4x4.identity, new Vector3(0.125f, 0.2f, 0.125f), LampLit);
            var lit = MapBuilder.Place("Glow", go.transform, MapBuilder.SaveMesh(glow.Build("BookLantern_Glow")), material,
                go.transform.TransformPoint(new Vector3(0f, LanternHeight + 0.13f, 0f)), go.transform.rotation);
            lit.SetActive(false);
            go.AddComponent<ReadBeacon>().Configure(bookId, lit);
            Undo.RegisterCreatedObjectUndo(go, go.name);
        }

        /// <summary>Un estandarte por campo, al costado de la punta del camino que llega (fuera del camino y de lo construido).</summary>
        public static void FieldBanners(Transform map, Material material)
        {
            Physics.SyncTransforms();
            var root = new GameObject("FieldBanners").transform;
            root.SetParent(map, false);
            foreach (var f in Fields)
            {
                if (!TryFindSpot(f.center, f.radius, f.pathEnd, out Vector3 at))
                {
                    Debug.LogWarning($"[Mapa] sin lugar libre para el estandarte de {f.site}.");
                    continue;
                }
                ColorUtility.TryParseHtmlString(f.html, out Color cloth);
                Banner(root, material, f.site, f.book, at, f.pathEnd, cloth);
            }
        }

        /// <summary>
        /// Prueba lugares en el borde del campo (radio − 1,5 m en adelante) mirando hacia la punta del camino, corriéndose de a poco al
        /// costado y hacia afuera, y se queda con el primero libre de caminos y de lo construido.
        /// </summary>
        private static bool TryFindSpot(Vector2 center, float radius, Vector2 pathEnd, out Vector3 at)
        {
            Vector2 dir = (pathEnd - center).normalized, side = new Vector2(-dir.y, dir.x);
            foreach (float r in new[] { -1.5f, 0f, 1.5f, 2.5f })
                foreach (float s in new[] { 0f, 1.6f, -1.6f, 3.2f, -3.2f })
                {
                    Vector2 p = center + dir * (radius + r) + side * s;
                    if (ValleyShape.PathDistance(p.x, p.y) < Signposts.PathClearance) continue;
                    Vector3 g = MapBuilder.Ground(p.x, p.y);
                    bool blocked = false;
                    foreach (Collider c in Physics.OverlapCapsule(g + Vector3.up * 0.4f, g + Vector3.up * 3f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                        if (!(c is TerrainCollider) && c.name != "Terrain") { blocked = true; break; }
                    if (blocked) continue;
                    at = g;
                    return true;
                }
            at = default;
            return false;
        }

        private static void Banner(Transform root, Material material, string site, string bookId, Vector3 at, Vector2 facePoint, Color cloth)
        {
            Vector3 face = new Vector3(facePoint.x, at.y, facePoint.y) - at;
            Quaternion rot = Quaternion.LookRotation(face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.forward);
            Color dim = cloth * 0.55f;
            dim.a = 1f;
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.7f, 0f)), new Vector3(0.1f, 3.4f, 0.1f), Mast);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 3.2f, 0f)), new Vector3(1.0f, 0.06f, 0.06f), Mast);
            b.Frustum(Matrix4x4.Translate(new Vector3(0f, 3.4f, 0f)), 0.08f, 0.01f, 0f, 0.14f, 4, Mast);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 2.55f, 0.06f)), new Vector3(0.84f, 1.2f, 0.025f), dim);
            // Punta triangular de la tela.
            b.Tri(new Vector3(-0.42f, 1.95f, 0.06f), new Vector3(0.42f, 1.95f, 0.06f), new Vector3(0f, 1.7f, 0.06f), dim);
            b.Tri(new Vector3(0.42f, 1.95f, 0.06f), new Vector3(-0.42f, 1.95f, 0.06f), new Vector3(0f, 1.7f, 0.06f), dim);
            var go = MapBuilder.Place("FieldBanner_" + site, root, MapBuilder.SaveMesh(b.Build("FieldBanner_" + site)), material, at, rot);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.7f, 0f);
            col.size = new Vector3(0.14f, 3.4f, 0.14f);

            // Encendido: tela en color pleno + emblema claro, a cada lado de la tela apagada (se ve de las dos caras).
            Color bright = cloth * 1.6f;
            bright.a = 1f;
            var g = new LowPolyMesh();
            foreach (float z in new[] { 0.09f, 0.03f })
            {
                g.Box(Matrix4x4.Translate(new Vector3(0f, 2.55f, z)), new Vector3(0.86f, 1.22f, 0.006f), bright);
                g.Box(Matrix4x4.TRS(new Vector3(0f, 2.6f, z + (z > 0.06f ? 0.006f : -0.006f)), Quaternion.Euler(0f, 0f, 45f), Vector3.one), new Vector3(0.28f, 0.28f, 0.006f), LampLit);
            }
            var lit = MapBuilder.Place("Glow", go.transform, MapBuilder.SaveMesh(g.Build("FieldBanner_" + site + "_Glow")), material, at, rot);
            lit.SetActive(false);
            go.AddComponent<ReadBeacon>().Configure(bookId, lit);
            Undo.RegisterCreatedObjectUndo(go, go.name);
        }
    }
}
