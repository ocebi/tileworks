using System;
using System.Collections.Generic;

namespace Tileworks.Tiles.Generation
{
    public enum PenLayout : byte
    {
        None,

        // A short shelf over the bottom row's middle, sealing two or three cells: the first pen a player meets.
        Intro,

        // Walls stepping in from each side to a column that runs to the bottom, sealing the bottom corners.
        Corners,

        // Walls stepping in to a chute over open bottom rows: pieces slide outwards as they arrive, and the cells under
        // the walls stay sealed.
        Funnel,

        // A hook on each side wall, sealing a short strip of the edge column.
        Sides,

        // A shelf across the middle of the board, sealing a triangle under it.
        Shelf,
    }

    // Mirrored box walls that seal empty pens. The pens are whatever the walls cut off from every refill
    // (BoardReach.Sealed); a pen fills by falls and slides once a wall piece breaks. Walls step in two cells thick
    // because slides come in diagonally.
    public static class BoardPens
    {
        public static readonly PenLayout[] Layouts = { PenLayout.Corners, PenLayout.Funnel, PenLayout.Sides, PenLayout.Shelf };

        // Besides the hard 5th, the chapter position that gets walls; never a breather or next to a hard level.
        private static readonly int[] OtherSlots = { 2, 3, 7, 8, 9 };

        // From level 11, two levels a chapter have pens: the hard 5th, with walls in even chapters and a pocket shape in
        // odd ones, and one more with walls. The opening levels place their own.
        public static (PenLayout Walls, bool Pockets) ForLevel(int number)
        {
            if (number <= 10) return (PenLayout.None, false);
            int chapter = (number - 1) / 10, position = (number - 1) % 10 + 1;
            if (position == 5) return chapter % 2 == 0 ? (Layouts[chapter / 2 % 2], false) : (PenLayout.None, true);
            return position == OtherSlots[chapter * 3 % OtherSlots.Length] ? (Layouts[(chapter + 1) % Layouts.Length], false) : (PenLayout.None, false);
        }

        public static List<int> Walls(PenLayout layout, int width, int height)
        {
            var cells = new List<int>();
            void Span(int y, int from, int to)
            {
                for (var x = from; x <= to; x++)
                {
                    cells.Add(y * width + x);
                    if (2 * x != width - 1) cells.Add(y * width + width - 1 - x);
                }
            }

            var half = (width - 1) / 2;
            switch (layout)
            {
                case PenLayout.Intro:
                    Span(height - 2, width % 2 == 1 ? half - 2 : half - 1, half);
                    break;
                case PenLayout.Shelf:
                    Span(height / 2, half - 2, half);
                    break;
                case PenLayout.Corners:
                    var top = height - 4;
                    Span(top, 0, 1);
                    Span(top + 1, 1, 2);
                    for (var y = top + 2; y < height; y++) Span(y, 2, 2);
                    break;
                case PenLayout.Funnel:
                    var steps = Math.Max(1, (width - 6) / 2);
                    for (var i = 0; i <= steps; i++) Span(height - 4 - steps + i, i, i + 1);
                    break;
                case PenLayout.Sides:
                    var roof = height / 2 - 2;
                    Span(roof, 0, 1);
                    Span(roof + 1, 1, 1);
                    Span(roof + 2, 1, 1);
                    break;
            }

            return cells;
        }

        // Whether the layout's walls stand on playable cells of the shape and seal at least one cell.
        public static bool Fits(PenLayout layout, ReadOnlySpan<bool> playable, int width, int height)
        {
            var walls = new bool[playable.Length];
            foreach (var cell in Walls(layout, width, height))
            {
                if (!playable[cell]) return false;
                walls[cell] = true;
            }

            return layout != PenLayout.None && BoardReach.Unreachable(playable, walls, width, height) > 0;
        }
    }
}
