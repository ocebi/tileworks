using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Levels
{
    public readonly struct LevelRef
    {
        public readonly int Index;
        public readonly string LevelId;
        public readonly string Label;
        public readonly int Reward;

        public LevelRef(int index, string levelId, string label, int reward)
        {
            Index = index;
            LevelId = levelId;
            Label = label;
            Reward = reward;
        }

        public override string ToString() => $"#{Index + 1} {LevelId}";
    }

    public interface ILevelSequence
    {
        int Count { get; }
        LevelRef At(int index);
    }

    // Progression order is separate from file names, so levels can be reordered without renaming files.
    public sealed class LevelSequence : ILevelSequence
    {
        private readonly List<LevelRef> _levels = new List<LevelRef>();

        public int Count => _levels.Count;

        public LevelRef At(int index)
        {
            if (_levels.Count == 0) throw new InvalidOperationException("The level sequence is empty.");
            if (index < _levels.Count) return _levels[Math.Max(0, index)];
            // Past the end, replay the last fifth so progression never runs out.
            var tail = Math.Max(1, _levels.Count / 5);
            var source = _levels[_levels.Count - tail + (index - _levels.Count) % tail];
            return new LevelRef(index, source.LevelId, source.Label, source.Reward);
        }

        public static LevelSequence From(IEnumerable<LevelRef> levels)
        {
            var sequence = new LevelSequence();
            foreach (var level in levels) sequence._levels.Add(new LevelRef(sequence._levels.Count, level.LevelId, level.Label, level.Reward));
            return sequence;
        }

        public static LevelSequence Parse(string json)
        {
            var sequence = new LevelSequence();
            var root = JObject.Parse(json);
            if (!(root["levels"] is JArray levels)) return sequence;
            foreach (var entry in levels)
                sequence._levels.Add(new LevelRef(sequence._levels.Count, (string)entry["id"], (string)entry["label"] ?? "", (int?)entry["reward"] ?? 0));
            return sequence;
        }

        public static string Write(IReadOnlyList<LevelRef> levels)
        {
            var array = new JArray();
            foreach (var level in levels)
            {
                var entry = new JObject { ["id"] = level.LevelId, ["reward"] = level.Reward };
                if (!string.IsNullOrEmpty(level.Label)) entry["label"] = level.Label;
                array.Add(entry);
            }

            return CanonicalJson.Write(new JObject { ["levels"] = array });
        }

        public void Validate(ILevelSource levels, ValidationReport report)
        {
            foreach (var level in _levels) report.Require(levels.TryGetText(level.LevelId, out _), $"sequence references missing level {level.LevelId}");
        }
    }

    public interface ILevelSource
    {
        bool TryGetText(string id, out string json);
        IEnumerable<string> Ids { get; }
    }

    public sealed class LevelRepository
    {
        private readonly ILevelSource _source;
        private readonly Dictionary<string, LevelEnvelope> _cache = new Dictionary<string, LevelEnvelope>();

        public LevelRepository(ILevelSource source) => _source = Guard.NotNull(source, nameof(source));

        // After the source's content changes.
        public void Clear() => _cache.Clear();

        public Result<LevelEnvelope> Get(string id)
        {
            if (_cache.TryGetValue(id, out var cached)) return Result<LevelEnvelope>.Ok(cached);
            if (!_source.TryGetText(id, out var json)) return Error.NotFound("level", id);
            try
            {
                var envelope = LevelEnvelope.Parse(json);
                _cache[id] = envelope;
                return Result<LevelEnvelope>.Ok(envelope);
            }
            catch (Exception e)
            {
                return new Error(ErrorKind.Corrupt, "level", $"{id}: {e.Message}");
            }
        }
    }
}
