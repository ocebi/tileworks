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
    public sealed class BlastRulesTests
    {
        private static readonly BlastRules Rules = new BlastRules();
        private static readonly BlastLevelSerializer Serializer = new BlastLevelSerializer();

        // Checkerboard of 2 and 3, so nothing groups unless a test says so.
        private static readonly string[] Plain = { "23232", "32323", "23232", "32323", "23232" };

        private static BlastLevel Level(string[] rows, int moves = 20, string goals = "[{\"kind\":\"colour\",\"colour\":5,\"count\":99}]", int colours = 5)
        {
            var payload = new JObject
            {
                ["moves"] = moves, ["colours"] = colours, ["rows"] = new JArray(rows.Cast<object>().ToArray()), ["goals"] = JArray.Parse(goals),
            };
            return Serializer.Read(new LevelEnvelope { Id = "test", Game = BlastLevelSerializer.GameId, Payload = payload });
        }

        private static string[] With(string[] rows, int x, int y, char c)
        {
            var copy = (string[])rows.Clone();
            var row = copy[y].ToCharArray();
            row[x] = c;
            copy[y] = new string(row);
            return copy;
        }

        private static int Cell(int x, int y) => y * 5 + x;

        private static List<BoardEvent> Events(RuleEngine<BlastState> engine, ushort type)
        {
            var list = new List<BoardEvent>();
            foreach (var e in engine.Events.AsSpan()) if (e.Type == type) list.Add(e);
            return list;
        }

        [Test]
        public void Tap_RejectsLoneCubes_AndBlockers()
        {
            var engine = Rules.CreateEngine(Level(With(With(With(Plain, 2, 2, 'x'), 3, 4, '1'), 4, 4, '1')), 1);
            Assert.That(engine.Apply(new Tap(Cell(0, 0))).Rejected, Is.EqualTo(BlastRejects.GroupTooSmall));
            Assert.That(engine.Apply(new Tap(Cell(2, 2))).Rejected, Is.EqualTo(BlastRejects.NotTappable));
        }

        [Test]
        public void Tap_ClearsTheGroup_PiecesFall_AndNewCubesFillFromTheTop()
        {
            var rows = With(With(Plain, 0, 4, '1'), 1, 4, '1');
            var engine = Rules.CreateEngine(Level(rows), 1);
            var s = engine.State;
            var aboveId = s.Ids[Cell(0, 3)];
            var aboveColour = s.Colours[Cell(0, 3)];

            Assert.That(engine.Apply(new Tap(Cell(0, 4))).Accepted, Is.True);
            Assert.That(Events(engine, Rules.Events.PieceCleared).Count, Is.EqualTo(2));
            Assert.That(Events(engine, Rules.Events.PieceFell).Count, Is.EqualTo(8));
            Assert.That(Events(engine, Rules.Events.PieceSpawned).Count, Is.EqualTo(2));
            Assert.That(s.Ids[Cell(0, 4)], Is.EqualTo(aboveId), "the piece above fell into the gap");
            Assert.That(s.Colours[Cell(0, 4)], Is.EqualTo(aboveColour));
            Assert.That(s.Pieces.All(p => p != Piece.Empty), Is.True);
            Assert.That(s.MovesLeft, Is.EqualTo(19));
        }

        [TestCase(5, Piece.RocketRow, Piece.RocketColumn)]
        [TestCase(7, Piece.Bomb, Piece.Bomb)]
        [TestCase(9, Piece.Disco, Piece.Disco)]
        public void BigGroups_LeaveASpecial_OnTheTappedCell(int size, Piece special, Piece alternative)
        {
            var rows = Plain;
            for (var i = 0; i < size; i++) rows = With(rows, i % 5, 4 - i / 5, '1');
            var engine = Rules.CreateEngine(Level(rows), 1);
            engine.Apply(new Tap(Cell(0, 4)));

            var created = Events(engine, Rules.Events.SpecialCreated).Select(e => SpecialCreated.From(e)).Single();
            Assert.That(created.Piece, Is.EqualTo(special).Or.EqualTo(alternative));
            Assert.That(created.Cell, Is.EqualTo(Cell(0, 4)));
            Assert.That(engine.State.Pieces[Cell(0, 4)], Is.EqualTo(created.Piece));
            if (special == Piece.Disco) Assert.That(created.Colour, Is.EqualTo(1));
        }

        [Test]
        public void Rocket_ClearsItsRow()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, 2, 2, 'h')), 1);
            engine.Apply(new Tap(Cell(2, 2)));

            var fired = Events(engine, Rules.Events.SpecialFired).Select(e => SpecialFired.From(e)).Single();
            Assert.That(fired.Effect, Is.EqualTo(Effect.Row));
            Assert.That(Events(engine, Rules.Events.PieceCleared).Select(e => PieceCleared.From(e).Cell), Is.EquivalentTo(new[] { 10, 11, 13, 14 }));
        }

        [Test]
        public void TouchingSpecials_FireAsOneCombo()
        {
            var engine = Rules.CreateEngine(Level(With(With(Plain, 1, 2, 'h'), 2, 2, 'b')), 1);
            engine.Apply(new Tap(Cell(1, 2)));

            Assert.That(Events(engine, Rules.Events.SpecialMerged).Select(e => SpecialMerged.From(e)).Single().Into, Is.EqualTo(Cell(1, 2)));
            Assert.That(Events(engine, Rules.Events.SpecialFired).Select(e => SpecialFired.From(e).Effect).Single(), Is.EqualTo(Effect.ThickCross));
            var combined = Events(engine, Rules.Events.SpecialsCombined).Select(e => SpecialsCombined.From(e)).Single();
            Assert.That((combined.Cell, combined.Effect, combined.Count), Is.EqualTo((Cell(1, 2), Effect.ThickCross, 2)));
        }

        [Test]
        public void ASpecialHitByAnother_GoesOffInTheNextWave()
        {
            var engine = Rules.CreateEngine(Level(With(With(Plain, 0, 2, 'h'), 4, 2, 'v')), 1);
            engine.Apply(new Tap(Cell(0, 2)));

            var fired = Events(engine, Rules.Events.SpecialFired);
            Assert.That(fired.Select(e => SpecialFired.From(e).Effect), Is.EqualTo(new[] { Effect.Row, Effect.Column }));
            Assert.That(fired[1].Step, Is.GreaterThan(fired[0].Step));
        }

        [Test]
        public void SpecialsInOneAction_CountUpTheChain()
        {
            var engine = Rules.CreateEngine(Level(With(With(Plain, 0, 2, 'h'), 4, 2, 'v')), 1);
            engine.Apply(new Tap(Cell(0, 2)));

            Assert.That(Events(engine, Rules.Events.SpecialFired).Select(e => SpecialFired.From(e).Chain), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void TheChain_StartsAgain_ForAHammerAfterAMove()
        {
            var engine = Rules.CreateEngine(Level(With(With(With(Plain, 0, 2, 'h'), 4, 2, 'v'), 1, 0, 'b')), 1);
            engine.Apply(new Tap(Cell(0, 2)));
            engine.Apply(new LandAll());
            var bomb = System.Array.IndexOf(engine.State.Pieces, Piece.Bomb);
            Assert.That(engine.Apply(new Hammer(bomb)).Accepted, Is.True);

            var fired = Events(engine, Rules.Events.SpecialFired).Select(e => SpecialFired.From(e)).Single();
            Assert.That((fired.Effect, fired.Chain), Is.EqualTo((Effect.Bomb, 1)));
        }

        [Test]
        public void ABlast_PopsBalloons_AndPeelsOneBoxLayer()
        {
            var rows = With(With(With(With(Plain, 0, 4, '1'), 1, 4, '1'), 2, 4, 'o'), 0, 3, 'X');
            var engine = Rules.CreateEngine(Level(rows, goals: "[{\"kind\":\"balloon\",\"count\":1},{\"kind\":\"box\",\"count\":1}]"), 1);
            engine.Apply(new Tap(Cell(0, 4)));

            Assert.That(Events(engine, Rules.Events.PieceCleared).Count(e => PieceCleared.From(e).Piece == Piece.Balloon), Is.EqualTo(1));
            Assert.That(BoxDamaged.From(Events(engine, Rules.Events.BoxDamaged).Single()).HpLeft, Is.EqualTo(1));
            Assert.That(engine.State.GoalsLeft, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(engine.State.Outcome, Is.EqualTo(Outcome.Ongoing));
        }

        [Test]
        public void ADuckReachingTheBottom_IsCollected_AndCanWin()
        {
            var rows = With(With(With(Plain, 0, 4, '1'), 1, 4, '1'), 0, 3, 'd');
            var engine = Rules.CreateEngine(Level(rows, goals: "[{\"kind\":\"duck\",\"count\":1}]"), 1);
            Assert.That(engine.Apply(new Tap(Cell(0, 4))).Outcome, Is.EqualTo(Outcome.Ongoing), "the duck is still falling");
            Assert.That(Events(engine, Rules.Events.PieceCleared).Any(e => PieceCleared.From(e).Piece == Piece.Duck), Is.False);
            var result = engine.Apply(new LandAll());

            Assert.That(Events(engine, Rules.Events.PieceCleared).Any(e => PieceCleared.From(e).Piece == Piece.Duck), Is.True);
            Assert.That(result.Outcome, Is.EqualTo(Outcome.Won));
            Assert.That(Events(engine, Rules.Events.LevelWon).Count, Is.EqualTo(1));
        }

        [Test]
        public void OutOfMoves_Loses_AndAddMovesContinues()
        {
            var rows = With(With(Plain, 0, 4, '1'), 1, 4, '1');
            var engine = Rules.CreateEngine(Level(rows, moves: 1), 1);
            Assert.That(engine.Apply(new Tap(Cell(0, 4))).Outcome, Is.EqualTo(Outcome.Ongoing), "nothing is lost while pieces fall");
            Assert.That(engine.Apply(new LandAll()).Outcome, Is.EqualTo(Outcome.Lost));
            Assert.That(engine.Apply(new AddMoves(5)).Outcome, Is.EqualTo(Outcome.Ongoing));
            Assert.That(engine.State.MovesLeft, Is.EqualTo(5));
            Assert.That(engine.State.ContinuesUsed, Is.EqualTo(1));
        }

        [Test]
        public void ABoardWithoutMoves_IsShuffledUntilItHasOne()
        {
            var s = Rules.CreateState(Level(Plain, colours: 3), 5);
            Assert.That(BlastBoard.HasMove(s), Is.True);
        }

        [Test]
        public void Hints_PointAtASpecialFirst_ThenTheBiggestGroup()
        {
            var cells = new int[25];
            var rows = With(With(With(With(Plain, 0, 4, '1'), 1, 4, '1'), 2, 4, '1'), 4, 0, '4');
            var groups = Rules.CreateState(Level(With(rows, 3, 0, '4')), 1);
            Assert.That(BlastHints.Best(groups, cells), Is.EqualTo(3));
            Assert.That(cells.Take(3), Is.EquivalentTo(new[] { Cell(0, 4), Cell(1, 4), Cell(2, 4) }));

            var special = Rules.CreateState(Level(With(rows, 2, 2, 'b')), 1);
            Assert.That(BlastHints.Best(special, cells), Is.EqualTo(1));
            Assert.That(cells[0], Is.EqualTo(Cell(2, 2)));
        }

        [Test]
        public void Serializer_RoundTrips()
        {
            var level = Level(With(With(With(With(Plain, 0, 0, '#'), 1, 1, 'X'), 2, 2, '*'), 3, 3, '_'), goals: "[{\"kind\":\"colour\",\"colour\":2,\"count\":10},{\"kind\":\"box\",\"count\":1}]");
            Assert.That(level.Playable[Cell(3, 3)], Is.True);
            Assert.That(level.Pieces[Cell(3, 3)], Is.EqualTo(Piece.Empty));
            var copy = Serializer.Read(new LevelEnvelope { Id = "test", Game = BlastLevelSerializer.GameId, Payload = Serializer.Write(level) });
            Assert.That(JToken.DeepEquals(Serializer.Write(copy), Serializer.Write(level)), Is.True);
            Assert.That((string)Serializer.Write(copy)["rows"][3], Is.EqualTo("323_3"));
            var report = new ValidationReport();
            Serializer.Validate(copy, report);
            Assert.That(report.HasErrors, Is.False, report.ToString());
        }

        // A pen: an empty cell walled in by boxes stays empty through moves elsewhere, and fills once its wall breaks.
        [Test]
        public void ASealedPen_StaysEmpty_UntilItsWallBreaks()
        {
            var rows = new[] { "21132", "3xxx3", "2x_x2", "32323", "11232" };
            var engine = Rules.CreateEngine(Level(rows), 1);
            var s = engine.State;
            var pen = Cell(2, 2);
            Assert.That(s.Pieces[pen], Is.EqualTo(Piece.Empty));
            Assert.That(BlastBoard.HasMove(s), Is.True);

            Assert.That(engine.Apply(new Tap(Cell(0, 4))).Rejected, Is.EqualTo(RejectReason.None));
            Assert.That(engine.Apply(new LandAll()).Outcome, Is.EqualTo(Outcome.Ongoing));
            Assert.That(s.Pieces[pen], Is.EqualTo(Piece.Empty), "a move elsewhere fed the pen");
            Assert.That(s.Flight.Count, Is.Zero);
            Assert.That(BlastBoard.HasMove(s), Is.True, "a settled board with a pen still has a move");

            Assert.That(engine.Apply(new Tap(Cell(1, 0))).Rejected, Is.EqualTo(RejectReason.None));
            engine.Apply(new LandAll());
            Assert.That(s.Pieces[Cell(2, 1)], Is.Not.EqualTo(Piece.Box), "the wall above the pen broke");
            Assert.That(s.Pieces[pen], Is.EqualTo(Piece.Cube), "the pen filled once fed");
            Assert.That(s.Outcome, Is.EqualTo(Outcome.Ongoing));
        }

        // Each dead board shuffled mid-game is counted once; the shuffle at the start isn't.
        [Test]
        public void Shuffles_CountDeadBoards()
        {
            var level = Level(new[] { "...##", "...##", "...##", "#####", "#####" }, moves: 40, colours: 6);
            var shuffled = 0;
            for (var seed = 1UL; seed <= 10; seed++)
            {
                var engine = Rules.CreateEngine(level, seed);
                var s = engine.State;
                Assert.That(s.Shuffles, Is.Zero);
                var counted = 0;
                while (s.Outcome == Outcome.Ongoing && s.MovesLeft > 0)
                {
                    var cell = 0;
                    while (engine.Apply(new Tap(cell)).Rejected != RejectReason.None) Assert.That(++cell, Is.LessThan(25), "a settled board had no move");
                    if (Recoloured(engine)) counted++;
                    engine.Apply(new LandAll());
                    if (Recoloured(engine)) counted++;
                    Assert.That(s.Shuffles, Is.EqualTo(counted));
                }

                var copy = Rules.CreateState(level, seed);
                s.CopyTo(copy);
                Assert.That(copy.Shuffles, Is.EqualTo(s.Shuffles));
                shuffled += s.Shuffles;
            }

            Assert.That(shuffled, Is.GreaterThan(0), "no seed reached a dead board");
        }

        private static bool Recoloured(RuleEngine<BlastState> engine) => Events(engine, Rules.Events.CubeRecoloured).Count > 0;

        [Test]
        public void Taps_DoNotAllocate_AfterWarmUp()
        {
            var level = Level(new[] { "#...v..#", "..x.o...", "........", ".X....b.", "........", ".o..x...", "...*....", "....h..." }, moves: 40,
                goals: "[{\"kind\":\"colour\",\"colour\":1,\"count\":500}]", colours: 4);
            var probe = Rules.CreateEngine(level, 9);
            var pick = new Pcg32(4);
            var taps = new List<Tap>();
            while (probe.State.Outcome == Outcome.Ongoing && taps.Count < 30)
            {
                var legal = Enumerable.Range(0, probe.State.CellCount).Where(c => probe.Check(new Tap(c)).IsNone).ToArray();
                var tap = new Tap(legal[pick.NextInt(legal.Length)]);
                probe.Apply(tap);
                LandAll(probe);
                taps.Add(tap);
            }

            var engine = Rules.CreateEngine(level, 9, new EventBuffer(4096));
            engine.RecordActions = false;
            engine.Apply(taps[0]);
            LandAll(engine);
            Assert.That(() =>
            {
                for (var i = 1; i < taps.Count; i++)
                {
                    engine.Apply(taps[i]);
                    LandAll(engine);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        private static void LandAll(RuleEngine<BlastState> engine)
        {
            if (engine.State.Outcome == Outcome.Ongoing && engine.State.Flight.Count > 0) engine.Apply(new LandAll());
        }

        // Random taps on random boards: once everything lands the board is full, ids stay unique, and a replay gives the
        // same hash.
        [Test]
        public void RandomPlayouts_SettleFull_AndReplayExactly()
        {
            var rows = new[] { "#...v..#", "..x.o...", "...d....", ".X....b.", "........", ".o..x...", "...*....", "....h..." };
            var level = Level(rows, moves: 40, goals: "[{\"kind\":\"colour\",\"colour\":1,\"count\":500}]", colours: 4);
            for (ulong seed = 1; seed <= 25; seed++)
            {
                var engine = Rules.CreateEngine(level, seed);
                var s = engine.State;
                var pick = new Pcg32(seed * 7919);
                while (s.Outcome == Outcome.Ongoing)
                {
                    var legal = Enumerable.Range(0, s.CellCount).Where(c => engine.Check(new Tap(c)).IsNone).ToArray();
                    Assert.That(legal, Is.Not.Empty, "an ongoing level always has a move");
                    var moves = s.MovesLeft;
                    Assert.That(engine.Apply(new Tap(legal[pick.NextInt(legal.Length)])).Accepted, Is.True);
                    Assert.That(s.MovesLeft, Is.EqualTo(moves - 1));
                    LandAll(engine);
                    Assert.That(s.Flight.Count == 0 || s.Outcome == Outcome.Won, Is.True);
                    Assert.That(s.PendingCount, Is.Zero);
                    for (var cell = 0; cell < s.CellCount; cell++)
                        Assert.That(s.Pieces[cell] != Piece.Empty, Is.EqualTo(level.Playable[cell]), $"seed {seed}, cell {cell}");
                    var ids = Enumerable.Range(0, s.CellCount).Where(c => s.Pieces[c] != Piece.Empty).Select(c => s.Ids[c]).ToArray();
                    Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length), "ids are unique");
                    Assert.That(s.GoalsLeft.All(g => g >= 0), Is.True);
                }

                var replay = Rules.CreateEngine(level, seed);
                foreach (var record in engine.Log) replay.ApplyRecord(record);
                Assert.That(replay.State.ComputeHash(), Is.EqualTo(s.ComputeHash()), $"seed {seed}");
            }
        }
    }
}
