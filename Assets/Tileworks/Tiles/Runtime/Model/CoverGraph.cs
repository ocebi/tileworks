using System;
using System.Collections.Generic;

namespace Tileworks.Tiles.Model
{
    // Which slots overlap which, in compressed (CSR) arrays. Immutable and shared by every state of a level.
    public sealed class CoverGraph
    {
        private readonly int[] _coversStart;
        private readonly int[] _covers;
        private readonly int[] _coveredByStart;
        private readonly int[] _coveredBy;

        private CoverGraph(int[] coversStart, int[] covers, int[] coveredByStart, int[] coveredBy)
        {
            _coversStart = coversStart;
            _covers = covers;
            _coveredByStart = coveredByStart;
            _coveredBy = coveredBy;
            for (var i = 0; i + 1 < coversStart.Length; i++) MaxCovers = Math.Max(MaxCovers, coversStart[i + 1] - coversStart[i]);
        }

        public int MaxCovers { get; }
        public int SlotCount => _coversStart.Length - 1;

        // A covers B when A is on a higher layer and their footprints overlap.
        public static CoverGraph Build(ReadOnlySpan<Slot> slots, int footprint = 2)
        {
            var byPosition = new Dictionary<long, List<int>>(slots.Length);
            for (var i = 0; i < slots.Length; i++)
            {
                var key = Key(slots[i].X, slots[i].Y);
                if (!byPosition.TryGetValue(key, out var list)) byPosition[key] = list = new List<int>(2);
                list.Add(i);
            }

            var covers = new List<int>[slots.Length];
            var coveredBy = new List<int>[slots.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                covers[i] = new List<int>(4);
                coveredBy[i] = new List<int>(4);
            }

            var reach = footprint - 1;
            for (var i = 0; i < slots.Length; i++)
            {
                var a = slots[i];
                for (var dx = -reach; dx <= reach; dx++)
                for (var dy = -reach; dy <= reach; dy++)
                {
                    if (!byPosition.TryGetValue(Key(a.X + dx, a.Y + dy), out var candidates)) continue;
                    foreach (var j in candidates)
                    {
                        if (slots[j].Layer >= a.Layer) continue;
                        covers[i].Add(j);
                        coveredBy[j].Add(i);
                    }
                }
            }

            Flatten(covers, out var coversStart, out var coversFlat);
            Flatten(coveredBy, out var coveredByStart, out var coveredByFlat);
            return new CoverGraph(coversStart, coversFlat, coveredByStart, coveredByFlat);
        }

        public ReadOnlySpan<int> Covers(int slot) => new ReadOnlySpan<int>(_covers, _coversStart[slot], _coversStart[slot + 1] - _coversStart[slot]);
        public ReadOnlySpan<int> CoveredBy(int slot) => new ReadOnlySpan<int>(_coveredBy, _coveredByStart[slot], _coveredByStart[slot + 1] - _coveredByStart[slot]);
        public int InitialCoveringCount(int slot) => _coveredByStart[slot + 1] - _coveredByStart[slot];

        private static long Key(int x, int y) => ((long)x << 32) | (uint)y;

        private static void Flatten(List<int>[] lists, out int[] start, out int[] flat)
        {
            start = new int[lists.Length + 1];
            var total = 0;
            for (var i = 0; i < lists.Length; i++)
            {
                lists[i].Sort();
                start[i] = total;
                total += lists[i].Count;
            }

            start[lists.Length] = total;
            flat = new int[total];
            for (var i = 0; i < lists.Length; i++) lists[i].CopyTo(flat, start[i]);
        }
    }

    // The mutable side of the cover graph: how many occupied slots cover each slot.
    public struct CoverCounts
    {
        public int[] Covering;

        public static CoverCounts Create(CoverGraph graph, ReadOnlySpan<bool> occupied)
        {
            var counts = new CoverCounts { Covering = new int[graph.SlotCount] };
            for (var i = 0; i < graph.SlotCount; i++)
            {
                if (!occupied[i]) continue;
                foreach (var covered in graph.Covers(i)) counts.Covering[covered]++;
            }

            return counts;
        }

        public bool IsUncovered(int slot) => Covering[slot] == 0;

        // Returns how many slots became uncovered, written into `uncovered`.
        public int OnVacated(CoverGraph graph, int slot, Span<int> uncovered)
        {
            var count = 0;
            foreach (var covered in graph.Covers(slot))
            {
                if (--Covering[covered] == 0 && count < uncovered.Length) uncovered[count++] = covered;
            }

            return count;
        }

        // Returns how many slots became covered again, written into `covered`.
        public int OnOccupied(CoverGraph graph, int slot, Span<int> covered)
        {
            var count = 0;
            foreach (var below in graph.Covers(slot))
            {
                if (Covering[below]++ == 0 && count < covered.Length) covered[count++] = below;
            }

            return count;
        }

        public void CopyTo(ref CoverCounts target) => Array.Copy(Covering, target.Covering, Covering.Length);
    }
}
