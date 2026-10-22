using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // Permutes kinds among visible board tiles; the multiset of kinds never changes.
    public sealed class ShuffleBooster
    {
        private readonly ModifierSet _modifiers;
        private readonly TileMatchEventIds _ids;
        private readonly int _stream;

        public ShuffleBooster(ModifierSet modifiers, TileMatchEventIds ids, int stream)
        {
            _modifiers = modifiers;
            _ids = ids;
            _stream = stream;
        }

        public RejectReason Validate(TileMatchState s)
        {
            var count = 0;
            for (var tile = 0; tile < s.TileCount && count < 2; tile++)
            {
                if (IsShuffleable(s, tile)) count++;
            }

            return count >= 2 ? RejectReason.None : TileMatchRejects.NothingToShuffle;
        }

        public void Apply(TileMatchState s, RuleContext ctx)
        {
            Span<int> tiles = stackalloc int[s.TileCount];
            Span<ushort> kinds = stackalloc ushort[s.TileCount];
            var count = 0;
            for (var tile = 0; tile < s.TileCount; tile++)
            {
                if (!IsShuffleable(s, tile)) continue;
                tiles[count] = tile;
                kinds[count++] = s.Kinds[tile];
            }

            ctx.Rng.Get(_stream).Shuffle(kinds.Slice(0, count));
            for (var i = 0; i < count; i++)
            {
                if (s.Kinds[tiles[i]] == kinds[i]) continue;
                s.Kinds[tiles[i]] = kinds[i];
                TileKindChanged.Emit(ctx.Events, _ids, tiles[i], kinds[i]);
            }
        }

        private bool IsShuffleable(TileMatchState s, int tile) =>
            s.IsOnBoard(tile) && (_modifiers.Traits(s, tile) & TileTraits.KindHidden) == 0;
    }
}
