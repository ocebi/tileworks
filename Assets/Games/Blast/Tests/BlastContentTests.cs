using System.IO;
using System.Linq;
using Blast.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;

namespace Blast.Tests
{
    public sealed class BlastContentTests
    {
        private const string Root = "Assets/Games/Blast/Content";

        [Test]
        public void EveryShippedLevel_Reads_Validates_AndStartsWithAMove()
        {
            var serializer = new BlastLevelSerializer();
            var rules = new BlastRules();
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
                Assert.That(BlastBoard.HasMove(rules.CreateState(level, 1)), Is.True, envelope.Id);
            }

            var sequence = LevelSequence.Parse(File.ReadAllText(Root + "/sequence.json"));
            var ids = Enumerable.Range(0, sequence.Count).Select(i => sequence.At(i).LevelId);
            Assert.That(ids, Is.EqualTo(files.Select(Path.GetFileNameWithoutExtension).OrderBy(id => id)));
        }
    }
}
