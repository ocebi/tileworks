using System.Linq;
using Blast.Generation;
using Blast.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;

namespace Blast.Tests
{
    public sealed class BlastPlannerTests
    {
        private static readonly BlastRecipe[] Plan = BlastPlanner.Plan(100);

        [Test]
        public void NoTwoLevelsInARow_ShareABoardShape()
        {
            for (var i = 1; i < Plan.Length; i++)
                Assert.That(Shape(Plan[i]), Is.Not.EqualTo(Shape(Plan[i - 1])), Plan[i].Id);
        }

        [Test]
        public void EveryPlannedBoard_IsValid_AndMirrored()
        {
            var generator = new BlastGenerator(new BlastRules());
            var serializer = new BlastLevelSerializer();
            foreach (var recipe in Plan)
            {
                var level = generator.Build(recipe, 25);
                var report = new ValidationReport();
                serializer.Validate(level, report);
                Assert.That(report.HasErrors, Is.False, $"{recipe.Id}: {report}");
                for (var cell = 0; cell < level.CellCount; cell++)
                {
                    var mirror = cell / level.Width * level.Width + level.Width - 1 - cell % level.Width;
                    Assert.That((level.Pieces[mirror], level.Hp[mirror]), Is.EqualTo((level.Pieces[cell], level.Hp[cell])), $"{recipe.Id} cell {cell}");
                }
            }
        }

        [Test]
        public void EveryBlocker_ComesBackWithinTwentyLevels_OnceIntroduced()
        {
            foreach (var (from, has) in new (int, System.Func<BlastRecipe, bool>)[]
                     {
                         (BlastPlanner.BoxesFrom, r => r.Boxes > 0), (BlastPlanner.BalloonsFrom, r => r.Balloons > 0), (BlastPlanner.DucksFrom, r => r.Ducks > 0),
                     })
            {
                for (var start = from; start + 20 <= Plan.Length; start++)
                    Assert.That(Plan.Skip(start - 1).Take(20).Any(has), Is.True, $"a 20-level gap from level {start}");
            }
        }

        [Test]
        public void HardLevels_HaveMoreGoalsThanTheBreatherAfterThem()
        {
            for (var number = 15; number < 100; number += 5)
            {
                var hard = Plan[number - 1];
                var breather = Plan[number];
                Assert.That(LevelCurve.At(number).Role, Is.GreaterThanOrEqualTo(LevelRole.Hard));
                Assert.That(Goals(hard), Is.GreaterThan(Goals(breather)), hard.Id);
            }
        }

        private static int Goals(BlastRecipe r) => (r.ColourGoal > 0 ? 1 : 0) + (r.Boxes > 0 ? 1 : 0) + (r.Balloons > 0 ? 1 : 0) + (r.Ducks > 0 ? 1 : 0);

        private static BoardShape Shape(BlastRecipe r) => r.Shape == BoardShape.Full && r.Corners ? BoardShape.TopCorners : r.Shape;
    }
}
