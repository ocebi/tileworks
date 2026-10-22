using System.Linq;
using Match3.Generation;
using Match3.Model;
using NUnit.Framework;

namespace Match3.Tests
{
    public sealed class Match3GeneratorTests
    {
        private static readonly Match3Rules Rules = new Match3Rules();

        private static Match3Recipe Busy() => new Match3Recipe
        {
            Width = 7, Height = 8, Colours = 5, Moves = 20, Seed = 3, ColourGoal = 12, Boxes = 4, DoubleBoxes = true, Balloons = 2, Ducks = 1, Floors = 6,
            Chains = 3, Corners = true,
        };

        [Test]
        public void BotPlayouts_OnABoardWithEveryBlocker_AlwaysSettle()
        {
            var generator = new Match3Generator(Rules, 12);
            var level = generator.Build(Busy(), 20);
            var stats = generator.Measure(level, 5);
            Assert.That(stats.Runs, Is.EqualTo(12));
        }

        [Test]
        public void Build_LaysOutTheRecipe_WithAtMostFourGoals()
        {
            var level = new Match3Generator(Rules).Build(Busy(), 20);
            Assert.That(level.Pieces.Count(p => p == Piece.Box), Is.EqualTo(4));
            // Two, not three: the middle column is under the duck.
            Assert.That(level.Chains.Count(c => c), Is.EqualTo(2));
            Assert.That(level.Floors.Count(f => f > 0), Is.EqualTo(6));
            Assert.That(level.Goals.Length, Is.EqualTo(Match3Level.MaxGoals));
        }

        [Test]
        public void Generate_TunesTheMoveLimit_TowardsTheBand()
        {
            var recipe = new Match3Recipe { Width = 6, Height = 7, Colours = 4, Moves = 30, Band = Match3Band.Easy, ColourGoal = 10, Seed = 9 };
            var result = new Match3Generator(Rules, 8).Generate(recipe, 3);
            Assert.That(result.Level.Moves, Is.InRange(8, 60));
            Assert.That(result.Stats.WinRate, Is.GreaterThan(0f));
        }
    }
}
