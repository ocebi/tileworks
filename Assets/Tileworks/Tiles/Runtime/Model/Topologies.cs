using System;

namespace Tileworks.Tiles.Model
{
    public interface ITopology<TCoord> where TCoord : struct, IEquatable<TCoord>
    {
        int CellCount { get; }
        bool Contains(TCoord c);
        int IndexOf(TCoord c);
        TCoord CoordOf(int index);
        int GetNeighbours(TCoord c, Span<TCoord> into);
    }

    public sealed class SquareTopology : ITopology<Cell>
    {
        private static readonly int[] Dx4 = { 1, 0, -1, 0 };
        private static readonly int[] Dy4 = { 0, 1, 0, -1 };
        private static readonly int[] Dx8 = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] Dy8 = { 0, 1, 1, 1, 0, -1, -1, -1 };

        private readonly bool[] _mask;

        public SquareTopology(int width, int height, bool[] mask = null, bool diagonal = false)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (mask != null && mask.Length != width * height) throw new ArgumentException("Mask size must be width × height.");
            Width = width;
            Height = height;
            Diagonal = diagonal;
            _mask = mask;
        }

        public int Width { get; }
        public int Height { get; }
        public bool Diagonal { get; }
        public int CellCount => Width * Height;

        public bool Contains(Cell c) =>
            c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height && (_mask == null || _mask[c.Y * Width + c.X]);

        public int IndexOf(Cell c) => Contains(c) ? c.Y * Width + c.X : -1;
        public Cell CoordOf(int index) => new Cell(index % Width, index / Width);

        public int GetNeighbours(Cell c, Span<Cell> into)
        {
            var dx = Diagonal ? Dx8 : Dx4;
            var dy = Diagonal ? Dy8 : Dy4;
            var count = 0;
            for (var i = 0; i < dx.Length && count < into.Length; i++)
            {
                var n = new Cell(c.X + dx[i], c.Y + dy[i]);
                if (Contains(n)) into[count++] = n;
            }

            return count;
        }
    }

    public sealed class HexTopology : ITopology<Hex>
    {
        private static readonly int[] Dq = { 1, 1, 0, -1, -1, 0 };
        private static readonly int[] Dr = { 0, -1, -1, 0, 1, 1 };

        private readonly int _radius;
        private readonly int _side;

        public HexTopology(int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            _radius = radius;
            _side = radius * 2 + 1;
        }

        public int CellCount => _side * _side;
        public bool Contains(Hex c) => Math.Abs(c.Q) <= _radius && Math.Abs(c.R) <= _radius && Math.Abs(c.S) <= _radius;
        public int IndexOf(Hex c) => Contains(c) ? (c.R + _radius) * _side + (c.Q + _radius) : -1;
        public Hex CoordOf(int index) => new Hex(index % _side - _radius, index / _side - _radius);

        public int GetNeighbours(Hex c, Span<Hex> into)
        {
            var count = 0;
            for (var i = 0; i < 6 && count < into.Length; i++)
            {
                var n = new Hex(c.Q + Dq[i], c.R + Dr[i]);
                if (Contains(n)) into[count++] = n;
            }

            return count;
        }
    }

    public sealed class StackTopology : ITopology<StackPos>
    {
        public StackTopology(int stacks, int capacity)
        {
            if (stacks <= 0 || capacity <= 0) throw new ArgumentOutOfRangeException(nameof(stacks));
            Stacks = stacks;
            Capacity = capacity;
        }

        public int Stacks { get; }
        public int Capacity { get; }
        public int CellCount => Stacks * Capacity;
        public bool Contains(StackPos c) => c.Stack >= 0 && c.Stack < Stacks && c.Depth >= 0 && c.Depth < Capacity;
        public int IndexOf(StackPos c) => Contains(c) ? c.Stack * Capacity + c.Depth : -1;
        public StackPos CoordOf(int index) => new StackPos(index / Capacity, index % Capacity);

        public int GetNeighbours(StackPos c, Span<StackPos> into)
        {
            var count = 0;
            if (c.Depth > 0 && count < into.Length) into[count++] = new StackPos(c.Stack, c.Depth - 1);
            if (c.Depth < Capacity - 1 && count < into.Length) into[count++] = new StackPos(c.Stack, c.Depth + 1);
            return count;
        }
    }
}
