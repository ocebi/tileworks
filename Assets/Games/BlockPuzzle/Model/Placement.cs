namespace BlockPuzzle.Model
{
    // Board geometry shared by the rules, the search domain and the view's drop preview.
    public static class Placement
    {
        public static bool Fits(BlockState s, Shape shape, int anchor)
        {
            if (anchor < 0 || anchor >= s.CellCount) return false;
            var ax = anchor % s.Width;
            var ay = anchor / s.Width;
            if (ax + shape.Width > s.Width || ay + shape.Height > s.Height) return false;
            for (var i = 0; i < shape.Count; i++)
            {
                if (s.Cells[(ay + shape.Dy(i)) * s.Width + ax + shape.Dx(i)] != 0) return false;
            }

            return true;
        }

        public static bool FitsAnywhere(BlockState s, Shape shape)
        {
            for (var anchor = 0; anchor < s.CellCount; anchor++)
            {
                if (Fits(s, shape, anchor)) return true;
            }

            return false;
        }

        public static int CellOf(BlockState s, Shape shape, int anchor, int i) =>
            (anchor / s.Width + shape.Dy(i)) * s.Width + anchor % s.Width + shape.Dx(i);

        public static bool RowFull(BlockState s, int y)
        {
            for (var x = 0; x < s.Width; x++)
            {
                if (s.Cells[y * s.Width + x] == 0) return false;
            }

            return true;
        }

        public static bool ColumnFull(BlockState s, int x)
        {
            for (var y = 0; y < s.Height; y++)
            {
                if (s.Cells[y * s.Width + x] == 0) return false;
            }

            return true;
        }

        // Rows and columns a placement would complete, as bit masks; the board must be at most 32 wide and high.
        public static (int Rows, int Columns) LinesCompletedBy(BlockState s, Shape shape, int anchor)
        {
            var ax = anchor % s.Width;
            var ay = anchor / s.Width;
            int rows = 0, columns = 0;
            for (var dy = 0; dy < shape.Height; dy++)
            {
                var y = ay + dy;
                var empty = 0;
                for (var x = 0; x < s.Width; x++) empty += s.Cells[y * s.Width + x] == 0 ? 1 : 0;
                var covered = 0;
                for (var i = 0; i < shape.Count; i++) covered += shape.Dy(i) == dy ? 1 : 0;
                if (empty == covered) rows |= 1 << y;
            }

            for (var dx = 0; dx < shape.Width; dx++)
            {
                var x = ax + dx;
                var empty = 0;
                for (var y = 0; y < s.Height; y++) empty += s.Cells[y * s.Width + x] == 0 ? 1 : 0;
                var covered = 0;
                for (var i = 0; i < shape.Count; i++) covered += shape.Dx(i) == dx ? 1 : 0;
                if (empty == covered) columns |= 1 << x;
            }

            return (rows, columns);
        }
    }
}
