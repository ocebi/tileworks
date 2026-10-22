using System;
using System.Collections.Generic;

namespace Tileworks.Tiles.Model
{
    // Free positions on a half-unit grid in integer layers; a tile occupies Footprint × Footprint half-units.
    public sealed class LayeredLayout : ITopology<Slot>
    {
        private readonly Slot[] _slots;
        private readonly Dictionary<Slot, int> _index;

        public LayeredLayout(Slot[] slots, int footprint = 2)
        {
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
            if (footprint < 1) throw new ArgumentOutOfRangeException(nameof(footprint));
            Footprint = footprint;
            _index = new Dictionary<Slot, int>(slots.Length);
            for (var i = 0; i < slots.Length; i++)
            {
                if (_index.ContainsKey(slots[i])) throw new ArgumentException($"Duplicate slot {slots[i]}.");
                _index.Add(slots[i], i);
            }

            Cover = CoverGraph.Build(slots, footprint);
            ComputeBounds();
        }

        public int Footprint { get; }
        public CoverGraph Cover { get; }
        public int CellCount => _slots.Length;
        public ReadOnlySpan<Slot> Slots => _slots;
        public int MinX { get; private set; }
        public int MinY { get; private set; }
        public int MaxX { get; private set; }
        public int MaxY { get; private set; }
        public int MaxLayer { get; private set; }

        public bool Contains(Slot c) => _index.ContainsKey(c);
        public int IndexOf(Slot c) => _index.TryGetValue(c, out var i) ? i : -1;
        public Slot CoordOf(int index) => _slots[index];

        public int GetNeighbours(Slot c, Span<Slot> into)
        {
            var count = 0;
            var i = IndexOf(c);
            if (i < 0) return 0;
            foreach (var j in Cover.Covers(i))
            {
                if (count == into.Length) break;
                into[count++] = _slots[j];
            }

            foreach (var j in Cover.CoveredBy(i))
            {
                if (count == into.Length) break;
                into[count++] = _slots[j];
            }

            return count;
        }

        public bool Overlaps(int a, int b)
        {
            var sa = _slots[a];
            var sb = _slots[b];
            return Math.Abs(sa.X - sb.X) < Footprint && Math.Abs(sa.Y - sb.Y) < Footprint;
        }

        private void ComputeBounds()
        {
            if (_slots.Length == 0) return;
            MinX = MinY = int.MaxValue;
            MaxX = MaxY = int.MinValue;
            foreach (var s in _slots)
            {
                MinX = Math.Min(MinX, s.X);
                MinY = Math.Min(MinY, s.Y);
                MaxX = Math.Max(MaxX, s.X + Footprint);
                MaxY = Math.Max(MaxY, s.Y + Footprint);
                MaxLayer = Math.Max(MaxLayer, s.Layer);
            }
        }
    }
}
