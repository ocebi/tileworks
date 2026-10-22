using Tileworks.Tiles.Model;

namespace Match3.Model
{
    // Pieces are the entities (by id), so a piece's moves wait for its own earlier events on the event player's lanes.
    public readonly struct Match3EventIds
    {
        public readonly ushort PiecesSwapped;
        public readonly ushort PieceCleared;
        public readonly ushort BoxDamaged;
        public readonly ushort FloorDamaged;
        public readonly ushort ChainBroken;
        public readonly ushort SpecialCreated;
        public readonly ushort SpecialMerged;
        public readonly ushort SpecialsCombined;
        public readonly ushort SpecialFired;
        public readonly ushort GemConverted;
        public readonly ushort FlyerLaunched;
        public readonly ushort FlyerLanded;
        public readonly ushort PieceFell;
        public readonly ushort PieceSpawned;
        public readonly ushort BoardShuffled;
        public readonly ushort PieceShuffled;
        public readonly ushort Cascade;
        public readonly ushort MovesChanged;
        public readonly ushort GoalChanged;
        public readonly ushort LevelWon;
        public readonly ushort LevelLost;
        public readonly ushort HammerStruck;

        public Match3EventIds(EventTypeRegistry r)
        {
            PiecesSwapped = r.Register(nameof(PiecesSwapped), EntityFields.Entity | EntityFields.C);
            PieceCleared = r.Register(nameof(PieceCleared), EntityFields.Entity);
            BoxDamaged = r.Register(nameof(BoxDamaged), EntityFields.Entity);
            FloorDamaged = r.Register(nameof(FloorDamaged), EntityFields.Entity);
            ChainBroken = r.Register(nameof(ChainBroken), EntityFields.Entity);
            SpecialCreated = r.Register(nameof(SpecialCreated), EntityFields.Entity);
            SpecialMerged = r.Register(nameof(SpecialMerged), EntityFields.Entity);
            SpecialsCombined = r.Register(nameof(SpecialsCombined), EntityFields.Entity);
            SpecialFired = r.Register(nameof(SpecialFired), EntityFields.Entity);
            GemConverted = r.Register(nameof(GemConverted), EntityFields.Entity);
            FlyerLaunched = r.Register(nameof(FlyerLaunched), EntityFields.Entity);
            FlyerLanded = r.Register(nameof(FlyerLanded), EntityFields.Entity | EntityFields.B | EntityFields.C);
            PieceFell = r.Register(nameof(PieceFell), EntityFields.Entity, preempts: true);
            PieceSpawned = r.Register(nameof(PieceSpawned), EntityFields.Entity);
            BoardShuffled = r.Register(nameof(BoardShuffled), EntityFields.None, barrier: true);
            PieceShuffled = r.Register(nameof(PieceShuffled), EntityFields.Entity);
            Cascade = r.Register(nameof(Cascade), EntityFields.None);
            MovesChanged = r.Register(nameof(MovesChanged), EntityFields.None);
            GoalChanged = r.Register(nameof(GoalChanged), EntityFields.None);
            LevelWon = r.Register(nameof(LevelWon), EntityFields.None, barrier: true);
            LevelLost = r.Register(nameof(LevelLost), EntityFields.None, barrier: true);
            HammerStruck = r.Register(nameof(HammerStruck), EntityFields.Entity | EntityFields.C);
        }
    }

    // Typed views: nothing outside this file reads BoardEvent.A/B/C directly.

    // An accepted swap: the piece Id moved Source → Target, and the piece OtherId went the other way.
    public readonly struct PiecesSwapped
    {
        public readonly int Id;
        public readonly int Source;
        public readonly int Target;
        public readonly int OtherId;
        private PiecesSwapped(int id, int source, int target, int otherId) { Id = id; Source = source; Target = target; OtherId = otherId; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int from, int to, int otherId) => w.Emit(ids.PiecesSwapped, id, from, to, otherId);
        public static PiecesSwapped From(in BoardEvent e) => new PiecesSwapped(e.Entity, e.A, e.B, e.C);
    }

    // A piece removed from the board. Order is its distance from what cleared it, Cause the special that did (None for a
    // match), and Into the cell it slides into when its match makes a special there (-1 otherwise).
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

        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int cell, Piece piece, byte colour, int order, Effect cause, int into) =>
            w.Emit(ids.PieceCleared, id, cell, (int)cause << 16 | (int)piece << 8 | colour, (into + 1) << 8 | order);

        public static PieceCleared From(in BoardEvent e) =>
            new PieceCleared(e.Entity, e.A, (Piece)(e.B >> 8 & 0xFF), (byte)e.B, e.C & 0xFF, (Effect)(e.B >> 16 & 0xFF), (e.C >> 8) - 1);
    }

    public readonly struct BoxDamaged
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly int LayersLeft;
        public readonly int Order;
        private BoxDamaged(int id, int cell, int layersLeft, int order) { Id = id; Cell = cell; LayersLeft = layersLeft; Order = order; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int cell, int layersLeft, int order) => w.Emit(ids.BoxDamaged, id, cell, layersLeft, order);
        public static BoxDamaged From(in BoardEvent e) => new BoxDamaged(e.Entity, e.A, e.B, e.C);
    }

    // Id is the floor's own id; LayersLeft 0 means the cell is clear.
    public readonly struct FloorDamaged
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly int LayersLeft;
        public readonly int Order;
        private FloorDamaged(int id, int cell, int layersLeft, int order) { Id = id; Cell = cell; LayersLeft = layersLeft; Order = order; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int cell, int layersLeft, int order) => w.Emit(ids.FloorDamaged, id, cell, layersLeft, order);
        public static FloorDamaged From(in BoardEvent e) => new FloorDamaged(e.Entity, e.A, e.B, e.C);
    }

    // The chain on the gem Id broke; the gem stays.
    public readonly struct ChainBroken
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly int Order;
        private ChainBroken(int id, int cell, int order) { Id = id; Cell = cell; Order = order; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int cell, int order) => w.Emit(ids.ChainBroken, id, cell, order);
        public static ChainBroken From(in BoardEvent e) => new ChainBroken(e.Entity, e.A, e.B);
    }

    public readonly struct SpecialCreated
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly Piece Piece;
        private SpecialCreated(int id, int cell, Piece piece) { Id = id; Cell = cell; Piece = piece; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int cell, Piece piece) => w.Emit(ids.SpecialCreated, id, cell, (int)piece);
        public static SpecialCreated From(in BoardEvent e) => new SpecialCreated(e.Entity, e.A, (Piece)e.B);
    }

    // A special swapped onto another: it moves from its cell onto the other's, and the pair fires as a combo.
    public readonly struct SpecialMerged
    {
        public readonly int Id;
        public readonly int Source;
        public readonly int Into;
        private SpecialMerged(int id, int source, int into) { Id = id; Source = source; Into = into; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int from, int into) => w.Emit(ids.SpecialMerged, id, from, into);
        public static SpecialMerged From(in BoardEvent e) => new SpecialMerged(e.Entity, e.A, e.B);
    }

    public readonly struct SpecialsCombined
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly Effect Effect;
        private SpecialsCombined(int id, int cell, Effect effect) { Id = id; Cell = cell; Effect = effect; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int cell, Effect effect) => w.Emit(ids.SpecialsCombined, id, cell, (int)effect);
        public static SpecialsCombined From(in BoardEvent e) => new SpecialsCombined(e.Entity, e.A, (Effect)e.B);
    }

    // Colour is the one a disco clears or converts; 0 for other effects.
    public readonly struct SpecialFired
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly Effect Effect;
        public readonly byte Colour;
        private SpecialFired(int id, int cell, Effect effect, byte colour) { Id = id; Cell = cell; Effect = effect; Colour = colour; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int cell, Effect effect, byte colour) => w.Emit(ids.SpecialFired, id, cell, (int)effect, colour);
        public static SpecialFired From(in BoardEvent e) => new SpecialFired(e.Entity, e.A, (Effect)e.B, (byte)e.C);
    }

    // A disco combo turned this gem into a special, which fires in the next wave.
    public readonly struct GemConverted
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly Piece Piece;
        public readonly int Order;
        private GemConverted(int id, int cell, Piece piece, int order) { Id = id; Cell = cell; Piece = piece; Order = order; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int cell, Piece piece, int order) => w.Emit(ids.GemConverted, id, cell, (int)piece, order);
        public static GemConverted From(in BoardEvent e) => new GemConverted(e.Entity, e.A, (Piece)e.B, e.C);
    }

    // A flyer takes off from Source towards Target, carrying a rocket or bomb effect (None for a plain flyer). A flyer
    // combo launches extra flyers with new ids from the same cell.
    public readonly struct FlyerLaunched
    {
        public readonly int Id;
        public readonly int Source;
        public readonly int Target;
        public readonly Effect Carried;
        private FlyerLaunched(int id, int source, int target, Effect carried) { Id = id; Source = source; Target = target; Carried = carried; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int from, int target, Effect carried) => w.Emit(ids.FlyerLaunched, id, from, target, (int)carried);
        public static FlyerLaunched From(in BoardEvent e) => new FlyerLaunched(e.Entity, e.A, e.B, (Effect)e.C);
    }

    // The flyer reached Target. It holds the lanes of the piece and floor there, so their hits play on landing.
    public readonly struct FlyerLanded
    {
        public readonly int Id;
        public readonly int Target;
        public readonly int PieceId;
        public readonly int FloorId;
        private FlyerLanded(int id, int target, int pieceId, int floorId) { Id = id; Target = target; PieceId = pieceId; FloorId = floorId; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int target, int pieceId, int floorId) => w.Emit(ids.FlyerLanded, id, target, pieceId, floorId);
        public static FlyerLanded From(in BoardEvent e) => new FlyerLanded(e.Entity, e.A, e.B, e.C);
    }

    // One leg of a fall: straight down a column (passing holes) or one diagonal step. Lands is set on the piece's last leg.
    public readonly struct PieceFell
    {
        public readonly int Id;
        public readonly int Source;
        public readonly int Target;
        public readonly bool Lands;
        private PieceFell(int id, int source, int target, bool lands) { Id = id; Source = source; Target = target; Lands = lands; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int from, int to, bool lands) => w.Emit(ids.PieceFell, id, from, to, lands ? 1 : 0);
        public static PieceFell From(in BoardEvent e) => new PieceFell(e.Entity, e.A, e.B, e.C != 0);
    }

    // A new gem entering from above its column; Drop is how many rows above the top it starts.
    public readonly struct PieceSpawned
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly byte Colour;
        public readonly int Drop;
        public readonly bool Lands;
        private PieceSpawned(int id, int cell, byte colour, int drop, bool lands) { Id = id; Cell = cell; Colour = colour; Drop = drop; Lands = lands; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int cell, byte colour, int drop, bool lands) => w.Emit(ids.PieceSpawned, id, cell, colour, drop << 1 | (lands ? 1 : 0));
        public static PieceSpawned From(in BoardEvent e) => new PieceSpawned(e.Entity, e.A, (byte)e.B, e.C >> 1, (e.C & 1) != 0);
    }

    public readonly struct PieceShuffled
    {
        public readonly int Id;
        public readonly int Source;
        public readonly int Target;
        private PieceShuffled(int id, int source, int target) { Id = id; Source = source; Target = target; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int from, int to) => w.Emit(ids.PieceShuffled, id, from, to);
        public static PieceShuffled From(in BoardEvent e) => new PieceShuffled(e.Entity, e.A, e.B);
    }

    // The Count-th time matches cleared in one move; sent from the second on.
    public readonly struct Cascade
    {
        public readonly int Count;
        private Cascade(int count) => Count = count;
        public static void Emit(EventWriter w, in Match3EventIds ids, int count) => w.Emit(ids.Cascade, count);
        public static Cascade From(in BoardEvent e) => new Cascade(e.Entity);
    }

    public readonly struct MovesChanged
    {
        public readonly int Left;
        private MovesChanged(int left) => Left = left;
        public static void Emit(EventWriter w, in Match3EventIds ids, int left) => w.Emit(ids.MovesChanged, left);
        public static MovesChanged From(in BoardEvent e) => new MovesChanged(e.Entity);
    }

    public readonly struct GoalChanged
    {
        public readonly int Goal;
        public readonly int Left;
        private GoalChanged(int goal, int left) { Goal = goal; Left = left; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int goal, int left) => w.Emit(ids.GoalChanged, goal, left);
        public static GoalChanged From(in BoardEvent e) => new GoalChanged(e.Entity, e.A);
    }

    // The board is about to shuffle: by itself when it has no move, or from the shuffle booster.
    public readonly struct BoardShuffled
    {
        public readonly bool Booster;
        private BoardShuffled(bool booster) => Booster = booster;
        public static void Emit(EventWriter w, in Match3EventIds ids, bool booster) => w.Emit(ids.BoardShuffled, booster ? 1 : 0);
        public static BoardShuffled From(in BoardEvent e) => new BoardShuffled(e.Entity == 1);
    }

    // The hammer comes down on a cell; what it breaks follows as the usual clear, damage and chain events. It holds the
    // floor's lane too (FloorId -1 without one), so the floor cracks on impact.
    public readonly struct HammerStruck
    {
        public readonly int Id;
        public readonly int Cell;
        public readonly int FloorId;
        private HammerStruck(int id, int cell, int floorId) { Id = id; Cell = cell; FloorId = floorId; }
        public static void Emit(EventWriter w, in Match3EventIds ids, int id, int cell, int floorId) => w.Emit(ids.HammerStruck, id, cell, 0, floorId);
        public static HammerStruck From(in BoardEvent e) => new HammerStruck(e.Entity, e.A, e.C);
    }
}
