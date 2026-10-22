using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    [Flags]
    public enum TileTraits : byte
    {
        None = 0,
        Locked = 1,
        KindHidden = 2,
        Linked = 4,
    }

    // One stateless instance per modifier type; per-level state lives in the int[] it creates (copied and hashed generically).
    public interface ITileModifier
    {
        ushort TypeId { get; }
        int[] CreateState(TileMatchLevel level);

        RejectReason CanSelect(TileMatchState s, int[] data, int tile) => RejectReason.None;
        int CollectCompanions(TileMatchState s, int[] data, int tile, Span<int> into) => 0;
        TileTraits Traits(TileMatchState s, int[] data, int tile) => TileTraits.None;

        // Before uncovering: may refill vacated slots.
        bool OnRefill(TileMatchState s, int[] data, RuleContext ctx, in TileMatchEventIds ids) => false;

        // After uncovering: reacts to clears and newly free tiles.
        bool OnReact(TileMatchState s, int[] data, RuleContext ctx, in TileMatchEventIds ids) => false;
    }

    public sealed class ModifierSet
    {
        private readonly ITileModifier[] _modifiers;

        public ModifierSet(ITileModifier[] modifiers)
        {
            _modifiers = modifiers ?? Array.Empty<ITileModifier>();
            for (var i = 0; i < _modifiers.Length; i++)
            for (var j = i + 1; j < _modifiers.Length; j++)
            {
                if (_modifiers[i].TypeId == _modifiers[j].TypeId)
                    throw new ArgumentException($"Modifier type {_modifiers[i].TypeId} registered twice.");
            }
        }

        public int Count => _modifiers.Length;

        public bool Supports(ushort type)
        {
            foreach (var modifier in _modifiers)
            {
                if (modifier.TypeId == type) return true;
            }

            return false;
        }

        public int[][] CreateState(TileMatchLevel level)
        {
            var data = new int[_modifiers.Length][];
            for (var i = 0; i < _modifiers.Length; i++) data[i] = _modifiers[i].CreateState(level) ?? Array.Empty<int>();
            return data;
        }

        public RejectReason CanSelect(TileMatchState s, int tile)
        {
            for (var i = 0; i < _modifiers.Length; i++)
            {
                var reason = _modifiers[i].CanSelect(s, s.ModifierData[i], tile);
                if (!reason.IsNone) return reason;
            }

            return RejectReason.None;
        }

        public int CollectCompanions(TileMatchState s, int tile, Span<int> into)
        {
            var count = 0;
            for (var i = 0; i < _modifiers.Length; i++) count += _modifiers[i].CollectCompanions(s, s.ModifierData[i], tile, into.Slice(count));
            return count;
        }

        public TileTraits Traits(TileMatchState s, int tile)
        {
            var traits = TileTraits.None;
            for (var i = 0; i < _modifiers.Length; i++) traits |= _modifiers[i].Traits(s, s.ModifierData[i], tile);
            return traits;
        }

        public bool OnRefill(TileMatchState s, RuleContext ctx, in TileMatchEventIds ids)
        {
            var changed = false;
            for (var i = 0; i < _modifiers.Length; i++) changed |= _modifiers[i].OnRefill(s, s.ModifierData[i], ctx, ids);
            return changed;
        }

        public bool OnReact(TileMatchState s, RuleContext ctx, in TileMatchEventIds ids)
        {
            var changed = false;
            for (var i = 0; i < _modifiers.Length; i++) changed |= _modifiers[i].OnReact(s, s.ModifierData[i], ctx, ids);
            return changed;
        }
    }
}
