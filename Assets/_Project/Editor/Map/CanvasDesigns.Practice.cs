using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ValleyShape = SpellSimulator.World.Map.ValleyShape;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Zonas de practica (E4): lugares por la pradera con un lienzo de pie (caballetes, soporte con marco o mesa de dibujo),
    /// un cartel breve y props movibles. Varias en la orilla del lago y otras junto a los senderos entre la torre, el lago y
    /// el campo. Todo apoya en el piso: los lienzos son fijos o kinematicos hasta que se los agarra (nada flotando).
    /// </summary>
    public static partial class CanvasDesigns
    {
        // Angulos (grados desde el centro del lago, 0 = +X) de las zonas de la orilla y su cartel.
        private static readonly (float angle, string title, string hint)[] LakeSpots =
        {
            (40f, "Orilla este", "Practica agua y hielo\nfrente al lago"),
            (85f, "Muelle norte", "Dibuja un sello y\nmira como mueve el agua"),
            (140f, "Orilla oeste", "Zona de practica:\nviento sobre el agua"),
            (200f, "Playa del lago", "Prueba el rayo\ncerca del agua"),
            (270f, "Orilla sur", "Zona de practica\ntranquila"),
            (325f, "Piedras del lago", "Fuego y vapor:\ncuidado con el pasto"),
        };

        // Zonas junto a senderos: (desde, hasta, t sobre la recta, texto).
        private static readonly (int route, float t, string title, string hint)[] PathSpots =
        {
            (0, 0.45f, "Camino al lago", "Practica en el camino:\ndibuja y lanza"),
            (0, 0.75f, "Mirador", "Un lienzo para probar\nglifos nuevos"),
            (1, 0.4f, "Camino al campo", "Zona de practica\njunto al sendero"),
            (1, 0.8f, "Entrada del campo", "Aqui empieza el campo\nde practica"),
        };

        private const float ZoneClearRadius = 3.4f;

        /// <summary>Desnivel maximo (m) a 2,5 m del centro de una zona.</summary>
        private const float MaxHeightDelta = 0.28f;

        private static readonly Vector2[] FlatProbes =
        {
            new Vector2(2.5f, 0f), new Vector2(-2.5f, 0f), new Vector2(0f, 2.5f), new Vector2(0f, -2.5f),
            new Vector2(1.8f, 1.8f), new Vector2(-1.8f, 1.8f), new Vector2(1.8f, -1.8f), new Vector2(-1.8f, -1.8f)
        };

        /// <summary>Zonas donde NO se arma una zona de practica (el campo de practica, la colina de la torre).</summary>
        private static (Vector2 center, float radius)[] KeepOut;

        private static void PracticeZones(Transform map, GameObject template, Material material, Vector3 towerOrigin, Vector3 fieldOrigin)
        {
            var root = MovablesSetup.Root("Practice");
            var trees = GameObject.Find("Trees");
            var taken = new List<Vector3>();
            int index = 0;

            Vector2 lake = ValleyShape.LakeCenter, tower = ValleyShape.TowerCenter, field = new Vector2(fieldOrigin.x, fieldOrigin.z);
            KeepOut = new[] { (field, 16f), (tower, 15f), (ValleyShape.Spawn, 6f) };

            // Orilla del lago: se prueba cada angulo alejandose del agua hasta encontrar piso firme y libre de arboles.
            foreach (var spot in LakeSpots)
            {
                bool found = false;
                foreach (float da in new[] { 0f, 7f, -7f, 14f, -14f })
                {
                    float a = (spot.angle + da) * Mathf.Deg2Rad;
                    for (float k = 1.3f; k <= 2.2f && !found; k += 0.1f)
                    {
                        Vector2 p = lake + new Vector2(Mathf.Cos(a) * ValleyShape.LakeRadii.x * k, Mathf.Sin(a) * ValleyShape.LakeRadii.y * k);
                        if (!IsFirm(p, taken, trees)) continue;
                        Vector2 v = (p - lake).normalized;
                        BuildZone(root, template, material, $"Lake{index}", p, v, index, spot.title, spot.hint);
                        taken.Add(new Vector3(p.x, 0f, p.y));
                        found = true;
                    }
                    if (found) break;
                }
                index++;
            }

            // Senderos: a un lado de la recta torre-lago y torre-campo, mirando hacia el sendero.
            foreach (var spot in PathSpots)
            {
                Vector2 to = spot.route == 0 ? lake : field;
                Vector2 point = Vector2.Lerp(tower, to, spot.t);
                Vector2 dir = (to - tower).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x);
                bool found = false;
                foreach (float side in new[] { 1f, -1f })
                {
                    for (float d = 3.5f; d <= 7f && !found; d += 1.5f)
                    {
                        Vector2 p = point + perp * side * d;
                        if (Vector2.Distance(p, tower) < 10f || Vector2.Distance(p, new Vector2(fieldOrigin.x, fieldOrigin.z)) < 5f) continue;
                        if (ValleyShape.LakeFactor(p.x, p.y) < 1.3f || !IsFirm(p, taken, trees)) continue;
                        BuildZone(root, template, material, $"Path{index}", p, -perp * side, index, spot.title, spot.hint);
                        taken.Add(new Vector3(p.x, 0f, p.y));
                        found = true;
                    }
                    if (found) break;
                }
                index++;
            }

            MovablesSetup.BuildFieldProps(map, material, fieldOrigin);
            Debug.Log($"[Practica] zonas creadas: {root.childCount} hijos en {root.name}");
        }

        /// <summary>Piso apto: sobre el agua, poco desnivel, sin arboles ni otra zona cerca.</summary>
        private static bool IsFirm(Vector2 p, List<Vector3> taken, GameObject trees)
        {
            float h = ValleyShape.Height(p.x, p.y);
            if (h < ValleyShape.WaterLevel + 0.5f) return false;
            // Piso casi plano en 2,5 m a la redonda: en una pendiente los objetos con fisica se deslizan y se caen.
            foreach (var o in FlatProbes)
            {
                float ho = ValleyShape.Height(p.x + o.x, p.y + o.y);
                if (ho < ValleyShape.WaterLevel + 0.3f || Mathf.Abs(ho - h) > MaxHeightDelta) return false;
            }
            foreach (var k in KeepOut)
                if (Vector2.Distance(p, k.center) < k.radius) return false;
            foreach (var t in taken)
                if (Vector2.Distance(p, new Vector2(t.x, t.z)) < 9f) return false;
            if (trees != null)
                foreach (Transform tree in trees.transform)
                    if (Vector2.Distance(p, new Vector2(tree.position.x, tree.position.z)) < ZoneClearRadius) return false;
            return true;
        }

        private static void BuildZone(Transform root, GameObject template, Material material, string id, Vector2 p, Vector2 viewer2, int variant,
            string title, string hint)
        {
            Vector3 ground = MapBuilder.Ground(p.x, p.y);
            Vector3 v = new Vector3(viewer2.x, 0f, viewer2.y).normalized;   // hacia donde mira el lienzo (donde se para quien dibuja)
            Vector3 right = Vector3.Cross(Vector3.up, v).normalized;         // a la derecha de quien mira el lienzo
            float yaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            Vector3 At(float r, float f) => ground + right * r + v * f;

            switch (variant % 3)
            {
                case 0:
                    // Dos caballetes lado a lado + barril y banquito.
                    Parent(EaselGo(template, material, $"Easel_{id}_A", At(-0.8f, 0f), yaw), root);
                    Parent(EaselGo(template, material, $"Easel_{id}_B", At(0.9f, 0.1f), yaw - 12f), root);
                    MovablesSetup.Prop(root, material, $"Barrel_{id}", MovablesSetup.Kind.Barrel, At(2.3f, -0.9f), 0f, true);
                    MovablesSetup.Prop(root, material, $"Stool_{id}", MovablesSetup.Kind.Stool, At(0.3f, 1.8f), yaw + 20f, true);
                    MovablesSetup.Prop(root, material, $"SmallBox_{id}", MovablesSetup.Kind.SmallBox, At(2.4f, 0.3f), 15f, true);
                    break;
                case 1:
                    // Lienzo enmarcado de pie sobre su soporte, banco enfrente y maceta.
                    var rotation = StandingRotation(v, 0f);
                    Stand(material, $"Stand_{id}", ground, rotation);
                    Parent(GameObject.Find($"Stand_{id}"), root);
                    var canvas = Grabbable(template, material, $"Canvas_{id}", StandingPosition(ground, rotation, 1.6f), rotation, 1.4f, 1.6f, Dressing.Framed);
                    canvas.GetComponent<Rigidbody>().isKinematic = true;
                    canvas.transform.SetParent(root, true);
                    MovablesSetup.Prop(root, material, $"Bench_{id}", MovablesSetup.Kind.Bench, At(0f, 2.4f), yaw, true);
                    MovablesSetup.Prop(root, material, $"Pot_{id}", MovablesSetup.Kind.Pot, At(1.7f, 0.3f), 0f, true);
                    MovablesSetup.Prop(root, material, $"BigBox_{id}", MovablesSetup.Kind.BigBox, At(-2.0f, -0.6f), 10f, true);
                    break;
                default:
                    // Mesa de dibujo inclinada (su lado de trabajo es -Z) y una mesa suelta con vela y libros.
                    Parent(DraftingGo(template, material, $"DraftingTable_{id}", ground, yaw + 180f), root);
                    Vector3 tablePos = At(2.4f, 0.4f);
                    MovablesSetup.Prop(root, material, $"Table_{id}", MovablesSetup.Kind.Table, tablePos, yaw, true);
                    float top = MapBuilder.Ground(tablePos.x, tablePos.z).y + 0.02f + 0.8f;
                    MovablesSetup.Prop(root, material, $"Candle_{id}", MovablesSetup.Kind.Candle, new Vector3(tablePos.x, top, tablePos.z) + right * 0.35f, 0f);
                    MovablesSetup.Prop(root, material, $"BookA_{id}", MovablesSetup.Kind.Book, new Vector3(tablePos.x, top, tablePos.z) - right * 0.2f, yaw + 10f);
                    MovablesSetup.Prop(root, material, $"BookB_{id}", MovablesSetup.Kind.Book, new Vector3(tablePos.x, top + 0.05f, tablePos.z) - right * 0.2f, yaw - 8f);
                    MovablesSetup.Prop(root, material, $"Stool_{id}", MovablesSetup.Kind.Stool, At(0f, 1.9f), 0f, true);
                    break;
            }
            Sign(root, material, $"Sign_{id}", At(-2.4f, 0.4f), v, title, hint);
        }

        /// <summary>Reparenta (conservando la pose) lo que crearon Easel/DraftingTable/Stand, que cuelgan de la raiz de la escena.</summary>
        private static void Parent(GameObject go, Transform root)
        {
            if (go != null) go.transform.SetParent(root, true);
        }

        // Easel y DraftingTable no devuelven el objeto: lo buscan por nombre.
        private static GameObject EaselGo(GameObject template, Material material, string name, Vector3 ground, float yaw)
        {
            Easel(template, material, name, ground, yaw);
            return GameObject.Find(name);
        }

        private static GameObject DraftingGo(GameObject template, Material material, string name, Vector3 ground, float yaw)
        {
            DraftingTable(template, material, name, ground, yaw);
            return GameObject.Find(name);
        }

        /// <summary>Cartel de madera sobre un poste, legible desde donde se para quien dibuja (viewer). Lo usa tambien MapStructures.</summary>
        internal static void Sign(Transform root, Material material, string name, Vector3 at, Vector3 viewer, string title, string hint)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var b = new LowPolyMesh();
            var post = new Color(0.3f, 0.19f, 0.12f);
            // El poste va DETRAS del tablero (la cara con el texto mira a -Z): si no, tapa el texto del medio.
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.85f, 0.07f)), new Vector3(0.1f, 1.7f, 0.09f), post);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.45f, 0f)), new Vector3(1.0f, 0.55f, 0.05f), new Color(0.72f, 0.57f, 0.38f));
            var mesh = MapBuilder.SaveMesh(b.Build($"Sign_{name}"));
            // El texto se lee desde -Z: el cartel mira "hacia afuera" del jugador (forward = -viewer).
            var go = MapBuilder.Place(name, root, mesh, material, new Vector3(at.x, MapBuilder.Ground(at.x, at.z).y, at.z), Quaternion.LookRotation(-viewer));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.85f, 0.04f);
            col.size = new Vector3(1.0f, 1.7f, 0.18f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.45f, -0.03f);
            var label = labelGo.AddComponent<TMPro.TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(0.95f, 0.5f);
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.2f;
            label.fontSizeMax = 0.7f;
            label.color = new Color(0.14f, 0.08f, 0.04f);
            label.text = $"<b>{title}</b>\n{hint}";
            Undo.RegisterCreatedObjectUndo(go, name);
        }
    }
}
