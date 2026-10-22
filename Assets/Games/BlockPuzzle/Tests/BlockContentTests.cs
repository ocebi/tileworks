using BlockPuzzle.Generation;
using BlockPuzzle.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;

namespace BlockPuzzle.Tests
{
    public sealed class BlockContentTests
    {
        private static readonly BlockRules Rules = new BlockRules();

        private static PuzzleRecipe GemRecipe() => new PuzzleRecipe { Id = "gems", Fill = 0.35f, Gems = 5, Tier = 2, Seed = 11 };

        [Test]
        public void Build_IsDeterministic_AndValid()
        {
            var generator = new PuzzleGenerator(Rules);
            var serializer = new BlockLevelSerializer(Rules.Shapes);
            var first = serializer.Write(generator.Build(GemRecipe(), 5)).ToString();
            var second = serializer.Write(generator.Build(GemRecipe(), 5)).ToString();
            Assert.That(second, Is.EqualTo(first));

            var level = generator.Build(GemRecipe(), 5);
            var report = new ValidationReport();
            serializer.Validate(level, report);
            Assert.That(report.HasErrors, Is.False, string.Join("\n", report.Issues));
            Assert.That(level.GemCount, Is.EqualTo(5));
            Assert.That(level.Goal, Is.EqualTo(BlockGoal.Gems));
        }

        [Test]
        public void Serializer_RoundTripsALevel()
        {
            var serializer = new BlockLevelSerializer(Rules.Shapes);
            var level = new PuzzleGenerator(Rules).Build(GemRecipe(), 9);
            var envelope = new LevelEnvelope { Id = level.Id, Game = serializer.Game, Payload = serializer.Write(level) };
            var copy = serializer.Read(LevelEnvelope.Parse(envelope.ToJson()));

            Assert.That(copy.Colours, Is.EqualTo(level.Colours));
            Assert.That(copy.Gems, Is.EqualTo(level.Gems));
            Assert.That(copy.Pool, Is.EqualTo(level.Pool));
            Assert.That(copy.Weights, Is.EqualTo(level.Weights));
            Assert.That((copy.Goal, copy.ScoreTarget), Is.EqualTo((level.Goal, level.ScoreTarget)));
        }

        [Test]
        public void Measure_FindsAnOpenBoardWithALowTargetEasy()
        {
            var generator = new PuzzleGenerator(Rules, playouts: 10);
            var level = generator.Build(new PuzzleRecipe { ScoreTarget = 30, Tier = 1 }, 3);

            Assert.That(PuzzleGenerator.Classify(generator.Measure(level, 3).WinRate), Is.EqualTo(PuzzleBand.Easy));
        }
    }
}
