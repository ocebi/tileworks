using System;

namespace Tileworks.Kit.Core
{
    public interface IClock
    {
        DateTime UtcNow { get; } // trusted estimate; DateTime.Now is banned

        bool IsTrusted { get; }

        DateTime DeviceUtcNow { get; }
    }

    public sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow) => UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        public DateTime UtcNow { get; private set; }
        public bool IsTrusted { get; set; } = true;
        public DateTime DeviceUtcNow => UtcNow;

        public void Advance(TimeSpan delta) => UtcNow += delta;
        public void Set(DateTime utcNow) => UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }
}
