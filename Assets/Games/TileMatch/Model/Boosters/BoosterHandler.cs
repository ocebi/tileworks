using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // Spending is the level session's job; the rules only decide whether a booster applies and what it does.
    public sealed class BoosterHandler : IActionHandler<TileMatchState, UseBooster>
    {
        private readonly UndoBooster _undo;
        private readonly MagnetBooster _magnet;
        private readonly ShuffleBooster _shuffle;
        private readonly TileMatchEventIds _ids;

        public BoosterHandler(ModifierSet modifiers, TileMatchEventIds ids, int shuffleStream)
        {
            _ids = ids;
            _undo = new UndoBooster(ids);
            _magnet = new MagnetBooster(modifiers, ids);
            _shuffle = new ShuffleBooster(modifiers, ids, shuffleStream);
        }

        public const int MaxBoosterExtraSlots = 2;

        public RejectReason Validate(TileMatchState s, in UseBooster action)
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            switch (action.Booster)
            {
                case BoosterKind.Undo: return _undo.Validate(s);
                case BoosterKind.Magnet: return _magnet.Validate(s);
                case BoosterKind.Shuffle: return _shuffle.Validate(s);
                case BoosterKind.ExtraSlot:
                    return s.BoosterExtraSlots < MaxBoosterExtraSlots ? RejectReason.None : TileMatchRejects.ExtraSlotLimit;
                default: return RejectReason.InvalidTarget;
            }
        }

        public void Apply(TileMatchState s, in UseBooster action, RuleContext ctx)
        {
            switch (action.Booster)
            {
                case BoosterKind.Undo:
                    _undo.Apply(s, ctx);
                    break;
                case BoosterKind.Magnet:
                    _magnet.Apply(s, ctx);
                    break;
                case BoosterKind.Shuffle:
                    _shuffle.Apply(s, ctx);
                    break;
                case BoosterKind.ExtraSlot:
                    s.BoosterExtraSlots++;
                    BoardOps.SetCapacity(s, s.TrayCapacity + 1, ctx, _ids);
                    break;
            }
        }
    }
}
