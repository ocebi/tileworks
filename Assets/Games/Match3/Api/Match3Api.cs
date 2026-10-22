using Tileworks.Kit.Core;

namespace Match3.Api
{
    public interface IMatch3Progress
    {
        // Zero-based index of the next level to play.
        IReadOnlyObservable<int> CurrentIndex { get; }
    }
}
