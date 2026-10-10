using System.IO;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Output;

namespace SpellSimulator.Net
{
    /// <summary>Tipos de cuerpo que el anfitrion crea durante el juego y los clientes reproducen (F3 de MULTIPLAYER_PLAN).</summary>
    public enum NetSpawnKind : byte { None = 0, Rock = 1 }

    /// <summary>
    /// Cuerpos creados en juego (la roca del hechizo de piedra): el anfitrion los crea de verdad y manda a los clientes lo necesario para armar una
    /// copia que sigue sus poses. Los datos viajan en un arreglo de bytes por tipo (aca se codifican y se decodifican; sin Netcode, con tests).
    /// </summary>
    public static class NetSpawn
    {
        /// <summary>Datos de una roca: pose actual, variante de malla, radio, segundos de formacion y segundos que ya paso formandose.</summary>
        public struct RockData
        {
            public Vector3 pos;
            public Quaternion rot;
            public byte variant;
            public float radius, formSeconds, elapsed;
        }

        public static byte[] EncodeRock(RockData d)
        {
            using (var ms = new MemoryStream(40))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(d.pos.x); w.Write(d.pos.y); w.Write(d.pos.z);
                w.Write(d.rot.x); w.Write(d.rot.y); w.Write(d.rot.z); w.Write(d.rot.w);
                w.Write(d.variant);
                w.Write(d.radius); w.Write(d.formSeconds); w.Write(d.elapsed);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static bool TryDecodeRock(byte[] data, out RockData d)
        {
            d = default;
            if (data == null || data.Length < 12 + 16 + 1 + 12) return false;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var r = new BinaryReader(ms))
                {
                    d.pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    d.rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    d.variant = r.ReadByte();
                    d.radius = r.ReadSingle(); d.formSeconds = r.ReadSingle(); d.elapsed = r.ReadSingle();
                }
            }
            catch (EndOfStreamException) { return false; }
            if (float.IsNaN(d.pos.x) || float.IsNaN(d.pos.y) || float.IsNaN(d.pos.z) || float.IsInfinity(d.pos.x) || float.IsInfinity(d.pos.y) || float.IsInfinity(d.pos.z)) return false;
            if (!(d.radius > 0.01f && d.radius < 20f) || !(d.formSeconds > 0f && d.formSeconds < 60f) || d.elapsed < 0f) return false;
            float mag = d.rot.x * d.rot.x + d.rot.y * d.rot.y + d.rot.z * d.rot.z + d.rot.w * d.rot.w;
            if (!(mag > 0.5f && mag < 1.5f)) d.rot = Quaternion.identity;
            return true;
        }

        /// <summary>Arma la copia de un cuerpo que el anfitrion creo. Null si el tipo o los datos no sirven.</summary>
        public static GameObject Create(NetSpawnKind kind, byte[] payload)
        {
            switch (kind)
            {
                case NetSpawnKind.Rock:
                    if (!TryDecodeRock(payload, out RockData d)) return null;
                    var output = Object.FindFirstObjectByType<SpellOutputController>();
                    ElementLook look = output != null ? output.LookFor(ElementType.Earth, "earth") : null;
                    return RockSpell.BuildReplica(d.pos, d.rot, d.variant, d.radius, d.formSeconds, d.elapsed, look);
                default:
                    return null;
            }
        }
    }
}
