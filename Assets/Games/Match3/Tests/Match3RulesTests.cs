using System.Collections.Generic;
using System.Linq;
using Match3.Model;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;

namespace Match3.Tests
{
    public sealed class Match3RulesTests
    {
        private static readonly Match3Rules Rules = new Match3Rules();
        private static readonly Match3LevelSerializer Serializer = new Match3LevelSerializer();
        private const string Endless = "[{\"kind\":\"colour\",\"colour\":5,\"count\":99}]";

        // Diagonal stripes of 1-3: no two neighbours match, so no swap makes a match until a test adds colour 4.
        private static readonly string[] Plain = { "12312", "23123", "31231", "12312", "23123" };

        internal static Match3Level Level(string[] rows, string goals = Endless, int moves = 20, int colours = 5, string[] floor = null, string[] chains = null)
        {
            var payload = new JObject
            {
                ["moves"] = moves, ["colours"] = colours, ["rows"] = new JArray(rows.Cast<object>().ToArray()), ["goals"] = JArray.Parse(goals),
            };
            if (floor != null) payload["floor"] = new JArray(floor.Cast<object>().ToArray());
            if (chains != null) payload["chains"] = new JArray(chains.Cast<object>().ToArray());
            return Serializer.Read(new LevelEnvelope { Id = "test", Game = Match3LevelSerializer.GameId, Payload = payload });
        }

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

        // Lands whatever the action left in flight, as bots and replays do.
        internal static void LandAll(RuleEngine<Match3State> engine)
        {
            if (engine.State.Outcome == Outcome.Ongoing && engine.State.Flight.Count > 0) Assert.That(engine.Apply(new LandAll()).Accepted, Is.True);
        }

        private static List<BoardEvent> Events(RuleEngine<Match3State> engine, ushort type, int step = -1)
        {
            var list = new List<BoardEvent>();
            foreach (var e in engine.Events.AsSpan())
                if (e.Type == type && (step < 0 || e.Step == step)) list.Add(e);
            return list;
        }

        // Row 4 reads 4 4 _ ... and a 4 waits above the gap, so swapping it down lines up three.
        private static readonly string[] Three = With(Plain, (0, 4, '4'), (1, 4, '4'), (2, 3, '4'));
        private static readonly Swap DownIntoRow = new Swap(Cell(2, 3), Cell(2, 4));

        [Test]
        public void Swap_RefusesNoMatch_Chains_Boxes_AndCellsApart()
        {
            var level = Level(With(Three, (4, 0, 'x')), chains: With(new[] { ".....", ".....", ".....", ".....", "....." }, (3, 4, 'c')));
            var engine = Rules.CreateEngine(level, 1);
            Assert.That(engine.Apply(new Swap(Cell(0, 0), Cell(1, 0))).Rejected, Is.EqualTo(Match3Rejects.NoMatch));
            Assert.That(engine.Apply(new Swap(Cell(2, 4), Cell(3, 4))).Rejected, Is.EqualTo(Match3Rejects.Chained));
            Assert.That(engine.Apply(new Swap(Cell(4, 0), Cell(4, 1))).Rejected, Is.EqualTo(Match3Rejects.Fixed));
            Assert.That(engine.Apply(new Swap(Cell(0, 0), Cell(2, 0))).Rejected, Is.EqualTo(Match3Rejects.NotNeighbours));
            Assert.That(engine.State.MovesLeft, Is.EqualTo(20));
        }

        [Test]
        public void Swap_ClearsTheLine_AndTheBoardSettlesFullWithNoMatchLeft()
        {
            var engine = Rules.CreateEngine(Level(Three), 1);
            var s = engine.State;
            var aboveId = s.Ids[Cell(0, 3)];

            Assert.That(engine.Apply(DownIntoRow).Accepted, Is.True);
            Assert.That(Events(engine, Rules.Events.PiecesSwapped).Count, Is.EqualTo(1));
            Assert.That(Events(engine, Rules.Events.PieceCleared, 0).Count, Is.EqualTo(3));
            Assert.That(Events(engine, Rules.Events.SpecialCreated, 0), Is.Empty);
            Assert.That(Events(engine, Rules.Events.PieceFell, 1).Select(e => PieceFell.From(e).Id), Does.Contain(aboveId));
            Assert.That(s.Flight[Cell(0, 4)], Is.True, "the piece that fell flies until it lands");
            LandAll(engine);
            Assert.That(s.Flight.Count, Is.Zero);
            Assert.That(s.Pieces.All(p => p != Piece.Empty), Is.True);
            Assert.That(Matches.AnyMatch(s), Is.False);
            Assert.That(s.MovesLeft, Is.EqualTo(19));
        }

        [TestCase(Piece.RocketColumn)]
        [TestCase(Piece.Disco)]
        public void Lines_OfFourAndFive_LeaveTheirSpecial_WhereThePlayerMovedTheGem(Piece special)
        {
            var rows = With(Three, (3, 4, '4'));
            if (special == Piece.Disco) rows = With(rows, (4, 4, '4'));
            var engine = Rules.CreateEngine(Level(rows), 1);

            engine.Apply(DownIntoRow);

            var created = SpecialCreated.From(Events(engine, Rules.Events.SpecialCreated, 0).Single());
            Assert.That((created.Piece, created.Cell), Is.EqualTo((special, Cell(2, 4))));
            Assert.That(Events(engine, Rules.Events.PieceCleared, 0).Select(e => PieceCleared.From(e).Into), Is.All.EqualTo(Cell(2, 4)));
            Assert.That(engine.State.Pieces[Cell(2, 4)], Is.EqualTo(special));
        }

        [Test]
        public void AVerticalFour_MakesARowRocket()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (0, 1, '4'), (0, 2, '4'), (0, 4, '4'), (1, 3, '4'))), 1);
            engine.Apply(new Swap(Cell(1, 3), Cell(0, 3)));
            Assert.That(SpecialCreated.From(Events(engine, Rules.Events.SpecialCreated, 0).Single()).Piece, Is.EqualTo(Piece.RocketRow));
        }

        [Test]
        public void AnL_MakesABomb_AndASquare_MakesAFlyer()
        {
            var l = Rules.CreateEngine(Level(With(Plain, (0, 4, '4'), (1, 4, '4'), (2, 2, '4'), (2, 3, '4'), (3, 4, '4'))), 1);
            l.Apply(new Swap(Cell(3, 4), Cell(2, 4)));
            Assert.That(SpecialCreated.From(Events(l, Rules.Events.SpecialCreated, 0).Single()).Piece, Is.EqualTo(Piece.Bomb));

            var square = Rules.CreateEngine(Level(With(Plain, (0, 3, '4'), (1, 3, '4'), (0, 4, '4'), (2, 4, '4'))), 1);
            square.Apply(new Swap(Cell(2, 4), Cell(1, 4)));
            var created = SpecialCreated.From(Events(square, Rules.Events.SpecialCreated, 0).Single());
            Assert.That((created.Piece, created.Cell), Is.EqualTo((Piece.Flyer, Cell(1, 4))));
        }

        [Test]
        public void Boxes_StayPut_AndTheCellUnderOneFillsDiagonally()
        {
            var rows = With(Plain, (2, 2, 'X'), (1, 3, '4'), (3, 3, '4'), (2, 4, '4'));
            var engine = Rules.CreateEngine(Level(rows), 1);
            var s = engine.State;
            var boxId = s.Ids[Cell(2, 2)];

            engine.Apply(new Swap(Cell(2, 4), Cell(2, 3)));

            Assert.That((s.Pieces[Cell(2, 2)], s.Ids[Cell(2, 2)], s.Hp[Cell(2, 2)]), Is.EqualTo((Piece.Box, boxId, (byte)1)), "the match next to it took a layer");
            var slides = Events(engine, Rules.Events.PieceFell).Select(e => PieceFell.From(e)).Where(f => f.Target == Cell(2, 3) && f.Source % 5 != 2).ToList();
            Assert.That(slides, Is.Not.Empty);
            Assert.That(s.Pieces.All(p => p != Piece.Empty), Is.True);
        }

        [Test]
        public void AFallsPath_GoesOutAsLegs_WithTheLastOneLanding()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (2, 2, 'X'), (1, 3, '4'), (3, 3, '4'), (2, 4, '4'))), 1);
            engine.Apply(new Swap(Cell(2, 4), Cell(2, 3)));
            foreach (var group in Events(engine, Rules.Events.PieceFell).GroupBy(e => (e.Entity, e.Step)))
            {
                var legs = group.Select(e => PieceFell.From(e)).ToList();
                for (var i = 1; i < legs.Count; i++) Assert.That(legs[i].Source, Is.EqualTo(legs[i - 1].Target), "legs join up");
                Assert.That(legs.Select(l => l.Lands), Is.EqualTo(legs.Select((l, i) => i == legs.Count - 1)));
            }
        }

        [Test]
        public void AChainedGem_CantMove_ButAMatchThroughItBreaksTheChain_AndTheGemStays()
        {
            var chains = With(new[] { ".....", ".....", ".....", ".....", "....." }, (0, 4, 'c'));
            var engine = Rules.CreateEngine(Level(Three, "[{\"kind\":\"chain\",\"count\":1}]", chains: chains), 1);
            var s = engine.State;
            var chainedId = s.Ids[Cell(0, 4)];

            engine.Apply(DownIntoRow);

            Assert.That(ChainBroken.From(Events(engine, Rules.Events.ChainBroken, 0).Single()).Id, Is.EqualTo(chainedId));
            Assert.That(Events(engine, Rules.Events.PieceCleared, 0).Select(e => PieceCleared.From(e).Id), Has.No.Member(chainedId));
            Assert.That(s.Chained[Cell(0, 4)], Is.False);
            Assert.That(s.Outcome, Is.EqualTo(Outcome.Won));
        }

        [Test]
        public void AMatchOnFloor_TakesALayer()
        {
            var floor = new[] { ".....", ".....", ".....", ".....", "2221." };
            var engine = Rules.CreateEngine(Level(Three, "[{\"kind\":\"floor\",\"count\":4}]", floor: floor), 1);

            engine.Apply(DownIntoRow);

            Assert.That(Events(engine, Rules.Events.FloorDamaged, 0).Select(e => FloorDamaged.From(e).LayersLeft), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(Events(engine, Rules.Events.GoalChanged, 0), Is.Empty, "no cell is clear yet");
        }

        [Test]
        public void Balloons_PopNextToAMatch_AndDucksAreCollectedAtTheBottom()
        {
            var goals = "[{\"kind\":\"balloon\",\"count\":1},{\"kind\":\"duck\",\"count\":1}]";
            var engine = Rules.CreateEngine(Level(With(Three, (1, 3, 'o'), (0, 3, 'd')), goals), 1);

            engine.Apply(DownIntoRow);
            Assert.That(engine.State.GoalsLeft, Is.EqualTo(new[] { 0, 1 }), "the duck is still falling");
            LandAll(engine);

            Assert.That(engine.State.GoalsLeft, Is.EqualTo(new[] { 0, 0 }));
            Assert.That(engine.State.Outcome, Is.EqualTo(Outcome.Won));
        }

        [Test]
        public void SwappingARocket_FiresItWhereItLands()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (2, 2, 'h'))), 1);
            engine.Apply(new Swap(Cell(2, 2), Cell(2, 1)));
            var fired = SpecialFired.From(Events(engine, Rules.Events.SpecialFired, 1).Single());
            Assert.That((fired.Effect, fired.Cell), Is.EqualTo((Effect.Row, Cell(2, 1))));
            Assert.That(Events(engine, Rules.Events.PieceCleared, 1).Count, Is.EqualTo(4));
        }

        [Test]
        public void TwoRockets_MakeACross()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (2, 2, 'h'), (2, 1, 'v'))), 1);
            engine.Apply(new Swap(Cell(2, 2), Cell(2, 1)));
            Assert.That(SpecialsCombined.From(Events(engine, Rules.Events.SpecialsCombined, 0).Single()).Effect, Is.EqualTo(Effect.Cross));
            Assert.That(Events(engine, Rules.Events.PieceCleared, 1).Count, Is.EqualTo(7));
        }

        [Test]
        public void ADisco_ClearsTheColourItWasSwappedWith()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (2, 2, '*'))), 1);
            var s = engine.State;
            var colour = s.Colours[Cell(2, 1)];
            var count = Enumerable.Range(0, s.CellCount).Count(c => s.Pieces[c] == Piece.Gem && s.Colours[c] == colour);

            engine.Apply(new Swap(Cell(2, 2), Cell(2, 1)));

            Assert.That(SpecialFired.From(Events(engine, Rules.Events.SpecialFired, 1).Single()).Colour, Is.EqualTo(colour));
            Assert.That(Events(engine, Rules.Events.PieceCleared, 1).Count, Is.EqualTo(count));
        }

        [Test]
        public void AFlyer_HitsItsNeighbours_ThenFliesToAGoal()
        {
            var engine = Rules.CreateEngine(Level(With(Plain, (2, 2, 'f'), (4, 4, 'x')), "[{\"kind\":\"box\",\"count\":1}]"), 1);

            Assert.That(engine.Apply(new Activate(Cell(2, 2))).Accepted, Is.True);

            var launched = FlyerLaunched.From(Events(engine, Rules.Events.FlyerLaunched).Single());
            Assert.That((launched.Source, launched.Target), Is.EqualTo((Cell(2, 2), Cell(4, 4))));
            Assert.That(Events(engine, Rules.Events.PieceCleared, 1).Count, Is.EqualTo(5), "four neighbours, then the box");
            Assert.That(engine.State.Outcome, Is.EqualTo(Outcome.Won));
        }

        [Test]
        public void ADeadStartingBoard_IsShuffled_IntoOneWithAMoveAndNoMatch()
        {
            var s = Rules.CreateState(Level(Plain, colours: 3), 7);
            Assert.That(Matches.HasMove(s), Is.True);
            Assert.That(Matches.AnyMatch(s), Is.False);
        }

        [Test]
        public void RandomStartingBoards_HaveNoMatch_AndAMove()
        {
            var level = Level(new[] { "#.....#", ".......", ".......", "...x...", ".......", ".......", "......." }, colours: 4);
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var s = Rules.CreateState(level, seed);
                Assert.That(Matches.AnyMatch(s), Is.False, $"seed {seed}");
                Assert.That(Matches.HasMove(s), Is.True, $"seed {seed}");
            }
        }

        [Test]
        public void TheHint_PrefersASwapThatMakesASpecial()
        {
            var s = Rules.CreateState(Level(With(Three, (3, 4, '4'), (4, 3, '4'))), 1);
            Assert.That(Match3Hints.Best(s, out var from, out var to), Is.True);
            Assert.That(new[] { from, to }, Is.EquivalentTo(new[] { Cell(2, 3), Cell(2, 4) }));
        }

        [Test]
        public void Replaying_TheLog_RebuildsTheSameBoard()
        {
            var level = Level(new[] { ".......", ".......", "..f....", "...x...", ".......", "...h...", "......." }, colours: 4);
            var engine = Rules.CreateEngine(level, 42);
            for (var i = 0; i < 15 && engine.State.Outcome == Outcome.Ongoing; i++)
            {
                Assert.That(Match3Hints.Best(engine.State, out var from, out var to), Is.True);
                Assert.That(engine.Apply(new Swap(from, to)).Accepted, Is.True);
                LandAll(engine);
            }

            var replay = Rules.CreateEngine(level, 42);
            foreach (var record in engine.Log) Assert.That(replay.ApplyRecord(record).Accepted, Is.True);
            Assert.That(replay.State.ComputeHash(), Is.EqualTo(engine.State.ComputeHash()));
        }

        [Test]
        public void TheLevelFormat_RoundTrips()
        {
            var level = Level(With(Plain, (0, 0, '#'), (1, 1, 'X'), (2, 2, 'f')), "[{\"kind\":\"floor\",\"count\":2},{\"kind\":\"chain\",\"count\":1}]",
                floor: new[] { "#....", ".....", ".....", "...12", "....." }, chains: new[] { ".....", ".....", ".....", "c....", "....." });
            var again = Serializer.Read(new LevelEnvelope { Id = "test", Game = Match3LevelSerializer.GameId, Payload = Serializer.Write(level) });
            Assert.That(again.Pieces, Is.EqualTo(level.Pieces));
            Assert.That(again.Floors, Is.EqualTo(level.Floors));
            Assert.That(again.Chains, Is.EqualTo(level.Chains));
            Assert.That(again.Goals.Select(g => g.Kind), Is.EqualTo(new[] { GoalKind.Floor, GoalKind.Chain }));
        }

        // A pen in the bottom-left corner, walled in by two boxes: nothing falls or slides into it.
        private static readonly string[] Pen = With(Plain, (0, 4, '_'), (0, 3, 'x'), (1, 3, 'x'));

        [Test]
        public void TheLevelFormat_ReadsAndWritesEmptyCells_WithAFloorOnlyWhereTheFloorLayerSaysSo()
        {
            var level = Level(With(Pen, (1, 4, '_')), "[{\"kind\":\"floor\",\"count\":1}]", floor: new[] { ".....", ".....", ".....", ".....", "1...." });
            Assert.That((level.Playable[Cell(0, 4)], level.Pieces[Cell(0, 4)], level.Floors[Cell(0, 4)]), Is.EqualTo((true, Piece.Empty, (byte)1)));
            Assert.That((level.Playable[Cell(1, 4)], level.Pieces[Cell(1, 4)], level.Floors[Cell(1, 4)]), Is.EqualTo((true, Piece.Empty, (byte)0)));
            var payload = Serializer.Write(level);
            Assert.That((string)payload["rows"][4], Is.EqualTo("__123"));
            var again = Serializer.Read(new LevelEnvelope { Id = "test", Game = Match3LevelSerializer.GameId, Payload = payload });
            Assert.That(again.Playable, Is.EqualTo(level.Playable));
            Assert.That(again.Pieces, Is.EqualTo(level.Pieces));
            Assert.That(again.Floors, Is.EqualTo(level.Floors));
        }

        // A sealed pen is no move and no dead board: hints and swaps leave it be, and it fills once a match clears its wall.
        [Test]
        public void ASealedPen_StaysEmptyWhileWalled_AndFillsWhenItsBoxesClear()
        {
            // Column 4 lines up 5 5 5 by swapping (3, 2) right; row 2 lines up 4 4 4 next to both boxes by swapping (2, 1) down.
            var rows = With(Pen, (4, 0, '5'), (4, 1, '5'), (3, 2, '5'), (0, 2, '4'), (1, 2, '4'), (2, 1, '4'));
            var engine = Rules.CreateEngine(Level(rows), 1);
            var s = engine.State;
            Assert.That(s.Pieces[Cell(0, 4)], Is.EqualTo(Piece.Empty));
            Assert.That(Matches.HasMove(s), Is.True);
            Assert.That(Match3Hints.Best(s, out var from, out var to), Is.True);
            Assert.That(new[] { from, to }, Has.No.Member(Cell(0, 4)));

            Assert.That(engine.Apply(new Swap(Cell(3, 2), Cell(4, 2))).Accepted, Is.True);
            LandAll(engine);
            Assert.That(s.Pieces[Cell(0, 4)], Is.EqualTo(Piece.Empty), "the boxes still wall the pen in");
            Assert.That(s.Shuffles, Is.Zero);

            Assert.That(engine.Apply(new Swap(Cell(2, 1), Cell(2, 2))).Accepted, Is.True);
            LandAll(engine);
            Assert.That(s.Pieces[Cell(0, 3)] != Piece.Box && s.Pieces[Cell(1, 3)] != Piece.Box, Is.True, "the match next to the boxes clears them");
            for (var cell = 0; cell < s.CellCount; cell++) Assert.That(s.Pieces[cell], Is.Not.EqualTo(Piece.Empty), $"cell {cell}");
        }

        [Test]
        public void ADeadBoardWithAPen_IsShuffledAroundIt_AndCounted()
        {
            var s = Rules.CreateState(Level(Pen, colours: 3), 7);
            Assert.That(s.Pieces[Cell(0, 4)], Is.EqualTo(Piece.Empty), "the starting shuffle leaves the pen be");
            Assert.That(s.Shuffles, Is.Zero, "the starting shuffle isn't counted");
            for (var cell = 0; cell < s.CellCount; cell++)
                if (s.Pieces[cell] == Piece.Gem) s.Colours[cell] = (byte)(Plain[cell / 5][cell % 5] - '0');
            Assert.That(Matches.HasMove(s), Is.False);

            var rng = new RngStreams(1);
            rng.Register("refill");
            rng.Register("special");
            rng.Register("shuffle");
            var ctx = new RuleContext(new EventWriter(new EventBuffer()), rng);
            s.BeginAction();
            ctx.Events.Step = 1;
            Assert.That(new ShuffleSystem(Rules.Events).Run(s, ctx), Is.True);
            Assert.That(s.Pieces[Cell(0, 4)], Is.EqualTo(Piece.Empty));
            Assert.That(Matches.HasMove(s), Is.True);
            Assert.That(s.Shuffles, Is.EqualTo(1));

            var copy = Rules.CreateState(Level(Pen, colours: 3), 7);
            s.CopyTo(copy);
            Assert.That(copy.Shuffles, Is.EqualTo(1));
            var hash = s.ComputeHash();
            s.Shuffles = 5;
            Assert.That(s.ComputeHash(), Is.EqualTo(hash), "the count isn't part of the hash");
        }
    }
}
