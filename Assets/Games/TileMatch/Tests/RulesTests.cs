using NUnit.Framework;
using Tileworks.Tiles.Model;
using TileMatch.Model;

namespace TileMatch.Tests
{
    public sealed class RulesTests
    {
        private const string Covered = @"
            layer 0:
            A . B
            layer 1:
             A";

        [Test]
        public void CoveredTile_IsRejected_UntilTheTileAboveLeaves()
        {
            var f = RulesFixture.Ascii(Covered);

            Assert.That(f.Select(0).Rejected, Is.EqualTo(RejectReason.NotFree));
            Assert.That(f.Select(2).Accepted, Is.True);
            Assert.That(f.EventNames(), Does.Contain(nameof(TileMatchEventIds.TileUncovered)));
            Assert.That(f.Select(0).Accepted, Is.True);
        }

        [Test]
        public void Tray_GroupsSameKindsTogether()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A B A");

            f.Select(0);
            f.Select(1);
            f.Select(2);

            Assert.That(f.TrayTiles(), Is.EqualTo(new[] { 0, 2, 1 }));
        }

        [Test]
        public void ThreeOfAKind_ClearFromTheTray()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A A A B B B");

            f.Select(0);
            f.Select(1);
            var result = f.Select(2);

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Ongoing));
            Assert.That(f.S.TrayCount, Is.Zero);
            Assert.That(f.S.TriplesCleared, Is.EqualTo(1));
            Assert.That(f.EventNames(), Does.Contain(nameof(TileMatchEventIds.TripleCleared)));
        }

        [Test]
        public void TileThatFillsTheTray_ButCompletesATriple_DoesNotLose()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A A A B B B
                tray 3");

            f.Select(0);
            f.Select(1);
            Assert.That(f.Select(2).Outcome, Is.EqualTo(Outcome.Ongoing));
        }

        [Test]
        public void FullTrayWithoutATriple_Loses()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A B C A B C
                tray 3");

            f.Select(0);
            f.Select(1);
            var result = f.Select(2);

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Lost));
            Assert.That(f.S.LoseReason, Is.EqualTo(LoseReason.TrayFull));
            Assert.That(f.Select(3).Rejected, Is.EqualTo(RejectReason.GameOver));
        }

        [Test]
        public void EmptyBoardAndTray_Wins()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A A A");

            f.Select(0);
            f.Select(1);
            Assert.That(f.Select(2).Outcome, Is.EqualTo(Outcome.Won));
            Assert.That(f.EventNames(), Does.Contain(nameof(TileMatchEventIds.LevelWon)));
        }

        [Test]
        public void NoSelectableTile_LosesWithNoMoves()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A A A B", b => b.Frozen(1).Frozen(2));

            f.Select(0);
            var result = f.Select(3);

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Lost));
            Assert.That(f.S.LoseReason, Is.EqualTo(LoseReason.NoMoves));
        }

        [Test]
        public void Frozen_BlocksSelection_UntilATripleCracksTheIce()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A A A B B B", b => b.Frozen(3));

            Assert.That(f.Select(3).Rejected, Is.EqualTo(TileMatchRejects.Frozen));
            f.Select(0);
            f.Select(1);
            f.Select(2);

            Assert.That(f.EventNames(), Does.Contain(nameof(TileMatchEventIds.ModifierChanged)));
            Assert.That(f.Select(3).Accepted, Is.True);
        }

        [Test]
        public void Hidden_RevealsWhenUncovered()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A
                layer 1:
                 B", b => b.Hidden(0));

            f.Select(1);

            Assert.That(f.EventNames(), Is.EqualTo(new[]
            {
                nameof(TileMatchEventIds.TileLeftBoard), nameof(TileMatchEventIds.TileMovedToTray),
                nameof(TileMatchEventIds.TileUncovered), nameof(TileMatchEventIds.TileRevealed),
            }));
        }

        [Test]
        public void Linked_SelectingOneMovesBoth()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A A A B B B", b => b.Link(0, 3));

            f.Select(0);

            Assert.That(f.TrayTiles(), Is.EqualTo(new[] { 0, 3 }));
        }

        [Test]
        public void Linked_IsBlocked_WhilePartnerIsCovered()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A . B
                layer 1:
                    B", b => b.Link(0, 1));

            Assert.That(f.Select(0).Rejected, Is.EqualTo(TileMatchRejects.PartnerBlocked));
        }

        [Test]
        public void Spawner_RefillsItsSlot_AndKeepsTilesBelowCovered()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A
                layer 1:
                 A", b => b.Spawner(1, 0, 1));

            f.Select(1);
            Assert.That(f.S.Locations[2], Is.EqualTo(TileLocation.Board));
            Assert.That(f.EventNames(), Does.Not.Contain(nameof(TileMatchEventIds.TileUncovered)));

            f.Select(2);
            f.Select(3);
            Assert.That(f.EventNames(), Does.Contain(nameof(TileMatchEventIds.TileUncovered)));
            Assert.That(f.S.IsFree(0), Is.True);
        }

        [Test]
        public void Undo_ReturnsTheTile_AndCoversWhatItCoveredBefore()
        {
            var f = RulesFixture.Ascii(Covered);
            f.Select(2);

            Assert.That(f.Booster(BoosterKind.Undo).Accepted, Is.True);
            Assert.That(f.S.TrayCount, Is.Zero);
            Assert.That(f.EventNames(), Does.Contain(nameof(TileMatchEventIds.TileCovered)));
            Assert.That(f.Select(0).Rejected, Is.EqualTo(RejectReason.NotFree));
            Assert.That(f.Booster(BoosterKind.Undo).Rejected, Is.EqualTo(TileMatchRejects.NothingToUndo));
        }

        [Test]
        public void Magnet_CompletesTheKindMostPresentInTheTray()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A B B B A A");

            f.Select(0);
            Assert.That(f.Booster(BoosterKind.Magnet).Accepted, Is.True);

            Assert.That(f.S.TriplesCleared, Is.EqualTo(1));
            Assert.That(f.S.Locations[4], Is.EqualTo(TileLocation.Cleared));
            Assert.That(f.S.Locations[1], Is.EqualTo(TileLocation.Board));
        }

        [Test]
        public void Shuffle_KeepsTheMultisetOfKinds_AndIsDeterministic()
        {
            var level = RulesFixture.RandomLevel(7, 15, 5);
            var a = new RulesFixture(level, seed: 42);
            var b = new RulesFixture(level, seed: 42);
            var before = KindHistogram(a.S);

            a.Booster(BoosterKind.Shuffle);
            b.Booster(BoosterKind.Shuffle);

            Assert.That(KindHistogram(a.S), Is.EqualTo(before));
            Assert.That(a.S.ComputeHash(), Is.EqualTo(b.S.ComputeHash()));
        }

        [Test]
        public void ExtraSlot_AddsCapacity_UpToTwice()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A A A");

            f.Booster(BoosterKind.ExtraSlot);
            f.Booster(BoosterKind.ExtraSlot);

            Assert.That(f.S.TrayCapacity, Is.EqualTo(9));
            Assert.That(f.Booster(BoosterKind.ExtraSlot).Rejected, Is.EqualTo(TileMatchRejects.ExtraSlotLimit));
        }

        [Test]
        public void Continue_RevivesALostAttempt()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A B C A B C
                tray 3");
            f.Select(0);
            f.Select(1);
            f.Select(2);

            var result = f.Engine.Apply(new ContinueAfterFail(1, 3));

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Ongoing));
            Assert.That(f.S.TrayCapacity, Is.EqualTo(4));
            Assert.That(f.S.TrayCount, Is.Zero);
            Assert.That(f.S.IsOnBoard(0), Is.True);
        }

        [Test]
        public void TimeUp_LosesAndCannotBeContinued()
        {
            var f = RulesFixture.Ascii(@"
                layer 0:
                A A A");

            Assert.That(f.Engine.Apply(new TimeUp()).Outcome, Is.EqualTo(Outcome.Lost));
            Assert.That(f.Engine.Apply(new ContinueAfterFail(1, 3)).Rejected, Is.EqualTo(TileMatchRejects.NotLost));
        }

        private static int[] KindHistogram(TileMatchState s)
        {
            var counts = new int[s.Level.KindCount];
            foreach (var kind in s.Kinds) counts[kind]++;
            return counts;
        }
    }
}
