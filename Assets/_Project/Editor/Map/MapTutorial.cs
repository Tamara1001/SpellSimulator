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
        public const float HoleFrom = 48f, HoleTo = 180f;
        public const float FenceAngle = 115f, BeamAngle = TerraceRules.BeamAngle, JumpWallAngle = TerraceRules.JumpWallAngle;
        /// <summary>La viga y el muro van de la baranda del hueco (al otro lado del centro) hasta el parapeto: no se les da la vuelta por el medio.</summary>
        public const float ObstacleFrom = -3f;
        public const float LowWallHeight = 0.7f, BeamClearance = 1.3f, FenceHeight = 2.4f;

        /// <summary>
        /// Piso 2: la rampa de arriba llega en 0 (este) y la de abajo sale en 180 (oeste); la plaza sur queda entre las dos.
        /// La pila tapa el descanso en PileAngle y la baranda cierra el borde del hueco hasta RailEnd: para bajar hay que mover la pila.
        /// </summary>
        public const float PileAngle = 198f, RailEnd = 205f, RailHeight = 1f;
        public const string PropsName = "TutorialProps";

        /// <summary>
        /// Piso 1: igual que el piso 2 (misma baranda), pero la bajada la tapa una tela pesada colgada de una viga en ClothAngle, de pared a baranda.
        /// </summary>
        public const float ClothAngle = 195f, ClothHeight = 2.85f, ClothLift = 0.1f;
        public const string ClothName = "TutorialCloth";

        /// <summary>
        /// Planta baja: la puerta empieza trabada y se destraba de tres formas, en el muro al este de la puerta (270): un pestillo que el viento
        /// corre, un contrapeso que una columna (agua o aire) levanta, o una piedra que la tierra con Crush rompe.
        /// </summary>
        public const float LatchAngle = 258f, WeightAngle = 282f, StoneAngle = 294f;
        public const string DoorLatchesName = "DoorLatches", DoorStoneName = "TutorialDoorStone";

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
            Note(parent, material, "Note_Tutorial_Skip", at + Vector3.up * 1.95f, spawn, "<b>E</b>  saltear tutorial");
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
            const float bottom = -0.4f, top = 3f, r0 = TowerBuilder.RampInner - 0.1f, r1 = TowerBuilder.InnerRadius + 0.05f;
            int bars = 9;
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

            crouchGlow = CheckLamp(parent, material, "TerraceCheck_Crouch", TowerBuilder.Polar(6.2f, BeamAngle + 2.5f, y + 1.75f));
            jumpGlow = CheckLamp(parent, material, "TerraceCheck_Jump", TowerBuilder.Polar(6.2f, JumpWallAngle + 3f, y + LowWallHeight + 0.5f));
        }

        /// <summary>Farolito apagado con su luz como hijo (se enciende al cumplir la accion). Devuelve la luz.</summary>
        private static GameObject CheckLamp(Transform parent, Material material, string name, Vector3 local)
        {
            var body = new LowPolyMesh();
            body.Box(Matrix4x4.Translate(new Vector3(0f, 0.12f, 0f)), new Vector3(0.2f, 0.03f, 0.2f), Iron);
            body.Box(Matrix4x4.Translate(new Vector3(0f, -0.12f, 0f)), new Vector3(0.2f, 0.03f, 0.2f), Iron);
            body.Box(Matrix4x4.identity, new Vector3(0.14f, 0.21f, 0.14f), GlassOff);
            var glow = new LowPolyMesh();
            glow.Box(Matrix4x4.identity, new Vector3(0.17f, 0.22f, 0.17f), TowerBuilder.LanternGlow);
            Vector3 at = parent.position + local;
            var lamp = MapBuilder.Place(name, parent, MapBuilder.SaveMesh(body.Build("Tutorial_" + name)), material, at, Quaternion.identity);
            lamp.isStatic = false;
            var lit = MapBuilder.Place("Glow", lamp.transform, MapBuilder.SaveMesh(glow.Build("Tutorial_" + name + "Glow")), material, at, Quaternion.identity);
            lit.isStatic = false;
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
            Vector3 o = parent.position;
            var b = new LowPolyMesh();

            // Barandas del hueco: borde interior (centro), borde exterior (balcon) y el corte del anillo. La rampa llega en 180 (oeste):
            // los ultimos grados quedan abiertos para entrar desde el anillo o desde el balcon.
            Rails(b, parent, "Tutorial_TerraceRailIn", TowerBuilder.RampInner, HoleFrom, HoleTo - 8f, y);
            Rails(b, parent, "Tutorial_TerraceRailOut", TowerBuilder.InnerRadius, HoleFrom, HoleTo - 12f, y);
            RailSegment(b, parent, "Tutorial_TerraceRailEnd", TowerBuilder.Polar(TowerBuilder.RampInner, HoleFrom, y), TowerBuilder.Polar(TowerBuilder.InnerRadius, HoleFrom, y));

            // Camino del oeste: una valla alta de tablas que cierra el balcon (no se salta ni se pasa).
            float wallLen = TowerBuilder.TerraceRadius - TowerBuilder.InnerRadius, wallMid = (TowerBuilder.TerraceRadius + TowerBuilder.InnerRadius) * 0.5f;
            Matrix4x4 fence = Radial(wallMid, FenceAngle, y);
            b.Box(fence * Matrix4x4.Translate(new Vector3(0f, FenceHeight * 0.5f, 0f)), new Vector3(wallLen, FenceHeight, 0.16f), Wood, WoodDark);
            for (int i = 0; i < 4; i++)
                b.Box(fence * Matrix4x4.Translate(new Vector3(-wallLen * 0.5f + 0.15f + i * (wallLen - 0.3f) / 3f, FenceHeight * 0.5f, 0f)), new Vector3(0.14f, FenceHeight + 0.1f, 0.26f), WoodDark);
            Collider(parent, "Tutorial_Fence", wallMid, FenceAngle, y + FenceHeight * 0.5f, new Vector3(wallLen, FenceHeight, 0.2f));

            // Camino del este, primera parte: una viga que cruza TODA la terraza, del centro al parapeto (hay que agacharse).
            float span = TowerBuilder.TerraceRadius - ObstacleFrom, spanMid = (TowerBuilder.TerraceRadius + ObstacleFrom) * 0.5f;
            Matrix4x4 beam = Radial(spanMid, BeamAngle, y);
            const float beamThick = 0.2f;
            b.Box(beam * Matrix4x4.Translate(new Vector3(0f, BeamClearance + beamThick * 0.5f, 0f)), new Vector3(span, beamThick, 0.22f), Wood, WoodDark);
            foreach (float end in new[] { -span * 0.5f + 0.06f, span * 0.5f - 0.06f })
                b.Box(beam * Matrix4x4.Translate(new Vector3(end, (BeamClearance + beamThick) * 0.5f, 0f)), new Vector3(0.12f, BeamClearance + beamThick, 0.12f), WoodDark);
            for (int i = 0; i < 8; i++)
                b.Box(beam * Matrix4x4.Translate(new Vector3(-span * 0.5f + 0.5f + i * 0.8f, BeamClearance + beamThick + 0.33f, 0f)), new Vector3(0.3f, 0.6f, 0.05f),
                    LowPolyMesh.Jitter(Wood, 0.08f, i * 7));
            Collider(parent, "Tutorial_Beam", spanMid, BeamAngle, y + BeamClearance + 0.5f, new Vector3(span, 1f, 0.22f));

            // Segunda parte: un muro bajo que tambien cruza toda la terraza (hay que saltar).
            Matrix4x4 jump = Radial(spanMid, JumpWallAngle, y);
            b.Box(jump * Matrix4x4.Translate(new Vector3(0f, LowWallHeight * 0.5f, 0f)), new Vector3(span, LowWallHeight, 0.3f), Stone, Stone * 1.08f);
            Collider(parent, "Tutorial_JumpWall", spanMid, JumpWallAngle, y + LowWallHeight * 0.5f, new Vector3(span, LowWallHeight, 0.3f));

            var props = MapBuilder.Place("Tutorial_Terrace", parent, MapBuilder.SaveMesh(b.Build("Tutorial_Terrace")), material, o, Quaternion.identity);
            props.isStatic = true;

            // Tres notas, cada una sobre lo que explica y de cara a quien llega por el camino del este (viga primero, muro despues).
            Vector3 spawn = SpawnPoint(parent);
            // Se recorre en orden: caminar, viga (agacharse), muro (saltar), reja de la bajada. Cada cartel esta ANTES de lo que explica.
            Note(parent, material, "Note_Tutorial_Move", spawn + new Vector3(0f, 1.9f, -2.4f), spawn,
                "<b>1 · Caminar</b>\n<b>WASD</b> mover · <b>mouse</b> mirar\nSeguí el balcón hacia el este", 1.5f, 0.6f);
            Note(parent, material, "Note_Tutorial_Crouch", o + TowerBuilder.Polar(5.9f, BeamAngle + 14f, y + 2.15f), o + TowerBuilder.Polar(5.9f, BeamAngle + 60f, y),
                "<b>2 · Agacharse</b>\nMantené <b>C</b> y pasá\nbajo la viga", 1.3f, 0.6f);
            Note(parent, material, "Note_Tutorial_Jump", o + TowerBuilder.Polar(5.9f, JumpWallAngle + 14f, y + 2.15f), o + TowerBuilder.Polar(5.9f, JumpWallAngle + 60f, y),
                "<b>3 · Saltar</b>\nApretá <b>Espacio</b> para\npasar sobre el muro", 1.3f, 0.6f);
            Note(parent, material, "Note_Tutorial_Gate", o + TowerBuilder.Polar(3.9f, TerraceRules.GateAngle + 24f, y + 2.3f), o + TowerBuilder.Polar(5.5f, 310f, y),
                "<b>4 · Bajar</b>\nLa reja se apaga cuando\nte agachaste y saltaste", 1.3f, 0.6f);
        }

        // ---------------------------------------------------------------- nivel 2: mover objetos

        private static void ObjectsFloor(Transform parent, Transform props, Material material)
        {
            float y = TowerBuilder.FloorHeight * 2f;
            Vector3 o = parent.position;
            var b = new LowPolyMesh();

            // Baranda en el borde interior del hueco (el piso no tenia: desde el centro se bajaba por cualquier lado), que sigue
            // por el descanso hasta pasar la pila, y un tramo que cierra el anillo donde el piso se corta sobre el hueco.
            HoleRail(b, parent, material, "Tutorial_Rail", y, o);

            // La pila: dos cajas grandes que cruzan el descanso, dos chicas arriba y un barril detras.
            float yaw = TowerBuilder.TangentYaw(PileAngle);
            MovablesSetup.Prop(props, material, "TutorialCrate_A", MovablesSetup.Kind.BigBox, o + TowerBuilder.Polar(5.05f, PileAngle, y + 0.02f), yaw + 4f);
            MovablesSetup.Prop(props, material, "TutorialCrate_B", MovablesSetup.Kind.BigBox, o + TowerBuilder.Polar(5.85f, PileAngle, y + 0.02f), yaw - 6f);
            MovablesSetup.Prop(props, material, "TutorialBox_A", MovablesSetup.Kind.SmallBox, o + TowerBuilder.Polar(5.07f, PileAngle, y + 0.84f), yaw + 20f);
            MovablesSetup.Prop(props, material, "TutorialBox_B", MovablesSetup.Kind.SmallBox, o + TowerBuilder.Polar(5.83f, PileAngle, y + 0.84f), yaw - 15f);
            MovablesSetup.Prop(props, material, "TutorialBarrel", MovablesSetup.Kind.Barrel, o + TowerBuilder.Polar(5.5f, PileAngle - 9f, y + 0.02f), 0f);

            // Dos notas sobre la pila, a la vista al bajar de la rampa (que llega en 0).
            Vector3 viewer = o + TowerBuilder.Polar(3.9f, 345f, y);
            Note(parent, material, "Note_Tutorial_Grab", o + TowerBuilder.Polar(5.2f, PileAngle + 3f, y + 2.05f), viewer, "<b>E</b>  agarrar  ·  <b>E</b>  soltar");
            Note(parent, material, "Note_Tutorial_Throw", o + TowerBuilder.Polar(5.9f, PileAngle + 3f, y + 1.6f), viewer, "<b>Q</b>  arrojar");

            Exercises(parent, props, material, y, o);
        }

        // Ejercicios opcionales (no traban nada): dejar un lienzo flotando, ponerlo de canto y acertar un tiro.
        private const float ExerciseR = 4.0f;

        private static void Exercises(Transform parent, Transform props, Material material, float y, Vector3 o)
        {
            // Mesita con el lienzo de practica (copia del papel de la biblioteca).
            const float tableAngle = 14f;
            MovablesSetup.Prop(props, material, "TutorialTable", MovablesSetup.Kind.Table, o + TowerBuilder.Polar(ExerciseR, tableAngle, y + 0.02f), TowerBuilder.TangentYaw(tableAngle));
            var template = GameObject.Find("PaperCanvas");
            if (template != null)
            {
                var paper = Object.Instantiate(template, props);
                paper.name = "TutorialPaper";
                paper.transform.SetPositionAndRotation(o + TowerBuilder.Polar(ExerciseR, tableAngle, y + 0.87f), Quaternion.Euler(90f, TowerBuilder.TangentYaw(tableAngle), 0f));
                paper.transform.localScale = new Vector3(0.4f, 0.4f, template.transform.localScale.z);
                var rb = paper.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }
            }
            Vector3 viewer = o + TowerBuilder.Polar(3.9f, 345f, y);
            Note(parent, material, "Note_Tutorial_Free", o + TowerBuilder.Polar(ExerciseR, tableAngle, y + 1.7f), viewer,
                "<b>Opcional</b>  ·  agarrá el lienzo con <b>E</b> y volvé a apretar <b>E</b>: queda flotando", 1.5f, 0.62f, 0.62f);

            // 1) Marco para dejar el lienzo flotando; 2) el mismo gesto, pero de canto (girando el lienzo con el mouse y F); 3) diana.
            Frame(parent, material, props, "Exercise_Float", 30f, y, TutorialExercise.Kind.Float, "Note_Tutorial_Float", "<b>1</b>  dejalo flotando acá");
            Frame(parent, material, props, "Exercise_Upright", 46f, y, TutorialExercise.Kind.Upright, "Note_Tutorial_Upright", "<b>2</b>  de canto, girá el lienzo");
            Target(parent, material, props, 315f, y);
        }

        private static void Frame(Transform parent, Material material, Transform props, string name, float angle, float y, TutorialExercise.Kind kind, string noteName, string text)
        {
            Vector3 center = parent.position + TowerBuilder.Polar(ExerciseR, angle, y + 1.35f);
            Quaternion rot = Quaternion.Euler(0f, TowerBuilder.TangentYaw(angle), 0f);
            var b = new LowPolyMesh();
            const float w = 0.62f, h = 0.62f, t = 0.05f;
            b.Box(Matrix4x4.Translate(new Vector3(0f, h * 0.5f, 0f)), new Vector3(w + t * 2f, t, t), WoodDark);
            b.Box(Matrix4x4.Translate(new Vector3(0f, -h * 0.5f, 0f)), new Vector3(w + t * 2f, t, t), WoodDark);
            b.Box(Matrix4x4.Translate(new Vector3(w * 0.5f, 0f, 0f)), new Vector3(t, h, t), WoodDark);
            b.Box(Matrix4x4.Translate(new Vector3(-w * 0.5f, 0f, 0f)), new Vector3(t, h, t), WoodDark);
            b.Box(Matrix4x4.Translate(new Vector3(0f, -h * 0.5f - 0.65f, 0f)), new Vector3(0.06f, 1.3f, 0.06f), Wood);
            var frame = MapBuilder.Place(name, parent, MapBuilder.SaveMesh(b.Build("Tutorial_" + name)), material, center, rot);
            frame.isStatic = true;
            var zone = new GameObject("Zone");
            zone.transform.SetParent(frame.transform, false);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(w, h, 0.5f);
            var glow = CheckLamp(parent, material, name + "_Lamp", TowerBuilder.Polar(ExerciseR, angle, y + 2.2f));
            zone.AddComponent<TutorialExercise>().Configure(kind, glow);
            Vector3 viewer = parent.position + TowerBuilder.Polar(3.9f, 345f, y);
            Note(parent, material, noteName, parent.position + TowerBuilder.Polar(ExerciseR, angle, y + 2.55f), viewer, text, 1.1f, 0.34f);
        }

        private static void Target(Transform parent, Material material, Transform props, float angle, float y)
        {
            Vector3 at = parent.position + TowerBuilder.Polar(TowerBuilder.InnerRadius - 0.1f, angle, y + 1.3f);
            Quaternion rot = Quaternion.LookRotation(TowerBuilder.Polar(1f, angle, 0f));
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.identity, new Vector3(0.9f, 0.9f, 0.1f), WoodDark);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0f, -0.06f)), new Vector3(0.6f, 0.6f, 0.04f), new Color(0.85f, 0.25f, 0.2f));
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0f, -0.08f)), new Vector3(0.25f, 0.25f, 0.04f), new Color(0.97f, 0.93f, 0.8f));
            var plate = MapBuilder.Place("Exercise_Target", parent, MapBuilder.SaveMesh(b.Build("Tutorial_Target")), material, at, rot);
            plate.isStatic = true;
            plate.AddComponent<BoxCollider>().size = new Vector3(0.9f, 0.9f, 0.1f);
            var glow = CheckLamp(parent, material, "Exercise_Target_Lamp", TowerBuilder.Polar(5.6f, angle, y + 2.2f));
            plate.AddComponent<TutorialExercise>().Configure(TutorialExercise.Kind.Hit, glow);
            MovablesSetup.Prop(props, material, "TutorialBox_C", MovablesSetup.Kind.SmallBox, parent.position + TowerBuilder.Polar(5.4f, angle - 10f, y + 0.02f), 0f);
            Vector3 viewer = parent.position + TowerBuilder.Polar(3.9f, 345f, y);
            Note(parent, material, "Note_Tutorial_Target", parent.position + TowerBuilder.Polar(5.6f, angle, y + 2.55f), viewer, "<b>3</b>  <b>Q</b>  arrojá la caja a la diana", 1.1f, 0.34f);
        }

        /// <summary>Baranda del borde interior del hueco de la rampa en un piso (la misma en el piso 2 y en el piso 1).</summary>
        private static void HoleRail(LowPolyMesh b, Transform parent, Material material, string name, float y, Vector3 origin)
        {
            Rails(b, parent, name, TowerBuilder.RampInner, HoleFrom, RailEnd, y);
            RailSegment(b, parent, name + "_End", TowerBuilder.Polar(TowerBuilder.RampInner, HoleFrom, y), TowerBuilder.Polar(TowerBuilder.InnerRadius, HoleFrom, y));
            var rail = MapBuilder.Place(name, parent, MapBuilder.SaveMesh(b.Build(name)), material, origin, Quaternion.identity);
            rail.isStatic = true;
        }

        // ---------------------------------------------------------------- nivel 3: objetos magicos

        private static void MagicFloor(Transform parent, Transform props, Material material)
        {
            float y = TowerBuilder.FloorHeight;
            Vector3 o = parent.position;
            var rails = new LowPolyMesh();
            HoleRail(rails, parent, material, "Tutorial_Rail_1", y, o);

            // La tela: un panel de pared a baranda (hueco de 4 cm a cada lado), colgado de una viga fija. Cuerpo fisico con resorte.
            float inner = TowerBuilder.RampInner + 0.07f, outer = TowerBuilder.InnerRadius - 0.06f, width = outer - inner;
            float mid = (inner + outer) * 0.5f;
            var cloth = new LowPolyMesh();
            Color sack = new Color(0.82f, 0.68f, 0.4f), sackDark = new Color(0.64f, 0.5f, 0.3f);
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
            go.AddComponent<TutorialCloth>();

            // Viga de la que cuelga (fija, sin collider: la tela la cubre).
            var beam = new LowPolyMesh();
            Matrix4x4 at = Radial(mid, ClothAngle, y + ClothLift + ClothHeight + 0.05f);
            beam.Box(at, new Vector3(TowerBuilder.InnerRadius - TowerBuilder.RampInner + 0.1f, 0.14f, 0.16f), Wood, WoodDark);
            MapBuilder.Place("Tutorial_ClothBeam", parent, MapBuilder.SaveMesh(beam.Build("Tutorial_ClothBeam")), material, o, Quaternion.identity).isStatic = true;

            // Tres notas: tinta y papel (la mesa de estudio), el libro, y la tela. Se ven desde donde llega la rampa de arriba (angulo 0).
            Vector3 viewer = o + TowerBuilder.Polar(3.9f, 345f, y);
            Note(parent, material, "Note_Tutorial_Ink", o + new Vector3(-0.5f, y + 1.75f, 2.1f), o + new Vector3(0f, y, -1f), "<b>pluma</b>  +  tinta");
            Note(parent, material, "Note_Tutorial_Book", o + new Vector3(WindBook.x, y + 1.5f, WindBook.z), o + new Vector3(0f, y, -1f), "<b>E</b>  leer el libro");
            Note(parent, material, "Note_Tutorial_Cloth", o + TowerBuilder.Polar(3.35f, ClothAngle + 14f, y + 2.0f), viewer, "<b>apuntá</b> el rayo azul a la tela");

            // Lienzo de practica agarrable con plantilla fantasma (el sello del viento para calcar) y rayo de apunte al sostenerlo.
            const float tableAngle = 14f;
            MovablesSetup.Prop(props, material, "TutorialTable_1", MovablesSetup.Kind.Table, o + TowerBuilder.Polar(4.0f, tableAngle, y + 0.02f), TowerBuilder.TangentYaw(tableAngle));
            var template = GameObject.Find("PaperCanvas");
            if (template != null)
            {
                var paper = Object.Instantiate(template, props);
                paper.name = "TutorialPaper_Wind";
                paper.transform.SetPositionAndRotation(o + TowerBuilder.Polar(4.0f, tableAngle, y + 0.87f), Quaternion.Euler(90f, TowerBuilder.TangentYaw(tableAngle), 0f));
                paper.transform.localScale = new Vector3(0.5f, 0.5f, template.transform.localScale.z);
                var prb = paper.GetComponent<Rigidbody>();
                if (prb != null) { prb.isKinematic = false; prb.useGravity = true; }
                if (paper.GetComponent<SpellSimulator.Lab.TutorialPaperGuide>() == null) paper.AddComponent<SpellSimulator.Lab.TutorialPaperGuide>();
            }
            Note(parent, material, "Note_Tutorial_Steps", o + TowerBuilder.Polar(4.0f, tableAngle, y + 1.75f), viewer,
                "<b>1</b> mojá la pluma en la tinta  ·  <b>2</b> calcá el dibujo azul  ·  <b>3</b> agarrá el papel con <b>E</b> y apuntá a la tela", 1.6f, 0.7f, 0.62f);
        }

        /// <summary>Lugar (local a la torre, en planta) del libro «Tomo I: el viento» del piso 1; lo usa MapDetails.LibraryBooks.</summary>
        public static readonly Vector3 WindBook = new Vector3(0.6f, 0f, 0.5f);

        // ---------------------------------------------------------------- nivel 4: la puerta

        private static void DoorLocks(Transform parent, Transform props, Material material, out Breakable stone, out Transform latches)
        {
            Vector3 o = parent.position;
            latches = new GameObject(DoorLatchesName).transform;
            latches.SetParent(props, false);

            // Pestillo: una barra de madera sobre dos soportes, a la altura de la mano. Es un cuerpo con resorte: el viento la corre.
            var bar = new LowPolyMesh();
            bar.Box(Matrix4x4.identity, new Vector3(0.12f, 0.12f, 0.95f), Wood, WoodDark);
            bar.Box(Matrix4x4.Translate(new Vector3(0f, 0f, 0.55f)), new Vector3(0.2f, 0.2f, 0.06f), Iron);
            bar.Box(Matrix4x4.Translate(new Vector3(0f, 0f, -0.55f)), new Vector3(0.2f, 0.2f, 0.06f), Iron);
            LatchBody(latches, material, "DoorLatch_Bar", bar, o + TowerBuilder.Polar(5.85f, LatchAngle, 1.45f), LatchAngle, new Vector3(0.14f, 0.14f, 0.95f), 2.5f, false);

            // Contrapeso: un bloque de hierro colgado de una soga hasta el techo; hay que subirlo (columna de agua o de aire).
            var weight = new LowPolyMesh();
            weight.Box(Matrix4x4.identity, new Vector3(0.34f, 0.34f, 0.34f), Iron, Iron * 1.2f);
            weight.Box(Matrix4x4.Translate(new Vector3(0f, 1.1f, 0f)), new Vector3(0.04f, 2.2f, 0.04f), WoodDark);
            LatchBody(latches, material, "DoorLatch_Weight", weight, o + TowerBuilder.Polar(5.8f, WeightAngle, 1.0f), WeightAngle, new Vector3(0.34f, 0.34f, 0.34f), 3f, true);

            // Traba de piedra: un bloque contra el muro. La tierra con Crush (Wall Breaker) lo rompe.
            var rock = new LowPolyMesh();
            rock.Box(Matrix4x4.Translate(new Vector3(0f, 0.45f, 0f)), new Vector3(0.7f, 0.9f, 0.5f), Stone, Stone * 1.1f);
            rock.Box(Matrix4x4.TRS(new Vector3(0f, 0.55f, 0f), Quaternion.Euler(0f, 12f, 0f), Vector3.one), new Vector3(0.6f, 0.7f, 0.42f), Stone * 0.9f);
            var mesh = MapBuilder.SaveMesh(rock.Build("Tutorial_DoorStone"));
            var go = MapBuilder.Place(DoorStoneName, parent, mesh, material, o + TowerBuilder.Polar(5.8f, StoneAngle, 0f), Quaternion.Euler(0f, -StoneAngle + 90f, 0f));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            stone = go.AddComponent<Breakable>();
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0 & ~StaticEditorFlags.BatchingStatic);

            // Dos notas: la puerta y lo que la abre.
            Vector3 viewer = o + new Vector3(0f, 0f, 0f);
            Note(parent, material, "Note_Tutorial_Door", o + TowerBuilder.Polar(5.5f, TutorialRules.DoorAngle + 3f, 2.55f), viewer, "puerta  trabada");
            Note(parent, material, "Note_Tutorial_Locks", o + TowerBuilder.Polar(5.4f, WeightAngle, 2.35f), viewer, "viento · agua · tierra");
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
        }

        /// <summary>Baranda en arco, a ese radio, entre dos angulos (un tramo cada 12 grados).</summary>
        private static void Rails(LowPolyMesh b, Transform parent, string name, float radius, float from, float to, float y)
        {
            const float span = 12f;
            for (float a = from; a < to - 0.01f; a += span)
            {
                float a1 = Mathf.Min(a + span, to);
                RailSegment(b, parent, $"{name}_{Mathf.RoundToInt((a + a1) * 0.5f)}", TowerBuilder.Polar(radius, a, y), TowerBuilder.Polar(radius, a1, y));
            }
        }

        /// <summary>Tramo de baranda de p0 a p1 (locales a la torre): dos postes, pasamanos y travesaño, con un collider.</summary>
        private static void RailSegment(LowPolyMesh b, Transform parent, string name, Vector3 p0, Vector3 p1)
        {
            Vector3 d = p1 - p0, mid = (p0 + p1) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(d.normalized);
            foreach (Vector3 p in new[] { p0, p1 })
                b.Box(Matrix4x4.Translate(p + Vector3.up * RailHeight * 0.5f), new Vector3(0.08f, RailHeight, 0.08f), WoodDark);
            b.Box(Matrix4x4.TRS(mid + Vector3.up * (RailHeight - 0.03f), rot, Vector3.one), new Vector3(0.1f, 0.06f, d.magnitude), Wood);
            b.Box(Matrix4x4.TRS(mid + Vector3.up * (RailHeight * 0.5f), rot, Vector3.one), new Vector3(0.05f, 0.05f, d.magnitude), Wood);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = mid + Vector3.up * RailHeight * 0.5f;
            go.transform.localRotation = rot;
            go.AddComponent<BoxCollider>().size = new Vector3(0.1f, RailHeight, d.magnitude + 0.08f);
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
        private static void Note(Transform parent, Material material, string name, Vector3 at, Vector3 viewer, string text, float width = 0.92f, float height = 0.34f, float fontMax = 0f)
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
        }
    }
}
