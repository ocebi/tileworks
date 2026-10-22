using Tileworks.Kit.Core;

namespace Blast.Api
{
    public interface IBlastProgress
    {
        // Zero-based index of the next level to play.
        IReadOnlyObservable<int> CurrentIndex { get; }
    }
}
