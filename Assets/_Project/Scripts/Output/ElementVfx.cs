using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Construye sistemas Shuriken con el look de un elemento: pistas del hechizo, capas que las acompanian (espuma,
    /// polvo, hojas, estelas), impactos como sub-emisores al chocar y rafagas sueltas (polvo de las rocas que brotan).
    /// Todo se arma una vez al lanzar; nada aloca por frame. Presupuesto Quest: maxParticles del look.
    /// </summary>
    public static class ElementVfx
    {
        /// <summary>Capas que no chocan: el jugador, los triggers y la UI.</summary>
        private static int CollisionMask => ~LayerMask.GetMask("Ignore Raycast", "Player", "UI", "TransparentFX");

        private static readonly List<ParticleSystemVertexStream> MeshStreams = new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
        };

        public static ParticleSystem Create(string name, Transform parent, ElementLook look, Vector3 position, Quaternion rotation,
            float scale, bool local = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            Apply(ps, look, scale);
            return ps;
        }

        /// <summary>Aplica el look (material, malla, color, tamanio, gravedad, giro, estelas) a un sistema ya armado.</summary>
        public static void Apply(ParticleSystem ps, ElementLook look, float scale)
        {
            var main = ps.main;
            main.maxParticles = Mathf.Max(8, Mathf.RoundToInt(look.maxParticles * SpellSimulator.Rendering.GraphicsQuality.ParticleScale));
            main.startSize = new ParticleSystem.MinMaxCurve(look.size * scale * 1.2f, look.size * scale * 2.4f);
            main.startColor = Color.white;
            main.gravityModifier = look.gravity;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(look.color);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = look.sizeOverLife != null && look.sizeOverLife.length >= 2
                ? new ParticleSystem.MinMaxCurve(1f, look.sizeOverLife)
                : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));

            var noise = ps.noise;
            noise.enabled = look.turbulence > 0f;
            if (look.turbulence > 0f)
            {
                noise.strength = look.turbulence * scale;
                noise.frequency = 1.2f;
                noise.quality = ParticleSystemNoiseQuality.Low;
            }

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = look.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;

            float spin = look.spin * Mathf.Deg2Rad;
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = false;
            main.startRotation3D = false;
            main.startRotation = 0f;
            if (look.mesh != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = look.mesh;
                renderer.alignment = ParticleSystemRenderSpace.World;
                renderer.SetActiveVertexStreams(MeshStreams);
                main.startRotation3D = true;
                var full = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationX = full;
                main.startRotationY = full;
                main.startRotationZ = full;
                // Variacion de tono entre esquirlas (el material multiplica el color por vertice).
                main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.72f, 0.7f, 0.68f, 1f));
                rotation.enabled = spin > 0f;
                rotation.separateAxes = true;
                rotation.x = new ParticleSystem.MinMaxCurve(-spin, spin);
                rotation.y = new ParticleSystem.MinMaxCurve(-spin, spin);
                rotation.z = new ParticleSystem.MinMaxCurve(-spin, spin);
            }
            else if (spin > 0f)
            {
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                rotation.enabled = true;
                rotation.separateAxes = false;
                rotation.z = new ParticleSystem.MinMaxCurve(-spin, spin);
            }
            else if (look.stretch > 0f)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = look.stretch * 0.05f;
                renderer.lengthScale = 1.5f;
            }

            var trails = ps.trails;
            trails.enabled = look.trails;
            if (look.trails)
            {
                // Solo la estela: la cabeza no se dibuja (las curvas las dan el ruido y la orbita de la pista).
                renderer.renderMode = ParticleSystemRenderMode.None;
                renderer.trailMaterial = look.material;
                trails.mode = ParticleSystemTrailMode.PerParticle;
                trails.ratio = 1f;
                trails.lifetime = look.trailLifetime;
                trails.minVertexDistance = Mathf.Max(0.02f, 0.06f * scale);
                trails.worldSpace = true;
                trails.dieWithParticles = true;
                trails.sizeAffectsWidth = false;
                trails.inheritParticleColor = true;
                trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
                // Ruido lento: curvas largas y suaves en vez de zigzag.
                noise.frequency = 0.45f;
                noise.quality = ParticleSystemNoiseQuality.Medium;
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(look.size * scale, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));
            }
        }

        /// <summary>Capa que acompania una pista: copia forma y movimiento, cambia el look. Hermana de la pista.</summary>
        public static ParticleSystem AddLayer(ParticleSystem track, ElementLook layer, float scale)
        {
            var go = Object.Instantiate(track.gameObject, track.transform.parent);
            go.name = $"{track.name}_{layer.name}";
            go.transform.SetPositionAndRotation(track.transform.position, track.transform.rotation);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Apply(ps, layer, scale);
            var main = ps.main;
            if (!Mathf.Approximately(layer.layerSpeed, 1f))
            {
                var speed = main.startSpeed;
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed.constantMin * layer.layerSpeed, speed.constantMax * layer.layerSpeed);
            }
            return ps;
        }

        /// <summary>
        /// Impactos del look como sub-emisores: colision con el mundo en calidad baja (la particula muere al chocar) y,
        /// si el look se desmorona, tambien al morir en el aire.
        /// </summary>
        public static void AddImpacts(ParticleSystem track, ElementLook look, float scale)
        {
            if (look.impacts == null || look.impacts.Length == 0) return;
            var collision = track.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.collidesWith = CollisionMask;
            collision.enableDynamicColliders = false;
            collision.maxCollisionShapes = 16;
            collision.radiusScale = 0.4f;
            collision.lifetimeLoss = 1f;
            collision.bounce = 0.1f;
            collision.dampen = 0.6f;

            var subs = track.subEmitters;
            subs.enabled = true;
            foreach (var impact in look.impacts)
            {
                if (impact == null || impact.material == null) continue;
                var sub = CreateImpact(track.transform, impact, scale);
                subs.AddSubEmitter(sub, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing,
                    Mathf.Clamp01(impact.impactChance));
                if (look.crumble)
                    subs.AddSubEmitter(sub, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing,
                        Mathf.Clamp01(impact.impactChance * 0.5f));
            }
        }

        private static ParticleSystem CreateImpact(Transform track, ElementLook impact, float scale)
        {
            // Siempre hacia arriba: salpica y levanta polvo aunque el chorro venga de costado.
            var ps = Create($"Impact_{impact.name}", track, impact, track.position, Quaternion.LookRotation(Vector3.up), Mathf.Max(scale, 0.3f));
            var main = ps.main;
            main.loop = false;
            main.startSpeed = new ParticleSystem.MinMaxCurve(impact.speed * 0.6f, impact.speed * 1.2f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(impact.lifetime * 0.7f, impact.lifetime * 1.2f);
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, impact.impactCount)) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.05f * scale;
            return ps;
        }

        /// <summary>Rafaga suelta que se borra sola (polvo y cascotes de las rocas que brotan).</summary>
        public static ParticleSystem OneShot(string name, ElementLook look, Vector3 position, Vector3 up, float radius, int count,
            float sizeScale)
        {
            if (look == null || look.material == null || count <= 0) return null;
            var ps = Create(name, null, look, position, Quaternion.LookRotation(up), sizeScale);
            var main = ps.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startSpeed = new ParticleSystem.MinMaxCurve(look.speed * 0.5f, look.speed * 1.1f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(look.lifetime * 0.7f, look.lifetime * 1.3f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = Mathf.Max(0.02f, radius);
            shape.radiusThickness = 0.2f;
            ps.Play();
            ps.Emit(count);
            return ps;
        }
    }
}
