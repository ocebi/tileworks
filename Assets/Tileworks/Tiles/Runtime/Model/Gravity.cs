using System;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Model
{
    // Pieces fall straight down within their column segment (holes and static pieces end a segment); only the top
    // segment refills from above; an empty cell nothing can reach from above takes a resting piece sliding in diagonally
    // from up-left or up-right. Every piece that moves or arrives is left in flight at its destination.
    public static class Gravity
    {
        // Slides into a cell with an even x + y try the left source first, odd ones the right.
        private const int LeftFirstParity = 0;

        // Settles the board and records each piece's legs in grid.Legs; false when nothing moved.
        public static bool Settle<TGrid>(TGrid grid, ref Pcg32 rng) where TGrid : IGravityGrid
        {
            var legs = grid.Legs;
            legs.Clear();
            FindTops(grid, legs.Top);
            while (Fall(grid) | Spawn(grid, ref rng) | Slide(grid)) { }
            return legs.Count > 0;
        }

        // The cells a piece can reach from the top cell of each column (unless it is static) by falls and slides, with
        // holes and static cells as walls; statics may be empty for the bare shape.
        public static int Reachable(ReadOnlySpan<bool> playable, ReadOnlySpan<bool> statics, int width, int height, Span<bool> reached)
        {
            reached.Clear();
            var count = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cell = y * width + x;
                    if (!Open(playable, statics, cell) || !Reaches(playable, statics, reached, width, x, y)) continue;
                    reached[cell] = true;
                    count++;
                }
            }

            return count;
        }

        public static bool CanMove<TGrid>(TGrid grid, int cell) where TGrid : IGravityGrid => !grid.IsEmpty(cell) && !grid.IsStatic(cell);

        // The top segment refills. Below a wall, a cell is fed by a fall from the cell above or by a slide from a reached
        // cell diagonally above (that piece rests once the cell under it fills, or on a wall).
        private static bool Reaches(ReadOnlySpan<bool> playable, ReadOnlySpan<bool> statics, Span<bool> reached, int width, int x, int y)
        {
            if (InTopSegment(playable, statics, width, x, y)) return true;
            return reached[(y - 1) * width + x] || Reached(reached, width, x - 1, y - 1) || Reached(reached, width, x + 1, y - 1);
        }

        // Nothing but open cells above, up to the space over the column's top cell; a static cell or a hole with
        // playable cells over it cuts the cell off.
        private static bool InTopSegment(ReadOnlySpan<bool> playable, ReadOnlySpan<bool> statics, int width, int x, int y)
        {
            var hole = false;
            for (var row = y - 1; row >= 0; row--)
            {
                var cell = row * width + x;
                if (!playable[cell]) hole = true;
                else if (hole || IsWall(statics, cell)) return false;
            }

            return true;
        }

        private static bool Open(ReadOnlySpan<bool> playable, ReadOnlySpan<bool> statics, int cell) => playable[cell] && !IsWall(statics, cell);

        private static bool IsWall(ReadOnlySpan<bool> statics, int cell) => !statics.IsEmpty && statics[cell];

        private static bool Reached(Span<bool> reached, int width, int x, int y) => x >= 0 && x < width && reached[y * width + x];

        private static void FindTops<TGrid>(TGrid grid, int[] top) where TGrid : IGravityGrid
        {
            for (var x = 0; x < grid.Width; x++)
            {
                top[x] = grid.Height;
                for (var y = 0; y < grid.Height; y++)
                {
                    if (!grid.IsPlayable(y * grid.Width + x)) continue;
                    top[x] = y;
                    break;
                }
            }
        }

        // Per column, bottom-up: each movable piece drops to the lowest empty cell below it in its segment.
        private static bool Fall<TGrid>(TGrid grid) where TGrid : IGravityGrid
        {
            var moved = false;
            var width = grid.Width;
            for (var x = 0; x < width; x++)
            {
                var write = -1;
                for (var y = grid.Height - 1; y >= 0; y--)
                {
                    var cell = y * width + x;
                    if (!grid.IsPlayable(cell) || grid.IsStatic(cell))
                    {
                        write = -1;
                        continue;
                    }

                    if (grid.IsEmpty(cell))
                    {
                        if (write < 0) write = y;
                        continue;
                    }

                    if (write < 0) continue;
                    Move(grid, cell, write * width + x);
                    moved = true;
                    write--;
                }
            }

            return moved;
        }

        // Fills the empty run at the top of each column's top segment. A later batch in the same column starts higher,
        // behind the first.
        private static bool Spawn<TGrid>(TGrid grid, ref Pcg32 rng) where TGrid : IGravityGrid
        {
            var legs = grid.Legs;
            var width = grid.Width;
            var filled = false;
            for (var x = 0; x < width; x++)
            {
                var top = legs.Top[x];
                var empty = 0;
                for (var y = top; y < grid.Height; y++)
                {
                    var cell = y * width + x;
                    if (!grid.IsPlayable(cell) || !grid.IsEmpty(cell)) break;
                    empty++;
                }

                if (empty == 0) continue;
                legs.Spawned[x] += empty;
                for (var y = top; y < top + empty; y++)
                {
                    var cell = y * width + x;
                    grid.Spawn(cell, ref rng);
                    grid.Launch(cell);
                    Record(grid, grid.IdAt(cell), -1, cell, legs.Spawned[x], grid.ColourAt(cell));
                }

                filled = true;
            }

            return filled;
        }

        // Rows bottom-up, cells left to right: an empty cell nothing reaches from above takes a resting piece diagonally.
        private static bool Slide<TGrid>(TGrid grid) where TGrid : IGravityGrid
        {
            var moved = false;
            var width = grid.Width;
            for (var y = grid.Height - 1; y > 0; y--)
            {
                for (var x = 0; x < width; x++)
                {
                    var cell = y * width + x;
                    if (!grid.IsPlayable(cell) || !grid.IsEmpty(cell) || Fed(grid, x, y)) continue;
                    var first = ((x + y) & 1) == LeftFirstParity ? -1 : 1;
                    var source = Source(grid, x + first, y - 1);
                    if (source < 0) source = Source(grid, x - first, y - 1);
                    if (source < 0) continue;
                    Move(grid, source, cell);
                    moved = true;
                }
            }

            return moved;
        }

        // Walking up the column: a movable piece, or the space above the top cell, feeds the cell; a hole or a static
        // piece cuts it off.
        private static bool Fed<TGrid>(TGrid grid, int x, int y) where TGrid : IGravityGrid
        {
            var top = grid.Legs.Top[x];
            for (var row = y - 1; row >= top; row--)
            {
                var cell = row * grid.Width + x;
                if (!grid.IsPlayable(cell) || grid.IsStatic(cell)) return false;
                if (!grid.IsEmpty(cell)) return true;
            }

            return true;
        }

        // A movable piece that can't fall: the cell under it is filled, a hole or past the bottom.
        private static int Source<TGrid>(TGrid grid, int x, int y) where TGrid : IGravityGrid
        {
            if (x < 0 || x >= grid.Width) return -1;
            var cell = y * grid.Width + x;
            if (!grid.IsPlayable(cell) || !CanMove(grid, cell)) return -1;
            var below = cell + grid.Width;
            return y + 1 >= grid.Height || !grid.IsPlayable(below) || !grid.IsEmpty(below) ? cell : -1;
        }

        private static void Move<TGrid>(TGrid grid, int from, int to) where TGrid : IGravityGrid
        {
            grid.Move(from, to);
            grid.Launch(to);
            Record(grid, grid.IdAt(to), from, to, 0, 0);
        }

        // Adds a leg to the piece's path; a straight fall that continues a straight fall extends it.
        private static void Record<TGrid>(TGrid grid, int id, int from, int to, int drop, byte colour) where TGrid : IGravityGrid
        {
            var legs = grid.Legs;
            var width = grid.Width;
            var previous = from >= 0 ? legs.StepAt[from] : -1;
            if (from >= 0) legs.StepAt[from] = -1;
            if (previous >= 0)
            {
                ref var leg = ref legs.At(previous);
                if (leg.From >= 0 && leg.From % width == leg.To % width && from % width == to % width)
                {
                    leg.To = to;
                    legs.StepAt[to] = previous;
                    return;
                }

                leg.Continues = true;
            }

            legs.StepAt[to] = legs.Add(new SettleStep { Id = id, From = from, To = to, Drop = drop, Colour = colour });
        }
    }
}
