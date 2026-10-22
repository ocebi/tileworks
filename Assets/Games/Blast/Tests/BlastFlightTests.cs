using System.Collections.Generic;
using System.Linq;
using Blast.Model;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Blast.Tests
{
    // Play during falls: pieces gravity moved stay in flight until a Land; taps act on landed pieces only.
    public sealed class BlastFlightTests
    {
        private static readonly BlastRules Rules = new BlastRules();
        private static readonly string[] Plain = { "23232", "32323", "23232", "32323", "23232" };
        private const string Endless = "[{\"kind\":\"colour\",\"colour\":5,\"count\":99}]";
        private static readonly string[] Mixed = { "#...v..#", "..x.o...", "...d....", ".X....b.", "........", ".o..x...", "...*....", "....h..." };

        private static BlastLevel Level(string[] rows, int moves = 20, string goals = Endless, int colours = 5)
        {
            var payload = new JObject
            {
                ["moves"] = moves, ["colours"] = colours, ["rows"] = new JArray(rows.Cast<object>().ToArray()), ["goals"] = JArray.Parse(goals),
            };
            return new BlastLevelSerializer().Read(new LevelEnvelope { Id = "test", Game = BlastLevelSerializer.GameId, Payload = payload });
        }

        private static string[] With(string[] rows, params (int x, int y, char c)[] cells)
        {
            var copy = (string[])rows.Clone();
            foreach (var (x, y, c) in cells)
            {
                var row = copy[y].ToCharArray();
                row[x] = c;
                copy[y] = new string(row);
            }

            return copy;
        }

        private static int Cell(int x, int y) => y * 5 + x;

        private static ActionResult LandNext(RuleEngine<BlastState> engine)
        {
            var cell = engine.State.Flight.Next();
            return engine.Apply(new Land(cell, engine.State.Ids[cell]));
        }

        [Test]
        public void ATap_LeavesPiecesInFlight_AndGroupsCountLandedCubesOnly()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (0, 4, '1'), (1, 4, '1'), (1, 2, '4'), (2, 3, '4'))), 1);
            var s = engine.State;
            var falling = s.Ids[Cell(1, 2)];

            Assert.That(engine.Apply(new Tap(Cell(0, 4))).Accepted, Is.True);
            Assert.That(s.Ids[Cell(1, 3)], Is.EqualTo(falling));
            Assert.That(s.Flight[Cell(1, 3)], Is.True);
            Assert.That(s.Flight[Cell(2, 3)], Is.False);
            Assert.That(s.Flight.Count, Is.EqualTo(10), "both columns fell or refilled");
            Assert.That(BlastBoard.FindGroup(s, Cell(2, 3)), Is.EqualTo(1));
            Assert.That(engine.Apply(new Tap(Cell(2, 3))).Rejected, Is.EqualTo(BlastRejects.GroupTooSmall));
            Assert.That(engine.Apply(new Tap(Cell(1, 3))).Rejected, Is.EqualTo(BlastRejects.Falling));

            Assert.That(engine.Apply(new Land(Cell(1, 3), falling + 1)).Rejected, Is.EqualTo(RejectReason.NotInFlight), "wrong piece");
            Assert.That(engine.Apply(new Land(Cell(1, 3), falling)).Accepted, Is.True);
            Assert.That(engine.Apply(new Land(Cell(1, 3), falling)).Rejected, Is.EqualTo(RejectReason.NotInFlight), "lands once");
            Assert.That(s.MovesLeft, Is.EqualTo(19), "landing costs no move");
            Assert.That(BlastBoard.FindGroup(s, Cell(2, 3)), Is.EqualTo(2));
            Assert.That(engine.Apply(new Tap(Cell(2, 3))).Accepted, Is.True);
        }

        [Test]
        public void ALandedGroupUnderFallingPieces_MakesThemFallAgain_WithOneLandingEach()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (0, 4, '1'), (1, 4, '1'), (0, 3, '5'), (1, 3, '5'))), 1);
            var s = engine.State;
            engine.Apply(new Tap(Cell(0, 4)));
            LandNext(engine);
            LandNext(engine);
            Assert.That(s.Flight[Cell(0, 4)] || s.Flight[Cell(1, 4)], Is.False);
            var flying = Enumerable.Range(0, s.CellCount).Where(c => s.Flight[c]).Select(c => s.Ids[c]).ToArray();

            Assert.That(engine.Apply(new Tap(Cell(0, 4))).Accepted, Is.True);
            var legs = engine.Events.AsSpan().ToArray().Where(e => e.Type == Rules.Events.PieceFell).Select(e => PieceFell.From(e)).ToArray();
            foreach (var id in flying)
            {
                Assert.That(legs.Count(l => l.Id == id && l.Lands), Is.EqualTo(1), $"piece {id} lands once");
                var cell = System.Array.IndexOf(s.Ids, id);
                Assert.That(s.Flight[cell], Is.True, $"piece {id} still flies");
                Assert.That(legs.Single(l => l.Id == id && l.Lands).Target, Is.EqualTo(cell));
            }
        }

        [Test]
        public void LandAll_EqualsLandingOneByOneInCanonicalOrder()
        {
            var rows = With(Plain, (0, 4, '1'), (1, 4, '1'), (0, 3, 'd'));
            var all = Rules.CreateEngine(Level(rows, goals: "[{\"kind\":\"duck\",\"count\":2}]"), 3);
            var one = Rules.CreateEngine(Level(rows, goals: "[{\"kind\":\"duck\",\"count\":2}]"), 3);
            all.Apply(new Tap(Cell(0, 4)));
            one.Apply(new Tap(Cell(0, 4)));

            all.Apply(new LandAll());
            var together = Strip(all.Events.AsSpan().ToArray());
            var apart = new List<(ushort, int, int, int, int)>();
            while (one.State.Flight.Count > 0)
            {
                Assert.That(LandNext(one).Accepted, Is.True);
                apart.AddRange(Strip(one.Events.AsSpan().ToArray()));
            }

            Assert.That(together.Any(e => e.Item1 == Rules.Events.PieceCleared), Is.True, "the duck's landing collects it");
            Assert.That(apart, Is.EqualTo(together));
            Assert.That(one.State.ComputeHash(), Is.EqualTo(all.State.ComputeHash()));
            Assert.That(all.State.Flight.Count, Is.Zero);
        }

        [Test]
        public void OutOfMoves_WhilePiecesFly_IsDecidedByTheLanding()
        {
            var rows = With(Plain, (0, 4, '1'), (1, 4, '1'), (0, 3, 'd'));
            var win = Rules.CreateEngine(Level(rows, moves: 1, goals: "[{\"kind\":\"duck\",\"count\":1}]"), 1);
            Assert.That(win.Apply(new Tap(Cell(0, 4))).Outcome, Is.EqualTo(Outcome.Ongoing));
            Assert.That(win.State.MovesLeft, Is.Zero);
            Assert.That(win.Apply(new Tap(Cell(2, 4))).Rejected, Is.EqualTo(BlastRejects.NoMovesLeft));
            Assert.That(win.Apply(new Hammer(Cell(4, 4))).Rejected, Is.EqualTo(BlastRejects.NoMovesLeft));
            Assert.That(win.Apply(new ShuffleBoard()).Rejected, Is.EqualTo(BlastRejects.NoMovesLeft));
            Assert.That(LandNext(win).Outcome, Is.EqualTo(Outcome.Won), "the duck lands first and wins");
            Assert.That(LandNext(win).Rejected, Is.EqualTo(RejectReason.GameOver));

            var lose = Rules.CreateEngine(Level(rows, moves: 1), 1);
            lose.Apply(new Tap(Cell(0, 4)));
            while (lose.State.Flight.Count > 1) Assert.That(LandNext(lose).Outcome, Is.EqualTo(Outcome.Ongoing));
            Assert.That(LandNext(lose).Outcome, Is.EqualTo(Outcome.Lost), "the landing that settles the board decides the loss");
        }

        [Test]
        public void ShuffleAndHints_WaitUntilNothingFlies()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (0, 4, '1'), (1, 4, '1')), colours: 3), 2);
            var s = engine.State;
            engine.Apply(new Tap(Cell(0, 4)));

            Assert.That(s.Flight.Count, Is.GreaterThan(0));
            Assert.That(engine.Events.AsSpan().ToArray().Any(e => e.Type == Rules.Events.CubeRecoloured), Is.False, "no shuffle mid-fall");
            Assert.That(BlastHints.Best(s, new int[s.CellCount]), Is.Zero);
            Assert.That(engine.Apply(new ShuffleBoard()).Rejected, Is.EqualTo(BlastRejects.Falling));
            engine.Apply(new LandAll());
            Assert.That(BlastHints.Best(s, new int[s.CellCount]), Is.GreaterThan(0), "once settled the board always has a move");
        }

        // Random taps interleaved with random landings: a piece lands once per fall, never while resting, and a replay of
        // the log reproduces the state.
        [Test]
        public void Fuzz_TapsBetweenRandomLandings_KeepFlightConsistent_AndReplayExactly()
        {
            var level = Level(Mixed, moves: 60, goals: "[{\"kind\":\"colour\",\"colour\":1,\"count\":500}]", colours: 4);
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var engine = Rules.CreateEngine(level, seed);
                var s = engine.State;
                var pick = new Pcg32(seed * 104729);
                var flying = new HashSet<int>();
                for (var step = 0; step < 400 && s.Outcome == Outcome.Ongoing; step++)
                {
                    var legal = Enumerable.Range(0, s.CellCount).Where(c => engine.Check(new Tap(c)).IsNone).ToArray();
                    if (s.Flight.Count > 0 && (legal.Length == 0 || pick.NextInt(3) > 0))
                    {
                        var inFlight = Enumerable.Range(0, s.CellCount).Where(c => s.Flight[c]).ToArray();
                        var cell = inFlight[pick.NextInt(inFlight.Length)];
                        var id = s.Ids[cell];
                        Assert.That(flying.Remove(id), Is.True, $"seed {seed}: piece {id} was told to land");
                        Assert.That(engine.Apply(new Land(cell, id)).Accepted, Is.True);
                        Track(engine, flying);
                        Assert.That(engine.Apply(new Land(cell, id)).Rejected, Is.EqualTo(RejectReason.NotInFlight).Or.EqualTo(RejectReason.GameOver));
                    }
                    else
                    {
                        Assert.That(legal, Is.Not.Empty, $"seed {seed}: an ongoing settled level always has a move");
                        Assert.That(engine.Apply(new Tap(legal[pick.NextInt(legal.Length)])).Accepted, Is.True);
                        Track(engine, flying);
                    }

                    foreach (var resting in Enumerable.Range(0, s.CellCount).Where(c => s.Pieces[c] != Piece.Empty && !s.Flight[c]).Take(3))
                        Assert.That(engine.Check(new Land(resting, s.Ids[resting])).IsNone, Is.False, "a resting piece never lands");
                    for (var cell = 0; cell < s.CellCount; cell++)
                        if (s.Flight[cell]) Assert.That(flying.Contains(s.Ids[cell]), Is.True, $"seed {seed}: cell {cell} flies without a landing leg");
                    Assert.That(Enumerable.Range(0, s.CellCount).Count(c => s.Flight[c]), Is.EqualTo(s.Flight.Count));
                }

                var replay = Rules.CreateEngine(level, seed);
                foreach (var record in engine.Log) Assert.That(replay.ApplyRecord(record).Accepted, Is.True);
                Assert.That(replay.State.ComputeHash(), Is.EqualTo(s.ComputeHash()), $"seed {seed}");
            }
        }

        [Test]
        public void Lands_DoNotAllocate_AfterWarmUp()
        {
            var level = Level(Mixed, moves: 40, goals: "[{\"kind\":\"colour\",\"colour\":1,\"count\":500}]", colours: 4);
            var probe = Rules.CreateEngine(level, 9);
            var pick = new Pcg32(4);
            var taps = new List<Tap>();
            while (probe.State.Outcome == Outcome.Ongoing && taps.Count < 20)
            {
                var legal = Enumerable.Range(0, probe.State.CellCount).Where(c => probe.Check(new Tap(c)).IsNone).ToArray();
                var tap = new Tap(legal[pick.NextInt(legal.Length)]);
                probe.Apply(tap);
                while (probe.State.Outcome == Outcome.Ongoing && probe.State.Flight.Count > 0) LandNext(probe);
                taps.Add(tap);
            }

            // A whole run first, so lazy statics on paths a single tap doesn't reach are set up whatever ran before.
            Play(Engine(level), taps, 0);
            var engine = Engine(level);
            Play(engine, taps, 0, 1);
            Assert.That(() => Play(engine, taps, 1), Is.Not.AllocatingGCMemory());
        }

        private static RuleEngine<BlastState> Engine(BlastLevel level)
        {
            var engine = Rules.CreateEngine(level, 9, new EventBuffer(4096));
            engine.RecordActions = false;
            return engine;
        }

        private static void Play(RuleEngine<BlastState> engine, List<Tap> taps, int from, int count = int.MaxValue)
        {
            for (var i = from; i < taps.Count && i - from < count; i++)
            {
                engine.Apply(taps[i]);
                while (engine.State.Outcome == Outcome.Ongoing && engine.State.Flight.Count > 0) LandNext(engine);
            }
        }

        // Pieces told to land: a landing leg in the last action's events.
        private static void Track(RuleEngine<BlastState> engine, HashSet<int> flying)
        {
            foreach (var e in engine.Events.AsSpan())
            {
                if (e.Type == Rules.Events.PieceFell && PieceFell.From(e).Lands) flying.Add(e.Entity);
                if (e.Type == Rules.Events.PieceSpawned && PieceSpawned.From(e).Lands) flying.Add(e.Entity);
            }
        }

        private static List<(ushort, int, int, int, int)> Strip(BoardEvent[] events) => events.Select(e => (e.Type, e.Entity, e.A, e.B, e.C)).ToList();
    }
}
