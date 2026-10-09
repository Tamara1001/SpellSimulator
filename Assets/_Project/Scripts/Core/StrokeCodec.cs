using System;
using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Codec binario compacto de trazos (F0 de MULTIPLAYER_PLAN, seccion 2.3 «Trazos de tinta»). Logica pura: solo usa Vector2.
    /// Formato de un trazo (version 1):
    ///   [version=1][flags: bits0-1 tinta][ownerId varint zigzag][strokeId varint][ancho 8 bits][cantidad varint]
    ///   [primer punto: 12 bits x + 12 bits y = 3 bytes] y por cada punto siguiente, segun el primer byte:
    ///   0xxxxxxx   1 byte : dx (bits 5-3) y dy (bits 2-0), zigzag de 3 bits (-4..3 unidades)
    ///   10xxxxxx   2 bytes: 14 bits = zigzag dx (7 bits) | zigzag dy (7 bits) (-64..63 unidades)
    ///   11xxxxxx   4 bytes: escape a punto absoluto (x 12 bits, y 12 bits)
    /// Una unidad = 1,125/4095 UV (cubre [-0.0625, 1.0625]): error de reconstruccion maximo ~0,00014 UV (menor que 1/2048).
    /// Ancho: 0 = el del lienzo; 1..255 en escala logaritmica de MinWidth a MaxWidth (error ~1,2 %).
    /// Una instantanea (sesion) es [version=1][cantidad varint] y por trazo [longitud varint][bytes del trazo].
    /// No es thread-safe (usa un buffer estatico de trabajo); se llama desde el hilo principal.
    /// </summary>
    public static class StrokeCodec
    {
        public const byte Version = 1;
        public const int UvBits = 12;
        public const int UvLevels = (1 << UvBits) - 1; // 4095
        public const float UvMin = -0.0625f;
        public const float UvRange = 1.125f; // cubre [-0.0625, 1.0625]
        public const float MinWidth = 0.0005f, MaxWidth = 0.25f;

        private const int MaxPointsPerStroke = 1 << 20; // tope de cordura al decodificar
        private static byte[] scratch = new byte[1024];

        // ---------------------------------------------------------------- API

        /// <summary>Codifica un trazo. Aloca el arreglo resultado (se llama al terminar el trazo, no por frame).</summary>
        public static byte[] Encode(Stroke stroke)
        {
            int len = WriteStroke(stroke, ref scratch, 0);
            var result = new byte[len];
            Buffer.BlockCopy(scratch, 0, result, 0, len);
            return result;
        }

        /// <summary>Bytes que ocupa el trazo codificado.</summary>
        public static int EncodedSize(Stroke stroke) => WriteStroke(stroke, ref scratch, 0);

        /// <summary>Decodifica un trazo. Devuelve null si los datos estan truncados o corruptos.</summary>
        public static Stroke Decode(byte[] data, int offset = 0)
        {
            int pos = offset;
            return ReadStroke(data, ref pos, data != null ? data.Length : 0);
        }

        /// <summary>Decodifica un trazo y avanza <paramref name="offset"/> al final del mismo. Null si falla (offset no cambia).</summary>
        public static Stroke Decode(byte[] data, ref int offset)
        {
            int pos = offset;
            Stroke s = ReadStroke(data, ref pos, data != null ? data.Length : 0);
            if (s != null) offset = pos;
            return s;
        }

        /// <summary>Instantanea de todos los trazos de la sesion.</summary>
        public static byte[] EncodeSession(DrawingSession session)
        {
            int pos = 0;
            Ensure(ref scratch, pos, 8);
            scratch[pos++] = Version;
            int count = session != null ? session.Strokes.Count : 0;
            pos = WriteVarint(ref scratch, pos, (uint)count);
            for (int i = 0; i < count; i++)
            {
                // El trazo se escribe en el mismo buffer despues de reservar 5 bytes para su longitud; luego se compacta.
                int lenSlot = pos;
                Ensure(ref scratch, pos, 5);
                int bodyStart = pos + 5;
                int bodyLen = WriteStroke(session.Strokes[i], ref scratch, bodyStart);
                int lenBytes = WriteVarint(ref scratch, lenSlot, (uint)bodyLen) - lenSlot;
                Buffer.BlockCopy(scratch, bodyStart, scratch, lenSlot + lenBytes, bodyLen);
                pos = lenSlot + lenBytes + bodyLen;
            }
            var result = new byte[pos];
            Buffer.BlockCopy(scratch, 0, result, 0, pos);
            return result;
        }

        /// <summary>Reconstruye una sesion desde una instantanea (conserva ids y duenios). Null si los datos son invalidos.</summary>
        public static DrawingSession DecodeSession(byte[] data, int offset = 0)
        {
            if (data == null || offset < 0 || offset >= data.Length || data[offset] != Version) return null;
            int pos = offset + 1;
            if (!ReadVarint(data, ref pos, data.Length, out uint count)) return null;
            if (count > (uint)(data.Length - pos)) return null; // cada trazo ocupa al menos 1 byte
            var session = new DrawingSession();
            for (uint i = 0; i < count; i++)
            {
                if (!ReadVarint(data, ref pos, data.Length, out uint len)) return null;
                if (len > (uint)(data.Length - pos)) return null;
                int end = pos + (int)len;
                int p = pos;
                Stroke s = ReadStroke(data, ref p, end);
                if (s == null) return null;
                session.AddStroke(s);
                pos = end;
            }
            return session;
        }

        // ---------------------------------------------------------------- cuantizacion

        public static int QuantizeUv(float v)
        {
            int q = Mathf.RoundToInt((v - UvMin) / UvRange * UvLevels);
            return q < 0 ? 0 : (q > UvLevels ? UvLevels : q);
        }

        public static float DequantizeUv(int q) => q * (UvRange / UvLevels) + UvMin;

        public static byte QuantizeWidth(float w)
        {
            if (!(w > 0f)) return 0;
            float t = Mathf.Log(w / MinWidth) / Mathf.Log(MaxWidth / MinWidth);
            int q = 1 + Mathf.RoundToInt(t * 254f);
            return (byte)(q < 1 ? 1 : (q > 255 ? 255 : q));
        }

        public static float DequantizeWidth(byte q)
        {
            if (q == 0) return 0f;
            return MinWidth * Mathf.Pow(MaxWidth / MinWidth, (q - 1) / 254f);
        }

        // ---------------------------------------------------------------- escritura

        private static int WriteStroke(Stroke s, ref byte[] buf, int pos)
        {
            int start = pos;
            int count = s.Points.Count;
            Ensure(ref buf, pos, 24 + count * 4);
            buf[pos++] = Version;
            buf[pos++] = (byte)((int)s.Ink & 3);
            pos = WriteVarint(ref buf, pos, ZigZag(s.OwnerId));
            pos = WriteVarint(ref buf, pos, s.StrokeId);
            buf[pos++] = QuantizeWidth(s.Width);
            pos = WriteVarint(ref buf, pos, (uint)count);
            int px = 0, py = 0;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = s.Points[i];
                int x = QuantizeUv(p.x), y = QuantizeUv(p.y);
                int dx = x - px, dy = y - py;
                if (i == 0)
                {
                    // Primer punto: 3 bytes (x 12 bits | y 12 bits), sin marcador.
                    buf[pos++] = (byte)(x >> 4);
                    buf[pos++] = (byte)(((x & 0xF) << 4) | (y >> 8));
                    buf[pos++] = (byte)(y & 0xFF);
                }
                else if (dx >= -64 && dx <= 63 && dy >= -64 && dy <= 63)
                {
                    uint zx = ZigZag(dx), zy = ZigZag(dy);
                    if (zx < 8 && zy < 8)
                    {
                        buf[pos++] = (byte)((zx << 3) | zy);
                    }
                    else
                    {
                        uint v = zx | (zy << 7);
                        buf[pos++] = (byte)(0x80 | (v >> 8));
                        buf[pos++] = (byte)(v & 0xFF);
                    }
                }
                else
                {
                    buf[pos++] = (byte)(0xC0 | (x >> 8));
                    buf[pos++] = (byte)(x & 0xFF);
                    buf[pos++] = (byte)(y >> 8);
                    buf[pos++] = (byte)(y & 0xFF);
                }
                px = x; py = y;
            }
            return pos - start;
        }

        private static void Ensure(ref byte[] buf, int pos, int extra)
        {
            if (pos + extra <= buf.Length) return;
            int size = buf.Length;
            while (size < pos + extra) size *= 2;
            Array.Resize(ref buf, size);
        }

        private static int WriteVarint(ref byte[] buf, int pos, uint v)
        {
            Ensure(ref buf, pos, 5);
            while (v >= 0x80) { buf[pos++] = (byte)(v | 0x80); v >>= 7; }
            buf[pos++] = (byte)v;
            return pos;
        }

        private static uint ZigZag(int v) => (uint)((v << 1) ^ (v >> 31));
        private static int UnZigZag(uint v) => (int)(v >> 1) ^ -(int)(v & 1);

        // ---------------------------------------------------------------- lectura

        private static bool ReadVarint(byte[] data, ref int pos, int end, out uint value)
        {
            value = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                if (pos >= end) return false;
                byte b = data[pos++];
                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return true;
            }
            return false;
        }

        private static Stroke ReadStroke(byte[] data, ref int pos, int end)
        {
            if (data == null || pos < 0 || pos >= end || end > data.Length) return null;
            if (data[pos++] != Version) return null;
            if (pos >= end) return null;
            byte flags = data[pos++];
            if (!ReadVarint(data, ref pos, end, out uint ownerZ)) return null;
            if (!ReadVarint(data, ref pos, end, out uint strokeId)) return null;
            if (pos >= end) return null;
            byte widthQ = data[pos++];
            if (!ReadVarint(data, ref pos, end, out uint count)) return null;
            if (count > MaxPointsPerStroke || count > (uint)(end - pos)) return null; // cada punto ocupa al menos 1 byte

            var stroke = new Stroke
            {
                Ink = (InkKind)(flags & 3),
                OwnerId = UnZigZag(ownerZ),
                StrokeId = strokeId,
                Width = DequantizeWidth(widthQ),
            };
            stroke.Points.Capacity = (int)count;
            int x = 0, y = 0;
            for (int i = 0; i < count; i++)
            {
                if (i == 0)
                {
                    if (end - pos < 3) return null;
                    x = (data[pos] << 4) | (data[pos + 1] >> 4);
                    y = ((data[pos + 1] & 0xF) << 8) | data[pos + 2];
                    pos += 3;
                }
                else
                {
                    if (pos >= end) return null;
                    byte b = data[pos++];
                    if ((b & 0x80) == 0)
                    {
                        x += UnZigZag((uint)((b >> 3) & 7));
                        y += UnZigZag((uint)(b & 7));
                    }
                    else if ((b & 0x40) == 0)
                    {
                        if (pos >= end) return null;
                        uint v = (uint)(((b & 0x3F) << 8) | data[pos++]);
                        x += UnZigZag(v & 0x7F);
                        y += UnZigZag(v >> 7);
                    }
                    else
                    {
                        if (end - pos < 3) return null;
                        x = ((b & 0xF) << 8) | data[pos];
                        y = (data[pos + 1] << 8) | data[pos + 2];
                        pos += 3;
                    }
                }
                if (x < 0 || x > UvLevels || y < 0 || y > UvLevels) return null;
                stroke.Points.Add(new Vector2(DequantizeUv(x), DequantizeUv(y)));
            }
            return stroke;
        }
    }
}
