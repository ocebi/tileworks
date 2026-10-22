using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Tileworks.Kit.Core
{
    public interface IInitializable
    {
        void Initialize();
    }

    public interface IAsyncInitializable
    {
        UniTask InitializeAsync(CancellationToken ct);
    }

    public interface ITickable
    {
        void Tick();
    }

    public interface ILateTickable
    {
        void LateTick();
    }

    public interface IAppLifecycleListener
    {
        void OnApplicationPause(bool paused) { }
        void OnApplicationFocus(bool focused) { }
        void OnApplicationQuit() { }
        void OnLowMemory() { }
    }

    // Told before each app state is entered, so content updates can be applied between screens rather than during them.
    public interface IAppStateListener
    {
        void OnStateEntering(string stateId);
    }

    // Implemented by the engine loop driver so the container stays engine-free.
    public interface ILoopRegistry
    {
        IDisposable AddTickable(ITickable tickable);
        IDisposable AddLateTickable(ILateTickable tickable);
        IDisposable AddLifecycleListener(IAppLifecycleListener listener);
    }
}
