using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Tileworks.Tiles.Model
{
    public interface IEventPlayer
    {
        float IdleAt { get; }
        bool IsIdle { get; }

        // Whether an event is still playing on this entity's lane.
        bool IsBusy(int entity);
        void Play(ReadOnlySpan<BoardEvent> events);
        UniTask WaitIdleAsync(CancellationToken ct);
        void Reset();
    }
}
