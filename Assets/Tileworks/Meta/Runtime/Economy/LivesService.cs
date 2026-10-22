using System;
using Tileworks.Kit.Core;
using Tileworks.Meta.Api;
using Tileworks.Meta.Config;

namespace Tileworks.Meta.Economy
{
    [ConfigSection("lives")]
    public sealed class LivesConfig : IValidatable
    {
        public int Max = 5;
        public int RegenMinutes = 30;

        public void Validate(ValidationReport report)
        {
            report.Require(Max > 0, "max must be positive");
            report.Require(RegenMinutes > 0, "regenMinutes must be positive");
        }
    }

    // Lives are computed from (stored, last regen time), never ticked: time with the app closed still counts (§11.5).
    public sealed class LivesService : ILives
    {
        private const int RowId = 1;

        private readonly LivesDao _dao;
        private readonly IClock _clock;
        private readonly LivesConfig _config;
        private readonly Observable<int> _count = new Observable<int>();
        private LivesEntity _row;

        public LivesService(LivesDao dao, IClock clock, IConfig config)
        {
            _dao = Guard.NotNull(dao, nameof(dao));
            _clock = Guard.NotNull(clock, nameof(clock));
            _config = Guard.NotNull(config, nameof(config)).Get<LivesConfig>();
        }

        public IReadOnlyObservable<int> Count => _count;
        public int Max => _config.Max;
        public bool IsInfinite => _clock.UtcNow.Ticks < Row.InfiniteUntilTicks;
        public bool HasLife => IsInfinite || Current() > 0;

        public TimeSpan UntilNext
        {
            get
            {
                if (Current() >= Max) return TimeSpan.Zero;
                var interval = Interval.Ticks;
                var elapsed = Math.Max(0, _clock.UtcNow.Ticks - Row.LastRegenAtTicks);
                return TimeSpan.FromTicks(interval - elapsed % interval);
            }
        }

        public void Refresh() => _count.Value = Current();

        public bool TryConsume()
        {
            if (IsInfinite) return true;
            var current = Current();
            if (current <= 0) return false;
            var now = _clock.UtcNow.Ticks;
            var regenerated = current - Row.Stored;
            var lastRegen = current >= Max ? now : Row.LastRegenAtTicks + regenerated * Interval.Ticks;
            Save(current - 1, lastRegen, Row.InfiniteUntilTicks);
            return true;
        }

        public void Refill() => Save(Max, _clock.UtcNow.Ticks, Row.InfiniteUntilTicks);

        public void GrantInfinite(TimeSpan duration)
        {
            var from = Math.Max(_clock.UtcNow.Ticks, Row.InfiniteUntilTicks);
            Save(Current(), Row.LastRegenAtTicks, from + duration.Ticks);
        }

        private TimeSpan Interval => TimeSpan.FromMinutes(_config.RegenMinutes);

        private LivesEntity Row => _row ??= _dao.Find(RowId) ?? new LivesEntity { Id = RowId, Stored = Max, LastRegenAtTicks = _clock.UtcNow.Ticks };

        private int Current()
        {
            var row = Row;
            if (row.Stored >= Max) return row.Stored;
            var elapsed = Math.Max(0, _clock.UtcNow.Ticks - row.LastRegenAtTicks);
            return (int)Math.Min(Max, row.Stored + elapsed / Interval.Ticks);
        }

        private void Save(int stored, long lastRegen, long infiniteUntil)
        {
            _row = new LivesEntity { Id = RowId, Stored = stored, LastRegenAtTicks = lastRegen, InfiniteUntilTicks = infiniteUntil };
            _dao.Upsert(_row);
            _count.Value = Current();
        }
    }
}
