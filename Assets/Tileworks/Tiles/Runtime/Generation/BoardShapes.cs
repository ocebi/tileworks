using System;

namespace Tileworks.Tiles.Generation
{
    public enum BoardShape : byte
    {
        Full,
        TopCorners,
        Corners,
        Bevel,
        Octagon,
        Hourglass,
        Funnel,
        Tower,
        Pillars,
        Window,
        Plus,
        Heart,
        Arch,
        Castle,
        Bridge,
        Keyhole,
        Cave,
        Shelves,
        Goblet,
        Chimney,
    }

    // Playable-cell masks for square-grid boards, all mirrored left to right. Row 0 is the top.
    public static class BoardShapes
    {
        public static readonly BoardShape[] All = (BoardShape[])Enum.GetValues(typeof(BoardShape));

        // Shapes with cells under a hole, filled only by diagonal slides.
        public static readonly BoardShape[] Pockets =
        {
            BoardShape.Bridge, BoardShape.Keyhole, BoardShape.Cave, BoardShape.Shelves, BoardShape.Goblet, BoardShape.Chimney,
            BoardShape.Hourglass, BoardShape.Pillars, BoardShape.Window,
        };

        public static bool HasPockets(BoardShape shape) => Array.IndexOf(Pockets, shape) >= 0;

        public static bool[] Mask(BoardShape shape, int width, int height)
        {
            var mask = new bool[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                mask[y * width + x] = !Cut(shape, x, y, width, height);
            return mask;
        }

        private static bool Cut(BoardShape shape, int x, int y, int width, int height)
        {
            var edge = Math.Min(x, width - 1 - x);
            var bottom = height - 1 - y;
            var centre = Math.Abs(2 * x - (width - 1));
            var stem = width % 2 == 0 ? width / 2 - 2 : (width - 3) / 2;
            switch (shape)
            {
                case BoardShape.TopCorners: return y == 0 && edge == 0;
                case BoardShape.Corners: return edge == 0 && (y == 0 || bottom == 0);
                case BoardShape.Bevel: return edge + y < 2 || edge + bottom < 2;
                case BoardShape.Octagon: return edge + y < 3 || edge + bottom < 3;
                case BoardShape.Hourglass:
                    var middle = Math.Abs(2 * y - (height - 1));
                    return middle <= 3 && edge == 0 || middle <= 1 && edge == 1;
                case BoardShape.Funnel: return edge < 2 - bottom;
                case BoardShape.Tower: return edge < 2 - y;
                case BoardShape.Pillars: return edge == 2 && y >= 2 && bottom >= 2;
                // An odd board's three-wide window gets a sill cell under its middle, so slides reach the cell below.
                case BoardShape.Window:
                    return centre <= (width % 2 == 1 ? 2 : 1) && Math.Abs(2 * y - (height - 1)) <= 1 || width % 2 == 1 && centre == 0 && y == height / 2 + 1;
                case BoardShape.Plus: return edge < 2 && (y < 2 || bottom < 2);
                case BoardShape.Heart: return y == 0 && (edge == 0 || centre <= 1) || edge < 3 - bottom;
                case BoardShape.Arch: return centre <= 1 && bottom < 2;
                case BoardShape.Castle: return y == 0 && edge % 2 == 1;

                // Two 2×2 arches near the bottom with a pocket under each.
                case BoardShape.Bridge: return (edge == 1 || edge == 2) && (bottom == 2 || bottom == 3);

                // A head row over a slot one or two cells wide; the cells beside and under the slot fill by slides.
                case BoardShape.Keyhole:
                    return y == 2 && centre <= (width % 2 == 0 ? 3 : 2) || y >= 3 && y <= 5 && centre <= (width % 2 == 0 ? 1 : 0);
                case BoardShape.Cave: return edge == 0 && bottom >= 2 && bottom <= 4;

                // A two-cell shelf on each side, and a slot in the middle lower down.
                case BoardShape.Shelves:
                    var third = height / 3;
                    return (edge == 1 || edge == 2) && y == third ||
                           (width % 2 == 0 ? centre <= 1 && y == 2 * third : centre == 0 && y >= 2 * third - 1 && y <= 2 * third);

                // A cup on a stem, and a base that widens a column a row under the cup.
                case BoardShape.Goblet: return y >= 3 && edge < Math.Min(stem, bottom);

                // An attic over a room fed through a flue one or two cells wide; the room widens a column a row.
                case BoardShape.Chimney: return y == 2 && centre > 1 || y >= 3 && edge < Math.Min(stem, Math.Max(0, (width - 1) / 2 - 1 - (y - 3)));
                default: return false;
            }
        }
    }
}
