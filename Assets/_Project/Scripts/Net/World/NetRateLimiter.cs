using System.Collections.Generic;

namespace SpellSimulator.Net
{
    /// <summary>Canales de mensajes del cliente al anfitrion con tope de frecuencia.</summary>
    public enum NetChannel : byte { Grab = 0, Hold = 1, Release = 2, Reset = 3, PortalCross = 4, Strokes = 5, Cast = 6 }

    /// <summary>
    /// Anti-trampas simple del anfitrion: un cubo de fichas por (cliente, canal). Cada mensaje gasta una ficha; se recargan con el tiempo y, si no
    /// quedan, el mensaje se descarta. No busca detener a un tramposo decidido (no hay competitivo con premios): solo evita que un cliente roto o
    /// travieso sature al anfitrion. Logica pura (el tiempo lo pasa quien llama) y sin allocations despues de la primera vez que ve a un cliente.
    /// </summary>
    public sealed class NetRateLimiter
    {
        private struct Bucket { public float tokens, last; public bool init; }

        private readonly Dictionary<ulong, Bucket> buckets = new Dictionary<ulong, Bucket>(16);
        private readonly float[] perSecond, burst;
        public int Dropped { get; private set; }

        public NetRateLimiter()
        {
            perSecond = new float[7]; burst = new float[7];
            Set(NetChannel.Grab, 10f, 20f);
            Set(NetChannel.Hold, 120f, 240f);
            Set(NetChannel.Release, 10f, 20f);
            Set(NetChannel.Reset, 1f, 3f);
            Set(NetChannel.PortalCross, 4f, 6f);
            Set(NetChannel.Strokes, 120f, 300f);
            Set(NetChannel.Cast, 5f, 8f);
        }

        public void Set(NetChannel channel, float refillPerSecond, float maxBurst)
        {
            perSecond[(int)channel] = refillPerSecond;
            burst[(int)channel] = maxBurst;
        }

        /// <summary>True si el mensaje pasa (gasta una ficha); false si el cliente se paso de frecuencia.</summary>
        public bool Allow(ulong client, NetChannel channel, float now)
        {
            ulong key = client * 8UL + (ulong)channel;
            int c = (int)channel;
            buckets.TryGetValue(key, out Bucket b);
            if (!b.init) { b.init = true; b.tokens = burst[c]; b.last = now; }
            float dt = now - b.last;
            if (dt > 0f) { b.tokens = System.Math.Min(burst[c], b.tokens + dt * perSecond[c]); b.last = now; }
            bool ok = b.tokens >= 1f;
            if (ok) b.tokens -= 1f; else Dropped++;
            buckets[key] = b;
            return ok;
        }

        /// <summary>Olvida a un cliente que se fue.</summary>
        public void Forget(ulong client)
        {
            for (int c = 0; c < 7; c++) buckets.Remove(client * 8UL + (ulong)c);
        }
    }
}
