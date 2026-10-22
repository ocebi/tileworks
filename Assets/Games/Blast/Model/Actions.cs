using Tileworks.Tiles.Model;

namespace Blast.Model
{
    public readonly struct Tap : IGameAction
    {
        public readonly int Cell;

        public Tap(int cell) => Cell = cell;

        public override string ToString() => $"Tap({Cell})";
    }

    // The continue after running out of moves.
    public readonly struct AddMoves : IGameAction
    {
        public readonly int Moves;

        public AddMoves(int moves) => Moves = moves;
    }

    // Boosters; neither costs a move.
    public readonly struct Hammer : IGameAction
    {
        public readonly int Cell;

        public Hammer(int cell) => Cell = cell;

        public override string ToString() => $"Hammer({Cell})";
    }

    public readonly struct ShuffleBoard : IGameAction
    {
    }

    public static class BlastRejects
    {
        public static readonly RejectReason NotTappable = new RejectReason(100);
        public static readonly RejectReason GroupTooSmall = new RejectReason(101);
        public static readonly RejectReason NotHammerable = new RejectReason(102);
        public static readonly RejectReason NothingToShuffle = new RejectReason(103);
        public static readonly RejectReason Falling = new RejectReason(104);
        public static readonly RejectReason NoMovesLeft = new RejectReason(105);
    }

    public sealed class TapCodec : IActionCodec<Tap>
    {
        public ushort TypeId => 1;
        public ActionRecord Encode(in Tap a) => new ActionRecord(TypeId, a.Cell);
        public Tap Decode(in ActionRecord r) => new Tap(r.A);
    }

    public sealed class AddMovesCodec : IActionCodec<AddMoves>
    {
        public ushort TypeId => 2;
        public ActionRecord Encode(in AddMoves a) => new ActionRecord(TypeId, a.Moves);
        public AddMoves Decode(in ActionRecord r) => new AddMoves(r.A);
    }

    public sealed class HammerCodec : IActionCodec<Hammer>
    {
        public ushort TypeId => 3;
        public ActionRecord Encode(in Hammer a) => new ActionRecord(TypeId, a.Cell);
        public Hammer Decode(in ActionRecord r) => new Hammer(r.A);
    }

    public sealed class ShuffleBoardCodec : IActionCodec<ShuffleBoard>
    {
        public ushort TypeId => 4;
        public ActionRecord Encode(in ShuffleBoard a) => new ActionRecord(TypeId);
        public ShuffleBoard Decode(in ActionRecord r) => default;
    }

    public sealed class LandCodec : IActionCodec<Land>
    {
        public ushort TypeId => 5;
        public ActionRecord Encode(in Land a) => new ActionRecord(TypeId, a.Cell, a.Id);
        public Land Decode(in ActionRecord r) => new Land(r.A, r.B);
    }

    public sealed class LandAllCodec : IActionCodec<LandAll>
    {
        public ushort TypeId => 6;
        public ActionRecord Encode(in LandAll a) => new ActionRecord(TypeId);
        public LandAll Decode(in ActionRecord r) => default;
    }
}
