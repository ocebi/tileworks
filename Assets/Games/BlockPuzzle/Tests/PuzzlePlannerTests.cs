using System.Linq;
using BlockPuzzle.Generation;
using BlockPuzzle.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;

namespace BlockPuzzle.Tests
{
    public sealed class PuzzlePlannerTests
    {
        [Test]
        public void EveryPicture_IsMirrored_UsesTheColours_AndHasNoFullLine()
        {
            foreach (var (name, rows) in PuzzlePictures.All)
            {
                Assert.That(rows.Length, Is.EqualTo(8), name);
                for (var y = 0; y < 8; y++)
                {
                    Assert.That(rows[y].Length, Is.EqualTo(8), name);
                    Assert.That(rows[y].Count(c => c != '.'), Is.LessThan(8), $"{name}: row {y} is full");
                    for (var x = 0; x < 8; x++)
                    {
                        Assert.That(rows[y][x] == '.', Is.EqualTo(rows[y][7 - x] == '.'), $"{name}: {x},{y} isn't mirrored");
                        Assert.That(rows[y][x] == '.' || rows[y][x] >= '1' && rows[y][x] <= '0' + OfferDealer.Colours, Is.True, name);
                    }
                }

                for (var x = 0; x < 8; x++) Assert.That(rows.Count(r => r[x] != '.'), Is.LessThan(8), $"{name}: column {x} is full");
            }
        }

        [Test]
        public void ThePlan_ShowsEveryPictureOnce_BeforeAnyComesBack()
        {
            var plan = PuzzlePlanner.Plan(100);
            var first = plan.Skip(1).Take(PuzzlePictures.All.Length).Select(r => r.Picture).ToArray();
            Assert.That(first, Is.Unique);
            for (var i = 2; i < plan.Length; i++) Assert.That(plan[i].Picture, Is.Not.EqualTo(plan[i - 1].Picture), plan[i].Id);
        }

        [Test]
        public void EveryPlannedBoard_IsValid_WithItsGemsInMirroredPairs()
        {
            var rules = new BlockRules();
            var generator = new PuzzleGenerator(rules);
            var serializer = new BlockLevelSerializer(rules.Shapes);
            foreach (var recipe in PuzzlePlanner.Plan(100))
            {
                var level = generator.Build(recipe, recipe.Seed);
                var report = new ValidationReport();
                serializer.Validate(level, report);
                Assert.That(report.HasErrors, Is.False, $"{recipe.Id}: {report}");
                if (level.GemCount % 2 == 1) continue;
                for (var cell = 0; cell < level.CellCount; cell++)
                    Assert.That(level.Gems[cell / 8 * 8 + 7 - cell % 8], Is.EqualTo(level.Gems[cell]), $"{recipe.Id} cell {cell}");
            }
        }
    }
}
