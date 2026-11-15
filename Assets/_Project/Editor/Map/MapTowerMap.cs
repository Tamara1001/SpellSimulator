using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using SpellSimulator.World.Map;
using Object = UnityEngine.Object;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Genera el mapa del valle (Docs/LABORATORIO_V3_PLAN.md (e)): un cuadro con marco de madera y fondo de pergamino colgado
    /// en la planta baja de la torre, en la cara del muro a 342° (este-sudeste, a la derecha de quien entra y fuera del
    /// pasillo puerta-centro). Es la única cara libre a la altura de la vista: la rampa de la planta baja ocupa de 0° a 270°,
    /// el estante está en 292° y la estación de viento en 324°.
    /// Pinta el valle (montañas, pradera, loma, caminos, lago y arroyo), un marcador numerado por zona del catálogo
    /// (Data/WorldZones.json, posiciones resueltas con <see cref="ValleyShape"/> y <see cref="MapStructures.TryGetSite"/>), el
    /// rótulo fijo «Vos estás aquí» en la torre y la columna de texto (título, zona apuntada y leyenda) que maneja
    /// <see cref="TowerMap"/>. Texto legible: tamaño fijo, ≥ 6 mm sobre lo que tiene debajo, sin sombras ni ZTest Always.
    /// </summary>
    public static class MapTowerMap
    {
        public const string RootName = "TowerMap";

        /// <summary>Ángulo (grados, 0 = este) de la cara del muro donde cuelga (centro del segmento 19 del muro de 20 lados).</summary>
        public const float WallAngle = 342f;

        /// <summary>Altura del centro del cuadro sobre el piso de la planta baja (a la altura de la vista).</summary>
        public const float CenterHeight = 1.6f;

        /// <summary>Medidas exteriores del marco (m). Entra en la cara del segmento (1,53 m) sin pisar la esquina.</summary>
        public const float Width = 1.5f, Height = 1.14f;

        private const float FrameBar = 0.055f, FrameDepth = 0.088f;

        /// <summary>Columna de texto a la derecha del mapa (centro X y ancho, marco del cuadro).</summary>
        private const float ColumnX = 0.4975f, ColumnWidth = 0.36f;

        private const float TitleFontSize = 0.30f, InfoFontSize = 0.18f, LegendFontSize = 0.17f, NumberFontSize = 0.22f, HereFontSize = 0.16f;

        private static readonly Color Wood = new Color(0.42f, 0.26f, 0.14f);
        private static readonly Color WoodDark = new Color(0.3f, 0.18f, 0.1f);
        private static readonly Color Parchment = new Color(0.93f, 0.86f, 0.68f);
        private static readonly Color Ink = new Color(0.2f, 0.13f, 0.07f);
        private static readonly Color Mountain = new Color(0.66f, 0.58f, 0.47f);
        private static readonly Color PeakColor = new Color(0.46f, 0.38f, 0.3f);
        private static readonly Color Snow =new Color(0.96f, 0.95f, 0.92f);
        private static readonly Color Valley = new Color(0.66f, 0.76f, 0.48f);
        private static readonly Color Hill = new Color(0.76f, 0.8f, 0.55f);
        private static readonly Color PathColor = new Color(0.8f, 0.68f, 0.47f);
        private static readonly Color Water = new Color(0.36f, 0.6f, 0.8f);
        private static readonly Color HereColor = new Color(0.62f, 0.12f, 0.1f);

        /// <summary>
        /// Construye el cuadro como hijo de <paramref name="map"/> (reemplaza uno anterior) y devuelve su <see cref="TowerMap"/>.
        /// <paramref name="towerOrigin"/> es el pie de la torre (MapBuilder.TowerOrigin()).
        /// </summary>
        public static TowerMap Build(Transform map, Material material, Vector3 towerOrigin)
        {
            Transform old = map.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            float face = TowerBuilder.InnerRadius * Mathf.Cos(180f / TowerBuilder.Sides * Mathf.Deg2Rad);
            Vector3 outward = TowerBuilder.Polar(1f, WallAngle, 0f);
            Vector3 position = towerOrigin + outward * face + Vector3.up * CenterHeight;
            // +Z hacia el muro: el frente (−Z) mira a la habitación y el texto de TMP se lee desde adentro.
            Quaternion rotation = Quaternion.LookRotation(outward);

            var boardMesh = MapBuilder.SaveMesh(BoardMesh().Build("TowerMap_Board"));
            var board = MapBuilder.Place(RootName, map, boardMesh, material, position, rotation);
            board.isStatic = true;
            var collider = board.AddComponent<BoxCollider>();
            // Solo hasta la cara del pergamino: los marcadores sobresalen y el rayo los toca antes que al cuadro.
            collider.center = new Vector3(0f, 0f, MapProjection.FaceZ * 0.5f);
            collider.size = new Vector3(Width, Height, -MapProjection.FaceZ);

            WorldZoneCatalog.Zone[] zones = LoadResolvedZones();
            var meshes = new Mesh[zones.Length];
            for (int i = 0; i < zones.Length; i++)
                meshes[i] = MapBuilder.SaveMesh(MarkerMesh(zones[i].ParsedColor).Build($"TowerMap_Marker_{zones[i].id}"));

            var towerMap = board.AddComponent<TowerMap>();
            towerMap.Setup(zones, meshes, material);

            // Número sobre cada marcador (claro sobre colores oscuros y al revés).
            float numberZ = -(MapProjection.MarkerDepth + MapProjection.TextLift);
            for (int i = 0; i < zones.Length; i++)
            {
                Color c = zones[i].ParsedColor;
                Color textColor = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b > 0.6f ? Ink : Snow;
                float side = MapProjection.MarkerRadius * 2f;
                Text(towerMap.GetMarker(i).transform, "Number", new Vector3(0f, 0f, numberZ), new Vector2(side, side),
                    NumberFontSize, textColor, $"<b>{i + 1}</b>", TextAlignmentOptions.Center, false);
            }

            // Textos sobre la pintura del mapa: por encima de la capa más alta (lago).
            float mapTextZ = MapProjection.FaceZ - MapProjection.PaintTop - MapProjection.TextLift;
            Vector2 here = MapProjection.WorldToTable(ValleyShape.TowerCenter.x, ValleyShape.TowerCenter.y);
            Text(board.transform, "YouAreHere", new Vector3(here.x, here.y - 0.05f, mapTextZ), new Vector2(0.24f, 0.04f),
                HereFontSize, HereColor, "<b>Vos estás aquí</b>", TextAlignmentOptions.Center, false);
            Vector2 compass = CompassAt();
            Text(board.transform, "North", new Vector3(compass.x, compass.y - 0.035f, mapTextZ), new Vector2(0.05f, 0.03f),
                HereFontSize, Ink, "<b>N</b>", TextAlignmentOptions.Center, false);

            // Columna: título, zona apuntada y leyenda (las líneas de la columna sobresalen 2 mm del pergamino).
            float columnZ = MapProjection.FaceZ - 0.002f - MapProjection.TextLift;
            Text(board.transform, "Title", new Vector3(ColumnX, 0.45f, columnZ), new Vector2(ColumnWidth, 0.07f),
                TitleFontSize, Ink, "<b>Mapa del valle</b>", TextAlignmentOptions.Center, false);
            var info = Text(board.transform, "Info", new Vector3(ColumnX, 0.28f, columnZ), new Vector2(ColumnWidth, 0.24f),
                InfoFontSize, Ink, "", TextAlignmentOptions.TopLeft, true);
            var legend = Text(board.transform, "Legend", new Vector3(ColumnX, -0.18f, columnZ), new Vector2(ColumnWidth, 0.6f),
                LegendFontSize, Ink, "", TextAlignmentOptions.TopLeft, false);
            towerMap.SetPanels(info, legend);

            EditorUtility.SetDirty(towerMap);
            Debug.Log($"[Mapa] mapa de la torre: {zones.Length} zonas, cuadro en {position.x:F1}, {position.y:F1}, {position.z:F1}");
            return towerMap;
        }

        /// <summary>Lee Data/WorldZones.json y reemplaza x/z por la posición resuelta (ValleyShape o sitio de MapStructures).</summary>
        public static WorldZoneCatalog.Zone[] LoadResolvedZones()
        {
            if (!File.Exists(WorldZoneCatalog.AssetPath))
            {
                Debug.LogWarning($"[Mapa] no existe {WorldZoneCatalog.AssetPath}: el mapa queda sin zonas.");
                return Array.Empty<WorldZoneCatalog.Zone>();
            }
            WorldZoneCatalog catalog = WorldZoneCatalog.Parse(File.ReadAllText(WorldZoneCatalog.AssetPath));
            foreach (WorldZoneCatalog.Zone zone in catalog.zones)
            {
                if (string.IsNullOrEmpty(zone.origin)) continue;
                if (WorldZoneCatalog.TryResolveShape(zone.origin, out Vector2 p) || MapStructures.TryGetSite(zone.origin, out p, out _))
                {
                    zone.x = p.x;
                    zone.z = p.y;
                }
                else Debug.LogWarning($"[Mapa] zona «{zone.id}»: origen «{zone.origin}» desconocido, se usa ({zone.x}, {zone.z}).");
            }
            return catalog.zones;
        }

        // ---------------------------------------------------------------- mallas

        /// <summary>Marco, pergamino, divisorias y pintura del valle en una sola malla (marco del cuadro).</summary>
        private static LowPolyMesh BoardMesh()
        {
            var b = new LowPolyMesh();
            float fz = MapProjection.FaceZ;

            // Pergamino desde el muro (2 mm de aire) hasta la cara FaceZ, y marco de madera que sobresale 2 cm más.
            float innerW = Width - 2f * FrameBar, innerH = Height - 2f * FrameBar;
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0f, (fz - 0.002f) * 0.5f)), new Vector3(innerW + 0.004f, innerH + 0.004f, -fz - 0.002f), Parchment);
            float barZ = -0.002f - FrameDepth * 0.5f;
            b.Box(Matrix4x4.Translate(new Vector3(0f, Height * 0.5f - FrameBar * 0.5f, barZ)), new Vector3(Width, FrameBar, FrameDepth), Wood, WoodDark);
            b.Box(Matrix4x4.Translate(new Vector3(0f, -Height * 0.5f + FrameBar * 0.5f, barZ)), new Vector3(Width, FrameBar, FrameDepth), Wood);
            b.Box(Matrix4x4.Translate(new Vector3(-Width * 0.5f + FrameBar * 0.5f, 0f, barZ)), new Vector3(FrameBar, innerH, FrameDepth), Wood);
            b.Box(Matrix4x4.Translate(new Vector3(Width * 0.5f - FrameBar * 0.5f, 0f, barZ)), new Vector3(FrameBar, innerH, FrameDepth), Wood);
            // Cordel y clavo arriba (el cuadro "cuelga").
            Vector3 nail = new Vector3(0f, Height * 0.5f + 0.12f, -0.03f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 hook = new Vector3(sx * (Width * 0.5f - 0.15f), Height * 0.5f - 0.01f, -0.03f);
                Vector3 d = nail - hook;
                b.Box(Matrix4x4.TRS((nail + hook) * 0.5f, Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg), Vector3.one),
                    new Vector3(d.magnitude, 0.008f, 0.008f), new Color(0.72f, 0.62f, 0.42f));
            }
            b.Box(Matrix4x4.Translate(nail + new Vector3(0f, 0f, 0.012f)), new Vector3(0.02f, 0.02f, 0.04f), new Color(0.2f, 0.19f, 0.2f));

            // Divisoria entre el mapa y la columna, y raya bajo la zona apuntada (capa de 2 mm).
            float right = MapProjection.TableCenter.x + MapProjection.TableSide * 0.5f;
            b.Box(Matrix4x4.Translate(new Vector3(right + 0.015f, 0f, fz - 0.001f)), new Vector3(0.004f, MapProjection.TableSide, 0.002f), Ink);
            b.Box(Matrix4x4.Translate(new Vector3(ColumnX, 0.14f, fz - 0.001f)), new Vector3(ColumnWidth, 0.003f, 0.002f), Ink);

            PaintValley(b, fz);
            return b;
        }

        /// <summary>
        /// Pintura del valle en capas cada 2 mm (nada coplanar): montañas (cuadrado) → pradera (disco jugable recortado) →
        /// picos y loma → caminos → lago y arroyo. Todo de frente hacia −Z.
        /// </summary>
        private static void PaintValley(LowPolyMesh b, float fz)
        {
            Vector2 c = MapProjection.TableCenter;
            float side = MapProjection.TableSide, s = MapProjection.Scale;

            // Capa 1: montañas (todo el cuadrado del mapa).
            b.Box(Matrix4x4.Translate(new Vector3(c.x, c.y, fz - 0.001f)), new Vector3(side, side, 0.002f), Mountain);

            // Capa 2: pradera (disco jugable, recortado al cuadrado).
            Vector2 valley = MapProjection.WorldToTable(0f, 0f);
            Ellipse(b, valley, Vector2.one * ValleyShape.PlayableRadius * s, fz - 0.004f, Valley, 48, 0.006f);

            // Capa 3: picos con nieve en lo que queda de montaña, y la loma de la torre.
            Vector2 compass = CompassAt();
            float rValley = ValleyShape.PlayableRadius * s;
            const float step = 0.065f;
            for (float x = c.x - side * 0.5f + 0.04f; x <= c.x + side * 0.5f - 0.04f; x += step)
                for (float y = c.y - side * 0.5f + 0.03f; y <= c.y + side * 0.5f - 0.06f; y += step)
                {
                    var p = new Vector2(x, y);
                    if (Vector2.Distance(p, valley) < rValley + 0.035f || Vector2.Distance(p, compass) < 0.08f) continue;
                    Peak(b, p, fz - 0.006f, fz - 0.008f);
                }
            Ellipse(b, MapProjection.WorldToTable(ValleyShape.TowerCenter.x, ValleyShape.TowerCenter.y),
                Vector2.one * ValleyShape.HillRadius * s, fz - 0.006f, Hill, 24, 0f);
            // Flecha del norte (el «N» lo pone un texto encima).
            b.Tri(new Vector3(compass.x, compass.y + 0.03f, fz - 0.006f), new Vector3(compass.x + 0.014f, compass.y - 0.012f, fz - 0.006f),
                new Vector3(compass.x - 0.014f, compass.y - 0.012f, fz - 0.006f), Ink);

            // Capa 4: caminos.
            foreach (Vector2[] path in ValleyShape.Paths)
                for (int i = 0; i + 1 < path.Length; i++)
                    Strip(b, path[i], path[i + 1], ValleyShape.PathWidth * s, fz - 0.007f, PathColor);

            // Capa 5: lago y arroyo (el arroyo se recorta al borde del mapa).
            Vector2 lake = MapProjection.WorldToTable(ValleyShape.LakeCenter.x, ValleyShape.LakeCenter.y);
            Ellipse(b, lake, ValleyShape.LakeRadii * s, fz - 0.010f, Water, 32, 0f);
            Strip(b, ValleyShape.WaterfallBase, ValleyShape.RiverEnd, ValleyShape.RiverHalfWidth * 2f * s, fz - 0.009f, Water);
        }

        /// <summary>Marcador: disco de color con un aro oscuro, de la cara del pergamino (z = 0 local) hacia −Z.</summary>
        private static LowPolyMesh MarkerMesh(Color color)
        {
            var b = new LowPolyMesh();
            // Euler(−90, 0, 0) lleva el +Y del cilindro a −Z (hacia la habitación).
            Matrix4x4 m = Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f));
            float r = MapProjection.MarkerRadius, depth = MapProjection.MarkerDepth;
            b.Frustum(m, r + 0.004f, r + 0.004f, 0f, depth - 0.004f, 18, WoodDark);
            b.Frustum(m, r, r, 0f, depth, 18, color);
            return b;
        }

        // ---------------------------------------------------------------- pintura

        /// <summary>Elipse plana en z (abanico de frente hacia −Z); con clampMargin > 0 se recorta al área del mapa.</summary>
        private static void Ellipse(LowPolyMesh b, Vector2 center, Vector2 radii, float z, Color color, int sides, float clampMargin)
        {
            Vector3 P(int i)
            {
                float a = i * Mathf.PI * 2f / sides;
                var p = center + new Vector2(Mathf.Cos(a) * radii.x, Mathf.Sin(a) * radii.y);
                if (clampMargin > 0f) p = MapProjection.ClampToTable(p, clampMargin);
                return new Vector3(p.x, p.y, z);
            }
            Vector3 c = new Vector3(center.x, center.y, z);
            // Sentido horario visto desde −Z (normal hacia la habitación): centro, siguiente, actual.
            for (int i = 0; i < sides; i++) b.Tri(c, P(i + 1), P(i), color);
        }

        /// <summary>Tira plana (2 mm) entre dos puntos del mundo, recortada al área del mapa; z es el centro del espesor.</summary>
        private static void Strip(LowPolyMesh b, Vector2 worldFrom, Vector2 worldTo, float width, float z, Color color)
        {
            Vector2 a = MapProjection.ClampToTable(MapProjection.WorldToTable(worldFrom.x, worldFrom.y), 0.004f);
            Vector2 c = MapProjection.ClampToTable(MapProjection.WorldToTable(worldTo.x, worldTo.y), 0.004f);
            Vector2 d = c - a;
            if (d.sqrMagnitude < 1e-8f) return;
            var mid = (a + c) * 0.5f;
            b.Box(Matrix4x4.TRS(new Vector3(mid.x, mid.y, z), Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg), Vector3.one),
                new Vector3(d.magnitude + width, width, 0.002f), color);
        }

        /// <summary>Pico de montaña (triángulo) con la punta nevada en la capa de arriba.</summary>
        private static void Peak(LowPolyMesh b, Vector2 p, float z, float snowZ)
        {
            const float w = 0.024f, h = 0.04f;
            b.Tri(new Vector3(p.x, p.y + h, z), new Vector3(p.x + w, p.y, z), new Vector3(p.x - w, p.y, z), PeakColor);
            const float k = 0.35f;
            b.Tri(new Vector3(p.x, p.y + h, snowZ), new Vector3(p.x + w * k, p.y + h * (1f - k), snowZ), new Vector3(p.x - w * k, p.y + h * (1f - k), snowZ), Snow);
        }

        /// <summary>Lugar de la rosa (flecha del norte): esquina de arriba a la izquierda del mapa, en la montaña.</summary>
        private static Vector2 CompassAt() =>
            MapProjection.TableCenter + new Vector2(-MapProjection.TableSide * 0.5f + 0.045f, MapProjection.TableSide * 0.5f - 0.06f);

        // ---------------------------------------------------------------- texto

        /// <summary>
        /// Texto 3D de tamaño fijo (sin autosize), sin sombras. La posición tiene que dejarlo ≥ 6 mm por delante de lo que
        /// tiene detrás (TMP con ZTest LEqual parpadea si queda coplanar; no se usa ZTest Always).
        /// </summary>
        private static TextMeshPro Text(Transform parent, string name, Vector3 local, Vector2 size, float fontSize, Color color,
            string text, TextAlignmentOptions alignment, bool wrap)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshPro>();
            label.rectTransform.localPosition = local;
            label.rectTransform.localRotation = Quaternion.identity;
            label.rectTransform.localScale = Vector3.one;
            label.rectTransform.sizeDelta = size;
            label.enableAutoSizing = false;
            label.fontSize = fontSize;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.alignment = alignment;
            label.color = color;
            label.text = text;
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return label;
        }
    }
}
