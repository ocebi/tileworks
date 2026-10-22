using System;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Search
{
    public enum SearchStatus : byte
    {
        Ongoing,
        Won,
        Lost,
    }

    public interface ISearchDomain<TState, TMove> where TMove : struct
    {
        int MaxMoves { get; }
        TState CreateScratch();
        int GetMoves(TState s, Span<TMove> into);
        void Apply(TState s, in TMove move);
        void CopyTo(TState from, TState to);
        SearchStatus Status(TState s);
        ulong Hash(TState s);

        // Higher is tried first.
        int Score(TState s, in TMove move);

        // A game-specific measure of danger (tile match: tray occupancy); the solver can cap it.
        int Pressure(TState s);
    }

    public interface IPlayoutPolicy<TState, TMove> where TMove : struct
    {
        int Choose(TState s, ReadOnlySpan<TMove> moves, ISearchDomain<TState, TMove> domain, ref Pcg32 rng);
    }

    public readonly struct SearchBudget
    {
        public readonly int MaxNodes;
        public readonly int MaxPressure;

        public SearchBudget(int maxNodes, int maxPressure = int.MaxValue)
        {
            MaxNodes = maxNodes;
            MaxPressure = maxPressure;
        }
    }

    public sealed class SolveResult<TMove> where TMove : struct
    {
        public bool Solved;
        public bool ProvenUnsolvable;
        public TMove[] Path = Array.Empty<TMove>();
        public int Nodes;
        public int DeadEnds;
        public int PeakPressure;

        public bool BudgetExhausted => !Solved && !ProvenUnsolvable;
        public float DeadEndRatio => Nodes == 0 ? 0 : (float)DeadEnds / Nodes;
    }
}
