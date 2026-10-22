using System.IO;
using System.Linq;
using Match3.Model;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using UnityEditor;
using UnityEngine;

namespace Match3.Editor
{
    // Records the casual bot's playout of every shipped level as a golden replay, won or lost; every fifth level also
    // uses a hammer and a shuffle, so the boosters are pinned too. Everything in flight lands after each action, as bots
    // play. Re-run only for intended rule changes.
    public static class Match3ReplayBaseline
    {
        public const string Folder = "Assets/Games/Match3/Tests/Replays";
        public const ulong Seed = 7;
        private const int MaxTurns = 200;

        [MenuItem("Tileworks/Match3/Rebaseline Replays")]
        public static void Rebaseline()
        {
            Directory.CreateDirectory(Folder);
            var rules = new Match3Rules();
            var serializer = new Match3LevelSerializer();
            var paths = Directory.GetFiles(Match3ContentBuilder.LevelsFolder, "*.json").OrderBy(Path.GetFileName).ToArray();
            for (var index = 0; index < paths.Length; index++)
            {
                var envelope = LevelEnvelope.Parse(File.ReadAllText(paths[index]));
                var level = serializer.Read(envelope);
                var domain = new Match3SearchDomain(rules, level);
                var engine = rules.CreateEngine(level, Seed);
                var moves = new Swap[level.CellCount * 2];
                var boosters = index % 5 == 0;
                var digest = 0UL;
                for (var turn = 0; turn < MaxTurns && engine.State.Outcome == Outcome.Ongoing; turn++)
                {
                    var s = engine.State;
                    var count = domain.GetMoves(s, moves);
                    if (count == 0) break;
                    var best = moves.Take(count).OrderByDescending(m => domain.Score(s, m)).First();
                    var result = boosters && turn == 2 ? engine.Apply(new Hammer(best.From))
                        : boosters && turn == 4 ? engine.Apply(new ShuffleBoard())
                        : engine.Apply(best);
                    if (!result.Accepted) continue;
                    digest = EventDigest.Combine(digest, engine.Events.Slice(result.Events));
                    if (engine.State.Outcome != Outcome.Ongoing || engine.State.Flight.Count == 0) continue;
                    result = engine.Apply(new LandAll());
                    digest = EventDigest.Combine(digest, engine.Events.Slice(result.Events));
                }

                var actions = new JArray();
                foreach (var record in engine.Log) actions.Add(new JArray(record.Type, record.A, record.B));
                var json = new JObject
                {
                    ["levelId"] = envelope.Id,
                    ["contentHash"] = envelope.ContentHash,
                    ["rulesVersion"] = Match3Rules.Version,
                    ["seed"] = Seed,
                    ["outcome"] = engine.State.Outcome.ToString(),
                    ["finalHash"] = StableHash.ToHex(engine.State.ComputeHash()),
                    ["eventDigest"] = StableHash.ToHex(digest),
                    ["actions"] = actions,
                };
                File.WriteAllText($"{Folder}/{envelope.Id}.json", CanonicalJson.Write(json));
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Tileworks] {paths.Length} Match3 replays rebaselined");
        }
    }
}
