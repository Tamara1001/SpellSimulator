using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using SpellSimulator.Net;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Genera el avatar de red (Resources/Net/NetPlayer.prefab): capsula de cuerpo, cabeza, dos manos y un cartel con el nombre.
    /// Es lo que ven los demas jugadores (F1 de MULTIPLAYER_PLAN). Idempotente: se puede volver a correr.
    /// Menu: SpellSimulator/Net/Crear avatar de red.
    /// </summary>
    public static class NetSetup
    {
        private const string Folder = "Assets/_Project/Resources/Net";
        private const string PrefabPath = Folder + "/NetPlayer.prefab";
        private const string MaterialPath = Folder + "/NetAvatar.mat";

        [MenuItem("SpellSimulator/Net/Crear avatar de red")]
        public static void BuildAvatar()
        {
            Directory.CreateDirectory(Folder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetColor("_BaseColor", new Color(0.8f, 0.7f, 1f));
            material.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(material);

            var root = new GameObject("NetPlayer");
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<ClientNetworkTransform>();
            nt.SyncPositionX = nt.SyncPositionY = nt.SyncPositionZ = true;
            nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false; nt.SyncRotAngleY = true;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;

            var body = Part(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 0f, 0f), new Vector3(0.6f, 0.85f, 0.6f), material);
            var head = Part(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.7f, 0f), Vector3.one * 0.28f, material);
            var nose = Part(head.transform, "Nose", PrimitiveType.Cube, new Vector3(0f, 0f, 0.55f), new Vector3(0.18f, 0.18f, 0.45f), material);
            var main = Part(root.transform, "HandMain", PrimitiveType.Sphere, new Vector3(0.3f, 0.2f, 0.3f), Vector3.one * 0.12f, material);
            var second = Part(root.transform, "HandSecondary", PrimitiveType.Sphere, new Vector3(-0.3f, 0.2f, 0.3f), Vector3.one * 0.12f, material);

            var labelGo = new GameObject("Name");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var label = labelGo.AddComponent<TextMesh>();
            label.text = "Mago"; label.characterSize = 0.06f; label.fontSize = 64;
            label.anchor = TextAnchor.LowerCenter; label.alignment = TextAlignment.Center; label.color = Color.white;
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font != null ? label.font.material : null;

            var player = root.AddComponent<NetPlayer>();
            var so = new SerializedObject(player);
            so.FindProperty("head").objectReferenceValue = head.transform;
            so.FindProperty("handMain").objectReferenceValue = main.transform;
            so.FindProperty("handSecondary").objectReferenceValue = second.transform;
            so.FindProperty("nameLabel").objectReferenceValue = label;
            var renderers = so.FindProperty("bodyRenderers");
            var list = new[] { body, head, nose, main, second };
            renderers.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = list[i].GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Net] Avatar de red generado: " + PrefabPath);
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // el avatar no estorba a la fisica (F2 decide si tiene cuerpo)
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
