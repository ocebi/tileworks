using System.Linq;
using BlockPuzzle.Model;
using NUnit.Framework;
using Tileworks.Tiles.Model;

namespace BlockPuzzle.Tests
{
    public sealed class BlockRulesTests
    {
        private static readonly BlockRules Rules = new BlockRules();

        // Rows top to bottom: '.' empty, 'A'-'G' colours, 'a'-'g' with a gem.
        private static BlockLevel Level(BlockGoal goal, int score, params string[] rows)
        {
            var width = rows[0].Length;
            var colours = new byte[width * rows.Length];
            var gems = new bool[colours.Length];
            for (var y = 0; y < rows.Length; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var c = rows[y][x];
                    if (c == '.') continue;
                    colours[y * width + x] = (byte)(char.ToUpperInvariant(c) - 'A' + 1);
                    gems[y * width + x] = char.IsLower(c);
                }
            }

            var pool = new[] { Rules.Shapes.Find("dot").Id, Rules.Shapes.Find("i2").Id };
            return new BlockLevel("test", width, rows.Length, colours, gems, goal, score, pool, new[] { 1, 1 });
        }

        private static RuleEngine<BlockState> Engine(BlockLevel level, params string[] offer)
        {
            var engine = Rules.CreateEngine(level, 7);
            for (var slot = 0; slot < BlockState.OfferSize; slot++)
                engine.State.Offer[slot] = slot < offer.Length ? Rules.Shapes.Find(offer[slot]).Id : BlockState.NoShape;
            return engine;
        }

        private static int Cell(BlockState s, int x, int y) => y * s.Width + x;

        [Test]
        public void Catalog_ParsesPatterns()
        {
            var corner = Rules.Shapes.Find("c3a");
            Assert.That((corner.Width, corner.Height, corner.Count), Is.EqualTo((2, 2, 3)));
            Assert.That(Rules.Shapes.Find("o3").Count, Is.EqualTo(9));
            Assert.That(Rules.Shapes.All.Select(s => s.Name).Distinct().Count(), Is.EqualTo(Rules.Shapes.Count));
        }

        [Test]
        public void Place_FillsCells_ConsumesTheSlot_AndScoresItsCells()
        {
            var engine = Engine(Level(BlockGoal.Score, 999, "....", "....", "....", "...."), "i3", "dot");
            var result = engine.Apply(new PlacePiece(0, Cell(engine.State, 0, 1)));

            Assert.That(result.Accepted, Is.True);
            Assert.That(engine.State.Cells.Take(8).Select(c => c != 0), Is.EqualTo(new[] { false, false, false, false, true, true, true, false }));
            Assert.That(engine.State.Offer[0], Is.EqualTo(BlockState.NoShape));
            Assert.That(engine.State.Score, Is.EqualTo(3));
        }

        [Test]
        public void Place_RejectsOverlap_OutOfBounds_AndEmptySlots()
        {
            var engine = Engine(Level(BlockGoal.Score, 999, "A...", "....", "....", "...."), "i3", "dot");

            Assert.That(engine.Apply(new PlacePiece(0, 0)).Rejected, Is.EqualTo(BlockRejects.Blocked));
            Assert.That(engine.Apply(new PlacePiece(0, Cell(engine.State, 2, 0))).Rejected, Is.EqualTo(BlockRejects.Blocked));
            Assert.That(engine.Apply(new PlacePiece(2, 5)).Rejected, Is.EqualTo(BlockRejects.EmptySlot));
            Assert.That(engine.Apply(new PlacePiece(5, 5)).Rejected, Is.EqualTo(RejectReason.InvalidTarget));
        }

        [Test]
        public void CompletingARowAndAColumn_ClearsBothOnce_AndScoresTheCombo()
        {
            var engine = Engine(Level(BlockGoal.Score, 9999, "AAA.", "...B", "...B", "...B"), "dot", "dot");
            engine.Apply(new PlacePiece(0, Cell(engine.State, 3, 0)));

            Assert.That(engine.State.FilledCount, Is.Zero);
            Assert.That(engine.State.Streak, Is.EqualTo(1));
            Assert.That(engine.State.Score, Is.EqualTo(1 + BlockScoring.ClearPoints(7, 2, 0)));
        }

        [Test]
        public void Streak_GrowsOnConsecutiveClears_AndResetsOnAMiss()
        {
            var engine = Engine(Level(BlockGoal.Score, 9999, "AAA.", "BBB.", "....", "...."), "dot", "dot", "dot");
            engine.Apply(new PlacePiece(0, Cell(engine.State, 3, 0)));
            engine.Apply(new PlacePiece(1, Cell(engine.State, 3, 1)));
            Assert.That(engine.State.Streak, Is.EqualTo(2));

            engine.Apply(new PlacePiece(2, Cell(engine.State, 0, 3)));
            Assert.That(engine.State.Streak, Is.Zero);
        }

        [Test]
        public void ClearingTheLastGem_WinsAGemLevel()
        {
            var engine = Engine(Level(BlockGoal.Gems, 0, "aAA.", "....", "....", "...."), "dot", "dot");
            var result = engine.Apply(new PlacePiece(0, Cell(engine.State, 3, 0)));

            Assert.That(engine.State.GemsLeft, Is.Zero);
            Assert.That(result.Outcome, Is.EqualTo(Outcome.Won));
        }

        [Test]
        public void ReachingTheTarget_WinsAScoreLevel()
        {
            var engine = Engine(Level(BlockGoal.Score, 3, "....", "....", "....", "...."), "i3");
            Assert.That(engine.Apply(new PlacePiece(0, 0)).Outcome, Is.EqualTo(Outcome.Won));
        }

        [Test]
        public void UsingTheLastPiece_DealsANewOffer_WithAtLeastOneFittingPiece()
        {
            var engine = Engine(Level(BlockGoal.Score, 9999, "A.B.", ".C.D", "E.F.", ".G.A"), "dot");
            engine.Apply(new PlacePiece(0, 1));

            Assert.That(engine.State.OfferLeft, Is.EqualTo(BlockState.OfferSize));
            Assert.That(engine.State.Offer.Any(id => Placement.FitsAnywhere(engine.State, Rules.Shapes[id])), Is.True);
            Assert.That(engine.State.Outcome, Is.EqualTo(Outcome.Ongoing));
        }

        [Test]
        public void NoPieceFitting_LosesTheLevel_AndRefreshOfferContinues()
        {
            var engine = Engine(Level(BlockGoal.Score, 9999, "A.BB", "AAB.", "CC.D", ".DDD"), "dot", "o3");
            var result = engine.Apply(new PlacePiece(0, 1));
            Assert.That(result.Outcome, Is.EqualTo(Outcome.Lost));

            Assert.That(engine.Apply(new RefreshOffer()).Accepted, Is.True);
            Assert.That(engine.State.Outcome, Is.EqualTo(Outcome.Ongoing));
            Assert.That(engine.State.ContinuesUsed, Is.EqualTo(1));
            Assert.That(engine.State.Offer.All(id => Placement.FitsAnywhere(engine.State, Rules.Shapes[id])), Is.True);
        }

        [Test]
        public void RefreshOffer_IsRejectedWhileTheLevelIsOngoing()
        {
            var engine = Engine(Level(BlockGoal.Score, 9999, "....", "....", "....", "...."), "dot");
            Assert.That(engine.Apply(new RefreshOffer()).Rejected, Is.EqualTo(RejectReason.NotApplicable));
        }

        [Test]
        public void SameSeedAndActions_ReplayToTheSameState()
        {
            var level = Level(BlockGoal.Score, 400, "........", "........", "..AA....", "........", "........", "....B...", "........", "........");
            var first = Rules.CreateEngine(level, 42);
            var domain = new BlockSearchDomain(Rules, level, 42);
            var moves = new PlacePiece[domain.MaxMoves];
            for (var step = 0; step < 30 && first.State.Outcome == Outcome.Ongoing; step++)
            {
                var count = domain.GetMoves(first.State, moves);
                first.Apply(moves[step * 7 % count]);
            }

            var second = Rules.CreateEngine(level, 42);
            foreach (var record in first.Log) Assert.That(second.ApplyRecord(record).Accepted, Is.True);
            Assert.That(second.State.ComputeHash(), Is.EqualTo(first.State.ComputeHash()));
        }
    }
}
