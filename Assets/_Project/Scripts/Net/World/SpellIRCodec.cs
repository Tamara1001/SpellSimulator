using System;
using System.IO;
using System.Text;
using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Serializacion binaria del <see cref="SpellIR"/> ya compilado, para mandar un hechizo por la red (F3 de MULTIPLAYER_PLAN, 2.5): quien cerro
    /// el anillo lo lee con el modelo, y el servidor y los demas clientes lo reproducen sin volver a leer el sello. Logica pura (sin Netcode).
    /// No viajan los textos de diagnostico (warnings, notas del plan): solo lo que consume el modulo de salida.
    /// </summary>
    public static class SpellIRCodec
    {
        public const byte Version = 1;
        private const int MaxItems = 64;

        public static byte[] Encode(SpellIR spell)
        {
            using (var ms = new MemoryStream(256))
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(Version);
                byte flags = 0;
                if (spell.valid) flags |= 1;
                if (spell.active) flags |= 2;
                if (spell.prepared) flags |= 4;
                if (spell.waterproof) flags |= 8;
                if (spell.plan != null) flags |= 16;
                w.Write(flags);
                w.Write(spell.status ?? "");
                w.Write((byte)spell.element);
                w.Write(spell.sigilId ?? "");
                w.Write(spell.elementConfidence);
                w.Write(spell.primarySizeNorm);
                w.Write(spell.sizeRatio);
                w.Write(spell.strength);
                w.Write(spell.effectScale);
                w.Write(spell.primaryManifestation ?? "");
                int count = spell.manifestations != null ? Mathf.Min(spell.manifestations.Length, MaxItems) : 0;
                w.Write((byte)count);
                for (int i = 0; i < count; i++)
                {
                    ManifestationStrength m = spell.manifestations[i];
                    w.Write(m.Id ?? ""); w.Write(m.Strength); w.Write(m.Influence); Write(w, m.Point); w.Write(m.Radius); w.Write(m.Rigidity);
                }
                w.Write(spell.force); w.Write(spell.spread); w.Write(spell.focus); w.Write(spell.range); w.Write(spell.gravity);
                w.Write(spell.duration); w.Write(spell.stability); w.Write(spell.quality); w.Write(spell.neatness);
                Write(w, spell.direction);
                w.Write(spell.xTiltDeg); w.Write(spell.yTiltDeg); w.Write(spell.directionCoherence);
                w.Write(spell.sealRadius); w.Write(spell.inkPower);
                w.Write(spell.analysisBoost); Write(w, spell.analysisPivot);
                if (spell.plan != null) WritePlan(w, spell.plan);
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>Decodifica; devuelve false si los datos estan truncados o corruptos. <paramref name="activatedAt"/> es la hora local a poner si el hechizo esta activo.</summary>
        public static bool TryDecode(byte[] data, float activatedAt, out SpellIR spell)
        {
            spell = SpellIR.Invalid("Sin datos", null);
            if (data == null || data.Length < 3 || data[0] != Version) return false;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var r = new BinaryReader(ms, Encoding.UTF8))
                {
                    r.ReadByte();
                    byte flags = r.ReadByte();
                    var s = new SpellIR
                    {
                        valid = (flags & 1) != 0,
                        active = (flags & 2) != 0,
                        prepared = (flags & 4) != 0,
                        waterproof = (flags & 8) != 0,
                        status = r.ReadString(),
                        warnings = new string[0],
                    };
                    byte element = r.ReadByte();
                    if (element > (byte)ElementType.Light) return false;
                    s.element = (ElementType)element;
                    string sigil = r.ReadString();
                    s.sigilId = sigil.Length == 0 ? null : sigil;
                    s.elementConfidence = r.ReadSingle();
                    s.primarySizeNorm = r.ReadSingle();
                    s.sizeRatio = r.ReadSingle();
                    s.strength = r.ReadSingle();
                    s.effectScale = r.ReadSingle();
                    s.primaryManifestation = r.ReadString();
                    int count = r.ReadByte();
                    if (count > MaxItems) return false;
                    s.manifestations = new ManifestationStrength[count];
                    for (int i = 0; i < count; i++)
                        s.manifestations[i] = new ManifestationStrength { Id = r.ReadString(), Strength = r.ReadSingle(), Influence = r.ReadSingle(), Point = ReadV2(r), Radius = r.ReadSingle(), Rigidity = r.ReadSingle() };
                    s.force = r.ReadSingle(); s.spread = r.ReadSingle(); s.focus = r.ReadSingle(); s.range = r.ReadSingle(); s.gravity = r.ReadSingle();
                    s.duration = r.ReadSingle(); s.stability = r.ReadSingle(); s.quality = r.ReadSingle(); s.neatness = r.ReadSingle();
                    s.direction = ReadV3(r);
                    s.xTiltDeg = r.ReadSingle(); s.yTiltDeg = r.ReadSingle(); s.directionCoherence = r.ReadSingle();
                    s.sealRadius = r.ReadSingle(); s.inkPower = r.ReadSingle();
                    s.analysisBoost = r.ReadSingle(); s.analysisPivot = ReadV2(r);
                    s.activatedAt = s.active ? activatedAt : -1f;
                    s.plan = (flags & 16) != 0 ? ReadPlan(r) : null;
                    if (s.plan == null && (flags & 16) != 0) return false;
                    if (!IsFinite(s)) return false;
                    spell = s;
                    return true;
                }
            }
            catch (EndOfStreamException) { return false; }
            catch (IOException) { return false; }
            catch (FormatException) { return false; }
            catch (ArgumentException) { return false; }
        }

        // ---------------------------------------------------------------- plan

        private static void WritePlan(BinaryWriter w, SpellPlan p)
        {
            byte flags = 0;
            if (p.Hold != null) flags |= 1;
            if (p.Intake != null) flags |= 2;
            if (p.Leak) flags |= 4;
            w.Write(flags);
            w.Write((byte)p.Mode);
            Write(w, p.Aim);
            w.Write(p.Dispersion); w.Write(p.Circulation); w.Write(p.Budget); w.Write(p.Focus); w.Write(p.Quality);
            w.Write((int)p.Primitives);
            if (p.Hold != null) { Write(w, p.Hold.At); w.Write(p.Hold.Grip); w.Write(p.Hold.Spin); w.Write(p.Hold.Budget); }
            if (p.Intake != null) { w.Write(p.Intake.Budget); w.Write(p.Intake.Draw); w.Write(p.Intake.Swirl); Write(w, p.Intake.Lateral); }
            int signs = Mathf.Min(p.Signs.Count, MaxItems);
            w.Write((byte)signs);
            for (int i = 0; i < signs; i++)
            {
                SignReading s = p.Signs[i];
                w.Write(s.Id ?? ""); w.Write(s.Manifestation ?? ""); Write(w, s.At); w.Write(s.Length); Write(w, s.Facing); w.Write(s.FacingTrust); w.Write(s.FacingFromPose);
            }
        }

        private static SpellPlan ReadPlan(BinaryReader r)
        {
            byte flags = r.ReadByte();
            byte mode = r.ReadByte();
            if (mode > (byte)PlanMode.Manipulate) return null;
            var p = new SpellPlan { Mode = (PlanMode)mode, Leak = (flags & 4) != 0 };
            p.Aim = ReadV3(r);
            p.Dispersion = r.ReadSingle(); p.Circulation = r.ReadSingle(); p.Budget = r.ReadSingle(); p.Focus = r.ReadSingle(); p.Quality = r.ReadSingle();
            p.Primitives = (PlanPrimitive)r.ReadInt32();
            if ((flags & 1) != 0) p.Hold = new HoldSpec { At = ReadV3(r), Grip = r.ReadSingle(), Spin = r.ReadSingle(), Budget = r.ReadSingle() };
            if ((flags & 2) != 0) p.Intake = new IntakeSpec { Budget = r.ReadSingle(), Draw = r.ReadSingle(), Swirl = r.ReadSingle(), Lateral = ReadV2(r) };
            int signs = r.ReadByte();
            if (signs > MaxItems) return null;
            for (int i = 0; i < signs; i++)
                p.Signs.Add(new SignReading { Id = r.ReadString(), Manifestation = r.ReadString(), At = ReadV2(r), Length = r.ReadSingle(), Facing = ReadV2(r), FacingTrust = r.ReadSingle(), FacingFromPose = r.ReadBoolean() });
            return p;
        }

        // ---------------------------------------------------------------- utilidades

        private static void Write(BinaryWriter w, Vector2 v) { w.Write(v.x); w.Write(v.y); }
        private static void Write(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static Vector2 ReadV2(BinaryReader r) => new Vector2(r.ReadSingle(), r.ReadSingle());
        private static Vector3 ReadV3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        /// <summary>Rechaza NaN/infinito en lo que el modulo de salida usa (un cliente roto no debe romper el efecto).</summary>
        private static bool IsFinite(SpellIR s)
        {
            return Finite(s.strength) && Finite(s.effectScale) && Finite(s.force) && Finite(s.spread) && Finite(s.focus) && Finite(s.range)
                && Finite(s.gravity) && Finite(s.duration) && Finite(s.sealRadius) && Finite(s.inkPower) && Finite(s.direction.x)
                && Finite(s.direction.y) && Finite(s.direction.z) && Finite(s.analysisBoost);
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
