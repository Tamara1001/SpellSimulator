using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Reactions;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Vuelve inflamable el mundo: los pinos (arden y quedan carbonizados, sin romperse), el mobiliario y los libros de la
    /// torre, los caballetes y la mesa de dibujo (madera: arden, se propagan y se rompen). La piedra de la torre no arde.
    /// Menu: SpellSimulator > Interaction > Make World Flammable.
    /// </summary>
    public static class FlammableSetup
    {
        private const string WoodPath = "Assets/_Project/Data/Materials/Madera.asset";
        private const string TreePath = "Assets/_Project/Data/Materials/Arbol.asset";

        [MenuItem("SpellSimulator/Interaction/Make World Flammable")]
        public static string Apply()
        {
            var wood = AssetDatabase.LoadAssetAtPath<MaterialProfile>(WoodPath);
            if (wood == null) return "Sin perfil Madera.";
            var tree = TreeProfile(wood);

            int trees = 0, furniture = 0;
            var trunks = GameObject.Find("Trees");
            if (trunks != null)
                foreach (Transform t in trunks.transform)
                    if (AddReactive(t.gameObject, tree, false)) trees++;

            var tower = GameObject.Find("Tower");
            if (tower != null)
                foreach (var go in WoodenTowerObjects(tower.transform))
                    if (AddReactive(go, wood, true)) furniture++;
            foreach (var name in new[] { "DraftingTable", "Easel_Study", "Easel_Field", "StampTable" })
            {
                var go = GameObject.Find(name);
                if (go != null && AddReactive(go, wood, true)) furniture++;
            }
            string grass = SetupGrassFire();
            return $"Inflamable: {trees} pinos y {furniture} muebles. {grass}";
        }

        /// <summary>Agrega el incendio de pasto a SystemManagers con su material de quemado y el look del fuego.</summary>
        private static string SetupGrassFire()
        {
            var sys = GameObject.Find("SystemManagers");
            var output = sys != null ? sys.GetComponent<Output.SpellOutputController>() : null;
            if (sys == null || output == null) return "Sin SystemManagers.";
            var fire = output.LookFor(Compiler.ElementType.Fire);

            const string burntPath = "Assets/_Project/Materials/BurntGrass.mat";
            var burnt = AssetDatabase.LoadAssetAtPath<Material>(burntPath);
            if (burnt == null)
            {
                burnt = new Material(Shader.Find("SpellSimulator/GroundMark")) { name = "BurntGrass" };
                AssetDatabase.CreateAsset(burnt, burntPath);
            }
            burnt.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.035f, 0.92f));
            burnt.SetFloat("_Rim", 0f);
            burnt.enableInstancing = true;
            EditorUtility.SetDirty(burnt);

            var grass = sys.GetComponent<GrassFire>() ?? sys.AddComponent<GrassFire>();
            var so = new SerializedObject(grass);
            so.FindProperty("burnMaterial").objectReferenceValue = burnt;
            so.FindProperty("fireLook").objectReferenceValue = fire;
            so.FindProperty("spreadChance").floatValue = 0.03f;
            so.FindProperty("heatPerSecond").floatValue = 9f;
            so.ApplyModifiedProperties();
            return "GrassFire listo.";
        }

        /// <summary>Perfil de arbol: arde mucho, contagia a lo que tiene cerca y queda carbonizado en pie.</summary>
        private static MaterialProfile TreeProfile(MaterialProfile wood)
        {
            var tree = AssetDatabase.LoadAssetAtPath<MaterialProfile>(TreePath);
            if (tree == null)
            {
                tree = ScriptableObject.CreateInstance<MaterialProfile>();
                EditorUtility.CopySerialized(wood, tree);
                AssetDatabase.CreateAsset(tree, TreePath);
            }
            var so = new SerializedObject(tree);
            var p = so.FindProperty("parameters");
            p.FindPropertyRelative("burnSeconds").floatValue = 30f;
            p.FindPropertyRelative("burnDestroys").boolValue = false;
            p.FindPropertyRelative("fracturable").boolValue = false;
            p.FindPropertyRelative("ignitionHeat").floatValue = 1.4f;
            p.FindPropertyRelative("spreadHeatPerSecond").floatValue = 1.1f;
            var name = so.FindProperty("displayName");
            if (name != null) name.stringValue = "Arbol";
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
            return tree;
        }

        private static IEnumerable<GameObject> WoodenTowerObjects(Transform tower)
        {
            foreach (var r in tower.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.name;
                if (n.StartsWith("Chair") || n.StartsWith("Shelf") || n.EndsWith("Desk") || n.EndsWith("Table") ||
                    n.StartsWith("Workbench") || n == "TowerProps" || n == "Book" || n.StartsWith("Book_") || n.StartsWith("LibraryBook"))
                    yield return r.gameObject;
            }
        }

        private static bool AddReactive(GameObject go, MaterialProfile profile, bool movableSafe)
        {
            if (go.GetComponent<ElementReactive>() != null) return false;
            if (go.GetComponentInChildren<Renderer>() == null) return false;
            var reactive = go.AddComponent<ElementReactive>();
            var so = new SerializedObject(reactive);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.ApplyModifiedProperties();
            // Para que el fuego y las exploraciones los encuentren hace falta un collider; si no tiene, uno disparador.
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                var r = go.GetComponentInChildren<Renderer>();
                var local = go.transform.InverseTransformPoint(r.bounds.center);
                box.center = local;
                box.size = go.transform.InverseTransformVector(r.bounds.size);
                box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z));
                box.isTrigger = true;
            }
            return true;
        }
    }
}
