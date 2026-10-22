using System;

namespace Blast.Model
{
    public enum Piece : byte
    {
        Empty = 0,
        Cube = 1,
        RocketRow = 2,
        RocketColumn = 3,
        Bomb = 4,
        Disco = 5,
        Box = 6,
        Balloon = 7,
        Duck = 8,
    }

    public enum GoalKind : byte
    {
        Colour = 0,
        Box = 1,
        Balloon = 2,
        Duck = 3,
    }

    public readonly struct BlastGoal
    {
        public readonly GoalKind Kind;
        public readonly byte Colour;
        public readonly int Count;

        public BlastGoal(GoalKind kind, int count, byte colour = 0)
        {
            Kind = kind;
            Count = count;
            Colour = colour;
        }

        public override string ToString() => Kind == GoalKind.Colour ? $"{Count}× colour {Colour}" : $"{Count}× {Kind}";
    }

    public static class Pieces
    {
        public static bool IsSpecial(Piece p) => p >= Piece.RocketRow && p <= Piece.Disco;
        public static bool IsBlocker(Piece p) => p >= Piece.Box;
    }

    // Immutable level definition. Cells are row-major with row 0 at the top; gravity pulls towards the last row.
    // A cube or disco ball with colour 0 gets a colour from the seed when the level starts.
    public sealed class BlastLevel
    {
        public const int MinSize = 5;
        public const int MaxSize = 11;
        public const int MaxColours = 6;

        public BlastLevel(string id, int width, int height, int moves, int colours, bool[] playable, Piece[] pieces, byte[] tints, byte[] hp,
            BlastGoal[] goals)
        {
            if (width < MinSize || height < MinSize || width > MaxSize || height > MaxSize) throw new ArgumentException($"Board {width}x{height} is out of range.");
            var count = width * height;
            if (playable.Length != count || pieces.Length != count || tints.Length != count || hp.Length != count)
                throw new ArgumentException("Cell arrays do not match the board.");
            if (colours < 2 || colours > MaxColours) throw new ArgumentException($"{colours} colours is out of range.");
            if (moves <= 0) throw new ArgumentException("A level needs moves.");
            if (goals == null || goals.Length == 0) throw new ArgumentException("A level needs a goal.");
            for (var i = 0; i < count; i++)
                if (!playable[i] && pieces[i] != Piece.Empty) throw new ArgumentException($"Cell {i} is not playable but holds {pieces[i]}.");

            Id = id;
            Width = width;
            Height = height;
            Moves = moves;
            Colours = colours;
            Playable = playable;
            Pieces = pieces;
            Tints = tints;
            Hp = hp;
            Goals = goals;
        }

        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public int CellCount => Width * Height;
        public int Moves { get; }
        public int Colours { get; }
        public bool[] Playable { get; }
        public Piece[] Pieces { get; }
        public byte[] Tints { get; }
        public byte[] Hp { get; }
        public BlastGoal[] Goals { get; }
    }
}
