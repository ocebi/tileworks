using Tileworks.Tiles.Model;

namespace BlockPuzzle.Model
{
    // Cells are the entities, so a cell's clear waits for its fill on the event player's lanes.
    public readonly struct BlockEventIds
    {
        public readonly ushort PiecePlaced;
        public readonly ushort CellFilled;
        public readonly ushort CellCleared;
        public readonly ushort LinesCleared;
        public readonly ushort ScoreChanged;
        public readonly ushort OfferDealt;
        public readonly ushort LevelWon;
        public readonly ushort LevelLost;

        public BlockEventIds(EventTypeRegistry r)
        {
            PiecePlaced = r.Register(nameof(PiecePlaced), EntityFields.None);
            CellFilled = r.Register(nameof(CellFilled), EntityFields.Entity);
            CellCleared = r.Register(nameof(CellCleared), EntityFields.Entity);
            LinesCleared = r.Register(nameof(LinesCleared), EntityFields.None);
            ScoreChanged = r.Register(nameof(ScoreChanged), EntityFields.None);
            OfferDealt = r.Register(nameof(OfferDealt), EntityFields.None);
            LevelWon = r.Register(nameof(LevelWon), EntityFields.None, barrier: true);
            LevelLost = r.Register(nameof(LevelLost), EntityFields.None, barrier: true);
        }
    }

    // Typed views: nothing outside this file reads BoardEvent.A/B/C directly.
    public readonly struct PiecePlaced
    {
        public readonly int Slot;
        public readonly int Anchor;
        public readonly int Shape;
        private PiecePlaced(int slot, int anchor, int shape) { Slot = slot; Anchor = anchor; Shape = shape; }
        public static void Emit(EventWriter w, in BlockEventIds ids, int slot, int anchor, int shape) => w.Emit(ids.PiecePlaced, slot, anchor, shape);
        public static PiecePlaced From(in BoardEvent e) => new PiecePlaced(e.Entity, e.A, e.B);
    }

    public readonly struct CellFilled
    {
        public readonly int Cell;
        public readonly byte Colour;
        private CellFilled(int cell, byte colour) { Cell = cell; Colour = colour; }
        public static void Emit(EventWriter w, in BlockEventIds ids, int cell, byte colour) => w.Emit(ids.CellFilled, cell, colour);
        public static CellFilled From(in BoardEvent e) => new CellFilled(e.Entity, (byte)e.A);
    }

    public readonly struct CellCleared
    {
        public readonly int Cell;
        public readonly byte Colour;
        public readonly int Order;
        public readonly bool Gem;
        private CellCleared(int cell, byte colour, int order, bool gem) { Cell = cell; Colour = colour; Order = order; Gem = gem; }

        public static void Emit(EventWriter w, in BlockEventIds ids, int cell, byte colour, int order, bool gem) =>
            w.Emit(ids.CellCleared, cell, colour, order, gem ? 1 : 0);

        public static CellCleared From(in BoardEvent e) => new CellCleared(e.Entity, (byte)e.A, e.B, e.C != 0);
    }

    public readonly struct LinesCleared
    {
        public readonly int Lines;
        public readonly int Cells;
        public readonly int Streak;
        public readonly int Points;
        private LinesCleared(int lines, int cells, int streak, int points) { Lines = lines; Cells = cells; Streak = streak; Points = points; }

        public static void Emit(EventWriter w, in BlockEventIds ids, int lines, int cells, int streak, int points) =>
            w.Emit(ids.LinesCleared, lines, cells, streak, points);

        public static LinesCleared From(in BoardEvent e) => new LinesCleared(e.Entity, e.A, e.B, e.C);
    }

    public readonly struct ScoreChanged
    {
        public readonly int Score;
        public readonly int Delta;
        private ScoreChanged(int score, int delta) { Score = score; Delta = delta; }
        public static void Emit(EventWriter w, in BlockEventIds ids, int score, int delta) => w.Emit(ids.ScoreChanged, score, delta);
        public static ScoreChanged From(in BoardEvent e) => new ScoreChanged(e.Entity, e.A);
    }

    public readonly struct OfferDealt
    {
        public readonly int Slot;
        public readonly int Shape;
        public readonly byte Colour;
        private OfferDealt(int slot, int shape, byte colour) { Slot = slot; Shape = shape; Colour = colour; }
        public static void Emit(EventWriter w, in BlockEventIds ids, int slot, int shape, byte colour) => w.Emit(ids.OfferDealt, slot, shape, colour);
        public static OfferDealt From(in BoardEvent e) => new OfferDealt(e.Entity, e.A, (byte)e.B);
    }
}
