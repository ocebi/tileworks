using System.IO;
using Blast.Generation;
using Blast.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Search;

namespace Blast.Tests
{
    public sealed class BlastBotTests
    {
        private const string Levels = "Assets/Games/Blast/Content/Levels";

        [Test]
        public void Skilled_WinsAtLeastAsOftenAsCasual_OnShippedLevels()
        {
            var generator = new BlastGenerator(new BlastRules(), 24);
            float casualTotal = 0f, skilledTotal = 0f;
            for (var number = 5; number <= 100; number += 5)
            {
                var level = Read(number);
                var casual = generator.Probe(level, 3, BotProfile.Casual).WinRate;
                var skilled = generator.Probe(level, 3, BotProfile.Skilled).WinRate;
                Assert.That(skilled, Is.GreaterThanOrEqualTo(casual - 0.1f), $"{level.Id}: casual {casual:0.00}, skilled {skilled:0.00}");
                casualTotal += casual;
                skilledTotal += skilled;
            }

            Assert.That(skilledTotal, Is.GreaterThan(casualTotal + 2f));
        }

        [Test]
        public void Skilled_MakesMoreSpecialsPerGame_OnASandbox()
        {
            var level = Sandbox();
            var casual = SpecialsPerGame(level, BotProfile.Casual);
            var skilled = SpecialsPerGame(level, BotProfile.Skilled);
            Assert.That(skilled, Is.GreaterThan(casual * 1.2f), $"casual {casual:0.00}, skilled {skilled:0.00}");
        }

        [Test]
        public void Skilled_IsDeterministicForASeed()
        {
            var generator = new BlastGenerator(new BlastRules(), 16);
            var level = Read(40);
            var first = generator.Probe(level, 11, BotProfile.Skilled);
            var second = generator.Probe(level, 11, BotProfile.Skilled);
            Assert.That(second.Wins, Is.EqualTo(first.Wins));
            Assert.That(second.GoalLeftOnLoss, Is.EqualTo(first.GoalLeftOnLoss));
            Assert.That(second.MovesLeftOnWin, Is.EqualTo(first.MovesLeftOnWin));
        }

        // Taps on groups of 5 or more, which leave a special.
        private static float SpecialsPerGame(BlastLevel level, BotProfile profile)
        {
            const int games = 12;
            var rules = new BlastRules();
            var policy = profile.CreatePolicy(new BlastEvaluator());
            var made = 0;
            for (var game = 0; game < games; game++)
            {
                var seed = (ulong)(game + 1);
                var domain = new BlastSearchDomain(rules, level, seed);
                var state = rules.CreateState(level, seed);
                var moves = new Tap[domain.MaxMoves];
                var rng = new Pcg32(seed);
                while (domain.Status(state) == SearchStatus.Ongoing)
                {
                    var count = domain.GetMoves(state, moves);
                    if (count == 0) break;
                    var move = moves[policy.Choose(state, new System.ReadOnlySpan<Tap>(moves, 0, count), domain, ref rng)];
                    if (state.Pieces[move.Cell] == Piece.Cube && BlastBoard.FindGroup(state, move.Cell) >= TapHandler.RocketAt) made++;
                    domain.Apply(state, move);
                }
            }

            return (float)made / games;
        }

        // An open board with a goal nobody reaches, so every game plays all its moves.
        private static BlastLevel Sandbox() =>
            new BlastGenerator(new BlastRules()).Build(new BlastRecipe { Id = "bl_sandbox", Width = 8, Height = 9, Colours = 4, ColourGoal = 500, Shape = BoardShape.Full }, 20);

        private static BlastLevel Read(int number) =>
            new BlastLevelSerializer().Read(LevelEnvelope.Parse(File.ReadAllText($"{Levels}/{BlastPlanner.IdOf(number)}.json")));
    }
}
