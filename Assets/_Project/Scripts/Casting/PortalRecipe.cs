using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Casting
{
    // ---- Esquema del JSON (Data/Portals/portals.json, version 1). Solo para JsonUtility: la logica usa PortalRecipe. ----

    [Serializable]
    public sealed class PortalInnerData
    {
        public string sigil;
        public string[] signs;
    }

    [Serializable]
    public sealed class PortalRecipeData
    {
        public string sigil;
        public string[] signs;
        public bool nested;
        /// <summary>Solo si nested: la receta de cada circulo interior (un nivel).</summary>
        public PortalInnerData[] inner;
    }

    [Serializable]
    public sealed class PortalEntryData
    {
        public string id;
        public string pair;
        public string displayName;
        public PortalRecipeData recipe;
    }

    [Serializable]
    public sealed class PortalFileData
    {
        public int version;
        public PortalEntryData[] portals;
    }

    /// <summary>
    /// Receta de un portal: un sigilo y un multiconjunto de signos (el orden no importa). En una receta anidada el exterior
    /// no lleva sigilo ni signos: lo que cuenta es la receta de cada interior.
    /// </summary>
    public sealed class PortalRecipe
    {
        public string Sigil { get; }
        /// <summary>Signos ordenados (ordinal) para comparar y mostrar de forma estable.</summary>
        public IReadOnlyList<string> Signs => signs;
        public bool Nested { get; }
        /// <summary>Recetas de los interiores (vacio si no es anidada).</summary>
        public IReadOnlyList<PortalRecipe> Inner => inner;

        private readonly string[] signs;
        private readonly PortalRecipe[] inner;

        public PortalRecipe(string sigil, IEnumerable<string> signIds, bool nested = false, IEnumerable<PortalRecipe> innerRecipes = null)
        {
            Sigil = sigil ?? "";
            var list = new List<string>();
            if (signIds != null) foreach (string s in signIds) if (!string.IsNullOrEmpty(s)) list.Add(s);
            signs = list.ToArray();
            Array.Sort(signs, StringComparer.Ordinal);
            Nested = nested;
            inner = innerRecipes != null ? new List<PortalRecipe>(innerRecipes).ToArray() : Array.Empty<PortalRecipe>();
        }
    }

    /// <summary>Un portal declarado en los datos: su id, el id del portal par y su receta.</summary>
    public sealed class PortalDefinition
    {
        public string Id { get; }
        public string PairId { get; }
        public string DisplayName { get; }
        public PortalRecipe Recipe { get; }

        public PortalDefinition(string id, string pairId, string displayName, PortalRecipe recipe)
        {
            Id = id; PairId = pairId; DisplayName = displayName; Recipe = recipe;
        }
    }

    /// <summary>Catalogo de portales cargado de portals.json.</summary>
    public sealed class PortalRecipes
    {
        public const int SupportedVersion = 1;
        /// <summary>Ruta del archivo de datos en el proyecto (el editor lo carga de aca; en un build va como TextAsset asignado).</summary>
        public const string AssetPath = "Assets/_Project/Data/Portals/portals.json";

        public static readonly PortalRecipes Empty = new PortalRecipes(new List<PortalDefinition>());

        private readonly List<PortalDefinition> portals;
        public IReadOnlyList<PortalDefinition> Portals => portals;

        public PortalRecipes(List<PortalDefinition> definitions) { portals = definitions; }

        public PortalDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < portals.Count; i++)
                if (portals[i].Id == id) return portals[i];
            return null;
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
                if (entry == null || string.IsNullOrEmpty(entry.id) || entry.recipe == null) continue;
                list.Add(new PortalDefinition(entry.id, entry.pair, entry.displayName, ToRecipe(entry.recipe)));
            }
            return new PortalRecipes(list);
        }

        private static PortalRecipe ToRecipe(PortalRecipeData data)
        {
            List<PortalRecipe> inner = null;
            if (data.nested && data.inner != null)
            {
                inner = new List<PortalRecipe>(data.inner.Length);
                foreach (PortalInnerData i in data.inner)
                    if (i != null) inner.Add(new PortalRecipe(i.sigil, i.signs));
            }
            return new PortalRecipe(data.sigil, data.signs, data.nested, inner);
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
    /// si el sello es de un solo circulo.
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

    /// <summary>Logica pura de portales: decide si lo leido en la placa coincide con la receta.</summary>
    public static class PortalMath
    {
        /// <summary>Confianza minima de cada glifo leido para que cuente (valor inicial, ajustable tras probar).</summary>
        public const float DefaultMinConfidence = 0.35f;

        /// <summary>
        /// Coincidencia de un sello de un solo circulo con una receta NO anidada: mismo sigilo y mismo multiconjunto de signos
        /// (el orden no importa; signos de mas o de menos no coinciden). Los ids vienen ya filtrados por confianza (se leen con
        /// confianza 1): <paramref name="minConfidence"/> pesa de verdad en la sobrecarga que recibe un <see cref="PortalReading"/>.
        /// </summary>
        public static bool Matches(PortalRecipe recipe, string sigilId, IReadOnlyList<string> signIds, float minConfidence)
        {
            var reading = new PortalReading { SigilId = sigilId };
            if (signIds != null) foreach (string id in signIds) reading.Signs.Add(new PortalGlyph(id, 1f));
            return Matches(recipe, reading, minConfidence);
        }

        /// <summary>
        /// Coincidencia de una lectura (plana o anidada) con la receta. Un glifo con confianza menor a
        /// <paramref name="minConfidence"/> hace fallar la coincidencia (lectura dudosa). En una receta anidada cada interior de la
        /// receta debe coincidir con un interior distinto de la lectura (sin repetir) y la cantidad de interiores debe ser la misma.
        /// </summary>
        public static bool Matches(PortalRecipe recipe, PortalReading reading, float minConfidence)
        {
            if (recipe == null || reading == null) return false;
            if (recipe.Nested)
            {
                if (reading.Inners == null || reading.Inners.Count != recipe.Inner.Count || recipe.Inner.Count == 0) return false;
                return AssignInner(recipe, reading, 0, 0u, minConfidence);
            }
            if (reading.Inners != null) return false;
            return MatchesFlat(recipe, reading, minConfidence);
        }

        private static bool AssignInner(PortalRecipe recipe, PortalReading reading, int index, uint used, float minConfidence)
        {
            if (index == recipe.Inner.Count) return true;
            for (int j = 0; j < reading.Inners.Count && j < 32; j++)
            {
                if ((used & (1u << j)) != 0) continue;
                if (!MatchesFlat(recipe.Inner[index], reading.Inners[j], minConfidence)) continue;
                if (AssignInner(recipe, reading, index + 1, used | (1u << j), minConfidence)) return true;
            }
            return false;
        }

        private static bool MatchesFlat(PortalRecipe recipe, PortalReading reading, float minConfidence)
        {
            if (recipe.Nested || string.IsNullOrEmpty(reading.SigilId) || reading.SigilId != recipe.Sigil) return false;
            if (reading.SigilConfidence < minConfidence) return false;
            if (reading.Signs.Count != recipe.Signs.Count || reading.Signs.Count > 32) return false;

            uint used = 0u;
            for (int i = 0; i < recipe.Signs.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < reading.Signs.Count; j++)
                {
                    if ((used & (1u << j)) != 0 || reading.Signs[j].Id != recipe.Signs[i]) continue;
                    if (reading.Signs[j].Confidence < minConfidence) return false;
                    used |= 1u << j;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }

        /// <summary>El jugador esta dentro del radio de viaje del portal.</summary>
        public static bool InRange(Vector3 playerPosition, Vector3 anchorPosition, float radius)
            => (playerPosition - anchorPosition).sqrMagnitude <= radius * radius;
    }
}
