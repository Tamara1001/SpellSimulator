using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Output;
using SpellSimulator.Reactions;

namespace SpellSimulator.World
{
    /// <summary>
    /// Cuerpo cristalizado por el hechizo de cristal: queda rigido en el aire (cinematico), tenido de celeste hielo e
    /// indestructible durante unos segundos; su collider sigue solido (pared, puente, escalon). Se agrega en runtime,
    /// guarda lo original y lo restaura al terminar o al reiniciar el mundo. Maximo <see cref="MaxActive"/> a la vez.
    /// </summary>
    public class Crystallized : MonoBehaviour
    {
        public const int MaxActive = 40;
        private const float TintAmount = 0.6f;
        private static readonly Color IceColor = new Color(0.65f, 0.9f, 1f, 1f);
        private static readonly Color IceGlow = new Color(0.12f, 0.3f, 0.4f, 1f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private static readonly List<Crystallized> active = new List<Crystallized>();

        public static int ActiveCount => active.Count;

        private Rigidbody body;
        private bool wasKinematic;
        private CollisionDetectionMode wasDetection;
        private ElementReactive reactive;
        private Renderer[] renderers;
        private MaterialPropertyBlock[] originals;
        private MaterialPropertyBlock scratch;
        private ElementLook look;
        private float remaining;
        private bool frozen;

        /// <summary>Cristaliza el cuerpo <paramref name="rb"/> durante <paramref name="seconds"/>. Si ya lo estaba, reinicia el tiempo.</summary>
        public static Crystallized Apply(Rigidbody rb, float seconds, ElementLook shardLook)
        {
            if (rb == null) return null;
            var c = rb.GetComponent<Crystallized>();
            if (c == null) c = rb.gameObject.AddComponent<Crystallized>();
            c.Begin(seconds, shardLook, rb);
            return c;
        }

        private void Begin(float seconds, ElementLook shardLook, Rigidbody rb)
        {
            remaining = seconds;
            if (frozen) return;
            // Tope de cuerpos: los mas viejos terminan primero.
            while (active.Count >= MaxActive) active[0].End(false);

            body = rb;
            look = shardLook;
            wasKinematic = rb.isKinematic;
            wasDetection = rb.collisionDetectionMode;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            // Un cinematico no admite deteccion continua dinamica.
            if (rb.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic)
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            reactive = rb.GetComponentInChildren<ElementReactive>();
            if (reactive != null) reactive.Crystallized = true;

            scratch = new MaterialPropertyBlock();
            var all = rb.GetComponentsInChildren<Renderer>();
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] is MeshRenderer || all[i] is SkinnedMeshRenderer) n++;
            renderers = new Renderer[n];
            originals = new MaterialPropertyBlock[n];
            n = 0;
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                renderers[n] = r;
                originals[n] = new MaterialPropertyBlock();
                r.GetPropertyBlock(originals[n]);
                Tint(r);
                n++;
            }
            frozen = true;
            active.Add(this);
        }

        private void Tint(Renderer r)
        {
            Color baseColor = Color.white;
            var mat = r.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorId)) baseColor = mat.GetColor(BaseColorId);
                else if (mat.HasProperty(ColorId)) baseColor = mat.GetColor(ColorId);
            }
            Color tinted = Color.Lerp(baseColor, IceColor, TintAmount);
            tinted.a = baseColor.a;
            r.GetPropertyBlock(scratch);
            scratch.SetColor(BaseColorId, tinted);
            scratch.SetColor(ColorId, tinted);
            scratch.SetColor(EmissionId, IceGlow);
            r.SetPropertyBlock(scratch);
        }

        private void OnEnable() => MagicWorld.WorldReset += OnWorldReset;
        private void OnDisable() => MagicWorld.WorldReset -= OnWorldReset;

        private void OnWorldReset() => End(false);

        private void Update()
        {
            if (!frozen) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f) End(true);
        }

        private void OnDestroy()
        {
            if (frozen) Restore();
        }

        /// <summary>Descristaliza: vuelve a su fisica y color originales, con esquirlas si <paramref name="shards"/>.</summary>
        public void End(bool shards)
        {
            if (!frozen) { Destroy(this); return; }
            Vector3 center = body != null ? body.worldCenterOfMass : transform.position;
            var shardLook = look;
            Restore();
            if (shards && shardLook != null) CrystalShards.Burst(center, shardLook);
            Destroy(this);
        }

        private void Restore()
        {
            frozen = false;
            active.Remove(this);
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    renderers[i].SetPropertyBlock(originals[i]);
                }
            }
            if (reactive != null) reactive.Crystallized = false; // repinta con su propio aspecto
            if (body != null)
            {
                // Si el jugador lo agarro mientras duraba, queda como lo dejo el agarre (cinematico).
                if (!wasKinematic && body.isKinematic && !IsHeld(body.transform))
                {
                    body.isKinematic = false;
                    body.WakeUp();
                }
                body.collisionDetectionMode = wasDetection;
            }
        }

        private static bool IsHeld(Transform t)
        {
            return Player.PlayerHeld.IsOnAnyHead(t);
        }
    }
}
