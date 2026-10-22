using System;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Match3.Model
{
    public enum Effect : byte
    {
        None,
        Row,
        Column,
        Bomb,
        Disco,
        Flyer,
        Cross,
        ThickCross,
        BigBomb,
        DiscoRockets,
        DiscoBombs,
        DiscoFlyers,
        ClearAll,
        FlyerRow,
        FlyerColumn,
        FlyerBomb,
        TripleFlyer,
    }

    // Each piece keeps an id while it moves, so the view can follow it; floors have ids too, so a flyer's landing can hold
    // back the floor it hits. Specials waiting to go off live in the pending queue only while an action resolves. A piece
    // gravity moved is in flight until the view reports it landed; it takes no part in matches or swaps until then.
    public sealed class Match3State : IRuleState<Match3State>, IGravityGrid, IFlightState
    {
        public Match3State(Match3Level level)
        {
            Level = level;
            Width = level.Width;
            Height = level.Height;
            var count = level.CellCount;
            Pieces = new Piece[count];
            Colours = new byte[count];
            Hp = new byte[count];
            Ids = new int[count];
            Floors = new byte[count];
            FloorIds = new int[count];
            Chained = new bool[count];
            Landed = new int[count];
            GoalsLeft = new int[level.Goals.Length];
            Pending = new PendingSpecial[count * 2];
            Next = new PendingSpecial[count * 2];
            Marks = new int[count];
            Matched = new int[count];
            Stack = new int[count];
            Group = new int[count];
            SpareColours = new byte[count];
            SpareIds = new int[count];
            SparePieces = new Piece[count];
            Flight = new FlightMarks(Width, count);
            Legs = new GravityLegs(Width, count);
        }

        public Match3Level Level { get; }
        public int Width { get; }
        public int Height { get; }
        public int CellCount => Pieces.Length;
        public Piece[] Pieces { get; }
        public byte[] Colours { get; }
        public byte[] Hp { get; }
        public int[] Ids { get; }
        public byte[] Floors { get; }
        public int[] FloorIds { get; }
        public bool[] Chained { get; }
        public FlightMarks Flight { get; }

        // When the piece in each cell arrived; a match leaves its special where the newest piece landed.
        public int[] Landed { get; }
        public int LandClock { get; set; }
        public int[] GoalsLeft { get; }
        public int NextId { get; set; }
        public int MovesLeft { get; set; }
        public int MovesUsed { get; set; }
        public int Score { get; set; }
        public int ContinuesUsed { get; set; }

        // Dead boards shuffled this game; not part of the hash.
        public int Shuffles { get; set; }
        public Outcome Outcome { get; set; }

        public PendingSpecial[] Pending { get; private set; }
        public PendingSpecial[] Next { get; private set; }
        public int PendingCount { get; set; }
        public int NextCount { get; set; }

        // How many times matches cleared since the player's last move; landings keep the cascade counting.
        public int Matches { get; set; }

        // Per action: the last wave that cleared or moved pieces.
        public int ClearedWave { get; set; }
        public int MovedWave { get; set; }

        // Scratch; not part of the state.
        public int[] Marks { get; }
        public int MarkStamp { get; set; }
        public int[] Matched { get; }
        public int[] Stack { get; }
        public int[] Group { get; }
        public byte[] SpareColours { get; }
        public int[] SpareIds { get; }
        public Piece[] SparePieces { get; }
        public GravityLegs Legs { get; }

        public bool IsPlayable(int cell) => Level.Playable[cell];
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

        public void BeginAction()
        {
            ClearedWave = -1;
            MovedWave = -1;
        }

        // A player's move or booster starts a new cascade count.
        public void BeginMove()
        {
            BeginAction();
            Matches = 0;
        }

        public void SwapPending()
        {
            (Pending, Next) = (Next, Pending);
            PendingCount = NextCount;
            NextCount = 0;
        }

        public int NewMark() => ++MarkStamp;

        public void CopyTo(Match3State target)
        {
            Array.Copy(Pieces, target.Pieces, Pieces.Length);
            Array.Copy(Colours, target.Colours, Colours.Length);
            Array.Copy(Hp, target.Hp, Hp.Length);
            Array.Copy(Ids, target.Ids, Ids.Length);
            Array.Copy(Floors, target.Floors, Floors.Length);
            Array.Copy(FloorIds, target.FloorIds, FloorIds.Length);
            Array.Copy(Chained, target.Chained, Chained.Length);
            Array.Copy(Landed, target.Landed, Landed.Length);
            Array.Copy(GoalsLeft, target.GoalsLeft, GoalsLeft.Length);
            Flight.CopyTo(target.Flight);
            target.Matches = Matches;
            target.LandClock = LandClock;
            target.NextId = NextId;
            target.MovesLeft = MovesLeft;
            target.MovesUsed = MovesUsed;
            target.Score = Score;
            target.ContinuesUsed = ContinuesUsed;
            target.Shuffles = Shuffles;
            target.Outcome = Outcome;
            target.PendingCount = 0;
            target.NextCount = 0;
        }

        public ulong ComputeHash()
        {
            var hash = StableHash.Combine(0x3A7C, (ulong)(Width << 8 | Height));
            for (var i = 0; i < Pieces.Length; i++)
                hash = StableHash.Combine(hash, (ulong)Pieces[i] << 24 | (ulong)Colours[i] << 16 | (ulong)Hp[i] << 8 | (ulong)Floors[i] << 1 | (Chained[i] ? 1UL : 0UL));
            for (var i = 0; i < GoalsLeft.Length; i++) hash = StableHash.Combine(hash, (ulong)(uint)GoalsLeft[i]);
            hash = StableHash.Combine(hash, (ulong)(uint)MovesLeft << 32 | (uint)Score);
            hash = Flight.Hash(StableHash.Combine(hash, (ulong)(uint)Matches));
            return StableHash.Combine(hash, (ulong)ContinuesUsed << 8 | (byte)Outcome);
        }

        public bool IsStatic(int cell) => Match3Board.IsStatic(this, cell);
        public bool IsEmpty(int cell) => Pieces[cell] == Piece.Empty;
        public int IdAt(int cell) => Ids[cell];
        public byte ColourAt(int cell) => Colours[cell];
        public void Launch(int cell) => Flight.Launch(cell);

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
            Pieces[cell] = Piece.Gem;
            Colours[cell] = (byte)(1 + rng.NextInt(Level.Colours));
            Hp[cell] = 0;
            Ids[cell] = Match3Board.NewId(this);
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
