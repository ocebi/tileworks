using System;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Blast.Model
{
    public enum Effect : byte
    {
        None,
        Row,
        Column,
        Bomb,
        Disco,
        Cross,
        ThickCross,
        BigBomb,
        DiscoRockets,
        DiscoBombs,
        ClearAll,
    }

    // Each piece keeps an id while it falls, so the view can follow it. Specials waiting to go off live in the pending
    // queue only while an action resolves; it is empty between actions. A piece gravity moved stays in flight until the
    // view reports it landed; boxes never move.
    public sealed class BlastState : IRuleState<BlastState>, IGravityGrid, IFlightState
    {
        public BlastState(BlastLevel level)
        {
            Level = level;
            Width = level.Width;
            Height = level.Height;
            var count = level.CellCount;
            Pieces = new Piece[count];
            Colours = new byte[count];
            Hp = new byte[count];
            Ids = new int[count];
            GoalsLeft = new int[level.Goals.Length];
            Pending = new PendingSpecial[count * 2];
            Next = new PendingSpecial[count * 2];
            Marks = new int[count];
            Stack = new int[count];
            Group = new int[count];
            Flight = new FlightMarks(Width, count);
            Legs = new GravityLegs(Width, count);
        }

        public BlastLevel Level { get; }
        public int Width { get; }
        public int Height { get; }
        public int CellCount => Pieces.Length;
        public Piece[] Pieces { get; }
        public byte[] Colours { get; }
        public byte[] Hp { get; }
        public int[] Ids { get; }
        public int[] GoalsLeft { get; }
        public int NextId { get; set; }
        public int MovesLeft { get; set; }
        public int MovesUsed { get; set; }
        public int Score { get; set; }
        public int ContinuesUsed { get; set; }

        // Dead boards shuffled so far; a metric, left out of the hash.
        public int Shuffles { get; set; }
        public Outcome Outcome { get; set; }
        public FlightMarks Flight { get; }

        public PendingSpecial[] Pending { get; private set; }
        public PendingSpecial[] Next { get; private set; }
        public int PendingCount { get; set; }
        public int NextCount { get; set; }

        // The action and pass whose specials last went off; the board settles a pass later, once the blast has played.
        public long FiredAt { get; set; } = -1;

        // Specials gone off so far in the current action, for the HUD's combo callout; it doesn't affect the outcome.
        public int Chain { get; private set; }
        private int _chainAction = -1;

        // Counts one more special in action `action` and returns its place in that action's chain, from 1.
        public int NextChain(int action)
        {
            if (_chainAction != action) (_chainAction, Chain) = (action, 0);
            return ++Chain;
        }

        // Scratch for flood fills; not part of the state.
        public int[] Marks { get; }
        public int MarkStamp { get; set; }
        public int[] Stack { get; }
        public int[] Group { get; }
        public GravityLegs Legs { get; }

        public bool IsPlayable(int cell) => Level.Playable[cell];
        public bool IsStatic(int cell) => Pieces[cell] == Piece.Box;
        public bool IsEmpty(int cell) => Pieces[cell] == Piece.Empty;
        public int IdAt(int cell) => Ids[cell];
        public byte ColourAt(int cell) => Colours[cell];
        public int X(int cell) => cell % Width;
        public int Y(int cell) => cell / Width;
        public int CellAt(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? y * Width + x : -1;

        public bool GoalsMet
        {
            get
            {
                for (var i = 0; i < GoalsLeft.Length; i++)
                    if (GoalsLeft[i] > 0) return false;
                return true;
            }
        }

        public void SwapPending()
        {
            (Pending, Next) = (Next, Pending);
            PendingCount = NextCount;
            NextCount = 0;
        }

        public int NewMark() => ++MarkStamp;

        public void Move(int from, int to)
        {
            Pieces[to] = Pieces[from];
            Colours[to] = Colours[from];
            Hp[to] = Hp[from];
            Ids[to] = Ids[from];
            Pieces[from] = Piece.Empty;
            Colours[from] = 0;
            Hp[from] = 0;
            Flight.Move(from, to);
        }

        public void Spawn(int cell, ref Pcg32 rng)
        {
            Pieces[cell] = Piece.Cube;
            Colours[cell] = (byte)(1 + rng.NextInt(Level.Colours));
            Ids[cell] = NextId++;
        }

        public void Launch(int cell) => Flight.Launch(cell);

        // Empties a cell; a piece in flight there stops flying.
        public void Clear(int cell)
        {
            Pieces[cell] = Piece.Empty;
            Colours[cell] = 0;
            Hp[cell] = 0;
            Flight.Land(cell);
        }

        public void CopyTo(BlastState target)
        {
            Array.Copy(Pieces, target.Pieces, Pieces.Length);
            Array.Copy(Colours, target.Colours, Colours.Length);
            Array.Copy(Hp, target.Hp, Hp.Length);
            Array.Copy(Ids, target.Ids, Ids.Length);
            Array.Copy(GoalsLeft, target.GoalsLeft, GoalsLeft.Length);
            target.NextId = NextId;
            target.MovesLeft = MovesLeft;
            target.MovesUsed = MovesUsed;
            target.Score = Score;
            target.ContinuesUsed = ContinuesUsed;
            target.Shuffles = Shuffles;
            target.Outcome = Outcome;
            target.PendingCount = 0;
            target.NextCount = 0;
            target.FiredAt = -1;
            target._chainAction = -1;
            target.Chain = 0;
            Flight.CopyTo(target.Flight);
        }

        public ulong ComputeHash()
        {
            var hash = StableHash.Combine(0xB1A5, (ulong)(Width << 8 | Height));
            for (var i = 0; i < Pieces.Length; i++) hash = StableHash.Combine(hash, (ulong)Pieces[i] << 16 | (ulong)Colours[i] << 8 | Hp[i]);
            for (var i = 0; i < GoalsLeft.Length; i++) hash = StableHash.Combine(hash, (ulong)(uint)GoalsLeft[i]);
            hash = StableHash.Combine(hash, (ulong)(uint)MovesLeft << 32 | (uint)Score);
            hash = Flight.Hash(hash);
            return StableHash.Combine(hash, (ulong)ContinuesUsed << 8 | (byte)Outcome);
        }
    }

    public readonly struct PendingSpecial
    {
        public readonly int Cell;
        public readonly int Id;
        public readonly Effect Effect;
        public readonly byte Colour;

        public PendingSpecial(int cell, int id, Effect effect, byte colour)
        {
            Cell = cell;
            Id = id;
            Effect = effect;
            Colour = colour;
        }
    }
}
