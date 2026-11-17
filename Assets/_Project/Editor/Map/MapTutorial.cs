using UnityEditor;
using UnityEngine;
using SpellSimulator.World;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Torre-tutorial (Docs/MAPA_FLUJO_PLAN.md, §17). Genera, bajo <c>TowerTutorial</c> (al pie de la torre):
    /// - la barrera magica (prisma de colliders por fuera del muro + rejas de luz en la terraza y en la puerta) y la palanca
    ///   de salteo, que solo existen mientras el tutorial esta activo;
    /// - un farol por nivel (se enciende al pasarlo; malla emisiva, sin luz);
    /// - nivel 1, la terraza (movilidad basica): carteles, un murete para saltar y una viga para pasar agachado.
    /// Angulos como en TowerBuilder: 0 = este, 90 = norte, 270 = sur (puerta).
    /// </summary>
    public static class MapTutorial
    {
        public const string RootName = "TowerTutorial";

        /// <summary>Angulos (grados) del murete y de la viga de la terraza: entre los postes del techo, camino a la rampa (150).</summary>
        public const float LowWallAngle = 192f, BeamAngle = 206f;
        public const float LowWallHeight = 0.55f, BeamClearance = 1.3f;

        private static readonly Color Wood = new Color(0.5f, 0.33f, 0.2f);
        private static readonly Color WoodDark = new Color(0.3f, 0.19f, 0.12f);
        private static readonly Color Stone = new Color(0.62f, 0.58f, 0.52f);
        private static readonly Color Iron = new Color(0.2f, 0.2f, 0.23f);
        private static readonly Color GlassOff = new Color(0.28f, 0.26f, 0.24f);
        private static readonly Color Magic = new Color(1.5f, 0.7f, 2.6f);
        private static readonly Color LeverRed = new Color(0.72f, 0.16f, 0.12f);

        public static TowerTutorial Build(Transform map, Material material, Vector3 origin)
        {
            Transform old = map.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName);
            root.transform.SetParent(map, false);
            root.transform.position = origin;

            var activeOnly = new GameObject("TutorialActive");
            activeOnly.transform.SetParent(root.transform, false);
            Barrier(activeOnly.transform, material);
            SkipLever(activeOnly.transform, material);

            GameObject[] glows = Lamps(root.transform, material);
            Terrace(root.transform, material);

            var tutorial = root.AddComponent<TowerTutorial>();
            tutorial.Configure(activeOnly, glows);
            // En la escena queda apagado: solo se enciende al elegir «Hacer el tutorial».
            activeOnly.SetActive(false);
            EditorUtility.SetDirty(tutorial);
            Debug.Log("[Mapa] torre-tutorial: barrera, palanca de salteo, 4 faroles y nivel de la terraza");
            return tutorial;
        }

        // ---------------------------------------------------------------- barrera

        private static void Barrier(Transform parent, Material material)
        {
            const int sides = TowerBuilder.Sides;
            float step = 360f / sides;
            float width = 2f * TutorialRules.BarrierRadius * Mathf.Tan(step * 0.5f * Mathf.Deg2Rad) + 0.15f;
            float top = TowerBuilder.TerraceY + 5f, bottom = -1.5f;
            for (int s = 0; s < sides; s++)
            {
                float angle = (s + 0.5f) * step;
                var wall = new GameObject($"BarrierWall_{s}");
                wall.transform.SetParent(parent, false);
                wall.transform.localPosition = TowerBuilder.Polar(TutorialRules.BarrierRadius + 0.15f, angle, (top + bottom) * 0.5f);
                wall.transform.localRotation = Quaternion.Euler(0f, TowerBuilder.TangentYaw(angle), 0f);
                wall.AddComponent<BoxCollider>().size = new Vector3(width, top - bottom, 0.3f);
            }

            // Rejas de luz: sobre las almenas de la terraza y delante de la puerta.
            var b = new LowPolyMesh();
            float y0 = TowerBuilder.TerraceY + TowerBuilder.ParapetHeight, y1 = TowerBuilder.TerraceY + 3.3f;
            const int bars = 60;
            for (int i = 0; i < bars; i++)
            {
                float angle = i * 360f / bars;
                b.Box(Matrix4x4.Translate(TowerBuilder.Polar(TowerBuilder.TerraceRadius + 0.25f, angle, (y0 + y1) * 0.5f)), new Vector3(0.035f, y1 - y0, 0.035f), Magic);
            }
            foreach (float y in new[] { y0 + 0.55f, y1 - 0.4f })
                for (int i = 0; i < bars; i++)
                {
                    float a0 = i * 360f / bars, a1 = (i + 1) * 360f / bars;
                    Vector3 p0 = TowerBuilder.Polar(TowerBuilder.TerraceRadius + 0.25f, a0, y), p1 = TowerBuilder.Polar(TowerBuilder.TerraceRadius + 0.25f, a1, y);
                    b.Box(Matrix4x4.TRS((p0 + p1) * 0.5f, Quaternion.LookRotation(p1 - p0), Vector3.one), new Vector3(0.03f, 0.03f, (p1 - p0).magnitude), Magic);
                }
            for (int i = 0; i < 6; i++)
            {
                float x = (i - 2.5f) * TowerBuilder.DoorWidth / 5f;
                b.Box(Matrix4x4.Translate(new Vector3(x, TowerBuilder.DoorHeight * 0.5f, -(TowerBuilder.BaseRadius + 0.1f))),
                    new Vector3(0.035f, TowerBuilder.DoorHeight, 0.035f), Magic);
            }
            var cage = MapBuilder.Place("BarrierCage", parent, MapBuilder.SaveMesh(b.Build("Tutorial_BarrierCage")), material,
                parent.position, Quaternion.identity);
            cage.isStatic = false;
        }

        // ---------------------------------------------------------------- palanca de salteo

        private static void SkipLever(Transform parent, Material material)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.45f, 0f)), new Vector3(0.3f, 0.9f, 0.3f), Stone, Stone * 0.9f);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.93f, 0f)), new Vector3(0.38f, 0.06f, 0.38f), Iron);
            b.Box(Matrix4x4.TRS(new Vector3(0f, 1.14f, -0.06f), Quaternion.Euler(-22f, 0f, 0f), Vector3.one), new Vector3(0.05f, 0.42f, 0.05f), Iron);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.36f, -0.14f)), new Vector3(0.13f, 0.13f, 0.13f), LeverRed);
            Vector3 at = parent.position + TowerBuilder.Polar(3f, 302f, TowerBuilder.TerraceY);
            Vector3 toCenter = parent.position - at;
            toCenter.y = 0f;
            var go = MapBuilder.Place("TutorialSkipLever", parent, MapBuilder.SaveMesh(b.Build("Tutorial_SkipLever")), material, at,
                Quaternion.LookRotation(-toCenter.normalized));
            go.isStatic = false;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.72f, -0.04f);
            col.size = new Vector3(0.42f, 1.46f, 0.46f);
            go.AddComponent<TutorialSkipLever>();

            Vector3 spawn = parent.position + new Vector3(SpawnRules.TerraceOffset.x, TowerBuilder.TerraceY, SpawnRules.TerraceOffset.y);
            Sign(parent, material, "Sign_Tutorial_Skip", parent.position + TowerBuilder.Polar(3f, 283f, TowerBuilder.TerraceY), spawn,
                "¿Ya sabés jugar?", "Apuntá a la <b>palanca</b> y apretá <b>E</b>\npara saltear el tutorial\ne ir directo al nexo.");
        }

        // ---------------------------------------------------------------- faroles de nivel

        private static GameObject[] Lamps(Transform parent, Material material)
        {
            // Uno por nivel, en el muro, donde se llega al pasarlo: pie de cada rampa y, el ultimo, sobre la puerta.
            (float angle, float y)[] spots =
            {
                (TowerBuilder.RampStart[2] - 14f, TowerBuilder.FloorHeight * 2f + 2.3f),
                (TowerBuilder.RampStart[1] - 14f, TowerBuilder.FloorHeight + 2.3f),
                (TowerBuilder.RampStart[0] - 14f, 2.3f),
                (TutorialRules.DoorAngle, TowerBuilder.DoorHeight + 0.45f),
            };
            var body = new LowPolyMesh();
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0.17f, 0f)), new Vector3(0.2f, 0.04f, 0.2f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, -0.17f, 0f)), new Vector3(0.2f, 0.04f, 0.2f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0f, 0.14f)), new Vector3(0.05f, 0.05f, 0.2f), Iron);
            body.Box(Matrix4x4.identity, new Vector3(0.13f, 0.3f, 0.13f), GlassOff);
            Mesh bodyMesh = MapBuilder.SaveMesh(body.Build("Tutorial_Lamp"));
            var glow = new LowPolyMesh();
            glow.Box(Matrix4x4.identity, new Vector3(0.16f, 0.31f, 0.16f), TowerBuilder.LanternGlow);
            Mesh glowMesh = MapBuilder.SaveMesh(glow.Build("Tutorial_LampGlow"));

            var glows = new GameObject[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 at = parent.position + TowerBuilder.Polar(TowerBuilder.InnerRadius - 0.28f, spots[i].angle, spots[i].y);
                Vector3 outward = at - parent.position;
                outward.y = 0f;
                var lamp = MapBuilder.Place($"TutorialLamp_{i + 1}", parent, bodyMesh, material, at, Quaternion.LookRotation(outward.normalized));
                var lit = MapBuilder.Place("Glow", lamp.transform, glowMesh, material, at, lamp.transform.rotation);
                lit.isStatic = false;
                lit.SetActive(false);
                glows[i] = lit;
            }
            return glows;
        }

        // ---------------------------------------------------------------- nivel 1: terraza

        private static void Terrace(Transform parent, Material material)
        {
            float y = TowerBuilder.TerraceY;
            // Cruzan todo el piso, del borde del centro al parapeto del balcon.
            float inner = TowerBuilder.RampInner, outer = TowerBuilder.TerraceRadius;
            float length = outer - inner, mid = (inner + outer) * 0.5f;
            var b = new LowPolyMesh();

            // Murete bajo (se salta) y viga (se pasa agachado): cruzan el anillo del piso; por el centro se pueden rodear.
            Matrix4x4 wall = Radial(mid, LowWallAngle, y);
            b.Box(wall * Matrix4x4.Translate(new Vector3(0f, LowWallHeight * 0.5f, 0f)), new Vector3(length, LowWallHeight, 0.3f), Stone, Stone * 1.08f);
            Collider(parent, "Tutorial_LowWall", mid, LowWallAngle, y + LowWallHeight * 0.5f, new Vector3(length, LowWallHeight, 0.3f));

            Matrix4x4 beam = Radial(mid, BeamAngle, y);
            const float beamThick = 0.2f;
            b.Box(beam * Matrix4x4.Translate(new Vector3(0f, BeamClearance + beamThick * 0.5f, 0f)), new Vector3(length + 0.2f, beamThick, 0.22f), Wood, WoodDark);
            b.Box(beam * Matrix4x4.Translate(new Vector3(-length * 0.5f + 0.06f, (BeamClearance + beamThick) * 0.5f, 0f)), new Vector3(0.12f, BeamClearance + beamThick, 0.12f), WoodDark);
            // Tablas colgando: cierran el paso de pie sin tapar la vista.
            for (int i = 0; i < 8; i++)
                b.Box(beam * Matrix4x4.Translate(new Vector3(-length * 0.5f + 0.4f + i * 0.47f, BeamClearance + beamThick + 0.33f, 0f)), new Vector3(0.3f, 0.6f, 0.05f),
                    LowPolyMesh.Jitter(Wood, 0.08f, i * 7));
            Collider(parent, "Tutorial_Beam", mid, BeamAngle, y + BeamClearance + 0.5f, new Vector3(length + 0.2f, 1f, 0.22f));

            var props = MapBuilder.Place("Tutorial_Terrace", parent, MapBuilder.SaveMesh(b.Build("Tutorial_Terrace")), material, parent.position, Quaternion.identity);
            props.isStatic = true;

            Vector3 o = parent.position;
            Vector3 spawn = o + new Vector3(SpawnRules.TerraceOffset.x, y, SpawnRules.TerraceOffset.y);
            Sign(parent, material, "Sign_Tutorial_Move", o + TowerBuilder.Polar(3f, 256f, y), spawn,
                "1 · Moverse", "<b>WASD</b> caminar · <b>mouse</b> mirar\n<b>Shift</b> correr · <b>Espacio</b> saltar\n<b>C</b> agacharse\n<size=75%>Seguí los carteles hasta la rampa.</size>");
            Sign(parent, material, "Sign_Tutorial_Obstacles", o + TowerBuilder.Polar(2.35f, 224f, y), o + TowerBuilder.Polar(3.9f, 246f, y),
                "2 · Obstáculos", "Pasá la viga <b>agachado</b> (C)\ny <b>saltá</b> el murete (Espacio)…\no rodealos por el centro.\n<size=75%>Siempre hay más de un camino.</size>");
            Sign(parent, material, "Sign_Tutorial_Ramp", o + TowerBuilder.Polar(2.35f, 164f, y), o + TowerBuilder.Polar(3.9f, 176f, y),
                "3 · La rampa", "Bajá por la rampa al piso de abajo.\nLa <b>barrera mágica</b> encierra la torre\nhasta que salgas por la puerta.");
        }

        /// <summary>Marco local con +X a lo largo del radio en ese angulo, centrado a esa distancia del eje de la torre.</summary>
        private static Matrix4x4 Radial(float radius, float angle, float y) =>
            Matrix4x4.TRS(TowerBuilder.Polar(radius, angle, y), Quaternion.Euler(0f, -angle, 0f), Vector3.one);

        private static void Collider(Transform parent, string name, float radius, float angle, float y, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = TowerBuilder.Polar(radius, angle, y);
            go.transform.localRotation = Quaternion.Euler(0f, -angle, 0f);
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
        }

        /// <summary>Cartel de tablilla sobre un poste, apoyado en <paramref name="at"/> (no en el terreno) y legible desde viewer.</summary>
        private static void Sign(Transform parent, Material material, string name, Vector3 at, Vector3 viewer, string title, string hint)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.78f, 0.07f)), new Vector3(0.08f, 1.56f, 0.08f), WoodDark);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.3f, 0f)), new Vector3(1.05f, 0.66f, 0.05f), new Color(0.78f, 0.62f, 0.4f));
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.3f, 0.006f)), new Vector3(1.12f, 0.73f, 0.04f), WoodDark);
            Vector3 look = viewer - at;
            look.y = 0f;
            // El texto se lee desde -Z: el cartel le da la espalda (+Z) a lo que tiene detras.
            var go = MapBuilder.Place(name, parent, MapBuilder.SaveMesh(b.Build(name)), material, at, Quaternion.LookRotation(-look.normalized));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.83f, 0.03f);
            col.size = new Vector3(1.1f, 1.66f, 0.14f);
            go.isStatic = true;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.3f, -0.03f);
            var label = labelGo.AddComponent<TMPro.TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(0.98f, 0.6f);
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.2f;
            label.fontSizeMax = 0.6f;
            label.color = new Color(0.14f, 0.08f, 0.04f);
            label.text = $"<b>{title}</b>\n{hint}";
        }
    }
}
