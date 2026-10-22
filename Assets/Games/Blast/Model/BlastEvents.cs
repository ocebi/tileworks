using Tileworks.Tiles.Model;

namespace Blast.Model
{
    // Pieces are the entities (by id), so a piece's fall waits for its own earlier events on the event player's lanes.
    public readonly struct BlastEventIds
    {
        public readonly ushort PieceCleared;
        public readonly ushort BoxDamaged;
        public readonly ushort SpecialCreated;
        public readonly ushort SpecialMerged;
        public readonly ushort SpecialsCombined;
        public readonly ushort SpecialFired;
        public readonly ushort CubeConverted;
        public readonly ushort PieceFell;
        public readonly ushort PieceSpawned;
        public readonly ushort CubeRecoloured;
        public readonly ushort MovesChanged;
        public readonly ushort GoalChanged;
        public readonly ushort LevelWon;
        public readonly ushort LevelLost;
        public readonly ushort HammerStruck;
        public readonly ushort BoardShuffled;
        public readonly ushort PieceShuffled;

        public BlastEventIds(EventTypeRegistry r)
        {
            PieceCleared = r.Register(nameof(PieceCleared), EntityFields.Entity);
            BoxDamaged = r.Register(nameof(BoxDamaged), EntityFields.Entity);
            SpecialCreated = r.Register(nameof(SpecialCreated), EntityFields.Entity);
            SpecialMerged = r.Register(nameof(SpecialMerged), EntityFields.Entity);
            SpecialsCombined = r.Register(nameof(SpecialsCombined), EntityFields.Entity);
            SpecialFired = r.Register(nameof(SpecialFired), EntityFields.Entity);
            CubeConverted = r.Register(nameof(CubeConverted), EntityFields.Entity);
            PieceFell = r.Register(nameof(PieceFell), EntityFields.Entity, preempts: true);
            PieceSpawned = r.Register(nameof(PieceSpawned), EntityFields.Entity);
            CubeRecoloured = r.Register(nameof(CubeRecoloured), EntityFields.Entity);
            MovesChanged = r.Register(nameof(MovesChanged), EntityFields.None);
            GoalChanged = r.Register(nameof(GoalChanged), EntityFields.None);
            LevelWon = r.Register(nameof(LevelWon), EntityFields.None, barrier: true);
            LevelLost = r.Register(nameof(LevelLost), EntityFields.None, barrier: true);
            HammerStruck = r.Register(nameof(HammerStruck), EntityFields.Entity);
            BoardShuffled = r.Register(nameof(BoardShuffled), EntityFields.None, barrier: true);
            PieceShuffled = r.Register(nameof(PieceShuffled), EntityFields.Entity);
        }
    }

    // Typed views: nothing outside this file reads BoardEvent.A/B/C directly.

    // A piece removed from the board. Order is its distance from what cleared it, Cause the special that did (None for a
    // tapped group), and Into the cell it slides into when its group makes a special there (-1 otherwise).
    public readonly struct PieceCleared
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly Piece Piece;
        public readonly byte Colour;
        public readonly int Order;
        public readonly Effect Cause;
        public readonly int Into;

        private PieceCleared(int id, int cell, Piece piece, byte colour, int order, Effect cause, int into)
        {
            Id = id;
            Cell = cell;
            Piece = piece;
            Colour = colour;
            Order = order;
            Cause = cause;
            Into = into;
        }

        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int cell, Piece piece, byte colour, int order, Effect cause, int into) =>
            w.Emit(ids.PieceCleared, id, cell, (int)cause << 16 | (int)piece << 8 | colour, (into + 1) << 8 | order);

        public static PieceCleared From(in BoardEvent e) =>
            new PieceCleared(e.Entity, e.A, (Piece)(e.B >> 8 & 0xFF), (byte)e.B, e.C & 0xFF, (Effect)(e.B >> 16 & 0xFF), (e.C >> 8) - 1);
    }

    public readonly struct BoxDamaged
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly int HpLeft;
        public readonly int Order;
        private BoxDamaged(int id, int cell, int hpLeft, int order) { Id = id; Cell = cell; HpLeft = hpLeft; Order = order; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int cell, int hpLeft, int order) => w.Emit(ids.BoxDamaged, id, cell, hpLeft, order);
        public static BoxDamaged From(in BoardEvent e) => new BoxDamaged(e.Entity, e.A, e.B, e.C);
    }

    public readonly struct SpecialCreated
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly Piece Piece;
        public readonly byte Colour;
        private SpecialCreated(int id, int cell, Piece piece, byte colour) { Id = id; Cell = cell; Piece = piece; Colour = colour; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int cell, Piece piece, byte colour) => w.Emit(ids.SpecialCreated, id, cell, (int)piece, colour);
        public static SpecialCreated From(in BoardEvent e) => new SpecialCreated(e.Entity, e.A, (Piece)e.B, (byte)e.C);
    }

    // A special pulled into a combo: it moves from its cell onto the tapped one.
    public readonly struct SpecialMerged
    {
        public readonly int Id;
        public readonly int Source;
        public readonly int Into;
        private SpecialMerged(int id, int source, int into) { Id = id; Source = source; Into = into; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int from, int into) => w.Emit(ids.SpecialMerged, id, from, into);
        public static SpecialMerged From(in BoardEvent e) => new SpecialMerged(e.Entity, e.A, e.B);
    }

    // Touching specials pulled onto the tapped one, which then fires as a combo of `Count` specials. Chain is this special's
    // place among the specials gone off in the action, from 1. Count (at most the cell count) shares C with it.
    public readonly struct SpecialsCombined
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly Effect Effect;
        public readonly int Count;
        public readonly int Chain;
        private SpecialsCombined(int id, int cell, Effect effect, int count, int chain) { Id = id; Cell = cell; Effect = effect; Count = count; Chain = chain; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int cell, Effect effect, int count, int chain) =>
            w.Emit(ids.SpecialsCombined, id, cell, (int)effect, count & 0xFFFF | chain << 16);
        public static SpecialsCombined From(in BoardEvent e) => new SpecialsCombined(e.Entity, e.A, (Effect)e.B, e.C & 0xFFFF, e.C >> 16);
    }

    // Chain is this special's place among the specials gone off in the action, from 1.
    public readonly struct SpecialFired
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly Effect Effect;
        public readonly byte Colour;
        public readonly int Chain;
        private SpecialFired(int id, int cell, Effect effect, byte colour, int chain) { Id = id; Cell = cell; Effect = effect; Colour = colour; Chain = chain; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int cell, Effect effect, byte colour, int chain) =>
            w.Emit(ids.SpecialFired, id, cell, (int)effect, colour | chain << 8);
        public static SpecialFired From(in BoardEvent e) => new SpecialFired(e.Entity, e.A, (Effect)e.B, (byte)e.C, e.C >> 8);
    }

    // A disco combo turned this cube into a special.
    public readonly struct CubeConverted
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly Piece Piece;
        public readonly int Order;
        private CubeConverted(int id, int cell, Piece piece, int order) { Id = id; Cell = cell; Piece = piece; Order = order; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int cell, Piece piece, int order) => w.Emit(ids.CubeConverted, id, cell, (int)piece, order);
        public static CubeConverted From(in BoardEvent e) => new CubeConverted(e.Entity, e.A, (Piece)e.B, e.C);
    }

    // One leg of a piece's path: straight down or one diagonal slide; Lands on its last leg only.
    public readonly struct PieceFell
    {
        public readonly int Id;
        public readonly int Source;
        public readonly int Target;
        public readonly bool Lands;
        private PieceFell(int id, int source, int target, bool lands) { Id = id; Source = source; Target = target; Lands = lands; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int from, int to, bool lands) => w.Emit(ids.PieceFell, id, from, to, lands ? 1 : 0);
        public static PieceFell From(in BoardEvent e) => new PieceFell(e.Entity, e.A, e.B, e.C != 0);
    }

    // A new cube entering from above its column; Drop is how many rows above the top it starts.
    public readonly struct PieceSpawned
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly byte Colour;
        public readonly int Drop;
        public readonly bool Lands;
        private PieceSpawned(int id, int cell, byte colour, int drop, bool lands) { Id = id; Cell = cell; Colour = colour; Drop = drop; Lands = lands; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int cell, byte colour, int drop, bool lands) =>
            w.Emit(ids.PieceSpawned, id, cell, colour, drop << 1 | (lands ? 1 : 0));
        public static PieceSpawned From(in BoardEvent e) => new PieceSpawned(e.Entity, e.A, (byte)e.B, e.C >> 1, (e.C & 1) != 0);
    }

    public readonly struct CubeRecoloured
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly byte Colour;
        private CubeRecoloured(int id, int cell, byte colour) { Id = id; Cell = cell; Colour = colour; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int cell, byte colour) => w.Emit(ids.CubeRecoloured, id, cell, colour);
        public static CubeRecoloured From(in BoardEvent e) => new CubeRecoloured(e.Entity, e.A, (byte)e.B);
    }

    public readonly struct MovesChanged
    {
        public readonly int Left;
        private MovesChanged(int left) => Left = left;
        public static void Emit(EventWriter w, in BlastEventIds ids, int left) => w.Emit(ids.MovesChanged, left);
        public static MovesChanged From(in BoardEvent e) => new MovesChanged(e.Entity);
    }

    public readonly struct GoalChanged
    {
        public readonly int Goal;
        public readonly int Left;
        private GoalChanged(int goal, int left) { Goal = goal; Left = left; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int goal, int left) => w.Emit(ids.GoalChanged, goal, left);
        public static GoalChanged From(in BoardEvent e) => new GoalChanged(e.Entity, e.A);
    }

    // The hammer comes down on a piece; what it breaks follows as the usual clear or damage events.
    public readonly struct HammerStruck
    {
        public readonly int Id;
        public readonly int Cell;
        private HammerStruck(int id, int cell) { Id = id; Cell = cell; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int cell) => w.Emit(ids.HammerStruck, id, cell);
        public static HammerStruck From(in BoardEvent e) => new HammerStruck(e.Entity, e.A);
    }

    // A cube the shuffle booster moved from Source to Target.
    public readonly struct PieceShuffled
    {
        public readonly int Id;
        public readonly int Source;
        public readonly int Target;
        private PieceShuffled(int id, int source, int target) { Id = id; Source = source; Target = target; }
        public static void Emit(EventWriter w, in BlastEventIds ids, int id, int from, int to) => w.Emit(ids.PieceShuffled, id, from, to);
        public static PieceShuffled From(in BoardEvent e) => new PieceShuffled(e.Entity, e.A, e.B);
    }
}
