using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Deja el mundo listo para experimentar: todo lo que es materia (cajas, pesas, troncos) se puede mover, y el piso,
    /// la torre y esos objetos se pueden rayar con un sello. Menu: SpellSimulator > Interaction > Setup Current Scene.
    /// </summary>
    public static class InteractionSetup
    {
        private const string DecalMaterialPath = "Assets/_Project/Materials/InkDecal.mat";

        [MenuItem("SpellSimulator/Interaction/Setup Current Scene")]
        public static string Apply() => FixInteractMask() + " " + FixHoldPoints() + " " + FixInkAndStance() + " " + MakePropsMovable() + " " + EnsureRespawnables() + " " + MakeSurfacesInscribable();

        /// <summary>
        /// El rayo de E solo veia las capas Canvas e Interactable: libro, estampas y cajas (capa Default) no se podian apuntar.
        /// </summary>
        public static string FixInteractMask()
        {
            var player = Object.FindFirstObjectByType<Player.PlayerInteractor>();
            if (player == null) return "Sin interactor.";
            var so = new SerializedObject(player);
            so.FindProperty("interactLayer").intValue = Physics.DefaultRaycastLayers;
            so.ApplyModifiedProperties();
            return "Mascara de interaccion: todo salvo Ignore Raycast.";
        }

        /// <summary>
        /// Las manos de PC cuelgan de la camara (antes del cuerpo): lo que se lleva sigue a la mirada como si estuviera en
        /// la mano, y el jugador tiene el movedor de objetos a distancia (G).
        /// </summary>
        public static string FixHoldPoints()
        {
            var player = GameObject.Find("Player");
            var cameraRoot = GameObject.Find("CameraRootPC");
            if (player == null || cameraRoot == null) return "Sin jugador.";
            // Las manos originales viven dentro de un prefab (no se pueden mover de padre): se crean manos nuevas
            // colgando de la camara y el interactor pasa a usarlas.
            Transform Hand(string name, Vector3 local)
            {
                var existing = cameraRoot.transform.Find(name);
                var hand = existing != null ? existing : new GameObject(name).transform;
                hand.SetParent(cameraRoot.transform, false);
                hand.localPosition = local;
                hand.localRotation = Quaternion.identity;
                return hand;
            }
            var right = Hand("ManoDerechaCamara", new Vector3(0.28f, -0.24f, 0.55f));
            var left = Hand("ManoIzquierdaCamara", new Vector3(-0.3f, -0.15f, 0.62f));
            var interactor = player.GetComponent<Player.PlayerInteractor>();
            var so = new SerializedObject(interactor);
            so.FindProperty("rightHoldPoint").objectReferenceValue = right;
            so.FindProperty("leftHoldPoint").objectReferenceValue = left;
            so.ApplyModifiedProperties();
            if (player.GetComponent<Player.ObjectMover>() == null) player.AddComponent<Player.ObjectMover>();
            return "Manos en la camara y movedor de objetos.";
        }

        /// <summary>
        /// Tinta lisa (el pincel pasa al shader InkBrush con Min/Max: sin circulos apilados) y agacharse que baja bastante mas
        /// (la camara a la mitad de la altura de pie).
        /// </summary>
        public static string FixInkAndStance()
        {
            var brush = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/BrushMaterial.mat");
            var shader = Shader.Find("SpellSimulator/InkBrush");
            if (brush != null && shader != null && brush.shader != shader)
            {
                Color ink = brush.HasProperty("_BaseColor") ? brush.GetColor("_BaseColor") : new Color(0.368f, 0.085f, 0.085f, 1f);
                brush.shader = shader;
                brush.SetColor("_InkColor", ink);
                EditorUtility.SetDirty(brush);
            }
            var movement = Object.FindFirstObjectByType<Player.PlayerMovement>();
            if (movement != null)
            {
                var so = new SerializedObject(movement);
                so.FindProperty("crouchHeight").floatValue = 1f;
                so.FindProperty("crouchCameraDrop").floatValue = 0.95f;
                so.ApplyModifiedProperties();
            }
            // Grosores de la pluma y resolucion de los lienzos (para que los trazos finos no se corten).
            float[] presets = { 0.04f, 0.09f, 0.2f, 0.45f, 1f, 2.2f };
            string[] names = { "finisimo", "muy fino", "fino", "medio", "normal", "grueso" };
            foreach (var tool in Object.FindObjectsByType<World.PhysicalTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tool.Kind != World.PhysicalTool.ToolKind.Pen) continue;
                var tso = new SerializedObject(tool);
                var pp = tso.FindProperty("widthPresets");
                pp.arraySize = presets.Length;
                for (int i = 0; i < presets.Length; i++) pp.GetArrayElementAtIndex(i).floatValue = presets[i];
                var np = tso.FindProperty("widthNames");
                np.arraySize = names.Length;
                for (int i = 0; i < names.Length; i++) np.GetArrayElementAtIndex(i).stringValue = names[i];
                tso.ApplyModifiedProperties();
            }
            foreach (var canvas in Object.FindObjectsByType<DrawingCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var cso = new SerializedObject(canvas);
                var res = cso.FindProperty("resolution");
                if (res != null && res.intValue < 1024) { res.intValue = 1024; cso.ApplyModifiedProperties(); }
            }
            return "Tinta lisa, grosores y agacharse mas bajo.";
        }

        public static string MakePropsMovable()
        {
            int added = 0;
            foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rb.isKinematic || rb.GetComponent<Movable>() != null) continue;
                if (rb.GetComponent<PhysicalCanvas>() != null || rb.GetComponent<PhysicalTool>() != null ||
                    rb.GetComponent<Stamp>() != null || rb.GetComponent<Inkwell>() != null ||
                    rb.GetComponent<Player.PlayerMovement>() != null) continue;
                if (rb.GetComponent<ElementReactive>() == null) continue;
                var movable = rb.gameObject.AddComponent<Movable>();
                var so = new SerializedObject(movable);
                so.FindProperty("objectName").stringValue = Nice(rb.name);
                so.ApplyModifiedProperties();
                added++;
            }
            return $"Movable agregado a {added} objetos.";
        }

        /// <summary>
        /// Todo lo de juego (cajas, herramientas, estampas, tinteros, lienzos, lo que reacciona al fuego) vuelve a su lugar al
        /// reiniciar el mundo. Los kinematicos fijos (placas de presion, pizarras) no se tocan: son parte del mapa.
        /// </summary>
        public static string EnsureRespawnables()
        {
            int added = 0;
            foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = rb.gameObject;
                if (go.GetComponent<Respawnable>() != null || go.GetComponent<Player.PlayerMovement>() != null) continue;
                // Los fragmentos de Shatter son temporales: los borra el reinicio, no hace falta reubicarlos.
                if (go.name.EndsWith("_Fragment")) continue;
                bool piece = go.GetComponent<Movable>() != null || go.GetComponent<PhysicalTool>() != null ||
                             go.GetComponent<Stamp>() != null || go.GetComponent<Inkwell>() != null ||
                             go.GetComponent<PhysicalCanvas>() != null;
                if (!piece && go.GetComponent<ElementReactive>() == null) continue;
                if (rb.isKinematic && !piece) continue;
                go.AddComponent<Respawnable>();
                added++;
            }
            return $"Respawnable agregado a {added} objetos.";
        }

        /// <summary>Terreno, torre, plataforma y objetos movibles: se puede dibujar un sello en ellos.</summary>
        public static string MakeSurfacesInscribable()
        {
            var brush = FindBrush();
            var decal = DecalMaterial();
            var caster = Object.FindFirstObjectByType<SpellCaster>();
            if (brush == null || decal == null) return "Sin material de pincel o de calcomania.";

            int count = 0;
            void Mark(GameObject go, float size)
            {
                if (go == null) return;
                var surface = go.GetComponent<Inscribable>() ?? go.AddComponent<Inscribable>();
                surface.Configure(size, decal, brush, caster);
                EditorUtility.SetDirty(surface);
                count++;
            }
            // Grandes: un sello grande no debe cortarse en el borde de la calcomania.
            Mark(GameObject.Find("Terrain"), 2.4f);
            Mark(GameObject.Find("Tower"), 2f);
            Mark(GameObject.Find("Platform"), 1.8f);
            foreach (var movable in Object.FindObjectsByType<Movable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Un poco menos que la cara mas chica del objeto, para que el sello quepa.
                var bounds = movable.GetComponentInChildren<Renderer>() != null ? movable.GetComponentInChildren<Renderer>().bounds.size : Vector3.one * 0.5f;
                float face = Mathf.Min(bounds.x, bounds.y, bounds.z);
                Mark(movable.gameObject, Mathf.Clamp(face * 0.9f, 0.2f, 1f));
            }
            return $"Superficies rayables: {count}.";
        }

        private static Material FindBrush()
        {
            var template = GameObject.Find("PaperCanvas");
            if (template == null) return null;
            var canvas = template.GetComponent<DrawingCanvas>();
            return canvas != null ? new SerializedObject(canvas).FindProperty("brushMaterial").objectReferenceValue as Material : null;
        }

        private static Material DecalMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(DecalMaterialPath);
            if (material != null)
            {
                // Tinta oscura como la del papel (el material guarda su propio color).
                material.SetColor("_InkColor", new Color(0.2f, 0.07f, 0.1f, 1f));
                material.SetFloat("_Glow", 1f);
                return material;
            }
            var shader = Shader.Find("SpellSimulator/InkDecal");
            if (shader == null) return null;
            material = new Material(shader) { name = "InkDecal" };
            AssetDatabase.CreateAsset(material, DecalMaterialPath);
            return material;
        }

        private static string Nice(string name)
        {
            name = name.Replace("_", " ");
            if (name.StartsWith("Weight")) return "pesa";
            if (name.StartsWith("Crate")) return "caja";
            if (name.StartsWith("Chest")) return "baul";
            if (name.StartsWith("Small")) return "cajita";
            if (name.StartsWith("Target")) return "bloque";
            if (name.StartsWith("Heavy")) return "bloque de piedra";
            return name.ToLowerInvariant();
        }
    }
}
