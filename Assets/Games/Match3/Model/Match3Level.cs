using System;

namespace Match3.Model
{
    public enum Piece : byte
    {
        Empty = 0,
        Gem = 1,
        RocketRow = 2,
        RocketColumn = 3,
        Bomb = 4,
        Disco = 5,
        Flyer = 6,
        Box = 7,
        Balloon = 8,
        Duck = 9,
    }

    public enum GoalKind : byte
    {
        Colour = 0,
        Box = 1,
        Balloon = 2,
        Duck = 3,
        Floor = 4,
        Chain = 5,
    }

    public readonly struct Match3Goal
    {
        public readonly GoalKind Kind;
        public readonly byte Colour;
        public readonly int Count;

        public Match3Goal(GoalKind kind, int count, byte colour = 0)
        {
            Kind = kind;
            Count = count;
            Colour = colour;
        }

        public override string ToString() => Kind == GoalKind.Colour ? $"{Count}× colour {Colour}" : $"{Count}× {Kind}";
    }

    public static class Pieces
    {
        public static bool IsSpecial(Piece p) => p >= Piece.RocketRow && p <= Piece.Flyer;
        public static bool IsBlocker(Piece p) => p >= Piece.Box;
    }

    // Immutable level definition. Cells are row-major with row 0 at the top; gravity pulls towards the last row. A gem with
    // colour 0 gets a colour from the seed when the level starts. Floors sit under pieces; chains lock the gem they cover.
    // An empty playable cell is a sealed pen behind boxes or chains: it fills once its wall clears.
    public sealed class Match3Level
    {
        public const int MinSize = 5;
        public const int MaxWidth = 9;
        public const int MaxHeight = 11;
        public const int MinColours = 3;
        public const int MaxColours = 6;
        public const int MaxGoals = 4;
        public const int MaxLayers = 2;

        public Match3Level(string id, int width, int height, int moves, int colours, bool[] playable, Piece[] pieces, byte[] tints, byte[] hp,
            byte[] floors, bool[] chains, Match3Goal[] goals)
        {
            if (width < MinSize || height < MinSize || width > MaxWidth || height > MaxHeight) throw new ArgumentException($"Board {width}x{height} is out of range.");
            var count = width * height;
            if (playable.Length != count || pieces.Length != count || tints.Length != count || hp.Length != count || floors.Length != count || chains.Length != count)
                throw new ArgumentException("Cell arrays do not match the board.");
            if (colours < MinColours || colours > MaxColours) throw new ArgumentException($"{colours} colours is out of range.");
            if (moves <= 0) throw new ArgumentException("A level needs moves.");
            if (goals == null || goals.Length == 0 || goals.Length > MaxGoals) throw new ArgumentException($"A level needs 1 to {MaxGoals} goals.");
            for (var i = 0; i < count; i++)
            {
                if (!playable[i] && (pieces[i] != Piece.Empty || floors[i] > 0 || chains[i])) throw new ArgumentException($"Cell {i} is not playable but holds something.");
                if (chains[i] && pieces[i] != Piece.Gem) throw new ArgumentException($"Cell {i}: only gems can be chained.");
                if (floors[i] > MaxLayers || (pieces[i] == Piece.Box && (hp[i] < 1 || hp[i] > MaxLayers))) throw new ArgumentException($"Cell {i} has too many layers.");
            }

            Id = id;
            Width = width;
            Height = height;
            Moves = moves;
            Colours = colours;
            Playable = playable;
            Pieces = pieces;
            Tints = tints;
            Hp = hp;
            Floors = floors;
            Chains = chains;
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
        public byte[] Floors { get; }
        public bool[] Chains { get; }
        public Match3Goal[] Goals { get; }
    }
}
