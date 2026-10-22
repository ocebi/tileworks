using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.Search;
using TileMatch.Model;
using UnityEditor;
using UnityEngine;

namespace TileMatch.Editor
{
    // Records a solver line per shipped level as a golden replay. Re-run only for intended rule changes.
    public static class ReplayBaseline
    {
        public const string Folder = "Assets/Games/TileMatch/Tests/Replays";
        public const ulong Seed = 7;

        [MenuItem("Tileworks/Tile Match/Rebaseline Replays")]
        public static void Rebaseline()
        {
            Directory.CreateDirectory(Folder);
            var rules = TileMatchRules.CreateDefault();
            var serializer = new TileMatchLevelSerializer();
            foreach (var path in Directory.GetFiles(ContentBuilder.LevelsFolder, "*.json").OrderBy(Path.GetFileName))
            {
                var envelope = LevelEnvelope.Parse(File.ReadAllText(path));
                var level = serializer.Read(envelope);
                var domain = new TileMatchSearchDomain(rules, level);
                var solution = new DepthFirstSolver<TileMatchState, SelectTile>(domain).Solve(domain.CreateStart(), new SearchBudget(300_000));
                if (!solution.Solved)
                {
                    Debug.LogWarning($"[Tileworks] {envelope.Id}: no solution within budget; replay skipped");
                    continue;
                }

                var engine = rules.CreateEngine(level, Seed);
                var digest = 0UL;
                foreach (var move in solution.Path)
                {
                    var result = engine.Apply(move);
                    digest = EventDigest.Combine(digest, engine.Events.Slice(result.Events));
                }

                var actions = new JArray();
                foreach (var record in engine.Log) actions.Add(new JArray(record.Type, record.A, record.B));
                var json = new JObject
                {
                    ["levelId"] = envelope.Id,
                    ["contentHash"] = envelope.ContentHash,
                    ["rulesVersion"] = TileMatchRules.Version,
                    ["seed"] = Seed,
                    ["finalHash"] = StableHash.ToHex(engine.State.ComputeHash()),
                    ["eventDigest"] = StableHash.ToHex(digest),
                    ["actions"] = actions,
                };
                File.WriteAllText($"{Folder}/{envelope.Id}.json", CanonicalJson.Write(json));
            }

            AssetDatabase.Refresh();
            Debug.Log("[Tileworks] Replays rebaselined");
        }
    }
}
