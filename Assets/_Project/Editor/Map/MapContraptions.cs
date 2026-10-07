using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Sellos en objetos (M6): piedras de impacto que se arrojan y placas de presion con una pesa. Cada una es una copia
    /// del papel del laboratorio puesta de cara al cielo, sobre una losa, con un SealTrigger que deja el sello armado.
    /// </summary>
    public static class MapContraptions
    {
        public static void Build(Transform map, Material material, Vector3 origin)
        {
            var template = GameObject.Find("PaperCanvas");
            if (template == null) return;
            var caster = Object.FindFirstObjectByType<SpellCaster>();
            var stoneProfile = AssetDatabase.LoadAssetAtPath<MaterialProfile>("Assets/_Project/Data/Materials/Piedra.asset");

            // Dos piedras de impacto (mediana y chica) y una placa de presion con su pesa.
            Slab(template, material, caster, stoneProfile, "ImpactStone_M", origin + new Vector3(-0.6f, 0.2f, 0f), 0.34f, 0.07f, false, SealTrigger.Mode.Impact, 3f);
            Slab(template, material, caster, stoneProfile, "ImpactStone_S", origin + new Vector3(0.15f, 0.2f, 0f), 0.2f, 0.06f, false, SealTrigger.Mode.Impact, 3f);
            Slab(template, material, caster, stoneProfile, "PressurePlate", origin + new Vector3(0f, 0.1f, 2.2f), 0.9f, 0.1f, true, SealTrigger.Mode.Pressure, 1.5f);
            Weight(map, material, stoneProfile, origin + new Vector3(1.0f, 0.3f, 0f));
        }

        private static void Slab(GameObject template, Material material, SpellCaster caster, MaterialProfile profile, string name,
            Vector3 position, float size, float thickness, bool isFixed, SealTrigger.Mode mode, float threshold)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var go = Object.Instantiate(template);
            go.name = name;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(-90f, 0f, 0f));
            float depth = template.transform.localScale.z;
            go.transform.localScale = new Vector3(size, size, depth);

            // Losa debajo del lienzo. El hijo compensa la escala no uniforme del padre.
            var visual = new GameObject("Slab");
            visual.transform.SetParent(go.transform, false);
            var mesh = new LowPolyMesh();
            mesh.Box(Matrix4x4.identity, Vector3.one, new Color(0.55f, 0.55f, 0.58f), new Color(0.7f, 0.7f, 0.72f));
            visual.AddComponent<MeshFilter>().sharedMesh = MapBuilder.SaveMesh(mesh.Build("Slab"));
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            float childZ = thickness / depth;
            visual.transform.localScale = new Vector3(1f, 1f, childZ);
            visual.transform.localPosition = new Vector3(0f, 0f, -childZ * 0.5f - 0.002f / depth);
            var box = visual.AddComponent<BoxCollider>();
            box.size = Vector3.one;

            var rb = go.GetComponent<Rigidbody>();
            rb.mass = isFixed ? 50f : 1.5f + size * 4f;
            rb.isKinematic = isFixed;
            rb.useGravity = true;
            rb.linearDamping = 0.05f;

            var grab = go.GetComponent<PhysicalCanvas>();
            if (isFixed && grab != null) Object.DestroyImmediate(grab);
            else if (grab != null)
            {
                var gso = new SerializedObject(grab);
                gso.FindProperty("settleFlat").boolValue = false;
                gso.FindProperty("objectName").stringValue = "piedra con sello";
                gso.ApplyModifiedProperties();
            }

            var canvas = go.GetComponent<DrawingCanvas>();
            var cso = new SerializedObject(canvas);
            cso.FindProperty("emissionSide").enumValueIndex = (int)EmissionSide.InkSide;
            cso.ApplyModifiedProperties();

            var reactive = go.GetComponent<ElementReactive>();
            if (reactive != null && profile != null)
            {
                var rso = new SerializedObject(reactive);
                rso.FindProperty("profile").objectReferenceValue = profile;
                rso.ApplyModifiedProperties();
            }
            // Una piedra no pierde el sello por mojarse o arder.
            var breaker = go.GetComponent<SealBreaker>();
            if (breaker != null) Object.DestroyImmediate(breaker);

            var trigger = go.AddComponent<SealTrigger>();
            trigger.Configure(mode, caster, threshold);
            if (go.GetComponent<Respawnable>() == null) go.AddComponent<Respawnable>();
            Undo.RegisterCreatedObjectUndo(go, name);
        }

        private static void Weight(Transform map, Material material, MaterialProfile profile, Vector3 position)
        {
            var old = GameObject.Find("Weight");
            if (old != null) Object.DestroyImmediate(old);
            var mesh = new LowPolyMesh();
            mesh.Box(Matrix4x4.identity, new Vector3(0.45f, 0.45f, 0.45f), new Color(0.42f, 0.42f, 0.45f), new Color(0.55f, 0.55f, 0.58f));
            var go = MapBuilder.Place("Weight", map, MapBuilder.SaveMesh(mesh.Build("Weight")), material, position, Quaternion.identity);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 6f;
            go.AddComponent<BoxCollider>().size = new Vector3(0.45f, 0.45f, 0.45f);
            go.AddComponent<Respawnable>();
            var reactive = go.AddComponent<ElementReactive>();
            if (profile != null)
            {
                var so = new SerializedObject(reactive);
                so.FindProperty("profile").objectReferenceValue = profile;
                so.ApplyModifiedProperties();
            }
        }
    }
}
