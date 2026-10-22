using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blast.Model;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;

namespace Blast.Tests
{
    // Every shipped level has a recorded playout; replaying it must reproduce the outcome, the final state and every event.
    public sealed class BlastGoldenReplayTests
    {
        private const string Replays = "Assets/Games/Blast/Tests/Replays";
        private const string Levels = "Assets/Games/Blast/Content/Levels";
        private static readonly BlastRules Rules = new BlastRules();

        private static IEnumerable<string> ReplayFiles() =>
            Directory.Exists(Replays) ? Directory.GetFiles(Replays, "*.json").OrderBy(Path.GetFileName) : Enumerable.Empty<string>();

        [Test]
        public void EveryShippedLevel_HasAReplay()
        {
            var levels = Directory.GetFiles(Levels, "*.json").Select(Path.GetFileNameWithoutExtension);
            var replays = ReplayFiles().Select(Path.GetFileNameWithoutExtension);
            Assert.That(levels.Except(replays), Is.Empty, "run Tileworks/Blast/Rebaseline Replays");
        }

        [TestCaseSource(nameof(ReplayFiles))]
        public void Replay_ReproducesOutcomeStateAndEvents(string path)
        {
            var replay = JObject.Parse(File.ReadAllText(path));
            var envelope = LevelEnvelope.Parse(File.ReadAllText($"{Levels}/{(string)replay["levelId"]}.json"));
            Assert.That(envelope.ContentHash, Is.EqualTo((string)replay["contentHash"]), "level content changed; rebaseline if intended");

            var engine = Rules.CreateEngine(new BlastLevelSerializer().Read(envelope), (ulong)replay["seed"]);
            var digest = 0UL;
            foreach (var row in (JArray)replay["actions"])
            {
                var result = engine.ApplyRecord(new ActionRecord((ushort)(int)row[0], (int)row[1], (int)row[2]));
                Assert.That(result.Accepted, Is.True, $"{row} rejected: {result.Rejected}");
                digest = EventDigest.Combine(digest, engine.Events.Slice(result.Events));
            }

            Assert.That(engine.State.Outcome.ToString(), Is.EqualTo((string)replay["outcome"]));
            Assert.That(StableHash.ToHex(engine.State.ComputeHash()), Is.EqualTo((string)replay["finalHash"]));
            Assert.That(StableHash.ToHex(digest), Is.EqualTo((string)replay["eventDigest"]));
        }
    }
}
