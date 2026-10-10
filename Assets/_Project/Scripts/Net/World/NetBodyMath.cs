using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Net
{
    /// <summary>Resultado de un pedido de agarre al servidor.</summary>
    public enum GrabVerdict { Ok, AlreadyHeld, TooFar, UnknownBody }

    /// <summary>
    /// Lo puro de la replicacion de cuerpos (F2 de MULTIPLAYER_PLAN): codificar la rotacion en 4 shorts, decidir si un cuerpo cambio lo
    /// bastante para reenviarlo, validar un agarre, suavizado del seguidor y la huella de la lista de cuerpos (para detectar que dos
    /// jugadores tienen versiones distintas del mundo). Con tests EditMode.
    /// </summary>
    public static class NetBodyMath
    {
        /// <summary>Alcance maximo (m) entre el jugador y lo que pide agarrar: el movedor alcanza 12 m, mas margen por el retraso.</summary>
        public const float MaxGrabDistance = 20f;
        /// <summary>Un cuerpo seguidor mas lejos que esto de su objetivo salta de golpe en vez de deslizarse.</summary>
        public const float SnapDistance = 3f;
        public const float PositionEpsilon = 0.003f;
        public const float AngleEpsilon = 0.25f;

        private const float QuatScale = 32767f;

        /// <summary>Rotacion en 4 shorts (componentes normalizadas por 32767; q y -q son la misma rotacion, se fija w >= 0).</summary>
        public static void PackRotation(Quaternion q, out short x, out short y, out short z, out short w)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag < 1e-6f) { x = y = z = 0; w = (short)QuatScale; return; }
            float sign = q.w < 0f ? -1f : 1f;
            float k = sign / mag * QuatScale;
            x = (short)Mathf.Clamp(Mathf.Round(q.x * k), -32767f, 32767f);
            y = (short)Mathf.Clamp(Mathf.Round(q.y * k), -32767f, 32767f);
            z = (short)Mathf.Clamp(Mathf.Round(q.z * k), -32767f, 32767f);
            w = (short)Mathf.Clamp(Mathf.Round(q.w * k), -32767f, 32767f);
        }

        public static Quaternion UnpackRotation(short x, short y, short z, short w)
        {
            var q = new Quaternion(x / QuatScale, y / QuatScale, z / QuatScale, w / QuatScale);
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag < 1e-6f) return Quaternion.identity;
            float inv = 1f / mag;
            return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
        }

        /// <summary>True si la pose cambio lo bastante respecto de la ultima enviada.</summary>
        public static bool NeedsSend(Vector3 lastPos, Quaternion lastRot, Vector3 pos, Quaternion rot)
        {
            if ((pos - lastPos).sqrMagnitude > PositionEpsilon * PositionEpsilon) return true;
            return Quaternion.Angle(lastRot, rot) > AngleEpsilon;
        }

        /// <summary>Valida un pedido de agarre: existe, nadie mas lo tiene (heldBy distinto de requester) y esta al alcance.</summary>
        public static GrabVerdict ValidateGrab(bool bodyExists, int heldBy, int noOwner, int requester, float distance)
        {
            if (!bodyExists) return GrabVerdict.UnknownBody;
            if (heldBy != noOwner && heldBy != requester) return GrabVerdict.AlreadyHeld;
            if (distance > MaxGrabDistance) return GrabVerdict.TooFar;
            return GrabVerdict.Ok;
        }

        /// <summary>Factor de suavizado exponencial para un paso de dt segundos (0 = no se mueve, 1 = llega).</summary>
        public static float FollowAlpha(float rate, float dt) => 1f - Mathf.Exp(-Mathf.Max(0f, rate) * Mathf.Max(0f, dt));

        /// <summary>Cuantos mensajes de perPacket hacen falta para count elementos.</summary>
        public static int ChunkCount(int count, int perPacket) => perPacket <= 0 ? 0 : (count + perPacket - 1) / perPacket;

        /// <summary>Huella FNV-1a de la lista de claves (ordenadas): igual en dos maquinas si tienen el mismo mundo.</summary>
        public static uint Fingerprint(IReadOnlyList<string> sortedKeys)
        {
            uint h = 2166136261u;
            for (int i = 0; i < sortedKeys.Count; i++)
            {
                string key = sortedKeys[i];
                for (int c = 0; c < key.Length; c++) { h ^= key[c]; h *= 16777619u; }
                h ^= 0x1F; h *= 16777619u; // separador entre claves
            }
            return h;
        }

        /// <summary>
        /// Clave estable de un transform entre maquinas con la misma escena: nombre#indice de hermano desde la raiz. Los objetos de la RAIZ
        /// de la escena no usan el indice (en un ejecutable todos devuelven 0, en el editor tienen su orden): se distinguen por el nombre y la
        /// posicion de arranque en centimetros.
        /// </summary>
        public static string PathKey(Transform t)
        {
            if (t == null) return "";
            var parts = new List<string>(8);
            for (Transform cur = t; cur != null; cur = cur.parent)
            {
                if (cur.parent == null)
                {
                    Vector3 p = cur.position;
                    parts.Add(cur.name + "@" + p.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ","
                              + p.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ","
                              + p.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }
                else parts.Add(cur.name + "#" + cur.GetSiblingIndex());
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Orden de las claves (ordinal: igual en todas las maquinas).</summary>
        public static int CompareKeys(string a, string b) => string.CompareOrdinal(a, b);
    }
}
