using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Output;
using SpellSimulator.Reactions;

namespace SpellSimulator.EditorTools
{
    /// <summary>Captura una matriz de elementos y signos usando la salida visual real del juego.</summary>
    public static class HarnessModifierMatrix
    {
        private const string FlagKey = "HarnessModifierMatrix.Running";
        private static readonly string ResultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "harness-modifier-matrix.txt"));
        private static readonly string CaptureDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Capturas"));
        private static IEnumerator routine;
        private static readonly StringBuilder report = new StringBuilder();
        private static int startFrame = -1;
        private static int captureCount, extraCount;

        private struct Variant
        {
            public string Id;
            public float Value;
            public Variant(string id, float value) { Id = id; Value = value; }
        }

        private static readonly Variant[] Variants =
        {
            new Variant("base", 0f), new Variant("crush", 1f), new Variant("orb", 1f),
            new Variant("billowing", 1f), new Variant("cool", 1f), new Variant("empower", 1f),
            new Variant("entwine", 1f), new Variant("focus", 1f), new Variant("gather", 1f),
            new Variant("collection", 1f), new Variant("repetition", 1f), new Variant("weave", 1f),
            new Variant("spread_high", 0f), new Variant("gravity_low", 0f),
            // Extras (no cuentan en n=84): region, tinta resistente, estabilidad baja.
            new Variant("region", 1f), new Variant("waterproof", 1f), new Variant("stability_low", 0.2f),
            new Variant("float", 1f), new Variant("levitation", 1f)
        };

        private static bool IsExtra(string id) => id == "region" || id == "waterproof" || id == "stability_low" || id == "float" || id == "levitation";

        [MenuItem("SpellSimulator/Harness/Modifier Matrix")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ResultPath)));
            Directory.CreateDirectory(CaptureDirectory);
            File.WriteAllText(ResultPath, "RUNNING");
            SessionState.SetBool(FlagKey, true);
            startFrame = -1;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.update += Tick;

        private static void Tick()
        {
            if (routine != null)
            {
                bool more;
                try { more = routine.MoveNext(); }
                catch (Exception e) { report.AppendLine("EXCEPCION " + e); more = false; }
                if (!more)
                {
                    routine = null;
                    File.WriteAllText(ResultPath, "DONE n=" + captureCount + " extras=" + extraCount + "\n" + report);
                    EditorApplication.isPlaying = false;
                }
                return;
            }
            if (!SessionState.GetBool(FlagKey, false) || !EditorApplication.isPlaying) return;
            if (startFrame < 0) { startFrame = Time.frameCount; return; }
            if (Time.frameCount - startFrame < 40) return;
            SessionState.SetBool(FlagKey, false);
            report.Clear();
            captureCount = 0;
            extraCount = 0;
            routine = CaptureMatrix();
        }

        private static IEnumerator CaptureMatrix()
        {
            var caster = UnityEngine.Object.FindFirstObjectByType<SpellCaster>();
            var output = UnityEngine.Object.FindFirstObjectByType<SpellOutputController>();
            var canvas = UnityEngine.Object.FindFirstObjectByType<DrawingCanvas>();
            if (caster == null || output == null || canvas == null)
            {
                report.AppendLine("FALTA SpellCaster, SpellOutputController o DrawingCanvas en la escena Play.");
                yield break;
            }

            // El caster se comprueba para asegurar que el pipeline activo de la escena esta presente.
            caster.GetType();
            foreach (var element in new[]
            {
                new { Id = "fuego", Type = ElementType.Fire, Sigil = "fire" },
                new { Id = "agua", Type = ElementType.Water, Sigil = "water" },
                new { Id = "viento", Type = ElementType.Wind, Sigil = "wind" },
                new { Id = "tierra", Type = ElementType.Earth, Sigil = "earth" },
                new { Id = "cristal", Type = ElementType.Earth, Sigil = SpellOutputController.CrystalSigil },
                new { Id = "luz", Type = ElementType.Light, Sigil = "light" }
            })
            {
                string onlyEl = SessionState.GetString("HarnessModifierMatrix.OnlyElements", "");
                if (onlyEl.Length > 0 && !(";" + onlyEl + ";").Contains(";" + element.Id + ";")) continue;
                string onlyVar = SessionState.GetString("HarnessModifierMatrix.OnlyVariants", "");
                var hashesForElement = new Dictionary<string, string>();
                foreach (var variant in Variants)
                {
                    if (onlyVar.Length > 0 && !(";" + onlyVar + ";").Contains(";" + variant.Id + ";")) continue;
                    var spell = CreateSpell(element.Type, element.Sigil, variant);
                    SnapshotColliders();
                    var manifestation = output.SpawnSpellEffect(spell, canvas, new Vector2(0.5f, 0.5f));
                    if (manifestation == null)
                    {
                        report.AppendLine($"{element.Id}_{variant.Id} ERROR sin manifestacion");
                        continue;
                    }
                    // float/levitation: una caja de 1 kg sobre el sello; se espera en tiempo real y se informa a que altura queda.
                    Rigidbody box = null;
                    if (variant.Id == "float" || variant.Id == "levitation")
                    {
                        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.transform.localScale = Vector3.one * 0.2f;
                        cube.transform.position = manifestation.Frame.Origin + manifestation.Frame.Normal * 0.12f;
                        box = cube.AddComponent<Rigidbody>();
                        box.mass = 1f;
                        float until0 = Time.time + 1.9f; // tiempo de simulacion (no real): el Editor sin foco corre la fisica mas lento
                        float maxH = 0f;
                        while (Time.time < until0 && manifestation != null)
                        {
                            maxH = Mathf.Max(maxH, Vector3.Dot(box.position - manifestation.Frame.Origin, manifestation.Frame.Normal));
                            yield return null;
                        }
                        report.AppendLine($"  caja max {maxH:F2} m");
                        if (manifestation != null)
                            { string cajaTxt = $"  caja {variant.Id}: altura {Vector3.Dot(box.position - manifestation.Frame.Origin, manifestation.Frame.Normal):F2} m, vel {box.linearVelocity.magnitude:F2} m/s"; report.AppendLine(cajaTxt); File.AppendAllText(ResultPath + ".live", element.Id + " " + cajaTxt + "\n"); }
                    }
                    if (manifestation == null) { report.AppendLine($"{element.Id}_{variant.Id} ERROR manifestacion destruida"); continue; }
                    manifestation.FastForward(SpellManifestation.ChargeSeconds + SpellManifestation.StrikeSeconds + 0.7f);
                    yield return null;
                    // Tierra y cristal son cuerpos fisicos que nacen/caen en tiempo real: se espera para ver donde quedan (anclaje al suelo).
                    if (element.Type == ElementType.Earth)
                    {
                        float until = Time.realtimeSinceStartup + 1.6f;
                        while (Time.realtimeSinceStartup < until) yield return null;
                    }
                    if (manifestation == null) { report.AppendLine($"{element.Id}_{variant.Id} ERROR manifestacion destruida"); continue; }

                    string file = Path.GetFullPath(Path.Combine(CaptureDirectory, $"mod_{element.Id}_{variant.Id}.png"));
                    string hash = Capture(file, manifestation.Frame.Origin, manifestation.Frame.Normal, manifestation);
                    string duplicate = hashesForElement.TryGetValue(hash, out string original) ? " DUPLICADO_DE=" + original : string.Empty;
                    if (duplicate.Length == 0) hashesForElement.Add(hash, variant.Id);
                    report.AppendLine($"{element.Id}_{variant.Id} {hash}{duplicate}");
                    if (IsExtra(variant.Id)) extraCount++; else captureCount++;
                    UnityEngine.Object.Destroy(manifestation.gameObject);
                    DestroyNewBodies();
                    // No se llama a ResetWorld: destruia el DrawingCanvas y rompia las capturas siguientes.
                    yield return null;
                }
            }
        }

        private static SpellIR CreateSpell(ElementType element, string sigil, Variant variant)
        {
            var plan = new SpellPlan
            {
                Mode = PlanMode.Create,
                Aim = new Vector3(0f, 0.15f, 1f),
                Budget = 1f,
                Focus = 1f,
                Quality = 1f,
                Primitives = PlanPrimitive.Burst | PlanPrimitive.Jet
            };
            var spell = new SpellIR
            {
                valid = true, active = true, prepared = false,
                status = "Hechizo de matriz", warnings = new string[0],
                element = element, sigilId = sigil, elementConfidence = 1f,
                primarySizeNorm = 1f, sizeRatio = 1f, strength = 1f, effectScale = 1f,
                primaryManifestation = Manifestations.Column, manifestations = new ManifestationStrength[0],
                force = 0.7f, spread = 0.25f, focus = 0.5f, range = 0.6f, gravity = 1f,
                duration = 4f, stability = 1f, quality = 1f, neatness = 1f,
                direction = Vector3.forward, directionCoherence = 1f, activatedAt = Time.time,
                plan = plan, sealRadius = 0.35f, inkPower = 1f, waterproof = false,
                analysisBoost = 1f, analysisPivot = new Vector2(0.5f, 0.5f)
            };

            switch (variant.Id)
            {
                case "crush": plan.Crush = variant.Value; break;
                case "orb": plan.Orb = variant.Value; break;
                case "billowing": plan.Billow = variant.Value; break;
                case "cool": plan.Cool = variant.Value; break;
                case "empower": plan.Empower = variant.Value; break;
                case "entwine": plan.Entwine = variant.Value; break;
                case "focus": plan.Target = variant.Value; plan.Focus = 2f; break;
                case "gather": plan.Gather = variant.Value; break;
                case "collection": plan.Collection = variant.Value; break;
                case "repetition": plan.Repetition = variant.Value; break;
                case "weave": plan.Weave = variant.Value; break;
                case "spread_high": spell.spread = 1f; break;
                case "gravity_low": spell.gravity = 0.1f; break;
                case "region": plan.Directed = 1f; break;
                case "waterproof": spell.waterproof = true; break;
                case "stability_low": spell.stability = variant.Value; break;
                case "float":
                case "levitation":
                    plan.Aim = Vector3.zero; plan.Budget = 0f; plan.Primitives = PlanPrimitive.Burst;
                    plan.Signs.Add(new SignReading { Id = variant.Id, Manifestation = Manifestations.Levitation, At = Vector2.zero, Length = 1f, Facing = Vector2.up, FacingTrust = 1f });
                    break;
            }
            return spell;
        }

        private const int IsolatedLayer = 29;
        private static readonly HashSet<int> preexisting = new HashSet<int>();
        private static Vector3 snapshotCenter;

        /// <summary>Anota los colliders que ya existen cerca del sello para distinguir lo que crea el hechizo (rocas, muros, cristales).</summary>
        private static void SnapshotColliders()
        {
            var canvas = UnityEngine.Object.FindFirstObjectByType<DrawingCanvas>();
            snapshotCenter = canvas != null ? canvas.transform.position : Vector3.zero;
            preexisting.Clear();
            foreach (var col in Physics.OverlapSphere(snapshotCenter, 12f, ~0, QueryTriggerInteraction.Collide))
                preexisting.Add(col.GetInstanceID());
        }

        /// <summary>Borra los cuerpos que dejo el hechizo para que no contaminen la captura siguiente.</summary>
        private static void DestroyNewBodies()
        {
            foreach (var col in Physics.OverlapSphere(snapshotCenter, 12f, ~0, QueryTriggerInteraction.Collide))
            {
                if (col == null || preexisting.Contains(col.GetInstanceID())) continue;
                if (col.GetComponentInParent<DrawingCanvas>() != null) continue;
                UnityEngine.Object.Destroy(col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        private static string Capture(string path, Vector3 target, Vector3 normal, SpellManifestation manifestation)
        {
            const int width = 640, height = 360;
            var go = new GameObject("HarnessModifierMatrixCam");
            var texture = new RenderTexture(width, height, 24);
            Texture2D image = null;
            GameObject ground = null;
            Material groundMaterial = null;
            var movedLayers = new List<(GameObject go, int layer)>();
            var previous = RenderTexture.active;
            try
            {
                var camera = go.AddComponent<Camera>();
                // Vista LATERAL al eje del chorro (mirando de frente se ve solo la boca del efecto). Se prueba cada lado y distancia
                // hasta encontrar una linea de vision sin paredes ni muebles (la torre es angosta).
                Vector3 n = normal.normalized;
                Vector3 side = Vector3.Cross(n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 focus = target + n * 0.4f;
                float reach = 1.5f;
                // Encuadre por los limites reales de las particulas vivas (si las hay).
                bool hasBounds = false;
                Bounds live = new Bounds(focus, Vector3.zero);
                foreach (var ps in manifestation.GetComponentsInChildren<ParticleSystem>())
                {
                    if (ps.particleCount == 0) continue;
                    var r = ps.GetComponent<ParticleSystemRenderer>();
                    if (r == null) continue;
                    if (!hasBounds) { live = r.bounds; hasBounds = true; } else live.Encapsulate(r.bounds);
                }
                if (hasBounds)
                {
                    focus = live.center;
                    if (Vector3.Dot(focus - target, n) < 0.15f) focus = target + n * 0.4f;
                    reach = Mathf.Clamp(live.extents.magnitude * 2.2f, 1.0f, 6f);
                }
                string boundsText = hasBounds ? live.ToString() : "sin particulas";
                report.AppendLine("  bounds=" + boundsText + " origen=" + target + " normal=" + normal);
                // Aislado: solo se renderiza la capa del efecto sobre un fondo liso y un disco de suelo en el plano del sello,
                // asi arboles, postes y paredes no tapan nada (y se ve si el efecto queda hundido bajo el plano).
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.25f, 1f);
                camera.cullingMask = 1 << IsolatedLayer;
                camera.nearClipPlane = 0.05f;
                ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.layer = IsolatedLayer;
                ground.transform.SetPositionAndRotation(target - n * 0.01f, Quaternion.FromToRotation(Vector3.up, n));
                ground.transform.localScale = new Vector3(reach * 3f, 0.01f, reach * 3f);
                var groundShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                groundMaterial = new Material(groundShader);
                groundMaterial.color = new Color(0.36f, 0.5f, 0.3f, 1f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                SetLayerRecursively(manifestation.gameObject, IsolatedLayer);
                // Cuerpos fisicos del hechizo (rocas, cristales, agua) cerca del sello: se mueven un momento a la capa aislada.
                foreach (var col in Physics.OverlapSphere(target, 3.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (col == null || col.gameObject.layer == IsolatedLayer) continue;
                    if (preexisting.Contains(col.GetInstanceID()) || col.bounds.size.magnitude > 8f) continue; // solo lo que creo el hechizo
                    if (col.GetComponentInParent<SpellManifestation>() != null) continue;
                    movedLayers.Add((col.gameObject, col.gameObject.layer));
                    col.gameObject.layer = IsolatedLayer;
                }
                Vector3 position = focus + side * reach + Vector3.up * (reach * 0.3f);
                go.transform.position = position;
                go.transform.LookAt(focus);
                camera.fieldOfView = 42f;
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                byte[] bytes = image.EncodeToPNG();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
                using (var md5 = MD5.Create())
                    return BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                foreach (var moved in movedLayers) if (moved.go != null) moved.go.layer = moved.layer;
                if (ground != null) UnityEngine.Object.DestroyImmediate(ground);
                if (groundMaterial != null) UnityEngine.Object.DestroyImmediate(groundMaterial);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
