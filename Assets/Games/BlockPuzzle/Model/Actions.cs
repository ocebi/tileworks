using Tileworks.Tiles.Model;

namespace BlockPuzzle.Model
{
    public readonly struct PlacePiece : IGameAction
    {
        public readonly int Slot;

        // Cell under the top-left corner of the piece's bounding box.
        public readonly int Anchor;

        public PlacePiece(int slot, int anchor)
        {
            Slot = slot;
            Anchor = anchor;
        }

        public override string ToString() => $"PlacePiece({Slot}, {Anchor})";
    }

    // The continue after a loss: a fresh offer whose pieces all fit.
    public readonly struct RefreshOffer : IGameAction
    {
    }

    public static class BlockRejects
    {
        public static readonly RejectReason Blocked = new RejectReason(100);
        public static readonly RejectReason EmptySlot = new RejectReason(101);
    }

    public sealed class PlacePieceCodec : IActionCodec<PlacePiece>
    {
        public ushort TypeId => 1;
        public ActionRecord Encode(in PlacePiece a) => new ActionRecord(TypeId, a.Slot, a.Anchor);
        public PlacePiece Decode(in ActionRecord r) => new PlacePiece(r.A, r.B);
    }

    public sealed class RefreshOfferCodec : IActionCodec<RefreshOffer>
    {
        public ushort TypeId => 2;
        public ActionRecord Encode(in RefreshOffer a) => new ActionRecord(TypeId);
        public RefreshOffer Decode(in ActionRecord r) => default;
    }
}
