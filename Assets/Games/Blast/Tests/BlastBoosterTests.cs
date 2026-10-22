using System.Linq;
using Blast.Model;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;

namespace Blast.Tests
{
    public sealed class BlastBoosterTests
    {
        private static readonly BlastRules Rules = new BlastRules();

        // Checkerboard of 2 and 3, so nothing groups unless a test says so.
        private static readonly string[] Plain = { "23232", "32323", "23232", "32323", "23232" };

        internal static BlastLevel Level(string[] rows, string goals = "[{\"kind\":\"colour\",\"colour\":5,\"count\":99}]") =>
            new BlastLevelSerializer().Read(new LevelEnvelope
            {
                Id = "test", Game = BlastLevelSerializer.GameId,
                Payload = new JObject { ["moves"] = 20, ["colours"] = 5, ["rows"] = new JArray(rows.Cast<object>().ToArray()), ["goals"] = JArray.Parse(goals) },
            });

        internal static string[] With(string[] rows, params (int X, int Y, char C)[] cells)
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

        private static int Count(RuleEngine<BlastState> engine, ushort type) => engine.Events.AsSpan().ToArray().Count(e => e.Type == type);

        [Test]
        public void Hammer_ClearsOneCube_WithoutUsingAMove()
        {
            var engine = Rules.CreateEngine(Level(Plain, "[{\"kind\":\"colour\",\"colour\":2,\"count\":5}]"), 1);
            var s = engine.State;

            Assert.That(engine.Apply(new Hammer(Cell(0, 0))).Accepted, Is.True);
            Assert.That(engine.Events.AsSpan()[0].Type, Is.EqualTo(Rules.Events.HammerStruck));
            Assert.That(Count(engine, Rules.Events.PieceCleared), Is.EqualTo(1));
            Assert.That(s.MovesLeft, Is.EqualTo(20));
            Assert.That(s.GoalsLeft[0], Is.EqualTo(4), "a hammered cube counts for its goal");
            Assert.That(s.Pieces.All(p => p != Piece.Empty), Is.True, "the board settles and refills");
        }

        [Test]
        public void Hammer_PeelsOneBoxLayer_PopsABalloon_AndSetsOffASpecial()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (2, 2, 'X'), (0, 4, 'o'), (4, 0, 'h'))), 1);
            var s = engine.State;

            engine.Apply(new Hammer(Cell(2, 2)));
            Assert.That((s.Pieces[Cell(2, 2)], s.Hp[Cell(2, 2)]), Is.EqualTo((Piece.Box, (byte)1)));

            engine.Apply(new Hammer(Cell(0, 4)));
            Assert.That(s.Pieces[Cell(0, 4)], Is.Not.EqualTo(Piece.Balloon));

            engine.Apply(new Hammer(Cell(4, 0)));
            Assert.That(Count(engine, Rules.Events.SpecialFired), Is.EqualTo(1));
            Assert.That(s.MovesLeft, Is.EqualTo(20));
        }

        [Test]
        public void Hammer_RefusesDucksAndHoles()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (1, 1, 'd'), (3, 1, '#'))), 1);
            Assert.That(engine.Apply(new Hammer(Cell(1, 1))).Rejected, Is.EqualTo(BlastRejects.NotHammerable));
            Assert.That(engine.Apply(new Hammer(Cell(3, 1))).Rejected, Is.EqualTo(RejectReason.InvalidTarget));
        }

        [Test]
        public void Shuffle_MovesEveryCubeAndSpecial_KeepsBlockersAndColours_AndLeavesAMove()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (2, 2, 'x'), (1, 3, 'd'), (4, 4, 'b'))), 3);
            var s = engine.State;
            var before = Enumerable.Range(0, s.CellCount).ToDictionary(c => c, c => (s.Pieces[c], s.Colours[c], s.Ids[c]));

            Assert.That(engine.Apply(new ShuffleBoard()).Accepted, Is.True);
            Assert.That(s.MovesLeft, Is.EqualTo(20));
            Assert.That(BlastBoard.HasMove(s), Is.True);
            foreach (var cell in new[] { Cell(2, 2), Cell(1, 3) }) Assert.That((s.Pieces[cell], s.Ids[cell]), Is.EqualTo((before[cell].Item1, before[cell].Item3)));

            var movable = before.Keys.Where(c => before[c].Item1 == Piece.Cube || Pieces.IsSpecial(before[c].Item1)).ToArray();
            Assert.That(movable.All(c => s.Ids[c] != before[c].Item3), Is.True, "every piece moved");
            Assert.That(movable.Select(c => (s.Pieces[c], s.Colours[c])).OrderBy(p => p).SequenceEqual(movable.Select(c => (before[c].Item1, before[c].Item2)).OrderBy(p => p)),
                Is.True, "the same pieces, rearranged");
            Assert.That(Count(engine, Rules.Events.PieceShuffled), Is.EqualTo(movable.Length));
        }

        [Test]
        public void PlayoutsWithBoosters_ReplayExactly()
        {
            var level = Level(new[] { "....v", ".x.o.", "..d..", ".X..b", "....." });
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
                        var legal = Enumerable.Range(0, s.CellCount).Where(c => engine.Check(new Tap(c)).IsNone).ToArray();
                        engine.Apply(new Tap(legal[pick.NextInt(legal.Length)]));
                    }

                    if (s.Outcome == Outcome.Ongoing && s.Flight.Count > 0) engine.Apply(new LandAll());
                    Assert.That(s.Pieces.Where((p, c) => level.Playable[c]).All(p => p != Piece.Empty), Is.True, $"seed {seed}: the board is full");
                }

                var replay = Rules.CreateEngine(level, seed);
                foreach (var record in engine.Log) replay.ApplyRecord(record);
                Assert.That(replay.State.ComputeHash(), Is.EqualTo(s.ComputeHash()), $"seed {seed}");
            }
        }
    }
}
