using System;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Model
{
    // A running hash over emitted events; golden replays compare it to catch presentation-visible changes.
    public static class EventDigest
    {
        public static ulong Combine(ulong digest, ReadOnlySpan<BoardEvent> events)
        {
            foreach (var e in events)
            {
                digest = StableHash.Combine(digest, ((ulong)e.Type << 48) | ((ulong)e.Step << 32) | (uint)e.Entity);
                digest = StableHash.Combine(digest, ((ulong)(uint)e.A << 32) | (uint)e.B);
                digest = StableHash.Combine(digest, (uint)e.C);
            }

            return digest;
        }
    }
}
