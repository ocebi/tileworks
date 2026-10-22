using System;
using System.Text;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;
using Tileworks.Tiles.Model;

namespace Tileworks.Tiles.Tests
{
    // Boards are drawn top row first: '#' a hole, 'X' a static piece, '.' empty, a letter a piece; new pieces show as 'n'.
    public sealed class GravityTests
    {
        [Test]
        public void HolesBlockFalls()
        {
            var grid = Settle("a", "#", ".");

            Assert.That(grid.Draw(), Is.EqualTo(Board("a", "#", ".")));
            Assert.That(grid.Legs.Count, Is.Zero);
        }

        [Test]
        public void OnlyTheTopSegmentRefills()
        {
            var grid = Settle(".", "#", ".");

            Assert.That(grid.Draw(), Is.EqualTo(Board("n", "#", ".")));
            Assert.That(grid.Legs.Count, Is.EqualTo(1));
            Assert.That(grid.Legs[0].From, Is.EqualTo(-1));
            Assert.That(grid.Legs[0].Drop, Is.EqualTo(1));
        }

        [Test]
        public void StaticOnTheTopCellBlocksRefill()
        {
            var grid = Settle("X", ".");

            Assert.That(grid.Draw(), Is.EqualTo(Board("X", ".")));
        }

        [Test]
        public void PocketUnderAHoleIsFedBySlideAfterFalls()
        {
            var grid = Settle(
                "abc",
                "d#e",
                "...");

            Assert.That(grid.Draw(), Is.EqualTo(Board(
                "nbn",
                "a#n",
                "dce")));
        }

        [Test]
        public void SlidesPreferASideByParity()
        {
            var even = Settle(
                "aXb",
                "c.d");
            Assert.That(even.Draw(), Is.EqualTo(Board(
                "nXb",
                "cad")), "x + y even takes from the left");

            var odd = Settle(
                "a.b",
                "cXd",
                "e.f");
            Assert.That(odd.Draw(), Is.EqualTo(Board(
                "ann",
                "cXb",
                "edf")), "x + y odd takes from the right");
        }

        [Test]
        public void StraightLegsMergeAndOnlyTheLastLegLands()
        {
            var grid = Settle(
                "aX",
                "..",
                "b.",
                "c.");

            Assert.That(grid.Draw(), Is.EqualTo(Board(
                "nX",
                "nn",
                "an",
                "cb")));
            var a = grid.LegsOf('a');
            Assert.That(a.Length, Is.EqualTo(1));
            Assert.That((a[0].From, a[0].To, a[0].Lands), Is.EqualTo((0, 4, true)));
            var b = grid.LegsOf('b');
            Assert.That((b.Length, b[0].From, b[0].To), Is.EqualTo((1, 4, 7)));

            // The piece that spawned, slid and fell has three legs; only the last lands.
            var path = grid.LegsOfId(grid.IdAt(5));
            Assert.That(path.Length, Is.EqualTo(3));
            Assert.That(new[] { path[0].Lands, path[1].Lands, path[2].Lands }, Is.EqualTo(new[] { false, false, true }));
            Assert.That(path[0].From, Is.EqualTo(-1));
        }

        [Test]
        public void MovedAndSpawnedPiecesAreInFlightAtTheirDestination()
        {
            var grid = Settle(
                "aX",
                "..",
                "b.",
                "c.");

            for (var cell = 0; cell < grid.Cells.Length; cell++)
            {
                var flies = grid.Cells[cell] != '.' && grid.Cells[cell] != 'X' && grid.Cells[cell] != '#' && cell != 6;
                Assert.That(grid.Flight[cell], Is.EqualTo(flies), $"cell {cell}");
            }

            Assert.That(grid.Flight.Count, Is.EqualTo(6));
        }

        [Test]
        public void SettleAllocatesNothingAfterWarmUp()
        {
            var grid = AsciiGrid.Parse("abc", "d#e", "...");
            var rng = new Pcg32(1);
            Gravity.Settle(grid, ref rng);
            var reset = AsciiGrid.Parse("abc", "d#e", "...");
            reset.CopyTo(grid);

            var before = GC.GetAllocatedBytesForCurrentThread();
            Gravity.Settle(grid, ref rng);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [Test]
        public void Reachable_PocketsUnderOverhangsAreFedBySlides()
        {
            Assert.That(Reach(new[] { ".#.", "..." }), Is.EqualTo(5));
            Assert.That(Reach(new[] { "...", ".#.", "..." }), Is.EqualTo(8));
            Assert.That(Reach(new[] { ".", "#", "." }), Is.EqualTo(1), "a sealed column below a hole");
        }

        [Test]
        public void Reachable_StaticsAreWalls()
        {
            Assert.That(Reach(new[] { ".X.", "..." }), Is.EqualTo(5));
            Assert.That(Reach(new[] { "X", "." }), Is.Zero, "a box on the only top cell seals the column");
            Assert.That(Reach(new[] { "XXX", "..." }), Is.Zero);
        }

        [Test]
        public void Reachable_FillsEveryPlannerShapeTheGravityFills(
            [ValueSource(nameof(Shapes))] BoardShape shape, [Values(7, 8, 9)] int width, [Values(8, 9, 10)] int height)
        {
            var playable = BoardShapes.Mask(shape, width, height);
            var reached = new bool[playable.Length];
            var count = Gravity.Reachable(playable, default, width, height, reached);

            var grid = AsciiGrid.Empty(playable, width, height);
            var rng = new Pcg32(3);
            Gravity.Settle(grid, ref rng);
            for (var cell = 0; cell < playable.Length; cell++)
                Assert.That(grid.Cells[cell] == 'n', Is.EqualTo(reached[cell]), $"cell {cell}: Reachable must match a real settle");
            Assert.That(count, Is.LessThanOrEqualTo(Count(playable)));
        }

        // Shapes that ship must also be fully reachable; listed so the planner owner sees which ones aren't.
        [Test]
        public void Reachable_ReportsPlannerShapesWithSealedCells()
        {
            var sealedShapes = new StringBuilder();
            foreach (var shape in BoardShapes.All)
            {
                var playable = BoardShapes.Mask(shape, 9, 10);
                var reached = new bool[playable.Length];
                if (Gravity.Reachable(playable, default, 9, 10, reached) < Count(playable)) sealedShapes.Append(shape).Append(' ');
            }

            TestContext.Out.WriteLine($"Shapes with cells gravity can't reach at 9×10: {(sealedShapes.Length > 0 ? sealedShapes.ToString() : "none")}");
        }

        private static readonly BoardShape[] Shapes = BoardShapes.All;

        private static AsciiGrid Settle(params string[] rows)
        {
            var grid = AsciiGrid.Parse(rows);
            var rng = new Pcg32(7);
            Gravity.Settle(grid, ref rng);
            return grid;
        }

        private static int Reach(string[] rows)
        {
            var grid = AsciiGrid.Parse(rows);
            var playable = new bool[grid.Cells.Length];
            var statics = new bool[grid.Cells.Length];
            for (var cell = 0; cell < playable.Length; cell++)
            {
                playable[cell] = grid.Cells[cell] != '#';
                statics[cell] = grid.Cells[cell] == 'X';
            }

            var count = Gravity.Reachable(playable, statics, grid.Width, grid.Height, new bool[playable.Length]);

            // A real settle over the empty board fills exactly the reachable cells.
            var empty = AsciiGrid.Parse(rows);
            var rng = new Pcg32(5);
            Gravity.Settle(empty, ref rng);
            var filled = 0;
            foreach (var c in empty.Cells)
                if (c == 'n') filled++;
            Assert.That(filled, Is.EqualTo(count), "a real settle fills the reachable cells");
            return count;
        }

        private static int Count(bool[] mask)
        {
            var count = 0;
            foreach (var playable in mask)
                if (playable) count++;
            return count;
        }

        private static string Board(params string[] rows) => string.Join("\n", rows);

        private sealed class AsciiGrid : IGravityGrid
        {
            private int[] _ids;

            private AsciiGrid(int width, int height)
            {
                Width = width;
                Height = height;
                Cells = new char[width * height];
                _ids = new int[width * height];
                Legs = new GravityLegs(width, width * height);
                Flight = new FlightMarks(width, width * height);
            }

            public int Width { get; }
            public int Height { get; }
            public char[] Cells { get; }
            public GravityLegs Legs { get; }
            public FlightMarks Flight { get; }
            private int _nextId = 1;

            public static AsciiGrid Parse(params string[] rows)
            {
                var grid = new AsciiGrid(rows[0].Length, rows.Length);
                for (var y = 0; y < rows.Length; y++)
                {
                    for (var x = 0; x < grid.Width; x++)
                    {
                        var cell = y * grid.Width + x;
                        grid.Cells[cell] = rows[y][x];
                        if (char.IsLetter(rows[y][x]) && rows[y][x] != 'X') grid._ids[cell] = grid._nextId++;
                    }
                }

                return grid;
            }

            public static AsciiGrid Empty(bool[] playable, int width, int height)
            {
                var grid = new AsciiGrid(width, height);
                for (var cell = 0; cell < playable.Length; cell++) grid.Cells[cell] = playable[cell] ? '.' : '#';
                return grid;
            }

            public void CopyTo(AsciiGrid target)
            {
                Array.Copy(Cells, target.Cells, Cells.Length);
                Array.Copy(_ids, target._ids, _ids.Length);
                Flight.CopyTo(target.Flight);
            }

            public bool IsPlayable(int cell) => Cells[cell] != '#';
            public bool IsStatic(int cell) => Cells[cell] == 'X';
            public bool IsEmpty(int cell) => Cells[cell] == '.';
            public int IdAt(int cell) => _ids[cell];
            public byte ColourAt(int cell) => (byte)Cells[cell];

            public void Move(int from, int to)
            {
                Cells[to] = Cells[from];
                _ids[to] = _ids[from];
                Cells[from] = '.';
                _ids[from] = 0;
                Flight.Move(from, to);
            }

            public void Spawn(int cell, ref Pcg32 rng)
            {
                rng.NextUInt();
                Cells[cell] = 'n';
                _ids[cell] = _nextId++;
            }

            public void Launch(int cell) => Flight.Launch(cell);

            public string Draw()
            {
                var text = new StringBuilder();
                for (var y = 0; y < Height; y++)
                {
                    if (y > 0) text.Append('\n');
                    text.Append(Cells, y * Width, Width);
                }

                return text.ToString();
            }

            public SettleStep[] LegsOf(char piece)
            {
                var id = 0;
                for (var cell = 0; cell < Cells.Length; cell++)
                    if (Cells[cell] == piece) id = _ids[cell];
                return LegsOfId(id);
            }

            public SettleStep[] LegsOfId(int id)
            {
                var count = 0;
                for (var i = 0; i < Legs.Count; i++)
                    if (Legs[i].Id == id) count++;
                var legs = new SettleStep[count];
                count = 0;
                for (var i = 0; i < Legs.Count; i++)
                    if (Legs[i].Id == id) legs[count++] = Legs[i];
                return legs;
            }
        }
    }
}
