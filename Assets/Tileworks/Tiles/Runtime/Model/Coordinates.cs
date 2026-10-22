using System;

namespace Tileworks.Tiles.Model
{
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }

    // Axial hex coordinates.
    public readonly struct Hex : IEquatable<Hex>
    {
        public readonly int Q;
        public readonly int R;

        public Hex(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int S => -Q - R;
        public int DistanceTo(Hex other) => (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(S - other.S)) / 2;

        public bool Equals(Hex other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is Hex other && Equals(other);
        public override int GetHashCode() => (Q * 397) ^ R;
        public override string ToString() => $"hex({Q},{R})";
    }

    // A position on a half-unit grid with an integer layer (tile match, mahjong).
    public readonly struct Slot : IEquatable<Slot>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Layer;

        public Slot(int x, int y, int layer)
        {
            X = x;
            Y = y;
            Layer = layer;
        }

        public bool Equals(Slot other) => X == other.X && Y == other.Y && Layer == other.Layer;
        public override bool Equals(object obj) => obj is Slot other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ (Y * 31) ^ Layer;
        public override string ToString() => $"({X},{Y})L{Layer}";
    }

    public readonly struct StackPos : IEquatable<StackPos>
    {
        public readonly int Stack;
        public readonly int Depth;

        public StackPos(int stack, int depth)
        {
            Stack = stack;
            Depth = depth;
        }

        public bool Equals(StackPos other) => Stack == other.Stack && Depth == other.Depth;
        public override bool Equals(object obj) => obj is StackPos other && Equals(other);
        public override int GetHashCode() => (Stack * 397) ^ Depth;
        public override string ToString() => $"stack{Stack}[{Depth}]";
    }
}
