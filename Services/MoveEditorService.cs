using ProjectSky.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectSky.Services
{
    public sealed class MoveEditorService
    {
        private readonly string _moveJsonPath;
        private readonly string _moveListResourcePath;
        private WazaArray.Wazas? _original;
        private WazaArray.Wazas? _working;
        private Dictionary<string, WazaArray.Waza> _byId = new(StringComparer.OrdinalIgnoreCase);

        public MoveEditorService(string moveJsonPath, string moveListResourcePath)
        {
            _moveJsonPath = moveJsonPath;
            _moveListResourcePath = moveListResourcePath;
        }

        public IReadOnlyList<WazaArray.Waza> Moves => _working?.table ?? Array.Empty<WazaArray.Waza>();

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            await using var stream = File.OpenRead(_moveJsonPath);
            var loaded = await JsonSerializer.DeserializeAsync<WazaArray.Wazas>(stream, cancellationToken: cancellationToken)
                         ?? throw new InvalidDataException("Unable to load move data.");
            _original = JsonSerializer.Deserialize<WazaArray.Wazas>(JsonSerializer.Serialize(loaded));
            _working = loaded;
            _byId = loaded.table.Where(x => !string.IsNullOrWhiteSpace(x.move_id))
                .GroupBy(x => x.move_id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        }

        public WazaArray.Waza? Get(string moveId) => moveId != null && _byId.TryGetValue(moveId, out var move) ? move : null;

        public IEnumerable<WazaArray.Waza> Search(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Moves;
            return Moves.Where(x => x.move_id?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
        }

        public void Reset()
        {
            if (_original == null) return;
            _working = JsonSerializer.Deserialize<WazaArray.Wazas>(JsonSerializer.Serialize(_original));
            _byId = _working!.table.Where(x => !string.IsNullOrWhiteSpace(x.move_id))
                .ToDictionary(x => x.move_id, x => x, StringComparer.OrdinalIgnoreCase);
        }

        public void Duplicate(string sourceId, string newId)
        {
            var source = Get(sourceId) ?? throw new KeyNotFoundException(sourceId);
            if (Get(newId) != null) throw new InvalidOperationException($"Move '{newId}' already exists.");
            var copy = JsonSerializer.Deserialize<WazaArray.Waza>(JsonSerializer.Serialize(source))!;
            copy.move_id = newId;
            _working!.table.Add(copy);
            _byId[newId] = copy;
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            if (_working == null) throw new InvalidOperationException("Move data is not loaded.");
            var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            await File.WriteAllTextAsync(_moveJsonPath, JsonSerializer.Serialize(_working, options), cancellationToken);
        }
    }
}
