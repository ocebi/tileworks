using Match3.Generation;
using Match3.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;

namespace Match3.Tests
{
    public sealed class Match3PlannerTests
    {
        private static readonly Match3Recipe[] Plan = Match3Planner.Plan(100);

        [Test]
        public void NoTwoLevelsInARow_ShareABoardShape()
        {
            for (var i = 1; i < Plan.Length; i++)
                Assert.That(Shape(Plan[i]), Is.Not.EqualTo(Shape(Plan[i - 1])), Plan[i].Id);
        }

        [Test]
        public void EveryPlannedBoard_IsValid_AndMirrored()
        {
            var generator = new Match3Generator(new Match3Rules());
            var serializer = new Match3LevelSerializer();
            foreach (var recipe in Plan)
            {
                var level = generator.Build(recipe, 25);
                var report = new ValidationReport();
                serializer.Validate(level, report);
                Assert.That(report.HasErrors, Is.False, $"{recipe.Id}: {report}");
                for (var cell = 0; cell < level.CellCount; cell++)
                {
                    var mirror = cell / level.Width * level.Width + level.Width - 1 - cell % level.Width;
                    Assert.That((level.Pieces[mirror], level.Hp[mirror], level.Floors[mirror], level.Chains[mirror]),
                        Is.EqualTo((level.Pieces[cell], level.Hp[cell], level.Floors[cell], level.Chains[cell])), $"{recipe.Id} cell {cell}");
                }
            }
        }

        private static BoardShape Shape(Match3Recipe r) => r.Shape == BoardShape.Full && r.Corners ? BoardShape.TopCorners : r.Shape;
    }
}
