using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using TileMatch.Model;

namespace TileMatch.Tests
{
    // Every shipped level has a recorded line; replaying it must reproduce the final state and every event (§16.4).
    public sealed class GoldenReplayTests
    {
        private const string Replays = "Assets/Games/TileMatch/Tests/Replays";
        private const string Levels = "Assets/Games/TileMatch/Content/Levels";

        private static IEnumerable<string> ReplayFiles() =>
            Directory.Exists(Replays) ? Directory.GetFiles(Replays, "*.json").OrderBy(Path.GetFileName) : Enumerable.Empty<string>();

        [Test]
        public void EveryShippedLevel_HasAReplay()
        {
            var levels = Directory.GetFiles(Levels, "*.json").Select(Path.GetFileNameWithoutExtension);
            var replays = ReplayFiles().Select(Path.GetFileNameWithoutExtension);
            Assert.That(levels.Except(replays), Is.Empty, "run Tileworks/Tile Match/Rebaseline Replays");
        }

        [TestCaseSource(nameof(ReplayFiles))]
        public void Replay_ReproducesStateAndEvents(string path)
        {
            var replay = JObject.Parse(File.ReadAllText(path));
            var envelope = LevelEnvelope.Parse(File.ReadAllText($"{Levels}/{(string)replay["levelId"]}.json"));
            Assert.That(envelope.ContentHash, Is.EqualTo((string)replay["contentHash"]), "level content changed; rebaseline if intended");

            var level = new TileMatchLevelSerializer().Read(envelope);
            var engine = RulesFixture.Rules.CreateEngine(level, (ulong)replay["seed"]);
            var digest = 0UL;
            foreach (var row in (JArray)replay["actions"])
            {
                var result = engine.ApplyRecord(new ActionRecord((ushort)(int)row[0], (int)row[1], (int)row[2]));
                Assert.That(result.Accepted, Is.True, $"{row} rejected: {result.Rejected}");
                digest = EventDigest.Combine(digest, engine.Events.Slice(result.Events));
            }

            Assert.That(engine.State.Outcome, Is.EqualTo(Outcome.Won));
            Assert.That(StableHash.ToHex(engine.State.ComputeHash()), Is.EqualTo((string)replay["finalHash"]));
            Assert.That(StableHash.ToHex(digest), Is.EqualTo((string)replay["eventDigest"]));
        }
    }
}
