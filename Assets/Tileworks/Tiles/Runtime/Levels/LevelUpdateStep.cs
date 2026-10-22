using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Levels
{
    // Checks for new levels at boot without holding it up for long: boot waits up to `bootWaitMs`, and an update that takes longer
    // lands on the next return to Home. Coming back to the app after `refreshAfter` checks again.
    public sealed class LevelUpdateStep : BootStep, IAppLifecycleListener, IDisposable
    {
        private readonly LevelUpdater _updater;
        private readonly IClock _clock;
        private readonly ILog _log;
        private readonly int _bootWaitMs;
        private readonly TimeSpan _refreshAfter;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private DateTime _lastCheck = DateTime.MinValue;
        private bool _running;
        private bool _disposed;

        public LevelUpdateStep(LevelUpdater updater, IClock clock, ILog log, int bootWaitMs, TimeSpan refreshAfter)
        {
            _updater = Guard.NotNull(updater, nameof(updater));
            _clock = Guard.NotNull(clock, nameof(clock));
            _log = Guard.NotNull(log, nameof(log));
            _bootWaitMs = bootWaitMs;
            _refreshAfter = refreshAfter;
        }

        public override string Name => "LevelUpdate";
        public override bool Critical => false;

        // The step keeps its own budget; the pipeline's timeout would cancel the download instead of leaving it running.
        public override int TimeoutMs => 0;

        public override async UniTask RunAsync(CancellationToken ct)
        {
            var update = CheckAsync().Preserve();
            if (_bootWaitMs > 0) await UniTask.WhenAny(update, UniTask.Delay(_bootWaitMs, DelayType.Realtime, PlayerLoopTiming.Update, ct));
        }

        public void OnApplicationFocus(bool focused)
        {
            if (!focused || _running || _disposed || _refreshAfter <= TimeSpan.Zero || _clock.UtcNow - _lastCheck < _refreshAfter) return;
            CheckAsync().Forget();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async UniTask CheckAsync()
        {
            _running = true;
            _lastCheck = _clock.UtcNow;
            try
            {
                var result = await _updater.RunAsync(_lifetime.Token);
                _log.Info(LogTag.Content, "Level update {0}", result);
            }
            catch (OperationCanceledException)
            {
                // The scope closed mid-download; the next session starts over.
            }
            catch (Exception e)
            {
                _log.Error(LogTag.Content, "Level update failed", e);
            }
            finally
            {
                _running = false;
            }
        }
    }
}
