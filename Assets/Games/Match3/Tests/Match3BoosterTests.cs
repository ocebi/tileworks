using System.Linq;
using Match3.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;
using static Match3.Tests.Match3RulesTests;

namespace Match3.Tests
{
    public sealed class Match3BoosterTests
    {
        private static readonly Match3Rules Rules = new Match3Rules();

        // Diagonal stripes of 1-3: no two neighbours match.
        private static readonly string[] Plain = { "12312", "23123", "31231", "12312", "23123" };

        // Five colours, so a shuffle has room to move every gem without lining up a match.
        private static readonly string[] Five = { "12345", "34512", "51234", "23451", "45123" };

        private static int Cell(int x, int y) => y * 5 + x;

        private static int Count(RuleEngine<Match3State> engine, ushort type) => engine.Events.AsSpan().ToArray().Count(e => e.Type == type);

        [Test]
        public void Hammer_PopsAGem_AndOneFloorLayer_WithoutUsingAMove()
        {
            var floor = new[] { "2....", ".....", ".....", ".....", "....." };
            var goals = "[" + string.Join(",", Enumerable.Range(1, 3).Select(c => $"{{\"kind\":\"colour\",\"colour\":{c},\"count\":5}}")) + "]";
            var engine = Rules.CreateEngine(Level(Plain, goals, floor: floor), 1);
            var s = engine.State;
            var goal = s.Colours[Cell(0, 0)] - 1;

            Assert.That(engine.Apply(new Hammer(Cell(0, 0))).Accepted, Is.True);
            Assert.That(engine.Events.AsSpan()[0].Type, Is.EqualTo(Rules.Events.HammerStruck));
            Assert.That(s.Floors[Cell(0, 0)], Is.EqualTo(1));
            Assert.That(s.GoalsLeft[goal], Is.EqualTo(4), "a hammered gem counts for its goal");
            Assert.That(s.MovesLeft, Is.EqualTo(20));
            Assert.That(s.Pieces.All(p => p != Piece.Empty), Is.True, "the board settles and refills");
        }

        [Test]
        public void Hammer_TakesOnlyTheChain_OrOneBoxLayer()
        {
            var chains = new[] { ".....", ".c...", ".....", ".....", "....." };
            var engine = Rules.CreateEngine(Level(With(Plain, (3, 3, 'X')), chains: chains), 1);
            var s = engine.State;
            var chainedId = s.Ids[Cell(1, 1)];

            engine.Apply(new Hammer(Cell(1, 1)));
            Assert.That((s.Chained[Cell(1, 1)], s.Ids[Cell(1, 1)]), Is.EqualTo((false, chainedId)), "the gem stays, freed");

            engine.Apply(new Hammer(Cell(3, 3)));
            Assert.That((s.Pieces[Cell(3, 3)], s.Hp[Cell(3, 3)]), Is.EqualTo((Piece.Box, (byte)1)));
        }

        [Test]
        public void Hammer_SetsOffASpecial_AndRefusesDucksAndHoles()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (4, 0, 'h'), (1, 1, 'd'), (3, 1, '#'))), 1);
            Assert.That(engine.Apply(new Hammer(Cell(1, 1))).Rejected, Is.EqualTo(Match3Rejects.NotHammerable));
            Assert.That(engine.Apply(new Hammer(Cell(3, 1))).Rejected, Is.EqualTo(RejectReason.InvalidTarget));
            Assert.That(engine.Apply(new Hammer(Cell(4, 0))).Accepted, Is.True);
            Assert.That(Count(engine, Rules.Events.SpecialFired), Is.EqualTo(1));
        }

        [Test]
        public void Shuffle_MovesGemsAndSpecials_KeepsBlockersAndChains_AndLeavesNoMatch()
        {
            var chains = new[] { ".....", ".....", "..c..", ".....", "....." };
            var engine = Rules.CreateEngine(Level(With(Five, (0, 0, 'x'), (4, 1, 'd'), (1, 3, 'b')), chains: chains), 5);
            var s = engine.State;
            var before = Enumerable.Range(0, s.CellCount).ToDictionary(c => c, c => (s.Pieces[c], s.Colours[c], s.Ids[c]));

            Assert.That(engine.Apply(new ShuffleBoard()).Accepted, Is.True);
            Assert.That(s.MovesLeft, Is.EqualTo(20));
            Assert.That(Matches.AnyMatch(s), Is.False);
            Assert.That(Matches.HasMove(s), Is.True);
            foreach (var cell in new[] { Cell(0, 0), Cell(4, 1), Cell(2, 2) }) Assert.That(s.Ids[cell], Is.EqualTo(before[cell].Item3), $"cell {cell} stays");

            var movable = before.Keys.Where(c => c != Cell(2, 2) && (before[c].Item1 == Piece.Gem || Pieces.IsSpecial(before[c].Item1))).ToArray();
            Assert.That(movable.All(c => s.Ids[c] != before[c].Item3), Is.True, "every piece moved");
            Assert.That(movable.Select(c => (s.Pieces[c], s.Colours[c])).OrderBy(p => p).SequenceEqual(movable.Select(c => (before[c].Item1, before[c].Item2)).OrderBy(p => p)),
                Is.True, "the same pieces, rearranged");
        }

        [Test]
        public void PlayoutsWithBoosters_ReplayExactly()
        {
            var level = Level(new[] { "....v", ".x.o.", "..d..", ".X..b", "....." }, floor: new[] { ".....", ".....", "11111", ".....", "....." });
            for (ulong seed = 1; seed <= 10; seed++)
            {
                var engine = Rules.CreateEngine(level, seed);
                var s = engine.State;
                var pick = new Pcg32(seed * 31);
                for (var turn = 0; turn < 30 && s.Outcome == Outcome.Ongoing; turn++)
                {
                    var roll = pick.NextInt(6);
                    if (roll == 0) engine.Apply(new ShuffleBoard());
                    else if (roll == 1) engine.Apply(new Hammer(pick.NextInt(s.CellCount)));
                    else
                    {
                        var legal = Enumerable.Range(0, s.CellCount).SelectMany(a => new[] { a + 1, a + 5 }.Select(b => new Swap(a, b)))
                            .Where(m => m.To < s.CellCount && engine.Check(m).IsNone).ToArray();
                        if (legal.Length > 0) engine.Apply(legal[pick.NextInt(legal.Length)]);
                    }

                    Match3RulesTests.LandAll(engine);

                    Assert.That(s.Pieces.Where((p, c) => level.Playable[c]).All(p => p != Piece.Empty), Is.True, $"seed {seed}: the board is full");
                    Assert.That(Matches.AnyMatch(s), Is.False, $"seed {seed}: the board settled");
                }

                var replay = Rules.CreateEngine(level, seed);
                foreach (var record in engine.Log) replay.ApplyRecord(record);
                Assert.That(replay.State.ComputeHash(), Is.EqualTo(s.ComputeHash()), $"seed {seed}");
            }
        }
    }
}
