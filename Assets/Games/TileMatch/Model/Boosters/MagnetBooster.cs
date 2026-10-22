using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // Completes a triple: prefers the kind with most tiles already in the tray, then the one whose missing tiles
    // are least buried. Pulled tiles may be covered, but never locked, hidden or linked.
    public sealed class MagnetBooster
    {
        private readonly ModifierSet _modifiers;
        private readonly TileMatchEventIds _ids;

        public MagnetBooster(ModifierSet modifiers, TileMatchEventIds ids)
        {
            _modifiers = modifiers;
            _ids = ids;
        }

        public RejectReason Validate(TileMatchState s) => ChooseKind(s) >= 0 ? RejectReason.None : TileMatchRejects.NoMagnetKind;

        public void Apply(TileMatchState s, RuleContext ctx)
        {
            var kind = (ushort)ChooseKind(s);
            var needed = 3 - s.CountInTray(kind);
            var group = s.NextGroup++;
            for (var n = 0; n < needed; n++)
            {
                var tile = BestCandidate(s, kind);
                BoardOps.RemoveFromBoard(s, tile, group, ctx, _ids);
            }
        }

        public int ChooseKind(TileMatchState s)
        {
            var bestKind = -1;
            var bestInTray = -1;
            var bestCost = int.MaxValue;
            for (var kind = 0; kind < s.Level.KindCount; kind++)
            {
                var inTray = s.CountInTray((ushort)kind);
                var needed = 3 - inTray;
                if (needed > s.FreeTraySlots || !TryCost(s, (ushort)kind, needed, out var cost)) continue;
                if (inTray > bestInTray || (inTray == bestInTray && cost < bestCost))
                {
                    bestKind = kind;
                    bestInTray = inTray;
                    bestCost = cost;
                }
            }

            return bestKind;
        }

        private bool TryCost(TileMatchState s, ushort kind, int needed, out int cost)
        {
            cost = 0;
            Span<int> best = stackalloc int[3];
            var found = 0;
            for (var tile = 0; tile < s.TileCount; tile++)
            {
                if (!IsCandidate(s, tile, kind)) continue;
                var covering = s.Cover.Covering[s.SlotOf(tile)];
                if (found < needed)
                {
                    best[found++] = covering;
                    continue;
                }

                var worst = 0;
                for (var i = 1; i < found; i++)
                {
                    if (best[i] > best[worst]) worst = i;
                }

                if (covering < best[worst]) best[worst] = covering;
            }

            for (var i = 0; i < found; i++) cost += best[i];
            return found >= needed;
        }

        private int BestCandidate(TileMatchState s, ushort kind)
        {
            var best = -1;
            var bestCovering = int.MaxValue;
            for (var tile = 0; tile < s.TileCount; tile++)
            {
                if (!IsCandidate(s, tile, kind)) continue;
                var covering = s.Cover.Covering[s.SlotOf(tile)];
                if (covering >= bestCovering) continue;
                best = tile;
                bestCovering = covering;
            }

            return best;
        }

        private bool IsCandidate(TileMatchState s, int tile, ushort kind) =>
            s.Kinds[tile] == kind && s.IsOnBoard(tile) &&
            (_modifiers.Traits(s, tile) & (TileTraits.Locked | TileTraits.KindHidden | TileTraits.Linked)) == 0;
    }
}
