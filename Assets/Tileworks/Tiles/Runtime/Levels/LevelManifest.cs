using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Tileworks.Tiles.Levels
{
    // What the content server publishes for one game: the play order, with each level's content hash (docs/REMOTE-LEVELS.md).
    // The cache keeps the accepted copy as its index.
    public sealed class LevelManifest
    {
        public const int CurrentSchema = 1;

        // Ids and hashes become cache file names, so a manifest can't name a path.
        private static readonly Regex SafeId = new Regex(@"^[A-Za-z0-9_\-]{1,64}$");
        private static readonly Regex SafeHash = new Regex(@"^[0-9a-f]{8,64}$");

        public sealed class Entry
        {
            public string Id = string.Empty;
            public string Hash = string.Empty;
            public int PayloadVersion = 1;
            public int Reward;
            public string Label = string.Empty;
        }

        public int Schema = CurrentSchema;
        public string Game = string.Empty;

        // Raised by every publish; a client never goes back to a lower one, so a stale CDN copy can't undo an update.
        public int Revision;

        // Base URL of the level files, <Files>/<id>.json. Empty means the manifest's own folder.
        public string Files = string.Empty;

        public List<Entry> Levels = new List<Entry>();

        public static LevelManifest Parse(string json)
        {
            var root = JObject.Parse(json);
            var manifest = new LevelManifest
            {
                Schema = (int?)root["schema"] ?? 0,
                Game = (string)root["game"] ?? string.Empty,
                Revision = (int?)root["revision"] ?? 0,
                Files = (string)root["files"] ?? string.Empty,
            };
            if (!(root["levels"] is JArray levels)) throw new FormatException("The manifest has no levels array.");
            foreach (var level in levels)
            {
                var entry = new Entry
                {
                    Id = (string)level["id"] ?? string.Empty,
                    Hash = (string)level["hash"] ?? string.Empty,
                    PayloadVersion = (int?)level["payloadVersion"] ?? 1,
                    Reward = (int?)level["reward"] ?? 0,
                    Label = (string)level["label"] ?? string.Empty,
                };
                if (!SafeId.IsMatch(entry.Id) || !SafeHash.IsMatch(entry.Hash)) throw new FormatException($"Bad manifest entry '{entry.Id}'.");
                manifest.Levels.Add(entry);
            }

            return manifest;
        }

        public string ToJson()
        {
            var levels = new JArray();
            foreach (var entry in Levels)
            {
                var level = new JObject { ["id"] = entry.Id, ["hash"] = entry.Hash, ["payloadVersion"] = entry.PayloadVersion, ["reward"] = entry.Reward };
                if (entry.Label.Length > 0) level["label"] = entry.Label;
                levels.Add(level);
            }

            var root = new JObject { ["schema"] = Schema, ["game"] = Game, ["revision"] = Revision };
            if (Files.Length > 0) root["files"] = Files;
            root["levels"] = levels;
            return CanonicalJson.Write(root);
        }

        public LevelSequence ToSequence()
        {
            var refs = new List<LevelRef>(Levels.Count);
            foreach (var entry in Levels) refs.Add(new LevelRef(refs.Count, entry.Id, entry.Label, entry.Reward));
            return LevelSequence.From(refs);
        }
    }
}
