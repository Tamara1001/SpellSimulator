using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Cuerpo fisico del hechizo de AGUA (octava tanda, seccion 3). El empuje del chorro/caudal ya lo hace
    /// <see cref="MagicField"/> y el mojar/apagar/romper sellos, MagicWorld; aca se agrega:
    /// - Gotas que chocan con el mundo estatico (modulo de colision de las pistas Jet/Fan/Burst + <see cref="WaterDropSink"/>):
    ///   forman charcos (<see cref="WaterPuddleManager"/>) y llenan recipientes (<see cref="WaterContainer"/>, modulo Trigger).
    /// - Tinta poderosa: multiplica el caudal de particulas (con el tope maxParticles del look), el volumen de cada gota
    ///   y la vida de los charcos y de la zona inundada. La tinta resistente no cambia nada.
    /// - Hechizo grande y fuerte apuntado al suelo: una <see cref="FloodZone"/> temporal donde flota lo liviano.
    /// Todo se arma una vez al lanzar.
    /// </summary>
    public static class WaterSpell
    {
        private const float MaxFloodReach = 10f;
        private const float FloodSizeFactor = 6f;
        private const float MinGroundNormalY = 0.6f;

        public static void Spawn(SpellIR spell, SealFrame frame, ElementLook look, DrawingCanvas source, GameObject spellGo)
        {
            if (spellGo == null) return;
            float ink = spell.inkPower > 0f ? spell.inkPower : 1f;
            float flow = WaterMath.FlowMultiplier(ink);
            WaterPuddleManager.SetLook(look);

            int tracks = 0;
            foreach (var ps in spellGo.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!IsWaterTrack(ps.gameObject.name)) continue;
                ConfigureDrops(ps, ink, flow, null);
                tracks++;
            }

            bool flooded = TryFlood(spell, frame, look, spellGo, ink);
            Debug.Log($"[Agua] caudal x{flow:F1} | pistas con gotas {tracks} | charcos activos {WaterPuddleManager.ActiveCount} | " +
                      $"zona inundada {(flooded ? "si" : "no")} ({FloodZone.ActiveCount} vivas)");
        }

        /// <summary>Pistas que llevan el agua del hechizo (no la carga, ni las capas, ni los impactos).</summary>
        private static bool IsWaterTrack(string trackName) => trackName == "Jet" || trackName == "Fan" || trackName == "Burst";

        /// <summary>
        /// Prepara un sistema de gotas de agua: colision con el mundo estatico (la gota muere al chocar), mensajes de colision,
        /// modulo Trigger con la boca de cada recipiente y un <see cref="WaterDropSink"/>. Lo usan el hechizo y el derrame de los recipientes.
        /// </summary>
        public static void ConfigureDrops(ParticleSystem ps, float inkPower, float flowMultiplier, WaterContainer ignore)
        {
            if (ps == null) return;
            var collision = ps.collision;
            if (!collision.enabled)
            {
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.quality = ParticleSystemCollisionQuality.Low;
                collision.collidesWith = WaterPuddleManager.GroundMask;
                collision.maxCollisionShapes = 16;
                collision.radiusScale = 0.4f;
                collision.lifetimeLoss = 1f;
                collision.bounce = 0.1f;
                collision.dampen = 0.6f;
            }
            collision.enableDynamicColliders = false; // solo mundo estatico y terreno (decision cerrada)
            collision.sendCollisionMessages = true;

            var containers = WaterContainer.All;
            if (containers.Count > 0)
            {
                var trigger = ps.trigger;
                trigger.enabled = true;
                trigger.inside = ParticleSystemOverlapAction.Ignore;
                trigger.outside = ParticleSystemOverlapAction.Ignore;
                trigger.enter = ParticleSystemOverlapAction.Callback;
                trigger.exit = ParticleSystemOverlapAction.Ignore;
                trigger.radiusScale = 1f;
                for (int i = 0; i < containers.Count; i++)
                {
                    var c = containers[i];
                    if (c == null || c == ignore || c.Mouth == null) continue;
                    trigger.AddCollider(c.Mouth);
                }
            }

            var sink = ps.GetComponent<WaterDropSink>();
            if (sink == null) sink = ps.gameObject.AddComponent<WaterDropSink>();
            sink.Init(inkPower, flowMultiplier, ignore);
        }

        /// <summary>Si el hechizo es grande, fuerte y apunta al suelo, crea una zona inundada donde pega el agua.</summary>
        private static bool TryFlood(SpellIR spell, SealFrame frame, ElementLook look, GameObject spellGo, float ink)
        {
            var field = spellGo.GetComponent<MagicField>();
            Vector3 dir = field != null ? field.MainDirection : frame.Normal;
            if (!WaterMath.IsFloodSpell(frame.Radius, spell.force, Vector3.Dot(dir, Vector3.down))) return false;

            float reach = Mathf.Min(field != null ? field.ReachRadius : frame.Scale * 6f, MaxFloodReach);
            if (!Physics.Raycast(frame.Origin, dir, out RaycastHit hit, reach, WaterPuddleManager.GroundMask, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.normal.y < MinGroundNormalY || WaterPuddleManager.InsideWater(hit.point)) return false;

            float seconds = WaterMath.FloodSeconds(spell.force, ink);
            FloodZone.Create(hit.point, frame.Radius * FloodSizeFactor, seconds, SpellManifestation.ChargeSeconds, look);
            return true;
        }
    }
}
