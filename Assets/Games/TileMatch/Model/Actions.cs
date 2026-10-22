using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    public enum BoosterKind : byte
    {
        Undo = 1,
        Magnet = 2,
        Shuffle = 3,
        ExtraSlot = 4,
    }

    public readonly struct SelectTile : IGameAction
    {
        public readonly int Tile;
        public SelectTile(int tile) => Tile = tile;
        public override string ToString() => $"SelectTile({Tile})";
    }

    public readonly struct UseBooster : IGameAction
    {
        public readonly BoosterKind Booster;
        public UseBooster(BoosterKind booster) => Booster = booster;
        public override string ToString() => $"UseBooster({Booster})";
    }

    public readonly struct ContinueAfterFail : IGameAction
    {
        public readonly int ExtraSlots;
        public readonly int ReturnCount;

        public ContinueAfterFail(int extraSlots, int returnCount)
        {
            ExtraSlots = extraSlots;
            ReturnCount = returnCount;
        }
    }

    public readonly struct TimeUp : IGameAction
    {
    }

    public static class TileMatchRejects
    {
        public static readonly RejectReason Frozen = new RejectReason(100);
        public static readonly RejectReason PartnerBlocked = new RejectReason(101);
        public static readonly RejectReason NothingToUndo = new RejectReason(102);
        public static readonly RejectReason SlotOccupied = new RejectReason(103);
        public static readonly RejectReason NoMagnetKind = new RejectReason(104);
        public static readonly RejectReason ExtraSlotLimit = new RejectReason(105);
        public static readonly RejectReason NotLost = new RejectReason(106);
        public static readonly RejectReason NothingToShuffle = new RejectReason(107);
    }

    public sealed class SelectTileCodec : IActionCodec<SelectTile>
    {
        public ushort TypeId => 1;
        public ActionRecord Encode(in SelectTile a) => new ActionRecord(TypeId, a.Tile);
        public SelectTile Decode(in ActionRecord r) => new SelectTile(r.A);
    }

    public sealed class UseBoosterCodec : IActionCodec<UseBooster>
    {
        public ushort TypeId => 2;
        public ActionRecord Encode(in UseBooster a) => new ActionRecord(TypeId, (int)a.Booster);
        public UseBooster Decode(in ActionRecord r) => new UseBooster((BoosterKind)r.A);
    }

    public sealed class ContinueCodec : IActionCodec<ContinueAfterFail>
    {
        public ushort TypeId => 3;
        public ActionRecord Encode(in ContinueAfterFail a) => new ActionRecord(TypeId, a.ExtraSlots, a.ReturnCount);
        public ContinueAfterFail Decode(in ActionRecord r) => new ContinueAfterFail(r.A, r.B);
    }

    public sealed class TimeUpCodec : IActionCodec<TimeUp>
    {
        public ushort TypeId => 4;
        public ActionRecord Encode(in TimeUp a) => new ActionRecord(TypeId);
        public TimeUp Decode(in ActionRecord r) => default;
    }
}
