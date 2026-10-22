using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Levels
{
    // Every level file: a common envelope plus a game-specific payload (§9.8).
    public sealed class LevelEnvelope
    {
        public const int CurrentSchema = 1;

        // Set when a person saves the level; content generators keep tagged levels instead of regenerating them.
        public const string EditedTag = "edited";

        public int Schema = CurrentSchema;
        public string Id = string.Empty;
        public string Game = string.Empty;
        public int PayloadVersion = 1;
        public string ContentHash = string.Empty;
        public string Difficulty = "normal";
        public List<string> Tags = new List<string>();
        public SortedDictionary<string, double> Metrics = new SortedDictionary<string, double>(StringComparer.Ordinal);
        public JObject Payload = new JObject();

        public static LevelEnvelope Parse(string json)
        {
            var root = JObject.Parse(json);
            var meta = root["meta"] as JObject ?? new JObject();
            var envelope = new LevelEnvelope
            {
                Schema = (int?)root["schema"] ?? 0,
                Id = (string)root["id"] ?? string.Empty,
                Game = (string)root["game"] ?? string.Empty,
                PayloadVersion = (int?)root["payloadVersion"] ?? 1,
                ContentHash = (string)root["contentHash"] ?? string.Empty,
                Difficulty = (string)meta["difficulty"] ?? "normal",
                Payload = root["payload"] as JObject ?? new JObject(),
            };
            if (meta["tags"] is JArray tags)
                foreach (var tag in tags) envelope.Tags.Add((string)tag);
            if (meta["metrics"] is JObject metrics)
                foreach (var pair in metrics) envelope.Metrics[pair.Key] = (double)pair.Value;
            return envelope;
        }

        public string ToJson()
        {
            ContentHash = ComputeContentHash(Payload);
            var metrics = new JObject();
            foreach (var pair in Metrics) metrics[pair.Key] = Math.Round(pair.Value, 3);
            var root = new JObject
            {
                ["schema"] = Schema,
                ["id"] = Id,
                ["game"] = Game,
                ["payloadVersion"] = PayloadVersion,
                ["contentHash"] = ContentHash,
                ["meta"] = new JObject { ["difficulty"] = Difficulty, ["tags"] = new JArray(Tags.ToArray()), ["metrics"] = metrics },
                ["payload"] = Payload,
            };
            return CanonicalJson.Write(root);
        }

        public static string ComputeContentHash(JObject payload) =>
            StableHash.ToHex(StableHash.Hash64(payload.ToString(Newtonsoft.Json.Formatting.None)));

        public bool HashMatches() => ContentHash == ComputeContentHash(Payload);
    }

    public interface ILevelSerializer<TLevel>
    {
        string Game { get; }
        int CurrentPayloadVersion { get; }
        TLevel Read(LevelEnvelope envelope);
        JObject Write(TLevel level);
        void Validate(TLevel level, ValidationReport report);
    }
}
