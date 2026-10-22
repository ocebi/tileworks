using System;

namespace Tileworks.Tiles.View
{
    // What the Home shows next to the lives count: no timer at all, a full tank, or the seconds to the next life.
    public readonly struct LivesTimer
    {
        public readonly bool Infinite;
        public readonly bool Full;
        public readonly int Seconds;

        public LivesTimer(bool infinite, bool full, int seconds)
        {
            Infinite = infinite;
            Full = full;
            Seconds = seconds;
        }

        public static LivesTimer Of(int count, int max, bool infinite, TimeSpan untilNext) =>
            new LivesTimer(infinite, count >= max, (int)untilNext.TotalSeconds);
    }
}
