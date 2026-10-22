using System.IO;
using System.Linq;
using NUnit.Framework;
using Tileworks.Tiles.Editor;
using Tileworks.Tiles.Levels;

namespace Tileworks.Tiles.Tests
{
    public sealed class LevelContentTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tileworks-content-" + System.Guid.NewGuid().ToString("N")).Replace('\\', '/');
            Directory.CreateDirectory($"{_folder}/Levels");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_folder, true);

        [Test]
        public void Regenerating_KeepsTheArrangedOrder_LeavesTakenOutLevelsOut_AndAppendsNewOnes()
        {
            foreach (var id in new[] { "a", "b", "c", "d", "hand" }) Touch(id);
            File.WriteAllText($"{_folder}/sequence.json", LevelSequence.Write(new[] { Ref("c", 50), Ref("hand", 10), Ref("a", 10), Ref("b", 10) }));
            var before = LevelContent.LevelIds(_folder);
            Touch("e");

            LevelContent.WriteMerged(_folder, new[] { Ref("a", 20, "hard"), Ref("b", 20), Ref("c", 20), Ref("d", 20), Ref("e", 30) }, before);

            var merged = LevelContent.Sequence(_folder);
            Assert.That(merged.Select(r => r.LevelId), Is.EqualTo(new[] { "c", "hand", "a", "b", "e" }));
            Assert.That(merged.Select(r => r.Index), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(merged[0].Reward, Is.EqualTo(50), "an arranged level keeps its reward");
            Assert.That(merged[2].Label, Is.EqualTo("hard"), "the generator's badge follows the new difficulty");
            Assert.That(merged[4].Reward, Is.EqualTo(30));
        }

        [Test]
        public void Regenerating_DropsLevelsWhoseFilesAreGone()
        {
            Touch("a");
            File.WriteAllText($"{_folder}/sequence.json", LevelSequence.Write(new[] { Ref("gone", 10), Ref("a", 10) }));

            LevelContent.WriteMerged(_folder, new[] { Ref("a", 10) }, LevelContent.LevelIds(_folder));

            Assert.That(LevelContent.Sequence(_folder).Select(r => r.LevelId), Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void NextId_FollowsTheHighestNumber()
        {
            foreach (var id in new[] { "bl_0001", "bl_0007", "bl_notes", "bp_0040" }) Touch(id);
            Assert.That(LevelContent.NextId(_folder, "bl"), Is.EqualTo("bl_0008"));
        }

        private void Touch(string id) => File.WriteAllText(LevelContent.PathOf(_folder, id), "{}");

        private static LevelRef Ref(string id, int reward, string label = "") => new LevelRef(0, id, label, reward);
    }
}
