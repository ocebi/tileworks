using System.IO;
using Match3.Generation;
using Match3.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Search;

namespace Match3.Tests
{
    public sealed class Match3BotTests
    {
        private const string Levels = "Assets/Games/Match3/Content/Levels";

        [Test]
        public void Skilled_WinsAtLeastAsOftenAsCasual_OnShippedLevels()
        {
            var generator = new Match3Generator(new Match3Rules(), 16);
            float casualTotal = 0f, skilledTotal = 0f;
            for (var number = 10; number <= 100; number += 10)
            {
                var level = Read(number);
                var casual = generator.Probe(level, 3, BotProfile.Casual).WinRate;
                var skilled = generator.Probe(level, 3, BotProfile.Skilled).WinRate;
                Assert.That(skilled, Is.GreaterThanOrEqualTo(casual - 0.1f), $"{level.Id}: casual {casual:0.00}, skilled {skilled:0.00}");
                casualTotal += casual;
                skilledTotal += skilled;
            }

            Assert.That(skilledTotal, Is.GreaterThan(casualTotal + 1f));
        }

        [Test]
        public void Skilled_MakesMoreSpecialsPerGame_OnASandbox()
        {
            var level = Sandbox();
            var casual = SpecialsPerGame(level, BotProfile.Casual);
            var skilled = SpecialsPerGame(level, BotProfile.Skilled);
            Assert.That(skilled, Is.GreaterThan(casual), $"casual {casual:0.00}, skilled {skilled:0.00}");
        }

        [Test]
        public void Skilled_IsDeterministicForASeed()
        {
            var generator = new Match3Generator(new Match3Rules(), 8);
            var level = Read(40);
            var first = generator.Probe(level, 11, BotProfile.Skilled);
            var second = generator.Probe(level, 11, BotProfile.Skilled);
            Assert.That(second.Wins, Is.EqualTo(first.Wins));
            Assert.That(second.GoalLeftOnLoss, Is.EqualTo(first.GoalLeftOnLoss));
            Assert.That(second.MovesLeftOnWin, Is.EqualTo(first.MovesLeftOnWin));
        }

        // Specials left on the board by the bot's moves and their cascades: new special ids after each move.
        private static float SpecialsPerGame(Match3Level level, BotProfile profile)
        {
            const int games = 8;
            var rules = new Match3Rules();
            var policy = profile.CreatePolicy(new Match3Evaluator());
            var made = 0;
            for (var game = 0; game < games; game++)
            {
                var seed = (ulong)(game + 1);
                var domain = new Match3SearchDomain(rules, level, seed);
                var state = rules.CreateState(level, seed);
                var moves = new Swap[domain.MaxMoves];
                var rng = new Pcg32(seed);
                while (domain.Status(state) == SearchStatus.Ongoing)
                {
                    var count = domain.GetMoves(state, moves);
                    if (count == 0) break;
                    var move = moves[policy.Choose(state, new System.ReadOnlySpan<Swap>(moves, 0, count), domain, ref rng)];
                    var nextId = state.NextId;
                    domain.Apply(state, move);
                    for (var cell = 0; cell < state.CellCount; cell++)
                        if (Pieces.IsSpecial(state.Pieces[cell]) && state.Ids[cell] >= nextId) made++;
                }
            }

            return (float)made / games;
        }

        // An open board with a goal nobody reaches, so every game plays all its moves.
        private static Match3Level Sandbox() =>
            new Match3Generator(new Match3Rules()).Build(new Match3Recipe { Id = "m3_sandbox", Width = 8, Height = 9, Colours = 5, ColourGoal = 500, Shape = BoardShape.Full }, 20);

        private static Match3Level Read(int number) =>
            new Match3LevelSerializer().Read(LevelEnvelope.Parse(File.ReadAllText($"{Levels}/{Match3Planner.IdOf(number)}.json")));
    }
}
