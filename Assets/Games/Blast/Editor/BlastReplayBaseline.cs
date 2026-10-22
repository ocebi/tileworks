using System.IO;
using System.Linq;
using Blast.Model;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using UnityEditor;
using UnityEngine;

namespace Blast.Editor
{
    // Records the casual bot's playout of every shipped level as a golden replay, won or lost, landing every piece after
    // each move; every fifth level also uses a hammer and a shuffle, so the boosters are pinned too. Re-run only for
    // intended rule changes.
    public static class BlastReplayBaseline
    {
        public const string Folder = "Assets/Games/Blast/Tests/Replays";
        public const ulong Seed = 7;
        private const int MaxTurns = 200;

        [MenuItem("Tileworks/Blast/Rebaseline Replays")]
        public static void Rebaseline()
        {
            Directory.CreateDirectory(Folder);
            var rules = new BlastRules();
            var serializer = new BlastLevelSerializer();
            var paths = Directory.GetFiles(BlastContentBuilder.LevelsFolder, "*.json").OrderBy(Path.GetFileName).ToArray();
            for (var index = 0; index < paths.Length; index++)
            {
                var envelope = LevelEnvelope.Parse(File.ReadAllText(paths[index]));
                var level = serializer.Read(envelope);
                var domain = new BlastSearchDomain(rules, level);
                var engine = rules.CreateEngine(level, Seed);
                var moves = new Tap[level.CellCount * 2];
                var boosters = index % 5 == 0;
                var digest = 0UL;
                for (var turn = 0; turn < MaxTurns && engine.State.Outcome == Outcome.Ongoing; turn++)
                {
                    var s = engine.State;
                    var count = domain.GetMoves(s, moves);
                    if (count == 0) break;
                    var best = moves.Take(count).OrderByDescending(m => domain.Score(s, m)).First();
                    var result = boosters && turn == 2 ? engine.Apply(new Hammer(best.Cell))
                        : boosters && turn == 4 ? engine.Apply(new ShuffleBoard())
                        : engine.Apply(best);
                    if (!result.Accepted) continue;
                    digest = EventDigest.Combine(digest, engine.Events.Slice(result.Events));
                    if (s.Outcome != Outcome.Ongoing || s.Flight.Count == 0) continue;
                    var landed = engine.Apply(new LandAll());
                    digest = EventDigest.Combine(digest, engine.Events.Slice(landed.Events));
                }

                var actions = new JArray();
                foreach (var record in engine.Log) actions.Add(new JArray(record.Type, record.A, record.B));
                var json = new JObject
                {
                    ["levelId"] = envelope.Id,
                    ["contentHash"] = envelope.ContentHash,
                    ["rulesVersion"] = BlastRules.Version,
                    ["seed"] = Seed,
                    ["outcome"] = engine.State.Outcome.ToString(),
                    ["finalHash"] = StableHash.ToHex(engine.State.ComputeHash()),
                    ["eventDigest"] = StableHash.ToHex(digest),
                    ["actions"] = actions,
                };
                File.WriteAllText($"{Folder}/{envelope.Id}.json", CanonicalJson.Write(json));
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Tileworks] {paths.Length} Blast replays rebaselined");
        }
    }
}
