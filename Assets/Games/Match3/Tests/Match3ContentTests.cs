using System.IO;
using System.Linq;
using Match3.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;

namespace Match3.Tests
{
    public sealed class Match3ContentTests
    {
        private const string Root = "Assets/Games/Match3/Content";

        [Test]
        public void EveryShippedLevel_Reads_Validates_AndStartsWithAMoveButNoMatch()
        {
            var serializer = new Match3LevelSerializer();
            var rules = new Match3Rules();
            var files = Directory.GetFiles(Root + "/Levels", "*.json");
            Assert.That(files.Length, Is.EqualTo(100));
            foreach (var file in files)
            {
                var envelope = LevelEnvelope.Parse(File.ReadAllText(file));
                Assert.That(envelope.HashMatches(), Is.True, $"{envelope.Id}: content hash");
                var level = serializer.Read(envelope);
                var report = new ValidationReport();
                serializer.Validate(level, report);
                Assert.That(report.HasErrors, Is.False, $"{envelope.Id}: {report}");
                var start = rules.CreateState(level, 1);
                Assert.That(Matches.HasMove(start), Is.True, envelope.Id);
                Assert.That(Matches.AnyMatch(start), Is.False, $"{envelope.Id}: a ready-made match");
            }

            var sequence = LevelSequence.Parse(File.ReadAllText(Root + "/sequence.json"));
            var ids = Enumerable.Range(0, sequence.Count).Select(i => sequence.At(i).LevelId);
            Assert.That(ids, Is.EqualTo(files.Select(Path.GetFileNameWithoutExtension).OrderBy(id => id)));
        }
    }
}
