using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Generation
{
    public enum BlockerPattern : byte
    {
        Scatter,
        Rows,
        Band,
        Columns,
        Frame,
        Checker,
        Chevron,
        Corners,
        Mound,
        Diamond,
    }

    // Mirrored blocker layouts. Each pattern ranks a band of rows, so the first n cells it takes form a recognisable shape:
    // rows fill from the bottom, a frame rings the band's centre and leaves a pocket inside, and so on.
    public static class BoardPatterns
    {
        public static readonly BlockerPattern[] All = (BlockerPattern[])Enum.GetValues(typeof(BlockerPattern));

        private const int Preferred = 1_000_000;

        // Up to `count` free cells in rows fromRow..toRow, taken a mirrored pair at a time; an odd count needs a free cell in
        // the middle column, so an even board gets one fewer. Cells marked in `prefer` come first, in the pattern's order.
        // `fits` may turn a pair (a cell and its mirror, the same cell in the middle column) down.
        public static List<int> Pick(BlockerPattern pattern, int width, int height, int fromRow, int toRow, int count, IReadOnlyList<bool> free, ulong seed,
            IReadOnlyList<bool> prefer = null, Func<int, int, bool> fits = null)
        {
            var cells = new List<(int Key, int Cell)>();
            var rng = new Pcg32(seed);
            var noise = new int[width * height];
            for (var i = 0; i < noise.Length; i++) noise[i] = rng.NextInt(1000);
            toRow = Math.Min(toRow, height - 1);
            for (var y = Math.Max(0, fromRow); y <= toRow; y++)
            for (var x = 0; x <= (width - 1) / 2; x++)
            {
                var cell = y * width + x;
                var key = Key(pattern, x, y, width, fromRow, toRow, noise[cell]);
                if (key != int.MaxValue) cells.Add((prefer != null && prefer[cell] ? key - Preferred : key, cell));
            }

            cells.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : b.Cell.CompareTo(a.Cell));
            var picked = new List<int>();
            foreach (var (_, cell) in cells)
            {
                if (picked.Count >= count) break;
                int x = cell % width, y = cell / width;
                var mirror = y * width + (width - 1 - x);
                if (!free[cell] || !free[mirror] || mirror != cell && picked.Count + 2 > count || fits != null && !fits(cell, mirror)) continue;
                picked.Add(cell);
                if (mirror != cell) picked.Add(mirror);
            }

            return picked;
        }

        private static int Key(BlockerPattern pattern, int x, int y, int width, int fromRow, int toRow, int noise)
        {
            var edge = x;
            var centre = Math.Abs(2 * x - (width - 1));
            var lift = toRow - y;
            var middle = Math.Abs(2 * y - (fromRow + toRow));
            switch (pattern)
            {
                case BlockerPattern.Rows: return lift * 100 + centre;
                case BlockerPattern.Band: return middle * 100 + centre;
                case BlockerPattern.Columns:
                    var rank = edge == 1 ? 0 : centre <= 1 ? 1 : edge == 3 ? 2 : -1;
                    return rank < 0 ? int.MaxValue : rank * 100 + lift;
                case BlockerPattern.Frame:
                    var ring = Math.Max(centre, middle);
                    return (ring >= 4 && ring <= 5 ? 0 : ring > 5 ? 1 + (ring - 4) / 2 : 10 - ring) * 100 + centre;
                case BlockerPattern.Checker: return ((edge + y) % 2 == 0 ? 0 : 10000) + lift * 100 + centre;
                case BlockerPattern.Chevron: return Math.Abs(lift - centre / 2) * 100 + lift;
                case BlockerPattern.Corners: return (edge + lift) * 100 + lift;
                case BlockerPattern.Mound: return (lift + centre / 2) * 100 + lift;
                case BlockerPattern.Diamond: return Math.Abs((centre + middle) / 2 - 2) * 100 + centre + middle;
                default: return noise;
            }
        }
    }
}
