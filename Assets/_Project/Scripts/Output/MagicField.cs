using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Efecto fisico de un hechizo sobre los Rigidbodies cercanos (incluido el jugador).
    /// Modelo de arrastre: cada primitiva define una velocidad de flujo y los cuerpos se aceleran HACIA ella (nunca mas
    /// rapido). Canon (ground-truth.md 2): la forma de los signos fija la velocidad (intensiva) y el area del sello fija
    /// el caudal (extensiva), asi que el area escala el acople, no la velocidad.
    /// Rafaga = cambio de velocidad radial al golpe; chorro = flujo axial; abanico = flujo radial sobre el plano;
    /// vortice = giro + ascenso; orbe = resorte que sostiene; succion = flujo hacia el sello.
    /// FixedUpdate con buffers preasignados (sin GC). Primera version del MagicWorld de SYSTEMS_DESIGN.
    /// </summary>
    [RequireComponent(typeof(SpellManifestation))]
    public class MagicField : MonoBehaviour
    {
        private const int MaxHits = 256;
        private static readonly Collider[] hits = new Collider[MaxHits];
        private static readonly Rigidbody[] bodies = new Rigidbody[MaxHits];

        /// <summary>Acople base (1/s): que tan rapido un cuerpo de 1 kg alcanza la velocidad del flujo.</summary>
        public const float BaseCoupling = 2.5f;
        /// <summary>Aceleracion maxima que puede imponer el campo (m/s2).</summary>
        public const float MaxAcceleration = 45f;
        /// <summary>Aceleracion maxima final que el viento puede imponer sobre un cuerpo (m/s2), evitando desbordes por windGain (FIS-01).</summary>
        public const float MaxWindAcceleration = WindMath.MaxWindAcceleration;
        /// <summary>Cambio de velocidad maximo de la rafaga (m/s).</summary>
        public const float MaxBurstDeltaV = 7f;
        /// <summary>Area de referencia: anillo de 0.3 m de radio.</summary>
        public const float ReferenceRadius = 0.3f;
        /// <summary>Ajustes de potencia por elemento (prueba manual: el viento casi no empujaba, el agua poco y el fuego era excesivo).</summary>
        public const float WindPower = 2.6f, WaterPower = 1.7f, FireReach = 0.7f;

        /// <summary>Campos vivos (para MagicWorld: reacciones de materiales).</summary>
        public static readonly List<MagicField> Active = new List<MagicField>();

        /// <summary>Elemento del hechizo.</summary>
        public ElementType Element => spell.element;
        /// <summary>Sigilo primario (variante de look, p. ej. cristal).</summary>
        public string SigilId => spell.sigilId;
        /// <summary>Objeto que lo emitio (el papel): no se afecta a si mismo.</summary>
        public GameObject Source { get; private set; }
        public SealFrame Frame => frame;
        /// <summary>Hacia donde va la magia: el chorro si hay, si no la normal del sello.</summary>
        public Vector3 MainDirection => plan != null && plan.Has(PlanPrimitive.Jet) ? frame.Direction(plan.Aim) : frame.Normal;
        /// <summary>Tiene succion (pull): atrae materia de su elemento.</summary>
        public bool HasIntake => plan != null && plan.Has(PlanPrimitive.Intake);
        /// <summary>Esfera que contiene todo el alcance del hechizo.</summary>
        public float ReachRadius { get; private set; }
        /// <summary>Fuerza del signo Crush (0 = no hay): destruye estructuras (tierra), rompe lo fragil y suma dano.</summary>
        public float CrushPower => plan != null ? plan.Crush : 0f;
        /// <summary>Multiplicador del dano a seres vivos por el signo Crush.</summary>
        public float DamageMultiplier => 1f + 0.5f * Mathf.Min(CrushPower, 3f);

        private SpellManifestation manifestation;
        private SpellIR spell;
        private SpellPlan plan;
        private SealFrame frame;
        private float flowSpeed;
        private float coupling;
        private float push;
        private float areaFactor;
        private bool burstApplied;
        private bool signsApplied;

        // Viento: arrastre cuadratico (WindMath.Drag) en vez del acople lineal; la tinta poderosa lo potencia y alarga el cono.
        private bool isWind;
        private float windGain;
        private float coneSpread;

        /// <summary>Velocidad del flujo (m/s); en el viento ya incluye la tinta poderosa.</summary>
        public float WindSpeed => flowSpeed;
        /// <summary>Largo del chorro/cono (m); en el viento escala con la tinta poderosa.</summary>
        public float ConeLength { get; private set; }

        public void Init(SpellIR compiled, SealFrame sealFrame, ElementLook look, GameObject source = null)
        {
            Source = source;
            manifestation = GetComponent<SpellManifestation>();
            spell = compiled;
            plan = compiled.plan ?? SpellPlan.Inert();
            frame = sealFrame;
            push = look.push;
            areaFactor = Mathf.Clamp((frame.Radius * frame.Radius) / (ReferenceRadius * ReferenceRadius), 0.25f, 4f);
            // Misma velocidad que las particulas: lo que se ve es lo que empuja.
            flowSpeed = (2f + 8f * compiled.force) * look.speed;
            isWind = compiled.element == ElementType.Wind;
            float inkBoost = isWind && compiled.inkPower > 0f ? compiled.inkPower : 1f;
            if (isWind) flowSpeed = Mathf.Min(flowSpeed * inkBoost, 30f);
            windGain = push * Mathf.Sqrt(areaFactor) * Mathf.Lerp(0.6f, 1f, compiled.quality) * WindPower;
            coneSpread = Mathf.Clamp01(compiled.spread);
            float elementReach = compiled.element == ElementType.Fire ? FireReach : 1f;
            float elementCoupling = compiled.element == ElementType.Water ? WaterPower : 1f;
            ConeLength = frame.Scale * (4f + 10f * compiled.range) * inkBoost * elementReach;
            coupling = BaseCoupling * push * areaFactor * Mathf.Lerp(0.6f, 1f, compiled.quality) * elementCoupling;
            ReachRadius = frame.Scale * Mathf.Max(4f + 10f * compiled.range, 6f) * inkBoost * elementReach;
        }

        /// <summary>Radio del chorro a una distancia axial. Viento: se abre como cono segun la dispersion (spread).</summary>
        private float JetRadiusAt(float along, float length)
        {
            float radius = frame.Scale * 0.9f / plan.Focus;
            return isWind ? radius * (1f + 1.5f * coneSpread * Mathf.Clamp01(along / length)) : radius;
        }

        private float JetMaxRadius => JetRadiusAt(ConeLength, ConeLength);

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>
        /// Intensidad (0-1) del hechizo en un punto, con la misma geometria que la fisica: rafaga al golpe, chorro,
        /// abanico, vortice y orbe. La succion no expone (atrae, no toca). Lo usa MagicWorld para quemar, mojar, romper.
        /// </summary>
        public float ExposureAt(Vector3 point)
        {
            if (manifestation == null || plan == null) return 0f;
            float t = manifestation.SinceStrike;
            if (t < 0f) return 0f;
            Vector3 offset = point - frame.Origin;
            float best = 0f;

            if (plan.Has(PlanPrimitive.Burst) && t < SpellManifestation.StrikeSeconds && !IsLevitationOnly())
            {
                float radius = frame.Scale * 3f;
                if (Vector3.Dot(offset, frame.Normal) >= -0.1f)
                    best = Mathf.Max(best, 1f - Mathf.Clamp01(offset.magnitude / radius));
            }
            if (!manifestation.InBody) return best;
            float envelope = manifestation.Envelope(t);

            if (plan.Has(PlanPrimitive.Jet))
            {
                Vector3 dir = frame.Direction(plan.Aim);
                float length = ConeLength;
                float along = Vector3.Dot(offset, dir);
                float radius = JetRadiusAt(along, length);
                float off = (offset - dir * along).magnitude;
                if (along >= 0f && along <= length && off <= radius)
                    best = Mathf.Max(best, envelope * (1f - 0.5f * along / length) * (1f - 0.5f * off / radius));
            }
            if (plan.Has(PlanPrimitive.Fan))
            {
                float radius = frame.Scale * (3f + 6f * spell.range);
                float height = Vector3.Dot(offset, frame.Normal);
                float r = (offset - frame.Normal * height).magnitude;
                if (height >= -0.1f && height <= frame.Scale * 1.5f && r <= radius)
                    best = Mathf.Max(best, envelope * (1f - r / radius));
            }
            if (plan.Has(PlanPrimitive.Vortex))
            {
                float radius = frame.Scale * 2.5f, height = frame.Scale * 6f;
                float along = Vector3.Dot(offset, frame.Normal);
                float r = (offset - frame.Normal * along).magnitude;
                if (along >= -0.1f && along <= height && r <= radius)
                    best = Mathf.Max(best, envelope * (1f - r / radius));
            }
            if (plan.Has(PlanPrimitive.Hold))
            {
                Vector3 holdPoint = frame.Point(plan.Hold.At) + frame.Normal * frame.Scale * 0.3f;
                float radius = frame.Scale * 1.2f;
                float d = (point - holdPoint).magnitude;
                if (d <= radius) best = Mathf.Max(best, envelope * (1f - 0.5f * d / radius));
            }
            return best * Mathf.Lerp(0.5f, 1f, spell.force);
        }

        private void FixedUpdate()
        {
            if (manifestation == null || plan == null) return;
            float t = manifestation.SinceStrike;
            if (t < 0f) return;

            if (!burstApplied && plan.Has(PlanPrimitive.Burst))
            {
                burstApplied = true;
                if (!IsLevitationOnly()) ApplyBurst(); // float/levitation solos no empujan: sostienen
            }

            if (!signsApplied)
            {
                signsApplied = true;
                if (plan.Repetition > 0f) ApplyRepetition();
                if (plan.Crush > 0f) ApplyCrush();
            }

            if (!manifestation.InBody) return;
            float envelope = manifestation.Envelope(t);
            ApplyLevitationSigns(envelope);
            if (plan.Cool > 0f) ApplyCool(envelope);
            if (plan.Entwine > 0f) ApplyEntwine(envelope);
            if (plan.Gather > 0f) ApplyGather(envelope);
            if (plan.Orb > 0f) ApplyOrb(envelope);
            if (plan.Target > 0f) ApplyTarget(envelope);
            envelope *= 1f + 0.35f * Mathf.Min(plan.Empower, 3f); // Empower: fortalece el hechizo (mas caudal)
            if (plan.Collection > 0f) ApplyCollection(envelope);
            if (plan.Billow > 0f) ApplyBillow(envelope);
            if (plan.Has(PlanPrimitive.Jet)) ApplyJet(envelope);
            if (plan.Has(PlanPrimitive.Fan)) ApplyFan(envelope);
            if (plan.Has(PlanPrimitive.Vortex)) ApplyVortex(envelope);
            if (plan.Has(PlanPrimitive.Hold)) ApplyHold(envelope);
            if (plan.Has(PlanPrimitive.Intake)) ApplyIntake(envelope);
        }

        // ---------------------------------------------------------------- signos con efecto propio (crush, repetition, collection, billowing)

        private static bool IsPlayerBody(Rigidbody rb) => rb.GetComponent<Player.PlayerMovement>() != null;

        /// <summary>Esfera delante del sello donde actuan los signos de area (crece con el anillo y con el largo de los signos).</summary>
        private float AreaRadius(float strength) => Mathf.Max(frame.Scale, 0.3f) * (2.2f + 1.2f * Mathf.Min(strength, 3f));

        /// <summary>Wall Breaker: la tierra abre un hueco en las estructuras fijas (no cerca de libros ni portales) y dispersa escombros.</summary>
        private void ApplyCrush()
        {
            if (spell.element != ElementType.Earth) return; // en los demas elementos solo suma dano y rompe objetos reactivos (MagicWorld)
            float radius = AreaRadius(plan.Crush);
            Vector3 center = frame.Origin + frame.Outward * radius * 0.7f + Vector3.up * (frame.Normal.y > 0.5f ? radius * 0.3f : 0f);
            if (Net.NetCast.IsClientOnly) return; // en red solo el anfitrion corta; los clientes reciben el corte (NetEffectsSync)
            int removed = World.MeshBreaker.Break(center, radius);
            if (removed > 0) Debug.Log($"[Romper] hueco de {radius:F1} m, {removed} triangulos");
        }

        /// <summary>Repetition: lo que toca vuelve a su estado anterior (lugar de origen, sin quemar ni mojar).</summary>
        private void ApplyRepetition()
        {
            if (Net.NetCast.IsClientOnly) return;
            int n = Gather(frame.Origin + frame.Outward * AreaRadius(plan.Repetition) * 0.5f, AreaRadius(plan.Repetition));
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                if (IsPlayerBody(rb)) continue;
                if (rb.TryGetComponent(out World.Respawnable respawnable)) respawnable.Respawn("repeticion (rebobinado)");
                if (rb.TryGetComponent(out Reactions.ElementReactive reactive)) reactive.ResetState();
            }
        }

        /// <summary>Collection: reune lo suelto alrededor y arriba del sello en un punto (resorte amortiguado que ademas sostiene el peso).</summary>
        private void ApplyCollection(float envelope)
        {
            float radius = AreaRadius(plan.Collection);
            Vector3 point = frame.Origin + frame.Normal * frame.Scale * 0.9f;
            int n = Gather(point, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                if (IsPlayerBody(rb) || rb.mass > 60f) continue;
                Vector3 a = (point - rb.worldCenterOfMass) * 10f - rb.linearVelocity * 3f;
                if (rb.useGravity) a -= Physics.gravity;
                rb.AddForce(Vector3.ClampMagnitude(a * envelope, MaxAcceleration), ForceMode.Acceleration);
            }
        }

        /// <summary>Billowing: las cosas sueltas se vuelven livianas como una nube (casi sin gravedad y con mucho roce) mientras dura el cuerpo.</summary>
        private void ApplyBillow(float envelope)
        {
            float radius = AreaRadius(plan.Billow);
            int n = Gather(frame.Origin + frame.Outward * radius * 0.5f, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                if (rb.mass > 250f) continue;
                if (rb.useGravity) rb.AddForce(-Physics.gravity * 0.85f * envelope, ForceMode.Acceleration);
                rb.AddForce(-rb.linearVelocity * 1.8f * envelope, ForceMode.Acceleration);
            }
        }


        // ---------------------------------------------------------------- signos de la wiki de WHA (cool, entwine, gather, orb, focus, empower)

        /// <summary>
        /// Float: zona suelta sobre el papel, lo liviano pierde peso, sube despacio y se mece. Levitation (sin agarre propio de Hold):
        /// lo de arriba queda suspendido a una altura fija y anclado al eje; con viento se desplaza hacia donde apunta el signo.
        /// </summary>
        private bool IsLevitationOnly()
        {
            bool any = false;
            for (int i = 0; i < plan.Signs.Count; i++) if (plan.Signs[i].Id == "float" || plan.Signs[i].Id == "levitation") { any = true; break; }
            return any && plan.Aim.magnitude < 0.05f && plan.Dispersion < 0.05f && Mathf.Abs(plan.Circulation) < 0.05f;
        }

        private void ApplyLevitationSigns(float envelope)
        {
            float floatLen = 0f, levLen = 0f;
            Vector2 levFacing = Vector2.zero;
            for (int i = 0; i < plan.Signs.Count; i++)
            {
                var s = plan.Signs[i];
                if (s.Id == "float") floatLen += s.Length;
                else if (s.Id == "levitation") { levLen += s.Length; levFacing += s.Facing * s.Length; }
            }
            if (floatLen > 0f) HoverZone(true, floatLen, Vector3.zero, envelope);
            if (levLen > 0f && plan.Hold == null)
            {
                Vector3 drift = Vector3.zero;
                if (spell.element == ElementType.Wind && levFacing.sqrMagnitude > 1e-4f)
                    drift = frame.Direction(new Vector3(levFacing.x, levFacing.y, 0f).normalized) * 2.2f;
                HoverZone(false, levLen, drift, envelope);
            }
        }

        private void HoverZone(bool isFloat, float length, Vector3 drift, float envelope)
        {
            float target = LevitationMath.HoverHeight(isFloat, frame.Scale, length);
            float radius = frame.Scale * (2f + 0.6f * Mathf.Min(length, 3f));
            int n = Gather(frame.Origin + frame.Normal * target * 0.5f, radius + target);
            float time = Time.time;
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                if (IsPlayerBody(rb) || rb.GetComponent<World.PhysicalCanvas>() != null) continue;
                float maxMass = isFloat ? LevitationMath.FloatMaxMass : LevitationMath.LevitationMaxMass;
                if (rb.mass > maxMass * 4f) continue;
                Vector3 off = rb.worldCenterOfMass - frame.Origin;
                float height = Vector3.Dot(off, frame.Normal);
                if (height < -0.1f) continue;
                Vector3 lateral = off - frame.Normal * height;
                float lateralDist = lateral.magnitude;
                if (lateralDist > radius) continue;

                Vector3 vel = rb.linearVelocity;
                float vUp = Vector3.Dot(vel, frame.Normal);
                float bob = Mathf.Sin(time * 1.7f + rb.GetInstanceID() * 0.37f);
                float cancel = rb.useGravity ? LevitationMath.GravityCancel(isFloat, rb.mass) : 0f;
                Vector3 a = frame.Normal * LevitationMath.VerticalAcceleration(isFloat, height, vUp, target, 0f, 0f, bob);
                // El peso se cancela casi entero mientras dura el cuerpo (si se atenuara con la envolvente, la caja no despegaria).
                Vector3 weight = -Physics.gravity * cancel * Mathf.Clamp01(envelope * 3f);
                if (lateralDist > 1e-3f)
                {
                    Vector3 dir = lateral / lateralDist;
                    a += dir * LevitationMath.LateralAcceleration(isFloat, lateralDist, Vector3.Dot(vel, dir));
                }
                a += drift * (1f - Mathf.Clamp01(lateralDist / radius)); // el viento arrastra mas en el centro; cerca del borde ya lo suelta
                rb.AddForce(Vector3.ClampMagnitude(a * envelope + weight, MaxAcceleration), ForceMode.Acceleration);
            }
        }

        /// <summary>Cool: enfria lo reactivo de la zona (calor negativo).</summary>
        private void ApplyCool(float envelope)
        {
            float radius = AreaRadius(plan.Cool);
            Vector3 center = frame.Origin + frame.Outward * radius * 0.5f;
            var chill = new Reactions.Exposure { Heat = -1.2f * Mathf.Min(plan.Cool, 3f) };
            var all = Reactions.ElementReactive.All;
            for (int i = 0; i < all.Count; i++)
            {
                var r = all[i];
                if (r == null || (r.WorldBounds.center - center).sqrMagnitude > radius * radius) continue;
                // FIS-02: el segundo parametro de AddExposure es peso/intensidad (no dt); ReactionModel.Step ya multiplica por dt.
                r.AddExposure(chill, envelope);
            }
        }

        /// <summary>Entwine: lo suelto se enrosca alrededor del eje del sello (giro tangencial + leve atraccion al eje).</summary>
        private void ApplyEntwine(float envelope)
        {
            float radius = AreaRadius(plan.Entwine);
            Vector3 axisPoint = frame.Origin + frame.Outward * radius * 0.5f;
            int n = Gather(axisPoint, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                if (IsPlayerBody(rb) || rb.mass > 120f) continue;
                Vector3 off = rb.worldCenterOfMass - axisPoint;
                Vector3 radial = off - frame.Outward * Vector3.Dot(off, frame.Outward);
                Vector3 tangent = Vector3.Cross(frame.Outward, radial).normalized;
                Vector3 a = tangent * 8f - radial * 4f - rb.linearVelocity * 1.2f;
                rb.AddForce(Vector3.ClampMagnitude(a * envelope, MaxAcceleration), ForceMode.Acceleration);
            }
        }

        /// <summary>Gather: como Collection pero mas activo: alcance mayor y tira de cosas mas pesadas hacia el sello.</summary>
        private void ApplyGather(float envelope)
        {
            float radius = AreaRadius(plan.Gather) * 1.4f;
            Vector3 point = frame.Origin + frame.Normal * frame.Scale * 0.6f;
            int n = Gather(point, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                if (IsPlayerBody(rb) || rb.mass > 120f) continue;
                Vector3 a = (point - rb.worldCenterOfMass).normalized * 14f - rb.linearVelocity * 2f;
                rb.AddForce(Vector3.ClampMagnitude(a * envelope, MaxAcceleration), ForceMode.Acceleration);
            }
        }

        /// <summary>Orb: esfera sobre el sello; lo que esta dentro se queda adentro (la pared lo devuelve) y se asienta.</summary>
        private void ApplyOrb(float envelope)
        {
            float radius = frame.Scale * (1.2f + 0.5f * Mathf.Min(plan.Orb, 3f));
            Vector3 center = frame.Origin + frame.Normal * radius;
            int n = Gather(center, radius * 1.6f);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                if (IsPlayerBody(rb) || rb.mass > 60f) continue;
                Vector3 off = rb.worldCenterOfMass - center;
                float d = off.magnitude;
                if (d > radius * 0.85f)
                    rb.AddForce(Vector3.ClampMagnitude(-off / Mathf.Max(d, 1e-3f) * (d - radius * 0.85f) * 30f * envelope, MaxAcceleration), ForceMode.Acceleration);
                rb.AddForce(-rb.linearVelocity * 1.5f * envelope, ForceMode.Acceleration);
            }
        }

        /// <summary>Focus (Sights Set): lo suelto delante del sello se orienta hacia el objetivo vivo mas cercano a la linea de tiro.</summary>
        private void ApplyTarget(float envelope)
        {
            float range = ConeLength * 1.5f;
            Combat.Health best = null;
            float bestScore = float.MaxValue;
            var all = Combat.Health.All;
            for (int i = 0; i < all.Count; i++)
            {
                var h = all[i];
                if (h == null) continue;
                Vector3 off = h.transform.position - frame.Origin;
                float along = Vector3.Dot(off, frame.Outward);
                if (along < 0f || along > range) continue;
                float score = (off - frame.Outward * along).magnitude;
                if (score < bestScore) { bestScore = score; best = h; }
            }
            if (best == null) return;
            Vector3 aim = (best.transform.position + Vector3.up * 0.8f - frame.Origin).normalized;
            int n = Gather(frame.Origin + aim * range * 0.4f, range * 0.5f);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                if (IsPlayerBody(rb) || rb.mass > 60f) continue;
                Drag(rb, aim * Mathf.Max(flowSpeed, 6f), envelope * Mathf.Min(plan.Target, 2f));
            }
        }

        private int Gather(Vector3 center, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, hits, ~0, QueryTriggerInteraction.Ignore);
            int unique = 0;
            for (int i = 0; i < count; i++)
            {
                var rb = hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                bool seen = false;
                for (int j = 0; j < unique; j++) if (bodies[j] == rb) { seen = true; break; }
                if (!seen) bodies[unique++] = rb;
            }
            return unique;
        }

        /// <summary>Los cuerpos pesados responden menos, pero no proporcionalmente.</summary>
        private static float MassFactor(Rigidbody rb) => 1f / Mathf.Sqrt(Mathf.Max(rb.mass, 0.1f));

        /// <summary>
        /// Acelera el cuerpo a lo largo del flujo hasta alcanzar su velocidad. Solo empuja en la direccion del flujo:
        /// no frena lo que ya va mas rapido ni anula la gravedad (eso lo hace el orbe).
        /// </summary>
        private void Drag(Rigidbody rb, Vector3 desiredVelocity, float strength)
        {
            if (isWind) { WindDrag(rb, desiredVelocity, strength); return; }
            float speed = desiredVelocity.magnitude;
            if (speed < 1e-4f) return;
            Vector3 dir = desiredVelocity / speed;
            float missing = speed - Vector3.Dot(rb.linearVelocity, dir);
            if (missing <= 0f) return;
            float a = Mathf.Min(missing * coupling * strength * MassFactor(rb), MaxAcceleration);
            rb.AddForce(dir * a, ForceMode.Acceleration);
        }

        /// <summary>
        /// Viento: arrastre cuadratico F = 0.5 rho Cd A (vViento - vCuerpo)|.|, a = F/m, con A = cara mas grande de las bounds del
        /// collider y tope MaxAcceleration. Los livianos vuelan, los pesados casi no se mueven. Cd = 1 (no hay Cd por material aun).
        /// </summary>
        private void WindDrag(Rigidbody rb, Vector3 windVelocity, float strength)
        {
            float area = 0f;
            if (rb.TryGetComponent(out Collider col)) area = WindMath.ProjectedArea(col.bounds.size);
            if (area < 0.01f) area = 0.5f; // collider apagado/sin bounds (p. ej. el jugador): area tipica
            Vector3 a = WindMath.Drag(windVelocity, rb.linearVelocity, rb.mass, area, WindMath.DefaultCd, MaxAcceleration);
            // FIS-01: windGain puede multiplicar hasta ~5.2x y romper el tope de aceleracion; acotamos con MaxWindAcceleration.
            Vector3 effectiveA = WindMath.ClampAcceleration(a * (Mathf.Min(strength, 2f) * windGain), MaxWindAcceleration);
            rb.AddForce(effectiveA, ForceMode.Acceleration);
        }

        private void ApplyBurst()
        {
            float radius = frame.Scale * 3f;
            int n = Gather(frame.Origin, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 offset = rb.worldCenterOfMass - frame.Origin;
                // Pared del disco: la magia no va "debajo" del sello.
                if (Vector3.Dot(offset, frame.Normal) < -0.1f) continue;
                float falloff = 1f - Mathf.Clamp01(offset.magnitude / radius);
                Vector3 dir = offset.sqrMagnitude > 1e-4f ? offset.normalized : frame.Normal;
                float dv = Mathf.Min(4f * spell.force * push * Mathf.Sqrt(areaFactor), MaxBurstDeltaV) * falloff * MassFactor(rb);
                rb.AddForce(dir * dv, ForceMode.VelocityChange);
            }
        }

        private void ApplyJet(float envelope)
        {
            Vector3 dir = frame.Direction(plan.Aim);
            float length = ConeLength;
            int n = Gather(frame.Origin + dir * length * 0.5f, length * 0.5f + JetMaxRadius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 offset = rb.worldCenterOfMass - frame.Origin;
                float along = Vector3.Dot(offset, dir);
                if (along < 0f || along > length) continue;
                float off = (offset - dir * along).magnitude;
                float radius = JetRadiusAt(along, length);
                if (off > radius) continue;
                float falloff = (1f - 0.5f * along / length) * (1f - off / radius);
                // La lente concentra el caudal: mas acople en un tubo mas angosto.
                Drag(rb, dir * flowSpeed * falloff, envelope * plan.Focus);
            }
        }

        private void ApplyFan(float envelope)
        {
            float radius = frame.Scale * (3f + 6f * spell.range);
            int n = Gather(frame.Origin, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 offset = rb.worldCenterOfMass - frame.Origin;
                float height = Vector3.Dot(offset, frame.Normal);
                if (height < -0.1f || height > frame.Scale * 1.5f) continue;
                Vector3 radial = offset - frame.Normal * height;
                if (radial.sqrMagnitude < 1e-4f) continue;
                float falloff = 1f - Mathf.Clamp01(radial.magnitude / radius);
                Drag(rb, radial.normalized * flowSpeed * falloff, envelope * Mathf.Min(plan.Dispersion, 2f));
            }
        }

        private void ApplyVortex(float envelope)
        {
            float radius = frame.Scale * 2.5f;
            float height = frame.Scale * 6f;
            int n = Gather(frame.Origin + frame.Normal * height * 0.5f, height * 0.5f + radius);
            float spin = Mathf.Sign(plan.Circulation);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 offset = rb.worldCenterOfMass - frame.Origin;
                float along = Vector3.Dot(offset, frame.Normal);
                if (along < -0.1f || along > height) continue;
                Vector3 radial = offset - frame.Normal * along;
                float r = radial.magnitude;
                if (r > radius || r < 1e-3f) continue;
                Vector3 tangent = Vector3.Cross(frame.Normal, radial / r) * spin;
                float falloff = 1f - r / radius;
                Vector3 desired = (tangent * 0.7f - radial / r * 0.2f + frame.Normal * 0.35f) * flowSpeed * falloff;
                Drag(rb, desired, envelope * Mathf.Min(Mathf.Abs(plan.Circulation), 3f));
            }
        }

        private void ApplyHold(float envelope)
        {
            Vector3 point = frame.Point(plan.Hold.At) + frame.Normal * frame.Scale * 0.3f;
            float radius = frame.Scale * 1.2f;
            int n = Gather(point, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 toPoint = point - rb.worldCenterOfMass;
                // Resorte amortiguado que ademas sostiene el peso (el sello carga con la masa).
                Vector3 a = toPoint * (8f * plan.Hold.Grip) - rb.linearVelocity * 3f;
                if (rb.useGravity) a -= Physics.gravity;
                rb.AddForce(Vector3.ClampMagnitude(a * envelope, MaxAcceleration), ForceMode.Acceleration);
            }
        }

        private void ApplyIntake(float envelope)
        {
            float radius = frame.Scale * 4f;
            int n = Gather(frame.Origin, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                // Canon (ground-truth 7): el pull atrae materia de su mismo elemento, no cualquier objeto.
                var matter = rb.GetComponent<IElementMatter>();
                if (matter == null || matter.Element != spell.element) continue;
                Vector3 toSeal = frame.Origin - rb.worldCenterOfMass;
                float d = toSeal.magnitude;
                if (d < frame.Scale * 0.5f) continue;
                Vector3 inward = toSeal / d;
                Vector3 swirl = Vector3.Cross(frame.Normal, -inward) * Mathf.Clamp(plan.Intake.Swirl, -1f, 1f);
                float falloff = 1f - Mathf.Clamp01(d / radius);
                Vector3 desired = (inward * Mathf.Clamp(plan.Intake.Draw, 0.3f, 1.5f) + swirl * 0.5f) * flowSpeed * 0.5f * falloff;
                Drag(rb, desired, envelope);
            }
        }
    }
}
