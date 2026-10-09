using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Casting
{
    // ---- Esquema del JSON (Data/Portals/portals.json, version 2). Solo para JsonUtility: la logica usa PortalIdentity. ----

    [Serializable]
    public sealed class PortalIdentityData
    {
        public string sigil;
        public string[] signs;
    }

    [Serializable]
    public sealed class PortalEntryData
    {
        public string id;
        public string displayName;
        public PortalIdentityData identity;
    }

    [Serializable]
    public sealed class PortalFileData
    {
        public int version;
        public PortalEntryData[] portals;
    }

    /// <summary>
    /// Identidad de un portal: 1 sigilo + N signos (el orden no importa). Es lo que esta grabado en el portal y lo que hay que
    /// escribir en la placa de OTRO portal para viajar hasta este. Los signos quedan ordenados (ordinal) para mostrar estable.
    /// </summary>
    public readonly struct PortalIdentity
    {
        public readonly string Sigil;
        public readonly string[] Signs;

        public PortalIdentity(string sigil, IEnumerable<string> signIds)
        {
            Sigil = sigil ?? "";
            var list = new List<string>();
            if (signIds != null) foreach (string s in signIds) if (!string.IsNullOrEmpty(s)) list.Add(s);
            Signs = list.ToArray();
            Array.Sort(Signs, StringComparer.Ordinal);
        }

        /// <summary>Tiene sigilo y al menos un signo.</summary>
        public bool IsValid => !string.IsNullOrEmpty(Sigil) && Signs != null && Signs.Length > 0;
    }

    /// <summary>Un portal declarado en los datos: su id, su nombre y su identidad.</summary>
    public sealed class PortalDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public PortalIdentity Identity { get; }

        public PortalDefinition(string id, string displayName, PortalIdentity identity)
        {
            Id = id; DisplayName = displayName; Identity = identity;
        }
    }

    /// <summary>Catalogo de portales cargado de portals.json.</summary>
    public sealed class PortalRecipes
    {
        public const int SupportedVersion = 2;
        /// <summary>Ruta del archivo de datos en el proyecto (el editor lo carga de aca; en un build va como TextAsset asignado).</summary>
        public const string AssetPath = "Assets/_Project/Data/Portals/portals.json";

        public static readonly PortalRecipes Empty = new PortalRecipes(new List<PortalDefinition>());

        private readonly List<PortalDefinition> portals;
        private readonly List<PortalIdentity> identities;
        public IReadOnlyList<PortalDefinition> Portals => portals;
        /// <summary>Identidades en el mismo orden que <see cref="Portals"/> (para <see cref="PortalMath.BestMatch"/>).</summary>
        public IReadOnlyList<PortalIdentity> Identities => identities;

        public PortalRecipes(List<PortalDefinition> definitions)
        {
            portals = definitions;
            identities = new List<PortalIdentity>(definitions.Count);
            foreach (PortalDefinition d in definitions) identities.Add(d.Identity);
        }

        public PortalDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < portals.Count; i++)
                if (portals[i].Id == id) return portals[i];
            return null;
        }

        /// <summary>
        /// Carga portals.json por ruta de proyecto. Sirve en el EDITOR sin escena (builders, tests); en un build devuelve el
        /// catalogo vacio (alli el Portal usa el TextAsset asignado).
        /// </summary>
        public static PortalRecipes LoadDefault()
        {
#if UNITY_EDITOR
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetPath));
            if (!File.Exists(path)) return Empty;
            return Load(File.ReadAllText(path));
#else
            return Empty;
#endif
        }

        /// <summary>Parsea el JSON (solo al cargar). Devuelve un catalogo vacio y avisa si el texto o la version no sirven.</summary>
        public static PortalRecipes Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Empty;
            PortalFileData file;
            try { file = JsonUtility.FromJson<PortalFileData>(json); }
            catch (ArgumentException e)
            {
                Debug.LogError("[Portal] portals.json invalido: " + e.Message);
                return Empty;
            }
            if (file == null || file.portals == null) return Empty;
            if (file.version != SupportedVersion)
            {
                Debug.LogError($"[Portal] portals.json version {file.version} no soportada (se esperaba {SupportedVersion}).");
                return Empty;
            }

            var list = new List<PortalDefinition>(file.portals.Length);
            foreach (PortalEntryData entry in file.portals)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || entry.identity == null) continue;
                list.Add(new PortalDefinition(entry.id, entry.displayName, new PortalIdentity(entry.identity.sigil, entry.identity.signs)));
            }
            return new PortalRecipes(list);
        }
    }

    /// <summary>Un glifo leido: su id y la confianza del reconocedor.</summary>
    public readonly struct PortalGlyph
    {
        public readonly string Id;
        public readonly float Confidence;
        public PortalGlyph(string id, float confidence) { Id = id; Confidence = confidence; }
    }

    /// <summary>
    /// Lo que se leyo de un sello, reducido a ids y confianzas (sin depender del reconocedor ni del lienzo). Inners es null
    /// si el sello es de un solo circulo (un sello compuesto nunca coincide con una identidad de portal).
    /// </summary>
    public sealed class PortalReading
    {
        public string SigilId;
        public float SigilConfidence = 1f;
        public readonly List<PortalGlyph> Signs = new List<PortalGlyph>(4);
        public List<PortalReading> Inners;

        public static PortalReading FromAst(GlyphAST ast)
        {
            var reading = new PortalReading();
            if (ast == null) return reading;
            if (ast.PrimarySigil != null)
            {
                reading.SigilId = ast.PrimarySigil.Id;
                reading.SigilConfidence = ast.PrimarySigil.Confidence;
            }
            foreach (GlyphRecognition sign in ast.Signs) reading.Signs.Add(new PortalGlyph(sign.Id, sign.Confidence));
            return reading;
        }

        public static PortalReading FromNested(NestedReading nested)
        {
            var reading = new PortalReading { Inners = new List<PortalReading>(nested.Subs.Count) };
            foreach (NestedSubReading sub in nested.Subs) reading.Inners.Add(FromAst(sub.Reading.Ast));
            return reading;
        }
    }

    /// <summary>Logica pura de portales: decide a que identidad corresponde lo leido en la placa.</summary>
    public static class PortalMath
    {
        /// <summary>Confianza minima de cada glifo leido para que cuente (valor inicial, ajustable tras probar).</summary>
        public const float DefaultMinConfidence = 0.2f;

        /// <summary>Signos de mas tolerados en la lectura respecto de la identidad.</summary>
        public const int DefaultMaxExtraSigns = 1;

        /// <summary>
        /// Coincidencia de una lectura con UNA identidad: mismo sigilo y todos los signos de la identidad presentes (multiconjunto,
        /// sin reutilizar un glifo leido para dos signos). Los glifos con confianza menor a <paramref name="minConfidence"/> no
        /// cuentan (ni como presentes ni como sobrantes). Se toleran hasta <paramref name="maxExtraSigns"/> signos sobrantes;
        /// <paramref name="excess"/> es cuantos sobraron.
        /// </summary>
        public static bool Matches(PortalIdentity identity, PortalReading reading, float minConfidence, out int excess, int maxExtraSigns = DefaultMaxExtraSigns)
        {
            excess = 0;
            if (!identity.IsValid || reading == null || reading.Inners != null) return false;
            if (string.IsNullOrEmpty(reading.SigilId) || reading.SigilId != identity.Sigil || reading.SigilConfidence < minConfidence) return false;
            if (reading.Signs.Count > 32) return false;

            int valid = 0;
            for (int j = 0; j < reading.Signs.Count; j++)
                if (reading.Signs[j].Confidence >= minConfidence) valid++;
            if (valid < identity.Signs.Length || valid - identity.Signs.Length > maxExtraSigns) return false;

            uint used = 0u;
            for (int i = 0; i < identity.Signs.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < reading.Signs.Count; j++)
                {
                    if ((used & (1u << j)) != 0 || reading.Signs[j].Confidence < minConfidence || reading.Signs[j].Id != identity.Signs[i]) continue;
                    used |= 1u << j;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            excess = valid - identity.Signs.Length;
            return true;
        }

        /// <summary>
        /// Indice de la identidad que mejor explica la lectura, o -1 si ninguna coincide. Gana la de mayor cobertura (mas signos
        /// de identidad cubiertos) y, a igualdad, la de menor exceso; si empatan del todo, la primera.
        /// </summary>
        public static int BestMatch(IReadOnlyList<PortalIdentity> identities, PortalReading reading, float minConfidence, int maxExtraSigns = DefaultMaxExtraSigns)
        {
            if (identities == null) return -1;
            int best = -1, bestCover = -1, bestExcess = int.MaxValue;
            for (int i = 0; i < identities.Count; i++)
            {
                if (!Matches(identities[i], reading, minConfidence, out int excess, maxExtraSigns)) continue;
                int cover = identities[i].Signs.Length;
                if (cover > bestCover || (cover == bestCover && excess < bestExcess))
                {
                    best = i; bestCover = cover; bestExcess = excess;
                }
            }
            return best;
        }
    }
}
