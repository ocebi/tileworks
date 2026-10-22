using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using UnityEngine;

namespace Tileworks.Kit.Time
{
    // Time since device boot; survives app restarts and resets only on reboot. None on platforms without one.
    public interface IMonotonicSource
    {
        bool TryGetSinceBoot(out TimeSpan elapsed);
    }

    public interface IDeviceTime
    {
        DateTime UtcNow { get; }
    }

    // Seam for an authoritative time (a server); v1 has none.
    public interface ITimeSource
    {
        bool TryGetAuthoritativeUtc(out DateTime utc);
    }

    public sealed class SystemDeviceTime : IDeviceTime
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public sealed class PlatformMonotonicSource : IMonotonicSource
    {
        public bool TryGetSinceBoot(out TimeSpan elapsed)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var clock = new AndroidJavaClass("android.os.SystemClock"))
            {
                elapsed = TimeSpan.FromMilliseconds(clock.CallStatic<long>("elapsedRealtime"));
                return true;
            }
#else
            elapsed = default;
            return false;
#endif
        }
    }

    // Offline guard (§11.5): if the device clock moved more than monotonic time since the last session, trust
    // last-trusted + monotonic elapsed instead. After a reboot the device clock is accepted.
    public sealed class TrustedClock : IClock, IAppLifecycleListener
    {
        public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(2);
        private const string TrustedKey = "clock.trusted";
        private const string MonotonicKey = "clock.monotonic";

        private readonly IKeyValueStore _store;
        private readonly IMonotonicSource _monotonic;
        private readonly IDeviceTime _device;
        private readonly IReadOnlyList<ITimeSource> _authorities;
        private readonly ILog _log;
        private TimeSpan _offset;
        private bool _initialized;

        public TrustedClock(IKeyValueStore store, IMonotonicSource monotonic, IDeviceTime device, IReadOnlyList<ITimeSource> authorities, ILog log)
        {
            _store = store;
            _monotonic = monotonic;
            _device = device;
            _authorities = authorities;
            _log = log;
        }

        public DateTime UtcNow => _device.UtcNow + _offset + DebugOffset;
        public DateTime DeviceUtcNow => _device.UtcNow;
        public bool IsTrusted { get; private set; } = true;

        // Dev builds only move it (debug time travel).
        public TimeSpan DebugOffset { get; set; }

        public void Initialize()
        {
            var device = _device.UtcNow;
            foreach (var authority in _authorities)
            {
                if (!authority.TryGetAuthoritativeUtc(out var utc)) continue;
                _offset = utc - device;
                IsTrusted = true;
                _initialized = true;
                Persist();
                return;
            }

            var lastTrusted = new DateTime(_store.GetLong(TrustedKey), DateTimeKind.Utc);
            var lastMonotonic = TimeSpan.FromTicks(_store.GetLong(MonotonicKey));
            _offset = TimeSpan.Zero;
            IsTrusted = true;

            if (lastTrusted.Ticks > 0 && _monotonic.TryGetSinceBoot(out var monotonic) && monotonic >= lastMonotonic)
            {
                var elapsed = monotonic - lastMonotonic;
                var expected = lastTrusted + elapsed;
                if (device - expected > Tolerance || device < lastTrusted)
                {
                    _offset = expected - device;
                    IsTrusted = false;
                    _log.Warn(LogTag.Time, "Device clock moved by {0} beyond elapsed time; using trusted time", device - expected);
                }
            }

            _initialized = true;
            Persist();
        }

        public void OnApplicationPause(bool paused)
        {
            if (!_initialized) return;
            if (paused) Persist();
            else Initialize();
        }

        public void OnApplicationQuit()
        {
            if (_initialized) Persist();
        }

        private void Persist()
        {
            _store.SetLong(TrustedKey, UtcNow.Ticks - DebugOffset.Ticks);
            if (_monotonic.TryGetSinceBoot(out var monotonic)) _store.SetLong(MonotonicKey, monotonic.Ticks);
        }
    }

    public sealed class TimeGuardStep : BootStep
    {
        private static readonly string[] Dependencies = { SaveSteps.Warm };
        private readonly TrustedClock _clock;

        public TimeGuardStep(TrustedClock clock) => _clock = clock;

        public override string Name => "TimeGuard";
        public override bool Critical => false;
        public override IReadOnlyList<string> DependsOn => Dependencies;

        public override UniTask RunAsync(CancellationToken ct)
        {
            _clock.Initialize();
            return UniTask.CompletedTask;
        }
    }
}
