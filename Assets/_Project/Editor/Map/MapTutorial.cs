using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.World;
using SpellSimulator.World.Map;
using SpellSimulator.Lab;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Torre-tutorial (Docs/MAPA_FLUJO_PLAN.md, §17). Genera, bajo <c>TowerTutorial</c> (al pie de la torre):
    /// - la barrera magica (prisma de colliders por fuera del muro + rejas de luz en la terraza y en la puerta) y la palanca
    ///   de salteo, que solo existen mientras el tutorial esta activo;
    /// - un farol por nivel (se enciende al pasarlo; malla emisiva, sin luz);
    /// - nivel 1, la terraza (movilidad basica): barandas en el hueco, un murete para saltar, una viga para pasar agachado y tres notas flotantes;
    /// - nivel 2, el piso 2 (mover objetos): baranda en el hueco de la rampa, una pila de cajas y un barril sobre el descanso y dos notas;
/// - nivel 3, el piso 1 (objetos magicos): la misma baranda, una tela pesada que tapa la bajada (se vuela con viento) y tres notas.
    /// Angulos como en TowerBuilder: 0 = este, 90 = norte, 270 = sur (puerta).
    /// </summary>
    public static class MapTutorial
    {
        public const string RootName = "TowerTutorial";

        /// <summary>
        /// Terraza: el hueco de la rampa va de HoleFrom a HoleTo (la rampa llega arriba en 180, al oeste). Se empieza en el balcon, al norte.
        /// El camino del oeste esta cerrado por una valla alta (FenceAngle); el del este es un recorrido en serie que cruza todo el
        /// ancho de la terraza: primero una viga baja (BeamAngle: hay que agacharse) y despues un muro bajo (JumpWallAngle: hay que saltar).
        /// </summary>
        public const float HoleFrom = 61f, HoleTo = 180f;
        public const float FenceAngle = 115f, BeamAngle = TerraceRules.BeamAngle, JumpWallAngle = TerraceRules.JumpWallAngle;
        /// <summary>La viga y el muro van de la baranda del hueco (al otro lado del centro) hasta el parapeto: no se les da la vuelta por el medio.</summary>
        public const float ObstacleFrom = TowerBuilder.RampInner;
        public const float LowWallHeight = 0.7f, BeamClearance = 1.3f, FenceHeight = 2.4f;

        /// <summary>
        /// Piso 2: la rampa de arriba llega en 0 (este) y la de abajo sale en 180 (oeste); la plaza sur queda entre las dos.
        /// La pila tapa el descanso en PileAngle y la baranda cierra el borde del hueco hasta RailEnd: para bajar hay que mover la pila.
        /// </summary>
        public const float PileAngle = 198f, RailEnd = 205f, RailHeight = 1f;
        /// <summary>El collider de las barandas es mas alto que la baranda visible: no se saltan.</summary>
        public const float RailColliderHeight = 2.2f;
        public const string PropsName = "TutorialProps";

        /// <summary>
        /// Piso 1: igual que el piso 2 (misma baranda), pero la bajada la tapa una tela pesada colgada de una viga en ClothAngle, de pared a baranda.
        /// </summary>
        public const float ClothAngle = 195f, ClothHeight = 2.85f, ClothLift = 0.1f;
        public const string ClothName = "TutorialCloth";
        /// <summary>Hueco entre la bisagra y la pared: con menos, la esquina de la hoja roza el muro y la puerta no gira (medido en Play).</summary>
        private const float HingeGap = 0.26f;

        /// <summary>
        /// Planta baja: la puerta empieza trabada y se destraba de tres formas, en el muro al este de la puerta (270): un pestillo que el viento
        /// corre, un contrapeso que una columna (agua o aire) levanta, o una piedra que la tierra con Crush rompe.
        /// </summary>
        public const float LatchAngle = 270f, WeightAngle = 288f, StoneAngle = 234f;
        /// <summary>Separacion (m) entre la barra del pestillo y el muro: la barra cruza la puerta por dentro (la hoja abre hacia adentro y choca con ella).</summary>
        public const float LatchGap = 0.4f;
        public const string DoorLatchesName = "DoorLatches", DoorStoneName = "TutorialDoorStone";

        /// <summary>Color de la plantilla fantasma de cada tutorial: viento azul, tierra marron.</summary>
        private static readonly Color WindGhost = new Color(0.25f, 0.5f, 0.95f, 0.75f), EarthGhost = new Color(0.62f, 0.4f, 0.18f, 0.85f);
        private static readonly Color Wood = new Color(0.5f, 0.33f, 0.2f);
        private static readonly Color WoodDark = new Color(0.3f, 0.19f, 0.12f);
        private static readonly Color Stone = new Color(0.62f, 0.58f, 0.52f);
        private static readonly Color Iron = new Color(0.2f, 0.2f, 0.23f);
        private static readonly Color GlassOff = new Color(0.28f, 0.26f, 0.24f);
        private static readonly Color Magic = new Color(1.5f, 0.7f, 2.6f);
        /// <summary>Llama de los faroles encendidos: HDR mas fuerte que el farol comun de la torre (el emisivo la hace brillar sin luz real).</summary>
        private static readonly Color LampLit = new Color(3.8f, 2.6f, 1.1f);
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
            TerraceGate(activeOnly.transform, material, out var gate, out var crouchGlow, out var jumpGlow);

            GameObject[] glows = Lamps(root.transform, material);
            Terrace(root.transform, material);
            var props = new GameObject(PropsName).transform;
            props.SetParent(root.transform, false);
            ObjectsFloor(root.transform, props, material);
            MagicFloor(root.transform, props, material);
            DoorLocks(root.transform, props, material, out var stone, out var latches);

            var tutorial = root.AddComponent<TowerTutorial>();
            tutorial.Configure(activeOnly, glows, props);
            tutorial.ConfigureTerrace(gate, crouchGlow, jumpGlow);
            var doorGo = GameObject.Find("TowerDoor");
            tutorial.ConfigureDoor(doorGo != null ? doorGo.GetComponent<TowerDoor>() : null, latches, stone);
            // En la escena queda apagado: solo se enciende al elegir «Hacer el tutorial».
            activeOnly.SetActive(false);
            EditorUtility.SetDirty(tutorial);
            Debug.Log("[Mapa] torre-tutorial: barrera, palanca de salteo, 4 faroles, terraza, piso 2, piso 1 y la puerta");
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
            Vector3 at = parent.position + TowerBuilder.Polar(7.4f, 102f, TowerBuilder.TerraceY);
            Vector3 spawn = SpawnPoint(parent);
            Vector3 toSpawn = spawn - at;
            toSpawn.y = 0f;
            var go = MapBuilder.Place("TutorialSkipLever", parent, MapBuilder.SaveMesh(b.Build("Tutorial_SkipLever")), material, at,
                Quaternion.LookRotation(-toSpawn.normalized));
            go.isStatic = false;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.72f, -0.04f);
            col.size = new Vector3(0.42f, 1.46f, 0.46f);
            go.AddComponent<TutorialSkipLever>();
            Note(parent, material, "Note_Tutorial_Skip", at + Vector3.up * 2.15f, spawn, "<b>E</b> · Saltear tutorial\nIr directo al Nexo", 1.3f, 0.5f, 0.62f);
        }

        // ---------------------------------------------------------------- reja de la terraza

        /// <summary>
        /// Reja de luz que tapa la rampa entre las barandas del hueco (se apaga al agacharse bajo la viga y saltar el muro) y dos faroles testigo,
        /// uno junto a la viga y otro junto al muro, que se encienden al hacer cada cosa.
        /// </summary>
        private static void TerraceGate(Transform parent, Material material, out GameObject gate, out GameObject crouchGlow, out GameObject jumpGlow)
        {
            float y = TowerBuilder.TerraceY;
            float a = TerraceRules.GateAngle;
            var b = new LowPolyMesh();
            const float bottom = -0.4f, top = 3f, r0 = TowerBuilder.RampInner - 0.1f, r1 = TowerBuilder.TerraceRadius + 0.05f;
            int bars = 14;
            for (int i = 0; i <= bars; i++)
            {
                float r = Mathf.Lerp(r0, r1, i / (float)bars);
                b.Box(Matrix4x4.Translate(TowerBuilder.Polar(r, a, y + (bottom + top) * 0.5f)), new Vector3(0.05f, top - bottom, 0.05f), Magic);
            }
            foreach (float h in new[] { 0.3f, 1.2f, 2.1f, top - 0.1f })
            {
                Vector3 p0 = TowerBuilder.Polar(r0, a, y + h), p1 = TowerBuilder.Polar(r1, a, y + h);
                b.Box(Matrix4x4.TRS((p0 + p1) * 0.5f, Quaternion.LookRotation(p1 - p0), Vector3.one), new Vector3(0.04f, 0.04f, (p1 - p0).magnitude), Magic);
            }
            var go = MapBuilder.Place("TerraceGate", parent, MapBuilder.SaveMesh(b.Build("Tutorial_TerraceGate")), material, parent.position, Quaternion.identity);
            go.isStatic = false;
            var col = new GameObject("GateCollider");
            col.transform.SetParent(go.transform, false);
            col.transform.localPosition = TowerBuilder.Polar((r0 + r1) * 0.5f, a, y + 0.5f);
            col.transform.localRotation = Quaternion.Euler(0f, -a, 0f);
            col.AddComponent<BoxCollider>().size = new Vector3(r1 - r0 + 0.1f, 6f, 0.3f);
            gate = go;

            crouchGlow = CheckLamp(parent, material, "TerraceCheck_Crouch", TowerBuilder.Polar(5.0f, BeamAngle, y + BeamClearance + 0.2f + 0.2f));
            jumpGlow = CheckLamp(parent, material, "TerraceCheck_Jump", TowerBuilder.Polar(5.0f, JumpWallAngle, y + LowWallHeight + 0.15f));
        }

        /// <summary>Farolito apagado con su luz como hijo (se enciende al cumplir la accion). Devuelve la luz.</summary>
        private static GameObject CheckLamp(Transform parent, Material material, string name, Vector3 local, float pole = 0f)
        {
            var body = new LowPolyMesh();
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0.12f, 0f)), new Vector3(0.2f, 0.03f, 0.2f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0.16f, 0f)), new Vector3(0.1f, 0.05f, 0.1f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, -0.12f, 0f)), new Vector3(0.2f, 0.03f, 0.2f), Iron);
            body.Box(Matrix4x4.identity, new Vector3(0.14f, 0.21f, 0.14f), GlassOff);
            // Poste de hierro hasta el piso: el farol queda alto y se ve desde lejos.
            if (pole > 0.2f)
            {
                body.Box(Matrix4x4.Translate(new Vector3(0f, -0.12f - (pole - 0.12f) * 0.5f, 0f)), new Vector3(0.05f, pole - 0.12f, 0.05f), Iron);
                body.Box(Matrix4x4.Translate(new Vector3(0f, -pole + 0.03f, 0f)), new Vector3(0.24f, 0.06f, 0.24f), Iron);
            }
            var glow = new LowPolyMesh();
            glow.Box(Matrix4x4.identity, new Vector3(0.18f, 0.24f, 0.18f), LampLit);
            Vector3 at = parent.position + local;
            var lamp = MapBuilder.Place(name, parent, MapBuilder.SaveMesh(body.Build("Tutorial_" + name)), material, at, Quaternion.identity);
            lamp.isStatic = false;
            var lit = MapBuilder.Place("Glow", lamp.transform, MapBuilder.SaveMesh(glow.Build("Tutorial_" + name + "Glow")), material, at, Quaternion.identity);
            lit.isStatic = false;
            lit.AddComponent<TutorialLampGlow>();
            lit.SetActive(false);
            return lit;
        }

        private static Vector3 SpawnPoint(Transform parent) =>
            parent.position + new Vector3(SpawnRules.TerraceOffset.x, TowerBuilder.TerraceY, SpawnRules.TerraceOffset.y);

        // ---------------------------------------------------------------- faroles de nivel

        private static GameObject[] Lamps(Transform parent, Material material)
        {
            // Uno por nivel, en el muro, donde se llega al pasarlo: pie de cada rampa y, el ultimo, sobre la puerta.
            (float angle, float y)[] spots =
            {
                (TowerBuilder.RampStart[2] - 14f, TowerBuilder.FloorHeight * 2f + 3.1f),
                (TowerBuilder.RampStart[1] - 14f, TowerBuilder.FloorHeight + 3.1f),
                (TowerBuilder.RampStart[0] - 14f, 3.1f),
                (TutorialRules.DoorAngle, TowerBuilder.DoorHeight + 0.45f),
            };
            var body = new LowPolyMesh();
            // Farol de pared grande (1,5x): brazo largo al muro, tapa con remate y base; se ve desde el otro lado del piso.
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0.26f, 0f)), new Vector3(0.3f, 0.06f, 0.3f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0.32f, 0f)), new Vector3(0.14f, 0.07f, 0.14f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, -0.26f, 0f)), new Vector3(0.3f, 0.06f, 0.3f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0f, 0.2f)), new Vector3(0.07f, 0.07f, 0.34f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, -0.1f, 0.3f)), new Vector3(0.05f, 0.36f, 0.05f), Iron);
            body.Box(Matrix4x4.identity, new Vector3(0.2f, 0.46f, 0.2f), GlassOff);
            Mesh bodyMesh = MapBuilder.SaveMesh(body.Build("Tutorial_Lamp"));
            var glow = new LowPolyMesh();
            glow.Box(Matrix4x4.identity, new Vector3(0.24f, 0.47f, 0.24f), LampLit);
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
                lit.AddComponent<TutorialLampGlow>();
                lit.SetActive(false);
                glows[i] = lit;
            }
            return glows;
        }

        // ---------------------------------------------------------------- carteles

        /// <summary>
        /// Cartel de Docs/Torre/carteles.md: 1,30 x 0,50 m, dos lineas, a 1,60 m sobre el piso del nivel y de cara a <paramref name="viewerLocal"/>
        /// (punto local a la torre desde donde se viene). Es un cuerpo fisico sin gravedad: el viento lo mueve y vuelve solo.
        /// </summary>
        private static void Card(Transform parent, Material material, string name, float angle, float radius, float floorY, Vector3 viewerLocal, string line1, string line2 = null)
        {
            Note(parent, material, name, parent.position + TowerBuilder.Polar(radius, angle, floorY + 1.6f), parent.position + viewerLocal,
                line2 == null ? line1 : line1 + "\n" + line2, 1.3f, 0.5f, 0.62f);
        }

        /// <summary>
        /// Cartel PEGADO al muro, de frente al centro (nunca en diagonal ni en el pasillo): <paramref name="faceAngle"/> tiene que ser el centro de una
        /// cara del muro (multiplo de 18 grados) sin ventana ni puerta; <paramref name="height"/> es la altura del centro sobre el piso del nivel.
        /// </summary>
        private static void WallCard(Transform parent, Material material, string name, float faceAngle, float floorY, string line1, string line2, float height = 1.6f)
        {
            Vector3 at = parent.position + TowerBuilder.Polar(TowerBuilder.WallRadius(faceAngle, 0.07f), faceAngle, floorY + height);
            Vector3 viewer = parent.position + new Vector3(0f, floorY + height, 0f);
            Note(parent, material, name, at, viewer, line1 + "\n" + line2, 1.3f, 0.5f, 0.62f);
        }

        /// <summary>Mini-rotulo de estacion opcional (0,90 x 0,28 m, una linea).</summary>
        private static void Tag(Transform parent, Material material, string name, Vector3 localAt, Vector3 viewerLocal, string text) =>
            Note(parent, material, name, parent.position + localAt, parent.position + viewerLocal, text, 0.9f, 0.28f, 0.5f);

        // ---------------------------------------------------------------- nivel 1: terraza

        private static void Terrace(Transform parent, Material material)
        {
            float y = TowerBuilder.TerraceY;
            Vector3 o = parent.position;
            var b = new LowPolyMesh();

            // Baranda circular COMPLETA en el borde interior de la rampa: sella el disco central (no se camina por el medio) y el hueco.
            Rails(b, parent, "Tutorial_TerraceRailIn", TowerBuilder.RampInner, 0f, 360f, y);
            // Borde exterior del hueco (lado del balcon): el pozo se cierra hasta unos grados antes de la boca de la rampa.
            Rails(b, parent, "Tutorial_TerraceRailOut", TowerBuilder.InnerRadius, HoleFrom, HoleTo - 12f, y);
            RailSegment(b, parent, "Tutorial_TerraceRailEnd", TowerBuilder.Polar(TowerBuilder.RampInner, HoleFrom, y), TowerBuilder.Polar(TowerBuilder.InnerRadius, HoleFrom, y));

            // Camino del oeste: una valla alta de tablas que cierra el balcon (no se salta ni se pasa).
            float wallLen = TowerBuilder.TerraceRadius - TowerBuilder.InnerRadius, wallMid = (TowerBuilder.TerraceRadius + TowerBuilder.InnerRadius) * 0.5f;
            Matrix4x4 fence = Radial(wallMid, FenceAngle, y);
            b.Box(fence * Matrix4x4.Translate(new Vector3(0f, FenceHeight * 0.5f, 0f)), new Vector3(wallLen, FenceHeight, 0.16f), Wood, WoodDark);
            for (int i = 0; i < 4; i++)
                b.Box(fence * Matrix4x4.Translate(new Vector3(-wallLen * 0.5f + 0.15f + i * (wallLen - 0.3f) / 3f, FenceHeight * 0.5f, 0f)), new Vector3(0.14f, FenceHeight + 0.1f, 0.26f), WoodDark);
            Collider(parent, "Tutorial_Fence", wallMid, FenceAngle, y + FenceHeight * 0.5f, new Vector3(wallLen, FenceHeight, 0.2f));

            // Camino del este, primera parte: una viga que cruza el pasillo de la baranda interior al parapeto (hay que agacharse).
            float span = TowerBuilder.TerraceRadius - ObstacleFrom, spanMid = (TowerBuilder.TerraceRadius + ObstacleFrom) * 0.5f;
            Matrix4x4 beam = Radial(spanMid, BeamAngle, y);
            const float beamThick = 0.2f;
            b.Box(beam * Matrix4x4.Translate(new Vector3(0f, BeamClearance + beamThick * 0.5f, 0f)), new Vector3(span, beamThick, 0.22f), Wood, WoodDark);
            foreach (float end in new[] { -span * 0.5f + 0.06f, span * 0.5f - 0.06f })
                b.Box(beam * Matrix4x4.Translate(new Vector3(end, (BeamClearance + beamThick) * 0.5f, 0f)), new Vector3(0.12f, BeamClearance + beamThick, 0.12f), WoodDark);
            Collider(parent, "Tutorial_Beam", spanMid, BeamAngle, y + BeamClearance + 0.5f, new Vector3(span, 1f, 0.22f));

            // Segunda parte: un muro bajo que tambien cruza todo el pasillo (hay que saltar).
            Matrix4x4 jump = Radial(spanMid, JumpWallAngle, y);
            b.Box(jump * Matrix4x4.Translate(new Vector3(0f, LowWallHeight * 0.5f, 0f)), new Vector3(span, LowWallHeight, 0.3f), Stone, Stone * 1.08f);
            Collider(parent, "Tutorial_JumpWall", spanMid, JumpWallAngle, y + LowWallHeight * 0.5f, new Vector3(span, LowWallHeight, 0.3f));

            var props = MapBuilder.Place("Tutorial_Terrace", parent, MapBuilder.SaveMesh(b.Build("Tutorial_Terrace")), material, o, Quaternion.identity);
            props.isStatic = true;

            // Carteles 2-5 (carteles.md), cada uno ANTES de lo que explica y de cara a quien llega por el camino (el 1 va con la palanca).
            Vector3 spawn = SpawnPoint(parent) - o;
            Card(parent, material, "Note_Tutorial_Move", 72f, 6.8f, y, spawn, "<b>WASD</b> · Caminar", "<b>Mouse</b> · Mirar");
            Card(parent, material, "Note_Tutorial_Crouch", 42f, 6.6f, y, TowerBuilder.Polar(6.6f, 67f, y), "<b>C</b> · Agacharse", "Pasá bajo la viga");
            Card(parent, material, "Note_Tutorial_Jump", 344f, 6.6f, y, TowerBuilder.Polar(6.6f, 9f, y), "<b>Espacio</b> · Saltar", "Pasá sobre el muro");
        }

        // ---------------------------------------------------------------- nivel 2: mover objetos

        private static void ObjectsFloor(Transform parent, Transform props, Material material)
        {
            float y = TowerBuilder.FloorHeight * 2f;
            Vector3 o = parent.position;
            var b = new LowPolyMesh();
            HoleRail(b, parent, material, "Tutorial_Rail", y, o);

            // La pila tapa el pasillo entre la baranda y el muro en PileAngle: dos cajas grandes de ancho, dos chicas arriba, otra grande
            // detras y un barril. Para bajar hay que moverla (E).
            float yaw = TowerBuilder.TangentYaw(PileAngle);
            MovablesSetup.Prop(props, material, "TutorialCrate_A", MovablesSetup.Kind.BigBox, o + TowerBuilder.Polar(4.58f, PileAngle, y + 0.02f), yaw + 2f);
            MovablesSetup.Prop(props, material, "TutorialCrate_B", MovablesSetup.Kind.BigBox, o + TowerBuilder.Polar(5.34f, PileAngle, y + 0.02f), yaw - 3f);
            MovablesSetup.Prop(props, material, "TutorialCrate_C", MovablesSetup.Kind.BigBox, o + TowerBuilder.Polar(4.95f, PileAngle - 7f, y + 0.02f), yaw + 5f);
            MovablesSetup.Prop(props, material, "TutorialBox_A", MovablesSetup.Kind.SmallBox, o + TowerBuilder.Polar(4.58f, PileAngle, y + 0.84f), yaw + 20f);
            MovablesSetup.Prop(props, material, "TutorialBox_B", MovablesSetup.Kind.SmallBox, o + TowerBuilder.Polar(5.34f, PileAngle, y + 0.84f), yaw - 15f);
            MovablesSetup.Prop(props, material, "TutorialBarrel", MovablesSetup.Kind.Barrel, o + TowerBuilder.Polar(5.0f, PileAngle + 6f, y + 0.02f), 0f);

            // Carteles 6 y 10 (carteles.md): pegados al muro, de frente al centro. El 6 en la llegada; el 10 antes de la pila (de cara a quien camina hacia ella).
            WallCard(parent, material, "Note_Tutorial_Practice", 342f, y, "<b>E</b> · Agarrar y soltar", "Probá con lo que haya");
            WallCard(parent, material, "Note_Tutorial_Pile", 216f, y, "<b>E</b> · Mover las cajas", "Despejá la bajada");

            Exercises(parent, props, material, y, o);
        }

        // Practica OPCIONAL en el disco central, del lado norte (junto a la baranda del hueco, lejos del camino este-oeste del anillo):
        // tres estaciones en fila, de izquierda a derecha para quien mira al norte: 1) pedestal con un lienzo acostado y un papel fantasma
        // flotando encima, 2) lo mismo con el fantasma parado, 3) diana contra la baranda. Mas objetos sueltos para agarrar, apilar y tirar.
        // El orden de numeracion es el del recorrido (de oeste a este, angulos decrecientes).
        private const float StationR = 3.2f, FloatAngle = 148f, UprightAngle = 118f, TargetAngle = 88f, TargetR = 3.97f;
        private const float PedestalTop = 0.9f, GhostHeight = 1.55f, PaperSize = 0.5f;

        private static void Exercises(Transform parent, Transform props, Material material, float y, Vector3 o)
        {
            Vector3 At(float r, float angle, float dy = 0.02f) => o + TowerBuilder.Polar(r, angle, y + dy);

            // Lo minimo para practicar agarrar y apilar, en el lado sudeste (el pasillo y el centro quedan libres): una caja grande con una chica
            // encima, otra chica suelta y un barril.
            MovablesSetup.Prop(props, material, "Practice_BigBox", MovablesSetup.Kind.BigBox, At(2.7f, 28f), 15f);
            MovablesSetup.Prop(props, material, "Practice_Box_A", MovablesSetup.Kind.SmallBox, At(2.7f, 28f, 0.84f), -10f);
            MovablesSetup.Prop(props, material, "Practice_Box_B", MovablesSetup.Kind.SmallBox, At(2.3f, 48f), 8f);
            MovablesSetup.Prop(props, material, "Practice_Barrel_A", MovablesSetup.Kind.Barrel, At(2.8f, 6f), 0f);

            var template = GameObject.Find("PaperCanvas");

            PaperStation(parent, material, props, template, "Exercise_Float", FloatAngle, y, TutorialExercise.Kind.Float, "Note_Tutorial_Float",
                "<b>1</b> · Lienzo en el aire", "Llevalo al papel fantasma");
            PaperStation(parent, material, props, template, "Exercise_Upright", UprightAngle, y, TutorialExercise.Kind.Upright, "Note_Tutorial_Upright",
                "<b>2</b> · Lienzo vertical", "<b>Clic der.</b> + mouse: girar");
            Target(parent, material, props, TargetAngle, y);
        }

        /// <summary>
        /// Estacion de lienzo: un pedestal con una hoja acostada encima, un papel fantasma flotando donde hay que dejarla (acostado en la 1,
        /// parado en la 2), el farolito de «hecho» al costado y el cartel arriba (de cara al centro del piso).
        /// </summary>
        private static void PaperStation(Transform parent, Material material, Transform props, GameObject template, string name, float angle, float y,
            TutorialExercise.Kind kind, string noteName, string line1, string line2)
        {
            Vector3 foot = parent.position + TowerBuilder.Polar(StationR, angle, y);
            float yaw = TowerBuilder.TangentYaw(angle);

            Pedestal(parent, material, name, foot, yaw);

            // Hoja acostada sobre el pedestal (se agarra con E).
            PhysicalCanvas paper = null;
            if (template != null)
            {
                var sheet = CanvasDesigns.SheetAt(template, name + "_Paper", foot + Vector3.up * (PedestalTop + 0.015f), Quaternion.Euler(90f, yaw, 0f), PaperSize, false);
                sheet.transform.SetParent(props, true);
                paper = sheet.GetComponent<PhysicalCanvas>();
            }

            // Fantasma encima del pedestal; el farolito detras, contra la baranda.
            var spot = new GameObject(name);
            spot.transform.SetParent(parent, false);
            spot.transform.position = foot + Vector3.up * GhostHeight;
            var glow = CheckLamp(parent, material, name + "_Lamp", TowerBuilder.Polar(StationR + 0.55f, angle, y + 1.75f), 1.75f);
            var exercise = spot.AddComponent<TutorialExercise>();
            exercise.Configure(kind, glow);
            exercise.ConfigurePaper(paper, PaperSize, yaw);
            Note(parent, material, noteName, foot + Vector3.up * 2.45f, parent.position + new Vector3(0f, y + 2.45f, 0f), line1 + "\n" + line2, 1.3f, 0.5f, 0.62f, true);
        }

        /// <summary>Pedestal de piedra (zocalo, fuste y tapa) con su collider; el pie queda en <paramref name="foot"/> y la tapa a <see cref="PedestalTop"/>.</summary>
        private static void Pedestal(Transform parent, Material material, string name, Vector3 foot, float yaw)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.06f, 0f)), new Vector3(0.62f, 0.12f, 0.62f), Stone * 0.92f);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.48f, 0f)), new Vector3(0.4f, 0.72f, 0.4f), Stone, Stone * 1.08f);
            b.Box(Matrix4x4.Translate(new Vector3(0f, PedestalTop - 0.04f, 0f)), new Vector3(0.66f, 0.08f, 0.66f), Stone * 1.1f);
            var pedestal = MapBuilder.Place(name + "_Pedestal", parent, MapBuilder.SaveMesh(b.Build("Tutorial_" + name + "Pedestal")), material, foot, Quaternion.Euler(0f, yaw, 0f));
            pedestal.isStatic = true;
            var col = pedestal.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, PedestalTop * 0.5f, 0f);
            col.size = new Vector3(0.66f, PedestalTop, 0.66f);
        }

        /// <summary>
        /// Mini tutorial de calco (piso 1): pedestal con un papel acostado y, sobre el, la plantilla fantasma de un glifo (<paramref name="glyphId"/>:
        /// "earth", "column" o "ring"). Al dibujar encima algo que la copia (Lab.TutorialTraceStation + TutorialTraceRules) se completa el
        /// TutorialExercise de la estacion y se enciende su farolito. HOOK: ese farolito (<c>name + "_Lamp"/Glow</c>) y TutorialExercise.Complete/Done
        /// son lo que hay que enlazar si otra logica de faroles quiere reaccionar.
        /// </summary>
        private static void TraceStation(Transform parent, Material material, Transform props, GameObject template, string name, float angle, float y,
            string glyphId, string noteName, string line1, string line2)
        {
            Vector3 foot = parent.position + TowerBuilder.Polar(StationR, angle, y);
            float yaw = TowerBuilder.TangentYaw(angle);
            Pedestal(parent, material, name, foot, yaw);
            // El papel se lee desde el centro del piso: su arriba apunta hacia afuera (la baranda), no hacia el costado.
            Vector3 outward = TowerBuilder.Polar(1f, angle, 0f);
            float paperYaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;

            var exerciseGo = new GameObject(name);
            exerciseGo.transform.SetParent(parent, false);
            exerciseGo.transform.position = foot + Vector3.up * PedestalTop;
            var glow = CheckLamp(parent, material, name + "_Lamp", TowerBuilder.Polar(StationR + 0.55f, angle, y + PedestalTop + 0.2f));
            var exercise = exerciseGo.AddComponent<TutorialExercise>();
            exercise.Configure(TutorialExercise.Kind.Trace, glow);

            if (template != null)
            {
                var sheet = CanvasDesigns.SheetAt(template, name + "_Paper", foot + Vector3.up * (PedestalTop + 0.015f), Quaternion.Euler(90f, paperYaw, 0f), PaperSize, false);
                sheet.transform.SetParent(props, true);
                var guide = sheet.AddComponent<TutorialPaperGuide>();
                var gso = new SerializedObject(guide);
                gso.FindProperty("glyphId").stringValue = glyphId;
                gso.FindProperty("showAim").boolValue = false;
                gso.FindProperty("ghostColor").colorValue = EarthGhost;
                gso.ApplyModifiedProperties();
                var trace = sheet.AddComponent<TutorialTraceStation>();
                trace.Configure(exercise);
                EditorUtility.SetDirty(trace);
            }
            Note(parent, material, noteName, foot + Vector3.up * 2.45f, parent.position + new Vector3(0f, y + 2.45f, 0f), line1 + "\n" + line2, 1.3f, 0.5f, 0.62f, true);
        }

        /// <summary>Diana de madera colgada de la baranda del hueco, de cara al centro, con su farolito y el cartel; una caja para tirar al pie.</summary>
        private static void Target(Transform parent, Material material, Transform props, float angle, float y)
        {
            Vector3 at = parent.position + TowerBuilder.Polar(TargetR, angle, y + 1.3f);
            Quaternion rot = Quaternion.LookRotation(-TowerBuilder.Polar(1f, angle, 0f));   // la cara mira al centro
            // Diana redonda: tablero de madera, anillos rojo / blanco / rojo / blanco y un centro dorado en relieve (+Z mira al centro del piso);
            // dos postes detras la apoyan en el piso (el eje Y de los frustums se gira a +Z con Rx(90)).
            var b = new LowPolyMesh();
            Matrix4x4 R = Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f));
            Color red = new Color(0.82f, 0.18f, 0.14f), white = new Color(0.95f, 0.91f, 0.8f), gold = new Color(1.4f, 1.0f, 0.3f);
            b.Frustum(R, 0.52f, 0.52f, -0.06f, 0.04f, 20, WoodDark, true, true);
            (float r, float z0, float z1, Color c)[] rings =
            {
                (0.46f, 0.04f, 0.055f, red), (0.37f, 0.055f, 0.07f, white), (0.28f, 0.07f, 0.085f, red), (0.19f, 0.085f, 0.1f, white),
            };
            foreach (var ring in rings) b.Frustum(R, ring.r, ring.r, ring.z0, ring.z1, 20, ring.c, true, false);
            b.Frustum(R, 0.1f, 0.03f, 0.1f, 0.2f, 12, gold, true, false);
            b.Box(Matrix4x4.Translate(new Vector3(-0.3f, -0.65f, -0.14f)), new Vector3(0.08f, 1.3f, 0.08f), WoodDark);
            b.Box(Matrix4x4.Translate(new Vector3(0.3f, -0.65f, -0.14f)), new Vector3(0.08f, 1.3f, 0.08f), WoodDark);
            b.Box(Matrix4x4.Translate(new Vector3(0f, -0.3f, -0.14f)), new Vector3(0.68f, 0.07f, 0.07f), WoodDark);
            var plate = MapBuilder.Place("Exercise_Target", parent, MapBuilder.SaveMesh(b.Build("Tutorial_Target")), material, at, rot);
            plate.isStatic = true;
            var targetCol = plate.AddComponent<BoxCollider>();
            targetCol.center = new Vector3(0f, 0f, 0.02f);
            targetCol.size = new Vector3(1.04f, 1.04f, 0.2f);
            var glow = CheckLamp(parent, material, "Exercise_Target_Lamp", TowerBuilder.Polar(TargetR - 0.1f, angle, y + 2.0f), 2.0f);
            plate.AddComponent<TutorialExercise>().Configure(TutorialExercise.Kind.Hit, glow);
            MovablesSetup.Prop(props, material, "TutorialBox_C", MovablesSetup.Kind.SmallBox, parent.position + TowerBuilder.Polar(2.5f, angle - 8f, y + 0.02f), 0f);
            Note(parent, material, "Note_Tutorial_Target", parent.position + TowerBuilder.Polar(TargetR - 0.15f, angle, y + 2.55f), parent.position + new Vector3(0f, y + 2.55f, 0f),
                "<b>3</b> · Tirá una caja\n<b>Q</b> arrojar a la diana", 1.3f, 0.5f, 0.62f, true);
        }

        /// <summary>Baranda del borde interior del hueco de la rampa en un piso (la misma en el piso 2 y en el piso 1).</summary>
        private static void HoleRail(LowPolyMesh b, Transform parent, Material material, string name, float y, Vector3 origin)
        {
            Rails(b, parent, name, TowerBuilder.RampInner, HoleFrom, RailEnd, y);
            // El cierre del hueco cruza la rampa que sube por encima (a 61 grados esta 1,7 m sobre el piso): collider solo de la baranda visible.
            RailSegment(b, parent, name + "_End", TowerBuilder.Polar(TowerBuilder.RampInner, HoleFrom, y), TowerBuilder.Polar(TowerBuilder.InnerRadius, HoleFrom, y), RailHeight);
            var rail = MapBuilder.Place(name, parent, MapBuilder.SaveMesh(b.Build(name)), material, origin, Quaternion.identity);
            rail.isStatic = true;
        }

        // ---------------------------------------------------------------- nivel 3: objetos magicos

        /// <summary>Caballete del tutorial de la tierra (piso 1) y del viento (planta baja): posicion (angulo/radio) y a donde apunta.</summary>
        public const float EaselEarthAngle = 250f, EaselEarthR = 3.5f, EaselDoorAngle = 250f, EaselDoorR = 3.8f;
        public const string EaselEarthName = "Easel_Earth", EaselDoorName = "Easel_Door";

        private static void MagicFloor(Transform parent, Transform props, Material material)
        {
            float y = TowerBuilder.FloorHeight;
            Vector3 o = parent.position;
            var rails = new LowPolyMesh();
            HoleRail(rails, parent, material, "Tutorial_Rail_1", y, o);

            // La puerta: una hoja de pared a baranda (hueco de 4 cm a cada lado) que gira sobre una bisagra fisica del lado de la pared. Cuerpo fisico con HingeJoint.
            float inner = TowerBuilder.RampInner + 0.07f, outer = TowerBuilder.InnerRadius - HingeGap, width = outer - inner;
            float mid = (inner + outer) * 0.5f;
            var cloth = new LowPolyMesh();
            Color sack = new Color(0.58f, 0.37f, 0.22f), sackDark = new Color(0.47f, 0.29f, 0.17f); // tablones de una puerta pesada
            const int folds = 6;
            for (int i = 0; i < folds; i++)
            {
                float w = width / folds;
                float x = -width * 0.5f + w * (i + 0.5f);
                float dz = (i % 2 == 0 ? 0.03f : -0.03f);
                float h = ClothHeight - (i % 3) * 0.07f;
                cloth.Box(Matrix4x4.Translate(new Vector3(x, (ClothHeight - h) * -0.5f, dz)),
                    new Vector3(w * 0.96f, h, 0.05f), i % 2 == 0 ? sack : sackDark);
            }
            cloth.Box(Matrix4x4.Translate(new Vector3(0f, ClothHeight * 0.5f - 0.02f, 0f)), new Vector3(width, 0.04f, 0.09f), WoodDark);
            Vector3 center = o + TowerBuilder.Polar(mid, ClothAngle, y + ClothLift + ClothHeight * 0.5f);
            Quaternion rot = Quaternion.Euler(0f, -ClothAngle, 0f);
            var go = MapBuilder.Place(ClothName, props, MapBuilder.SaveMesh(cloth.Build("Tutorial_Cloth")), material, center, rot);
            go.isStatic = false;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(width, ClothHeight, 0.12f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 18f;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Bisagra de verdad (HingeJoint de eje vertical) en el borde pegado a la pared: el viento, el agua o un golpe la abren, y el jugador no.
            go.AddComponent<TutorialCloth>().SetHinge(new Vector3(width * 0.5f, 0f, 0f));

            // Viga de la que cuelga (fija, sin collider: la tela la cubre).
            var beam = new LowPolyMesh();
            Matrix4x4 at = Radial(mid, ClothAngle, y + ClothLift + ClothHeight + 0.05f);
            beam.Box(at, new Vector3(TowerBuilder.InnerRadius - TowerBuilder.RampInner + 0.1f, 0.14f, 0.16f), Wood, WoodDark);
            MapBuilder.Place("Tutorial_ClothBeam", parent, MapBuilder.SaveMesh(beam.Build("Tutorial_ClothBeam")), material, o, Quaternion.identity).isStatic = true;

            // Lienzo FIJO inclinado en un caballete, de cara a quien dibuja y apuntando a la tela, con la plantilla fantasma del viento y la
            // flecha de apunte siempre visible. Esta cerca de la tela (el viento tiene poco alcance).
            // Caballete del viento de la version anterior: cuelga de la raiz de la escena (no de Map), sobrevivia al Build y quedaba EN EL MISMO
            // LUGAR que el de tierra (dos lienzos coincidentes: lo dibujado no se veia y las plantillas de viento y tierra se superponian).
            foreach (string retired in new[] { "Easel_Wind_Canvas", "Easel_Wind" })
            {
                var stale = GameObject.Find(retired);
                if (stale != null) Object.DestroyImmediate(stale);
            }
            Vector3 easelGround = o + TowerBuilder.Polar(EaselEarthR, EaselEarthAngle, y);
            MakeEasel(material, EaselEarthName, easelGround, center, true, "", EarthGhost, "earth");

            // Mesas del centro (TowerProps): la mesa grande lleva dos plumas, una goma, un tintero normal y el libro de la tierra; la chica, dos
            // plumas. Cada una tiene UN lienzo acostado (a la izquierda, lejos de las herramientas). Lo que sobraba se mudo al campo (MapBuilder).
            var template = GameObject.Find("PaperCanvas");
            if (template != null)
            {
                Vector3 study = o + TowerProps.StudyTableCenter + Vector3.up * 0.82f;
                Quaternion studyRot = Quaternion.Euler(0f, TowerProps.StudyTableYaw, 0f);
                CanvasDesigns.SheetAt(template, "Study_Sheet_A", study + studyRot * new Vector3(-0.6f, 0.012f, 0.05f), studyRot * Quaternion.Euler(90f, 8f, 0f), 0.4f, false).transform.SetParent(props, true);
                Vector3 nt = o + TowerProps.NotebookTableCenter + Vector3.up * 0.82f;
                Quaternion ntRot = Quaternion.Euler(0f, TowerProps.NotebookTableYaw, 0f);
                CanvasDesigns.SheetAt(template, "Study_Sheet_B", nt + ntRot * new Vector3(-0.38f, 0.012f, 0f), ntRot * Quaternion.Euler(90f, -6f, 0f), 0.4f, false).transform.SetParent(props, true);
            }

            // Tres mini tutoriales de calco, en las MISMAS posiciones que las estaciones del piso 2 (FloatAngle, UprightAngle, TargetAngle a StationR):
            // sigilo de tierra, columna y el circulo que lo encierra. Cada uno: pedestal, papel con plantilla fantasma, farolito de "hecho" y cartel.
            TraceStation(parent, material, props, template, "Trace_Earth", FloatAngle, y, "earth", "Note_Tutorial_TraceEarth",
                "<b>1</b> · El sigilo de tierra", "Calcá la plantilla");
            TraceStation(parent, material, props, template, "Trace_Column", UprightAngle, y, "column", "Note_Tutorial_TraceColumn",
                "<b>2</b> · Una columna", "Calcá la plantilla");
            TraceStation(parent, material, props, template, "Trace_Ring", TargetAngle, y, TutorialPaperGuide.RingId, "Note_Tutorial_TraceRing",
                "<b>3</b> · El círculo", "Cerrá el anillo");

            // Carteles 11-13 (carteles.md): pegados al muro, en caras sin ventana y en el orden del recorrido (llegada al este, luego el sur).
            WallCard(parent, material, "Note_Tutorial_Ink", 342f, y, "<b>E</b> · Tomá la pluma", "<b>R</b> · Recargar tinta");
            WallCard(parent, material, "Note_Tutorial_Book", 306f, y, "<b>E</b> · Leé el Tomo II", "En la mesa: es la tierra");
            WallCard(parent, material, "Note_Tutorial_Cloth", 234f, y, "Dibujá la tierra y cerrá el anillo", "Agarrá la roca (<b>E</b>) y tirala (<b>Q</b>)");
        }

        /// <summary>
        /// Caballete con lienzo fijo de cara al jugador que apunta a <paramref name="target"/>. El lienzo emite por detras (lado opuesto
        /// a quien mira), asi que el caballete se gira con la cara visible hacia el lado contrario al objetivo.
        /// </summary>
        private static void MakeEasel(Material material, string name, Vector3 ground, Vector3 target, bool guide, string recipe, Color ghost, string glyph = "")
        {
            var template = GameObject.Find("PaperCanvas");
            if (template == null) { Debug.LogWarning("[Mapa] sin PaperCanvas: no se arma el caballete " + name); return; }
            Vector3 toTarget = target - ground;
            toTarget.y = 0f;
            float yaw = Mathf.Atan2(-toTarget.x, -toTarget.z) * Mathf.Rad2Deg;   // el +Z del caballete (cara visible) mira al lado contrario
            CanvasDesigns.Easel(template, material, name, ground, yaw, false, 0.9f, 1.2f);
            var canvas = GameObject.Find(name + "_Canvas");
            // El hechizo sale por el lado contrario a quien dibuja (hacia el objetivo), no por la cara con tinta.
            var dc = canvas != null ? canvas.GetComponent<DrawingCanvas>() : null;
            if (dc != null)
            {
                var so = new SerializedObject(dc);
                so.FindProperty("emissionSide").enumValueIndex = (int)EmissionSide.AwayFromViewer;
                so.ApplyModifiedProperties();
            }
            if (canvas != null && guide)
            {
                var paperGuide = canvas.GetComponent<SpellSimulator.Lab.TutorialPaperGuide>();
                if (paperGuide == null) paperGuide = canvas.AddComponent<SpellSimulator.Lab.TutorialPaperGuide>();
                var gso = new SerializedObject(paperGuide);
                gso.FindProperty("recipeId").stringValue = recipe;
                // Con glifo: sigilo + anillo (sin signos), mas chico que el anillo para que se lea como sello.
                if (!string.IsNullOrEmpty(glyph))
                {
                    gso.FindProperty("glyphId").stringValue = glyph;
                    gso.FindProperty("withRing").boolValue = true;
                    gso.FindProperty("glyphSize").floatValue = 0.3f;
                }
                gso.FindProperty("ghostColor").colorValue = ghost;
                gso.ApplyModifiedProperties();
            }
        }

        // ---------------------------------------------------------------- nivel 4: la puerta

        private static void DoorLocks(Transform parent, Transform props, Material material, out Breakable stone, out Transform latches)
        {
            Vector3 o = parent.position;
            latches = new GameObject(DoorLatchesName).transform;
            latches.SetParent(props, false);

            // Sensor de viento de la puerta: una placa INVISIBLE del tamano de la hoja, justo delante de ella, con resorte. El viento la empuja y,
            // al apartarse, la puerta se destraba y se abre sola (la hoja es lo que se ve moverse; no hay pestillo ni barra que desaparezca).
            var sensor = new LowPolyMesh();
            sensor.Box(Matrix4x4.identity, new Vector3(0.1f, 0.1f, 0.1f), Wood);
            LatchBody(latches, material, "DoorLatch_Bar", sensor, o + TowerBuilder.Polar(TowerBuilder.WallRadius(LatchAngle, 0.12f) - LatchGap, LatchAngle, 1.5f), LatchAngle, new Vector3(0.12f, 2.6f, 1.6f), 2.5f, false);
            var sensorGo = latches.Find("DoorLatch_Bar");
            Object.DestroyImmediate(sensorGo.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(sensorGo.GetComponent<MeshFilter>());

            // Contrapeso: un bloque de hierro colgado de una soga hasta el techo; hay que subirlo (columna de agua o de aire).
            var weight = new LowPolyMesh();
            weight.Box(Matrix4x4.identity, new Vector3(0.34f, 0.34f, 0.34f), Iron, Iron * 1.2f);
            // Soga hasta el techo (el cielorraso esta a ~4,5 m): del centro del bloque (y 1,0) hasta 4,9.
            weight.Box(Matrix4x4.Translate(new Vector3(0f, 1.95f, 0f)), new Vector3(0.04f, 3.9f, 0.04f), WoodDark);
            LatchBody(latches, material, "DoorLatch_Weight", weight, o + TowerBuilder.Polar(TowerBuilder.WallRadius(WeightAngle, 0.36f), WeightAngle, 1.0f), WeightAngle, new Vector3(0.34f, 0.34f, 0.34f), 3f, true);

            // Traba de piedra: un bloque contra el muro. La tierra con Crush (Wall Breaker) lo rompe.
            var rock = new LowPolyMesh();
            rock.Box(Matrix4x4.Translate(new Vector3(0f, 0.45f, 0f)), new Vector3(0.7f, 0.9f, 0.5f), Stone, Stone * 1.1f);
            rock.Box(Matrix4x4.TRS(new Vector3(0f, 0.55f, 0f), Quaternion.Euler(0f, 12f, 0f), Vector3.one), new Vector3(0.6f, 0.7f, 0.42f), Stone * 0.9f);
            var mesh = MapBuilder.SaveMesh(rock.Build("Tutorial_DoorStone"));
            var go = MapBuilder.Place(DoorStoneName, parent, mesh, material, o + TowerBuilder.Polar(TowerBuilder.WallRadius(StoneAngle, 0.5f), StoneAngle, 0f),
                Quaternion.Euler(0f, TowerBuilder.TangentYaw(StoneAngle), 0f));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            stone = go.AddComponent<Breakable>();
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0 & ~StaticEditorFlags.BatchingStatic);

            // Caballete de la planta baja (lienzo fijo apuntando al pestillo) y el cartel 14. Plantilla fantasma del viento tambien.
            MakeEasel(material, EaselDoorName, o + TowerBuilder.Polar(EaselDoorR, EaselDoorAngle, 0f), latches.Find("DoorLatch_Bar").position, true, "gust", WindGhost);
            WallCard(parent, material, "Note_Tutorial_Door", 342f, 0f, "Dibujá viento en el lienzo", "y apuntá a la puerta");
        }

        private static void LatchBody(Transform parent, Material material, string name, LowPolyMesh mesh, Vector3 at, float angle, Vector3 colliderSize, float mass, bool liftOnly)
        {
            var go = MapBuilder.Place(name, parent, MapBuilder.SaveMesh(mesh.Build("Tutorial_" + name)), material, at, Quaternion.Euler(0f, -angle, 0f));
            go.isStatic = false;
            go.AddComponent<BoxCollider>().size = colliderSize;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var cloth = go.AddComponent<TutorialCloth>();
            cloth.SetLiftOnly(liftOnly);
            cloth.SetDrops(!liftOnly);   // el pestillo cae al piso; el contrapeso se queda levantado
        }

        /// <summary>Baranda en arco, a ese radio, entre dos angulos (un tramo cada 12 grados).</summary>
        private static void Rails(LowPolyMesh b, Transform parent, string name, float radius, float from, float to, float y)
        {
            const float span = 12f;
            for (float a = from; a < to - 0.01f; a += span)
            {
                float a1 = Mathf.Min(a + span, to);
                // El collider se corre 10 cm hacia afuera del pasillo de la rampa (el borde interno hacia el centro, el externo hacia el muro): asi quien
                // sube o baja pegado a la baranda de la rampa no lo roza ni se traba.
                float push = radius <= TowerBuilder.RampInner + 0.01f ? -0.1f : radius >= TowerBuilder.InnerRadius - 0.01f ? 0.1f : 0f;
                RailSegment(b, parent, $"{name}_{Mathf.RoundToInt((a + a1) * 0.5f)}", TowerBuilder.Polar(radius, a, y), TowerBuilder.Polar(radius, a1, y), RailColliderHeight, push);
            }
        }

        /// <summary>Tramo de baranda de p0 a p1 (locales a la torre): dos postes, pasamanos y travesaño, con un collider.</summary>
        private static void RailSegment(LowPolyMesh b, Transform parent, string name, Vector3 p0, Vector3 p1, float colliderHeight = RailColliderHeight, float pushRadial = 0f)
        {
            Vector3 d = p1 - p0, mid = (p0 + p1) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(d.normalized);
            foreach (Vector3 p in new[] { p0, p1 })
                b.Box(Matrix4x4.Translate(p + Vector3.up * RailHeight * 0.5f), new Vector3(0.08f, RailHeight, 0.08f), WoodDark);
            b.Box(Matrix4x4.TRS(mid + Vector3.up * (RailHeight - 0.03f), rot, Vector3.one), new Vector3(0.1f, 0.06f, d.magnitude), Wood);
            b.Box(Matrix4x4.TRS(mid + Vector3.up * (RailHeight * 0.5f), rot, Vector3.one), new Vector3(0.05f, 0.05f, d.magnitude), Wood);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Vector3 flatMid = new Vector3(mid.x, 0f, mid.z);
            Vector3 push = flatMid.sqrMagnitude > 0.01f ? flatMid.normalized * pushRadial : Vector3.zero;
            go.transform.localPosition = mid + push + Vector3.up * RailHeight * 0.5f;
            go.transform.localRotation = rot;
            var railCol = go.AddComponent<BoxCollider>();
            railCol.center = new Vector3(0f, (colliderHeight - RailHeight) * 0.5f, 0f);
            // Fino (4 cm): el jugador que va pegado a la baranda interna de la rampa no roza este collider y no se traba.
            railCol.size = new Vector3(0.04f, colliderHeight, d.magnitude + 0.08f);
            go.isStatic = true;
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

        /// <summary>
        /// Nota flotante: una tarjeta de pergamino con dos o tres palabras, centrada en <paramref name="at"/> y de cara a viewer.
        /// Es un cuerpo fisico sin gravedad (World.FloatingNote): el viento o un golpe la mueven y vuelve sola a su lugar.
        /// </summary>
        private static void Note(Transform parent, Material material, string name, Vector3 at, Vector3 viewer, string text, float width = 0.92f, float height = 0.34f, float fontMax = 0f, bool bothSides = false)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.identity, new Vector3(width, height, 0.02f), new Color(0.97f, 0.93f, 0.8f));
            // Marco de madera fino (cuatro listones) y una cinta arriba: sin borde de color.
            Color trim = new Color(0.4f, 0.27f, 0.16f);
            const float t = 0.022f;
            b.Box(Matrix4x4.Translate(new Vector3(0f, height * 0.5f, 0.004f)), new Vector3(width + t * 2f, t, 0.03f), trim);
            b.Box(Matrix4x4.Translate(new Vector3(0f, -height * 0.5f, 0.004f)), new Vector3(width + t * 2f, t, 0.03f), trim);
            b.Box(Matrix4x4.Translate(new Vector3(width * 0.5f, 0f, 0.004f)), new Vector3(t, height, 0.03f), trim);
            b.Box(Matrix4x4.Translate(new Vector3(-width * 0.5f, 0f, 0.004f)), new Vector3(t, height, 0.03f), trim);
            Vector3 look = viewer - at;
            look.y = 0f;
            // El texto se lee desde -Z.
            var go = MapBuilder.Place(name, parent, MapBuilder.SaveMesh(b.Build("Tutorial_Note")), material, at, Quaternion.LookRotation(-look.normalized));
            go.isStatic = false;
            go.AddComponent<BoxCollider>().size = new Vector3(width, height, 0.04f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<FloatingNote>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.014f);
            var label = labelGo.AddComponent<TMPro.TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(width - 0.06f, height - 0.06f);
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.2f;
            label.fontSizeMax = fontMax > 0f ? fontMax : height > 0.4f ? 1.1f : 1.4f;
            label.textWrappingMode = height > 0.4f ? TMPro.TextWrappingModes.Normal : TMPro.TextWrappingModes.NoWrap;
            label.color = new Color(0.14f, 0.08f, 0.04f);
            label.text = text;
            if (bothSides)
            {
                // Misma leyenda por detras (el texto de TextMeshPro no se ve desde el reves).
                var back = Object.Instantiate(labelGo, go.transform);
                back.name = "LabelBack";
                back.transform.localPosition = new Vector3(0f, 0f, 0.014f);
                back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }
    }
}
