using Tileworks.Kit.Core;

namespace BlockPuzzle.Api
{
    public interface IPuzzleProgress
    {
        // Zero-based index of the next level to play.
        IReadOnlyObservable<int> CurrentIndex { get; }
    }
}
