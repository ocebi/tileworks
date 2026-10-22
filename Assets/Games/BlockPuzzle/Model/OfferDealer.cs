using System;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace BlockPuzzle.Model
{
    // Deals three weighted pieces. A refill guarantees at least one fits; a continue guarantees all three do.
    public sealed class OfferDealer
    {
        public const int Colours = 7;
        private const int Attempts = 24;

        private readonly ShapeCatalog _shapes;
        private readonly BlockEventIds _ids;

        public OfferDealer(ShapeCatalog shapes, BlockEventIds ids)
        {
            _shapes = shapes;
            _ids = ids;
        }

        public bool CanDealFitting(BlockState s) => SmallestFitting(s) != null;

        public bool Deal(BlockState s, ref Pcg32 rng, bool allMustFit, EventWriter events)
        {
            Span<int> draws = stackalloc int[BlockState.OfferSize];
            var accepted = false;
            for (var attempt = 0; attempt < Attempts && !accepted; attempt++)
            {
                for (var slot = 0; slot < draws.Length; slot++) draws[slot] = Draw(s.Level, ref rng);
                accepted = allMustFit ? CountFitting(s, draws) == draws.Length : CountFitting(s, draws) > 0;
            }

            if (!accepted)
            {
                var fallback = SmallestFitting(s);
                if (fallback == null && allMustFit) return false;
                for (var slot = 0; slot < draws.Length && fallback != null; slot++)
                {
                    if (!Placement.FitsAnywhere(s, _shapes[draws[slot]])) draws[slot] = fallback.Id;
                    if (!allMustFit) break;
                }
            }

            for (var slot = 0; slot < draws.Length; slot++)
            {
                s.Offer[slot] = draws[slot];
                s.OfferColours[slot] = (byte)rng.NextInt(1, Colours + 1);
                if (events != null) OfferDealt.Emit(events, _ids, slot, draws[slot], s.OfferColours[slot]);
            }

            return true;
        }

        private int Draw(BlockLevel level, ref Pcg32 rng)
        {
            var total = 0;
            for (var i = 0; i < level.Weights.Length; i++) total += level.Weights[i];
            var pick = rng.NextInt(total);
            for (var i = 0; i < level.Pool.Length; i++)
            {
                pick -= level.Weights[i];
                if (pick < 0) return level.Pool[i];
            }

            return level.Pool[level.Pool.Length - 1];
        }

        private int CountFitting(BlockState s, ReadOnlySpan<int> draws)
        {
            var fitting = 0;
            for (var i = 0; i < draws.Length; i++) fitting += Placement.FitsAnywhere(s, _shapes[draws[i]]) ? 1 : 0;
            return fitting;
        }

        // From the level's pool first, then from every piece, so a continue works on any board with a free cell.
        private Shape SmallestFitting(BlockState s)
        {
            Shape best = null;
            foreach (var id in s.Level.Pool) best = Smaller(best, _shapes[id], s);
            if (best != null) return best;
            foreach (var shape in _shapes.All) best = Smaller(best, shape, s);
            return best;
        }

        private static Shape Smaller(Shape best, Shape candidate, BlockState s) =>
            (best == null || candidate.Count < best.Count) && Placement.FitsAnywhere(s, candidate) ? candidate : best;
    }
}
