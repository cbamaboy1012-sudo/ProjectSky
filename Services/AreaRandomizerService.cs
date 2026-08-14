using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjectSky.Services
{
    public enum AreaRandomizationMode
    {
        AllAreasTogether,
        EachAreaSeparately
    }

    public sealed record AreaRandomizationResult(string Area, int SlotsChanged, string OutputFile);

    public static class AreaRandomizerService
    {
        private static readonly string[] DefaultKeys = { "species", "species_id", "pokemon", "pokemon_id" };

        public static IReadOnlyList<AreaRandomizationResult> Randomize(
            IReadOnlyList<string> areaFiles,
            string outputDirectory,
            AreaRandomizationMode mode,
            string propertyName,
            int? seed = null)
        {
            if (areaFiles.Count == 0) throw new ArgumentException("No area files were selected.");
            Directory.CreateDirectory(outputDirectory);

            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var documents = areaFiles.Select(path => (Path: path, Json: JsonNode.Parse(File.ReadAllText(path)))).ToList();
            var locations = new List<(JsonNode Node, string Area)>();

            foreach (var document in documents)
                FindTargets(document.Json, propertyName, locations, Path.GetFileNameWithoutExtension(document.Path));

            if (locations.Count == 0)
                throw new InvalidDataException($"No '{propertyName}' fields were found in the selected area files.");

            if (mode == AreaRandomizationMode.AllAreasTogether)
            {
                var values = locations.Select(x => GetValue(x.Node, propertyName)!.DeepClone()).ToList();
                Shuffle(values, rng);
                for (var i = 0; i < locations.Count; i++)
                    SetValue(locations[i].Node, propertyName, values[i]);
            }
            else
            {
                foreach (var group in locations.GroupBy(x => x.Area))
                {
                    var values = group.Select(x => GetValue(x.Node, propertyName)!.DeepClone()).ToList();
                    Shuffle(values, rng);
                    var index = 0;
                    foreach (var target in group)
                        SetValue(target.Node, propertyName, values[index++]);
                }
            }

            var results = new List<AreaRandomizationResult>();
            foreach (var document in documents)
            {
                var output = Path.Combine(outputDirectory, Path.GetFileName(document.Path));
                var json = document.Json!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(output, json);
                var count = locations.Count(x => x.Area == Path.GetFileNameWithoutExtension(document.Path));
                results.Add(new AreaRandomizationResult(Path.GetFileNameWithoutExtension(document.Path), count, output));
            }
            return results;
        }

        public static IReadOnlyList<string> GetSupportedPropertyNames() => DefaultKeys;

        private static void FindTargets(JsonNode? node, string propertyName, List<(JsonNode Node, string Area)> results, string area)
        {
            if (node is JsonObject obj)
            {
                foreach (var property in obj.ToList())
                {
                    if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase) && property.Value != null)
                        results.Add((obj, area));
                    FindTargets(property.Value, propertyName, results, area);
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                    FindTargets(item, propertyName, results, area);
            }
        }

        private static JsonNode? GetValue(JsonNode node, string propertyName) => ((JsonObject)node)[propertyName];

        private static void SetValue(JsonNode node, string propertyName, JsonNode value) => ((JsonObject)node)[propertyName] = value;

        private static void Shuffle<T>(IList<T> values, Random rng)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }
}
