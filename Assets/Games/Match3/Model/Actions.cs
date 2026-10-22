using Tileworks.Tiles.Model;

namespace Match3.Model
{
    // The piece at From moves to its neighbour To, and the piece there takes its place.
    public readonly struct Swap : IGameAction
    {
        public readonly int From;
        public readonly int To;

        public Swap(int from, int to)
        {
            From = from;
            To = to;
        }

        public override string ToString() => $"Swap({From},{To})";
    }

    // A double tap on a special fires it where it is.
    public readonly struct Activate : IGameAction
    {
        public readonly int Cell;

        public Activate(int cell) => Cell = cell;

        public override string ToString() => $"Activate({Cell})";
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

    // The view plays a refused swap by its reason: NoMatch goes there and back, Chained shakes the chain, Falling does
    // nothing.
    public static class Match3Rejects
    {
        public static readonly RejectReason NotNeighbours = new RejectReason(100);
        public static readonly RejectReason Fixed = new RejectReason(101);
        public static readonly RejectReason Chained = new RejectReason(102);
        public static readonly RejectReason NoMatch = new RejectReason(103);
        public static readonly RejectReason NotSpecial = new RejectReason(104);
        public static readonly RejectReason NotHammerable = new RejectReason(105);
        public static readonly RejectReason NothingToShuffle = new RejectReason(106);
        public static readonly RejectReason Falling = new RejectReason(107);
        public static readonly RejectReason NoMovesLeft = new RejectReason(108);
    }

    public sealed class SwapCodec : IActionCodec<Swap>
    {
        public ushort TypeId => 1;
        public ActionRecord Encode(in Swap a) => new ActionRecord(TypeId, a.From, a.To);
        public Swap Decode(in ActionRecord r) => new Swap(r.A, r.B);
    }

    public sealed class ActivateCodec : IActionCodec<Activate>
    {
        public ushort TypeId => 2;
        public ActionRecord Encode(in Activate a) => new ActionRecord(TypeId, a.Cell);
        public Activate Decode(in ActionRecord r) => new Activate(r.A);
    }

    public sealed class AddMovesCodec : IActionCodec<AddMoves>
    {
        public ushort TypeId => 3;
        public ActionRecord Encode(in AddMoves a) => new ActionRecord(TypeId, a.Moves);
        public AddMoves Decode(in ActionRecord r) => new AddMoves(r.A);
    }

    public sealed class HammerCodec : IActionCodec<Hammer>
    {
        public ushort TypeId => 4;
        public ActionRecord Encode(in Hammer a) => new ActionRecord(TypeId, a.Cell);
        public Hammer Decode(in ActionRecord r) => new Hammer(r.A);
    }

    public sealed class ShuffleBoardCodec : IActionCodec<ShuffleBoard>
    {
        public ushort TypeId => 5;
        public ActionRecord Encode(in ShuffleBoard a) => new ActionRecord(TypeId);
        public ShuffleBoard Decode(in ActionRecord r) => default;
    }

    public sealed class LandCodec : IActionCodec<Land>
    {
        public ushort TypeId => 6;
        public ActionRecord Encode(in Land a) => new ActionRecord(TypeId, a.Cell, a.Id);
        public Land Decode(in ActionRecord r) => new Land(r.A, r.B);
    }

    public sealed class LandAllCodec : IActionCodec<LandAll>
    {
        public ushort TypeId => 7;
        public ActionRecord Encode(in LandAll a) => new ActionRecord(TypeId);
        public LandAll Decode(in ActionRecord r) => default;
    }
}
