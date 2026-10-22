using UnityEngine;

namespace Tileworks.Tiles.View
{
    // Layout of a scrolling grid that keeps only the rows on screen alive: where each index sits, which indices are
    // visible, and how many cells that takes. Matches a top-down, centred GridLayoutGroup with a fixed column count.
    public sealed class GridWindow
    {
        private readonly int _columns;
        private readonly Vector2 _cell;
        private readonly Vector2 _spacing;
        private readonly RectOffset _padding;

        public GridWindow(int columns, Vector2 cell, Vector2 spacing, RectOffset padding)
        {
            _columns = Mathf.Max(1, columns);
            _cell = cell;
            _spacing = spacing;
            _padding = padding ?? new RectOffset();
        }

        private float Pitch => _cell.y + _spacing.y;

        public float ContentHeight(int count)
        {
            var rows = Rows(count);
            return _padding.top + _padding.bottom + rows * _cell.y + Mathf.Max(0, rows - 1) * _spacing.y;
        }

        // The centre of the cell, from the content's top-left corner.
        public Vector2 PositionOf(int index, float contentWidth)
        {
            var rowWidth = _columns * _cell.x + (_columns - 1) * _spacing.x;
            var left = _padding.left + (contentWidth - _padding.horizontal - rowWidth) * 0.5f;
            var column = index % _columns;
            var row = index / _columns;
            return new Vector2(left + column * (_cell.x + _spacing.x) + _cell.x * 0.5f, -(_padding.top + row * Pitch + _cell.y * 0.5f));
        }

        // Indices [first, end) on screen at scroll offset `scrollY`, plus `bufferRows` above and below.
        public (int First, int End) Visible(int count, float scrollY, float viewportHeight, int bufferRows)
        {
            var firstRow = Mathf.Max(0, Mathf.FloorToInt((scrollY - _padding.top) / Pitch) - bufferRows);
            var endRow = Mathf.CeilToInt((scrollY + viewportHeight - _padding.top) / Pitch) + bufferRows;
            var end = Mathf.Clamp(endRow * _columns, 0, count);
            return (Mathf.Min(firstRow * _columns, end), end);
        }

        // Enough cells for any scroll offset.
        public int CellsFor(float viewportHeight, int bufferRows) => (Mathf.CeilToInt(Mathf.Max(0f, viewportHeight) / Pitch) + 1 + 2 * bufferRows) * _columns;

        // The scroll offset that puts `index`'s row in the middle of the viewport, within the scroll range.
        public float ScrollToCentre(int index, int count, float viewportHeight)
        {
            var centre = _padding.top + index / _columns * Pitch + _cell.y * 0.5f;
            return Mathf.Clamp(centre - viewportHeight * 0.5f, 0f, Mathf.Max(0f, ContentHeight(count) - viewportHeight));
        }

        private int Rows(int count) => (Mathf.Max(0, count) + _columns - 1) / _columns;
    }
}
