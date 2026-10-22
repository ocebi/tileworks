using Tileworks.Kit.Core;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    // Presentation time, separate from rules time; Speed scales every scheduled animation.
    public sealed class PresentationClock : ITickable
    {
        public float Now { get; private set; }
        public float Speed { get; set; } = 1f;
        public bool ReduceMotion { get; set; }

        public float Scale(float seconds) => ReduceMotion ? seconds * 0.5f / Speed : seconds / Speed;

        public void Tick() => Advance(Time.unscaledDeltaTime);

        public void Advance(float seconds) => Now += seconds;
    }
}
