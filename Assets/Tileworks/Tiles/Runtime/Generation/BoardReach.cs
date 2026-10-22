using System;
using Tileworks.Tiles.Model;

namespace Tileworks.Tiles.Generation
{
    // Whether refills reach every cell of a board: over the bare shape, and with its static blockers (boxes, chains) in
    // place. Cells the blockers cut off are pens, which start empty.
    public static class BoardReach
    {
        public static int Unreachable(ReadOnlySpan<bool> playable, ReadOnlySpan<bool> statics, int width, int height)
        {
            Span<bool> reached = stackalloc bool[playable.Length];
            Gravity.Reachable(playable, statics, width, height, reached);
            var count = 0;
            for (var cell = 0; cell < playable.Length; cell++)
                if (playable[cell] && (statics.IsEmpty || !statics[cell]) && !reached[cell]) count++;
            return count;
        }

        // Cells under a hole or overhang: walking up the column meets a hole with playable cells above it, so only slides
        // fill them.
        public static bool[] SlideFed(ReadOnlySpan<bool> playable, int width, int height)
        {
            var fed = new bool[playable.Length];
            for (var x = 0; x < width; x++)
            {
                bool roof = false, hole = false, covered = false;
                for (var y = 0; y < height; y++)
                {
                    var cell = y * width + x;
                    if (!playable[cell]) hole |= roof;
                    else if (hole) covered = true;
                    else roof = true;
                    fed[cell] = playable[cell] && covered;
                }
            }

            return fed;
        }

        // Playable cells that static blockers cut off from every refill: pens, which stay empty until a wall breaks.
        public static bool[] Sealed(ReadOnlySpan<bool> playable, ReadOnlySpan<bool> statics, int width, int height)
        {
            var pens = new bool[playable.Length];
            Gravity.Reachable(playable, statics, width, height, pens);
            for (var cell = 0; cell < pens.Length; cell++) pens[cell] = playable[cell] && !statics[cell] && !pens[cell];
            return pens;
        }

        // True when a piece at `cell` falls straight to its column's lowest playable cell: no hole on the way.
        public static bool FallsToBottom(ReadOnlySpan<bool> playable, int width, int height, int cell)
        {
            var gap = false;
            for (var below = cell + width; below < width * height; below += width)
            {
                if (!playable[below]) gap = true;
                else if (gap) return false;
            }

            return true;
        }
    }
}
