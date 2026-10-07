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
        public static string Apply() => FixInteractMask() + " " + MakePropsMovable() + " " + MakeSurfacesInscribable();

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
