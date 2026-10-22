using System;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Unity
{
    // Device time as-is; the trusted clock with a monotonic guard replaces it (§11.5).
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public bool IsTrusted => true;
        public DateTime DeviceUtcNow => DateTime.UtcNow;
    }
}
