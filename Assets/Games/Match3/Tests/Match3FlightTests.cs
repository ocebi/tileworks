using System.Collections.Generic;
using System.Linq;
using Match3.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Match3.Tests
{
    // Pieces gravity moves are in flight until the view reports them landed: swaps, matches, hints, shuffles and the
    // end of the level wait for them.
    public sealed class Match3FlightTests
    {
        private static readonly Match3Rules Rules = new Match3Rules();
        private static readonly string[] Plain = { "12312", "23123", "31231", "12312", "23123" };

        // Row 4 reads 4 4 _ ... with a 4 above the gap: swapping it down, or hammering the gem under it, lines up three.
        private static readonly string[] Three = Match3RulesTests.With(Plain, (0, 4, '4'), (1, 4, '4'), (2, 3, '4'));
        private static readonly Swap DownIntoRow = new Swap(Cell(2, 3), Cell(2, 4));

        // After DownIntoRow clears 4 4 4, the 5s above fall into row 4 next to the 5 at (3, 4): landing them clears again.
        private static readonly string[] Cascade = Match3RulesTests.With(Three, (1, 3, '5'), (2, 4, '5'), (3, 4, '5'));

        private static int Cell(int x, int y) => y * 5 + x;

        private static Match3Level Level(string[] rows, string goals = "[{\"kind\":\"colour\",\"colour\":1,\"count\":99}]", int moves = 20) =>
            Match3RulesTests.Level(rows, goals, moves);

        private static List<BoardEvent> Events(RuleEngine<Match3State> engine, ushort type) =>
            engine.Events.AsSpan().ToArray().Where(e => e.Type == type).ToList();

        private static ActionResult Land(RuleEngine<Match3State> engine, int cell) => engine.Apply(new Land(cell, engine.State.Ids[cell]));

        [Test]
        public void AFall_LeavesPiecesInFlight_ThatNeitherMatchNorSwap_UntilTheyLand()
        {
            var engine = Rules.CreateEngine(Level(Match3RulesTests.With(Three, (3, 4, '4'))), 1);
            var s = engine.State;

            Assert.That(engine.Apply(new Hammer(Cell(2, 4))).Accepted, Is.True);

            Assert.That(Enumerable.Range(0, 5).Select(y => s.Flight[Cell(2, y)]), Is.All.True, "the hammered column fell");
            Assert.That(s.Flight.Count, Is.EqualTo(5));
            Assert.That((s.Pieces[Cell(2, 4)], s.Colours[Cell(2, 4)]), Is.EqualTo((Piece.Gem, (byte)4)));
            Assert.That(Events(engine, Rules.Events.PieceCleared).Count, Is.EqualTo(1), "4 4 [4] 4 waits for the 4 to land");
            Assert.That(engine.Apply(new Swap(Cell(2, 4), Cell(3, 4))).Rejected, Is.EqualTo(Match3Rejects.Falling));
            Assert.That(engine.Apply(new Activate(Cell(2, 4))).Rejected, Is.EqualTo(Match3Rejects.NotSpecial));
            Assert.That(engine.Apply(new Land(Cell(2, 4), s.Ids[Cell(2, 3)])).Rejected, Is.EqualTo(RejectReason.NotInFlight), "the wrong piece");
            Assert.That(engine.Apply(new Land(Cell(3, 4), s.Ids[Cell(3, 4)])).Rejected, Is.EqualTo(RejectReason.NotInFlight), "a piece at rest");

            var landed = Land(engine, Cell(2, 4));

            Assert.That(landed.Accepted, Is.True);
            var created = SpecialCreated.From(Events(engine, Rules.Events.SpecialCreated).Single());
            Assert.That((created.Piece, created.Cell), Is.EqualTo((Piece.RocketColumn, Cell(2, 4))), "the special sits on the piece that landed");
            Assert.That(Events(engine, Rules.Events.PieceCleared).Select(e => PieceCleared.From(e).Into), Is.All.EqualTo(Cell(2, 4)));
            Assert.That(s.MovesLeft, Is.EqualTo(20), "landings cost no move");
        }

        [Test]
        public void ASwap_BetweenLandedPieces_GoesThroughWhileOthersFall()
        {
            var engine = Rules.CreateEngine(Level(Three), 1);
            var s = engine.State;
            engine.Apply(new Hammer(Cell(4, 4)));
            Assert.That(s.Flight[Cell(4, 0)], Is.True);

            Assert.That(engine.Apply(new Swap(Cell(3, 0), Cell(4, 0))).Rejected, Is.EqualTo(Match3Rejects.Falling));
            Assert.That(engine.Apply(DownIntoRow).Accepted, Is.True);

            Assert.That(Events(engine, Rules.Events.PieceCleared).Count, Is.EqualTo(3));
            Assert.That(s.MovesLeft, Is.EqualTo(19));
        }

        [Test]
        public void ClearingUnderPiecesInFlight_SendsThemFurther_WithOneLandingEach()
        {
            var engine = Rules.CreateEngine(Level(Three), 1);
            var s = engine.State;
            engine.Apply(new Hammer(Cell(2, 1)));
            var flying = new[] { s.Ids[Cell(2, 0)], s.Ids[Cell(2, 1)] };
            Assert.That(s.Flight.Count, Is.EqualTo(2));

            Assert.That(engine.Apply(DownIntoRow).Accepted, Is.True);

            var legs = Events(engine, Rules.Events.PieceFell).Select(e => PieceFell.From(e)).ToList();
            foreach (var id in flying)
            {
                Assert.That(legs.Count(l => l.Id == id && l.Lands), Is.EqualTo(1), $"piece {id} lands once");
                var cell = System.Array.IndexOf(s.Ids, id);
                Assert.That(cell, Is.GreaterThanOrEqualTo(0));
                Assert.That(s.Flight[cell], Is.True, $"piece {id} is still in flight");
            }
        }

        [Test]
        public void LandAll_EqualsTheSameLandings_InCanonicalOrder()
        {
            var level = Level(Cascade);
            var all = Rules.CreateEngine(level, 3);
            var one = Rules.CreateEngine(level, 3);
            all.Apply(DownIntoRow);
            one.Apply(DownIntoRow);

            Assert.That(all.Apply(new LandAll()).Accepted, Is.True);
            var allEvents = all.Events.AsSpan().ToArray().Select(e => (e.Type, e.Entity, e.A, e.B, e.C)).ToList();

            var oneEvents = new List<(ushort, int, int, int, int)>();
            while (one.State.Flight.Count > 0 && one.State.Outcome == Outcome.Ongoing)
            {
                Assert.That(Land(one, one.State.Flight.Next()).Accepted, Is.True);
                oneEvents.AddRange(one.Events.AsSpan().ToArray().Select(e => (e.Type, e.Entity, e.A, e.B, e.C)));
            }

            Assert.That(all.State.Flight.Count, Is.Zero);
            Assert.That(oneEvents, Is.EqualTo(allEvents));
            Assert.That(one.State.ComputeHash(), Is.EqualTo(all.State.ComputeHash()));
        }

        [Test]
        public void ALanding_ThatCompletesALine_ClearsOnThatLanding_AndTheCascadeKeepsCounting()
        {
            var engine = Rules.CreateEngine(Level(Cascade), 1);
            var s = engine.State;
            engine.Apply(DownIntoRow);
            Assert.That(s.Matches, Is.EqualTo(1));

            Land(engine, Cell(0, 4));
            Land(engine, Cell(1, 4));
            Assert.That(Events(engine, Rules.Events.PieceCleared), Is.Empty);
            Assert.That(Land(engine, Cell(2, 4)).Accepted, Is.True);

            Assert.That(Events(engine, Rules.Events.PieceCleared).Select(e => PieceCleared.From(e).Cell), Is.EquivalentTo(new[] { Cell(1, 4), Cell(2, 4), Cell(3, 4) }));
            Assert.That(Match3.Model.Cascade.From(Events(engine, Rules.Events.Cascade).Single()).Count, Is.EqualTo(2));

            Match3RulesTests.LandAll(engine);
            Assert.That(engine.Apply(new Hammer(Cell(4, 4))).Accepted, Is.True);
            Assert.That(s.Matches, Is.Zero, "the next move starts a new count");
        }

        [Test]
        public void OutOfMoves_WithPiecesInFlight_TheLevelWaits_AndALandingCanWin()
        {
            var engine = Rules.CreateEngine(Level(Cascade, "[{\"kind\":\"colour\",\"colour\":5,\"count\":3}]", 1), 1);
            var s = engine.State;

            Assert.That(engine.Apply(DownIntoRow).Outcome, Is.EqualTo(Outcome.Ongoing));
            Assert.That(s.MovesLeft, Is.Zero);
            Assert.That(engine.Apply(new Swap(Cell(3, 0), Cell(4, 0))).Rejected, Is.EqualTo(Match3Rejects.NoMovesLeft));
            Assert.That(engine.Apply(new Hammer(Cell(3, 0))).Rejected, Is.EqualTo(Match3Rejects.NoMovesLeft));
            Assert.That(engine.Apply(new ShuffleBoard()).Rejected, Is.EqualTo(Match3Rejects.Falling));

            Assert.That(Land(engine, Cell(0, 4)).Outcome, Is.EqualTo(Outcome.Ongoing));
            Assert.That(Land(engine, Cell(1, 4)).Outcome, Is.EqualTo(Outcome.Ongoing));
            Assert.That(Land(engine, Cell(2, 4)).Outcome, Is.EqualTo(Outcome.Won));
            Assert.That(engine.Apply(new LandAll()).Rejected, Is.EqualTo(RejectReason.GameOver));
        }

        [Test]
        public void OutOfMoves_TheLandingThatSettlesTheBoard_LosesIt()
        {
            var engine = Rules.CreateEngine(Level(Cascade, "[{\"kind\":\"colour\",\"colour\":5,\"count\":99}]", 1), 1);
            var s = engine.State;
            engine.Apply(DownIntoRow);

            while (s.Outcome == Outcome.Ongoing)
            {
                Assert.That(s.Flight.Count, Is.GreaterThan(0), "an ongoing level with no moves left is still falling");
                Land(engine, s.Flight.Next());
            }

            Assert.That(s.Outcome, Is.EqualTo(Outcome.Lost));
            Assert.That(s.Flight.Count, Is.Zero, "the level is lost by the landing that settles the board");
        }

        [Test]
        public void HintsAndShuffles_WaitForEveryLanding()
        {
            var s = Rules.CreateState(Level(Three), 1);
            Assert.That(Match3Hints.Best(s, out _, out _), Is.True);
            s.Flight.Launch(Cell(4, 0));
            Assert.That(Match3Hints.Best(s, out _, out _), Is.False);

            // A dead board: Plain has no swap that matches.
            var dead = Rules.CreateState(Level(Three), 1);
            for (var cell = 0; cell < dead.CellCount; cell++) dead.Colours[cell] = (byte)(Plain[cell / 5][cell % 5] - '0');
            Assert.That(Matches.HasMove(dead), Is.False);
            dead.Flight.Launch(Cell(4, 0));
            var colours = (byte[])dead.Colours.Clone();
            var rng = new RngStreams(1);
            rng.Register("refill");
            rng.Register("special");
            rng.Register("shuffle");
            var ctx = new RuleContext(new EventWriter(new EventBuffer()), rng);
            var shuffle = new ShuffleSystem(Rules.Events);
            dead.BeginAction();
            ctx.Events.Step = 1;

            Assert.That(shuffle.Run(dead, ctx), Is.False);
            Assert.That(dead.Colours, Is.EqualTo(colours));
            Match3Board.Land(dead, Cell(4, 0));
            Assert.That(shuffle.Run(dead, ctx), Is.True);
        }

        // Swaps interleaved with random landings: a piece lands at most once per fall, the landed board never holds a
        // match, ids stay unique, and the log replays to the same hash.
        [Test]
        public void Fuzz_SwapsBetweenRandomLandings_KeepTheRules_AndReplayExactly()
        {
            var level = Match3PlayoutTests.Level();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var engine = Rules.CreateEngine(level, seed);
                var s = engine.State;
                var pick = new Pcg32(seed * 104729);
                var landed = new HashSet<int>();
                for (var turn = 0; turn < 400 && s.Outcome == Outcome.Ongoing; turn++)
                {
                    var legal = Match3PlayoutTests.Legal(engine);
                    var flying = Enumerable.Range(0, s.CellCount).Where(c => s.Flight[c]).ToArray();
                    if (flying.Length == 0 && legal.Length == 0) Assert.Fail($"seed {seed}: an ongoing settled level has no move");
                    if (flying.Length > 0 && (legal.Length == 0 || pick.NextInt(3) > 0))
                    {
                        var cell = flying[pick.NextInt(flying.Length)];
                        var id = s.Ids[cell];
                        Assert.That(landed.Add(id), Is.True, $"seed {seed}: piece {id} landed twice in one fall");
                        Assert.That(engine.Apply(new Land(cell, id)).Accepted, Is.True);
                        Forget(engine, landed);
                        Assert.That(engine.Apply(new Land(cell, id)).Accepted, Is.False, "a landed piece can't land again");
                    }
                    else
                    {
                        Assert.That(engine.Apply(legal[pick.NextInt(legal.Length)]).Accepted, Is.True);
                        Forget(engine, landed);
                    }

                    Assert.That(Matches.AnyMatch(s), Is.False, $"seed {seed}: a match among landed pieces was left");
                    var ids = Enumerable.Range(0, s.CellCount).Where(c => s.Pieces[c] != Piece.Empty).Select(c => s.Ids[c]).ToList();
                    Assert.That(ids, Is.Unique);
                    Assert.That(Enumerable.Range(0, s.CellCount).Count(c => s.Flight[c]), Is.EqualTo(s.Flight.Count));
                    Assert.That(Enumerable.Range(0, s.CellCount).Where(c => s.Flight[c]).All(c => s.Pieces[c] != Piece.Empty), Is.True);
                }

                var replay = Rules.CreateEngine(level, seed);
                foreach (var record in engine.Log) Assert.That(replay.ApplyRecord(record).Accepted, Is.True);
                Assert.That(replay.State.ComputeHash(), Is.EqualTo(s.ComputeHash()), $"seed {seed}");
            }
        }

        // A piece that moves again after landing may land again.
        private static void Forget(RuleEngine<Match3State> engine, HashSet<int> landed)
        {
            foreach (var e in engine.Events.AsSpan())
                if (e.Type == Rules.Events.PieceFell) landed.Remove(e.Entity);
        }

        [Test]
        public void LandAndLandAll_DoNotAllocate_AfterWarmUp()
        {
            var level = Match3PlayoutTests.Level();
            var probe = Rules.CreateEngine(level, 9);
            var pick = new Pcg32(4);
            var swaps = new List<Swap>();
            while (probe.State.Outcome == Outcome.Ongoing && swaps.Count < 30)
            {
                var legal = Match3PlayoutTests.Legal(probe);
                var swap = legal[pick.NextInt(legal.Length)];
                probe.Apply(swap);
                Match3RulesTests.LandAll(probe);
                swaps.Add(swap);
            }

            var engine = Rules.CreateEngine(level, 9, new EventBuffer(4096));
            engine.RecordActions = false;
            var s = engine.State;
            engine.Apply(swaps[0]);
            engine.Apply(new LandAll());
            Assert.That(() =>
            {
                for (var i = 1; i < swaps.Count; i++)
                {
                    engine.Apply(swaps[i]);
                    if (i % 2 == 0)
                    {
                        while (s.Flight.Count > 0 && s.Outcome == Outcome.Ongoing)
                        {
                            var cell = s.Flight.Next();
                            engine.Apply(new Land(cell, s.Ids[cell]));
                        }
                    }
                    else if (s.Flight.Count > 0)
                    {
                        engine.Apply(new LandAll());
                    }
                }
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
