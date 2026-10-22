using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Kit.Input;

namespace Tileworks.Kit.App
{
    public static class AppStates
    {
        public const string Boot = "Boot";
        public const string Home = "Home";
        public const string Level = "Level";
        public const string Restarting = "Restarting";
    }

    public interface IAppState
    {
        string Id { get; }
        UniTask EnterAsync(object args, CancellationToken ct);
        UniTask ExitAsync(CancellationToken ct);
    }

    public interface ILoadingOverlay
    {
        void Show();
        void Hide();
    }

    // What states and presenters depend on to request a transition; it has no dependencies, so no cycles.
    public interface IAppNavigator
    {
        void GoTo(string stateId, object args = null);
    }

    public sealed class AppNavigator : IAppNavigator
    {
        public event Action<string, object> Requested;

        public void GoTo(string stateId, object args = null)
        {
            if (Requested == null) throw new InvalidOperationException("No state machine is listening.");
            Requested(stateId, args);
        }
    }

    public interface IAppStateMachine
    {
        string Current { get; }
        bool IsTransitioning { get; }
        void GoTo(string stateId, object args = null);
        UniTask GoToAsync(string stateId, object args, CancellationToken ct);
    }

    // Transitions are serialised; a queued request to the same target as the last queued one collapses into it.
    public sealed class AppStateMachine : IAppStateMachine, IDisposable
    {
        private readonly Dictionary<string, IAppState> _states = new Dictionary<string, IAppState>();
        private readonly IReadOnlyList<ILoadingOverlay> _overlays;
        private readonly IReadOnlyList<IAppStateListener> _listeners;
        private readonly IInputGate _gate;
        private readonly ILog _log;
        private readonly List<Request> _queue = new List<Request>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private IAppState _current;
        private bool _running;

        private readonly AppNavigator _navigator;

        public AppStateMachine(IReadOnlyList<IAppState> states, IReadOnlyList<ILoadingOverlay> overlays, IReadOnlyList<IAppStateListener> listeners, IInputGate gate,
            AppNavigator navigator, ILog log)
        {
            foreach (var state in states) _states.Add(state.Id, state);
            _overlays = overlays;
            _listeners = listeners;
            _gate = Guard.NotNull(gate, nameof(gate));
            _log = Guard.NotNull(log, nameof(log));
            _navigator = Guard.NotNull(navigator, nameof(navigator));
            _navigator.Requested += OnRequested;
        }

        private void OnRequested(string stateId, object args) => GoTo(stateId, args);

        public string Current => _current?.Id;
        public bool IsTransitioning => _running;

        public void GoTo(string stateId, object args = null) => GoToAsync(stateId, args, _cts.Token).Forget();

        public UniTask GoToAsync(string stateId, object args, CancellationToken ct)
        {
            if (!_states.ContainsKey(stateId)) throw new ArgumentException($"Unknown app state '{stateId}'.");
            var last = _queue.Count > 0 ? _queue[_queue.Count - 1] : null;
            if (last != null && last.StateId == stateId)
            {
                last.Args = args;
                return last.Completion.Task;
            }

            var request = new Request(stateId, args);
            _queue.Add(request);
            if (!_running) RunQueueAsync().Forget();
            return request.Completion.Task;
        }

        public void Dispose()
        {
            _navigator.Requested -= OnRequested;
            _cts.Cancel();
            _cts.Dispose();
        }

        private async UniTaskVoid RunQueueAsync()
        {
            _running = true;
            try
            {
                while (_queue.Count > 0)
                {
                    var request = _queue[0];
                    _queue.RemoveAt(0);
                    try
                    {
                        await TransitionAsync(request);
                        request.Completion.TrySetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        request.Completion.TrySetCanceled();
                    }
                    catch (Exception e)
                    {
                        _log.Error(LogTag.App, $"Transition to {request.StateId} failed", e);
                        request.Completion.TrySetException(e);
                    }
                }
            }
            finally
            {
                _running = false;
            }
        }

        private async UniTask TransitionAsync(Request request)
        {
            var ct = _cts.Token;
            using (_gate.Block("AppTransition"))
            {
                foreach (var overlay in _overlays) overlay.Show();
                try
                {
                    var from = _current?.Id ?? "(none)";
                    if (_current != null) await _current.ExitAsync(ct);
                    _current = _states[request.StateId];
                    _log.Info(LogTag.App, "State {0} → {1}", from, request.StateId);
                    NotifyEntering(request.StateId);
                    await _current.EnterAsync(request.Args, ct);
                }
                finally
                {
                    foreach (var overlay in _overlays) overlay.Hide();
                }
            }
        }

        private void NotifyEntering(string stateId)
        {
            foreach (var listener in _listeners)
            {
                try
                {
                    listener.OnStateEntering(stateId);
                }
                catch (Exception e)
                {
                    _log.Error(LogTag.App, $"{listener.GetType().Name} failed before {stateId}", e);
                }
            }
        }

        private sealed class Request
        {
            public Request(string stateId, object args)
            {
                StateId = stateId;
                Args = args;
            }

            public string StateId { get; }
            public object Args { get; set; }
            public UniTaskCompletionSource Completion { get; } = new UniTaskCompletionSource();
        }
    }

    // Implemented by scene-scope services that need the arguments of the state that loaded their scene.
    public interface ISceneEntry
    {
        UniTask EnterAsync(object args, CancellationToken ct);
    }

    // Loads one additive scene on enter (building its scope) and unloads it, disposing the scope, on exit.
    public sealed class SceneState : IAppState
    {
        private readonly ISceneLoader _loader;
        private readonly string _sceneName;
        private SceneHandle _handle;

        public SceneState(string id, string sceneName, ISceneLoader loader)
        {
            Id = Guard.NotEmpty(id, nameof(id));
            _sceneName = Guard.NotEmpty(sceneName, nameof(sceneName));
            _loader = Guard.NotNull(loader, nameof(loader));
        }

        public string Id { get; }

        public async UniTask EnterAsync(object args, CancellationToken ct)
        {
            _handle = await _loader.LoadAsync(_sceneName, ct);
            foreach (var entry in _handle.Scope.Resolve<IReadOnlyList<ISceneEntry>>()) await entry.EnterAsync(args, ct);
        }

        public async UniTask ExitAsync(CancellationToken ct)
        {
            await _loader.UnloadAsync(_handle);
            _handle = null;
        }
    }

    public sealed class BootState : IAppState
    {
        private readonly BootPipeline _pipeline;
        private readonly IAppNavigator _navigator;
        private readonly IReadOnlyList<IScenarioHandler> _scenarios;
        private readonly ILog _log;

        public BootState(BootPipeline pipeline, IAppNavigator navigator, IReadOnlyList<IScenarioHandler> scenarios, ILog log)
        {
            _pipeline = Guard.NotNull(pipeline, nameof(pipeline));
            _navigator = Guard.NotNull(navigator, nameof(navigator));
            _scenarios = Guard.NotNull(scenarios, nameof(scenarios));
            _log = Guard.NotNull(log, nameof(log));
        }

        public string Id => AppStates.Boot;

        public async UniTask EnterAsync(object args, CancellationToken ct)
        {
            var report = await _pipeline.RunAsync(null, ct);
            if (report.CriticalFailure) throw new InvalidOperationException("Boot failed:\n" + report);
            // An app with no handlers (the hub) leaves the request for the game it opens.
            if (_scenarios.Count > 0 && ScenarioFile.TryTake(out var request))
            {
                foreach (var scenario in _scenarios)
                {
                    if (scenario.Kind != request.kind) continue;
                    _log.Info(LogTag.App, "Running scenario {0}", request.kind);
                    await scenario.RunAsync(request.payload, ct);
                    return;
                }

                _log.Warn(LogTag.App, "No handler for scenario {0}", request.kind);
            }

            _navigator.GoTo(AppStates.Home);
        }

        public UniTask ExitAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}
