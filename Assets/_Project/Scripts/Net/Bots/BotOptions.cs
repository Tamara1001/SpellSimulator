using System;
using System.Globalization;

namespace SpellSimulator.Net.Bots
{
    /// <summary>Que hace el bot una vez dentro de la sala.</summary>
    public enum BotScript { Walk, Churn, Grab, Draw, Cast, World, Portal, Duel, DuelInk }

    /// <summary>
    /// Opciones de un bot de pruebas, leidas de la linea de comandos del ejecutable (logica pura, con tests).
    /// Ejemplo: SpellSimulator.exe -batchmode -nographics -bot -botjoin ABC123 -botname Bot1 -botscript walk -botseconds 40 -botexpect 2 -botlog C:\ruta\bot1.txt
    /// </summary>
    public sealed class BotOptions
    {
        public bool Enabled;
        /// <summary>Codigo de Relay para unirse (vacio si se usa IP directa).</summary>
        public string JoinCode = "";
        /// <summary>IP directa para pruebas locales sin nube (vacio si se usa Relay).</summary>
        public string DirectIp = "";
        public ushort DirectPort = 7788;
        public string Name = "Bot";
        public BotScript Script = BotScript.Walk;
        /// <summary>Segundos jugando (por ciclo en Churn).</summary>
        public float Seconds = 30f;
        /// <summary>Cuantos jugadores remotos tiene que ver como minimo para dar el resultado por bueno.</summary>
        public int ExpectRemotes;
        /// <summary>Ciclos de entrar y salir (solo Churn).</summary>
        public int Cycles = 3;
        public string LogPath = "";
        /// <summary>Momento (ms UTC desde 1970) en que arranca el guion Grab: todos los bots empiezan a la vez para pelear por las mismas cajas.</summary>
        public long StartAtMs;
        /// <summary>Latencia simulada de ida (ms) en el transporte (solo builds de desarrollo): el viaje de ida y vuelta es el doble.</summary>
        public int LatencyMs;

        public bool HasTarget => !string.IsNullOrEmpty(JoinCode) || !string.IsNullOrEmpty(DirectIp);

        public static BotOptions Parse(string[] args)
        {
            var o = new BotOptions();
            if (args == null) return o;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == null) continue;
                bool hasValue = i + 1 < args.Length;
                switch (a.ToLowerInvariant())
                {
                    case "-bot": o.Enabled = true; break;
                    case "-botjoin": if (hasValue) o.JoinCode = args[++i].Trim().ToUpperInvariant(); break;
                    case "-botdirect":
                        if (hasValue) SplitEndpoint(args[++i], out o.DirectIp, ref o.DirectPort);
                        break;
                    case "-botname": if (hasValue) o.Name = args[++i]; break;
                    case "-botscript":
                        if (hasValue) o.Script = ParseScript(args[++i]);
                        break;
                    case "-botat": if (hasValue && long.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out long at)) o.StartAtMs = at; break;
                    case "-botlatency": if (hasValue) o.LatencyMs = Math.Max(0, (int)ParseFloat(args[++i], 0f)); break;
                    case "-botseconds": if (hasValue) o.Seconds = Math.Max(1f, ParseFloat(args[++i], o.Seconds)); break;
                    case "-botexpect": if (hasValue) o.ExpectRemotes = Math.Max(0, (int)ParseFloat(args[++i], 0f)); break;
                    case "-botcycles": if (hasValue) o.Cycles = Math.Max(1, (int)ParseFloat(args[++i], o.Cycles)); break;
                    case "-botlog": if (hasValue) o.LogPath = args[++i]; break;
                }
            }
            return o;
        }

        public static BotScript ParseScript(string text)
        {
            switch ((text ?? "").ToLowerInvariant())
            {
                case "churn": return BotScript.Churn;
                case "grab": return BotScript.Grab;
                case "draw": return BotScript.Draw;
                case "cast": return BotScript.Cast;
                case "world": return BotScript.World;
                case "portal": return BotScript.Portal;
                case "duel": return BotScript.Duel;
                case "duelink": return BotScript.DuelInk;
                default: return BotScript.Walk;
            }
        }

        /// <summary>Numero del final del nombre ("Bot3" -> 3; 0 si no hay).</summary>
        public static int IndexOfName(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            int end = name.Length, start = end;
            while (start > 0 && char.IsDigit(name[start - 1])) start--;
            return start < end && int.TryParse(name.Substring(start), out int n) ? n : 0;
        }

        /// <summary>"ip:puerto" o solo "ip".</summary>
        public static void SplitEndpoint(string text, out string ip, ref ushort port)
        {
            ip = text ?? "";
            int colon = ip.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(ip.Substring(colon + 1), out ushort p)) { port = p; ip = ip.Substring(0, colon); }
            ip = ip.Trim();
        }

        private static float ParseFloat(string s, float fallback)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }
}
