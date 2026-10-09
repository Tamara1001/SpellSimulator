using UnityEngine;
using SpellSimulator.Output;

namespace SpellSimulator.World
{
    /// <summary>Destello de esquirlas de cristal al descristalizar (una rafaga de particulas que se autodestruye).</summary>
    public static class CrystalShards
    {
        public static void Burst(Vector3 position, ElementLook look)
        {
            if (look == null || look.material == null) return;
            var go = new GameObject("CristalEsquirlas");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.7f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(look.size * 0.5f, look.size);
            main.startColor = new Color(0.75f, 0.95f, 1f, 1f);
            main.gravityModifier = 0.8f;
            main.maxParticles = 16;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 14) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = look.material;
            if (look.mesh != null) { rend.renderMode = ParticleSystemRenderMode.Mesh; rend.mesh = look.mesh; }
            ps.Play();
        }
    }
}
