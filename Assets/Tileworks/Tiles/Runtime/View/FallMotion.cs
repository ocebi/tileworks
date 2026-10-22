using UnityEngine;

namespace Tileworks.Tiles.View
{
    // How falling pieces move, in cells per second: a straight fall speeds up from its entry speed to a top speed; a
    // one-cell diagonal slide runs at an even, readable pace; a landing squashes harder the faster it hits.
    public readonly struct FallMotion
    {
        public const float SlideLength = 1.41421356f;

        public FallMotion(float startSpeed, float acceleration, float maxSpeed, float slideMinSpeed, float slideMaxSpeed, float squashFloor)
        {
            StartSpeed = Mathf.Max(0.01f, startSpeed);
            Acceleration = Mathf.Max(0f, acceleration);
            MaxSpeed = Mathf.Max(StartSpeed, maxSpeed);
            SlideMinSpeed = Mathf.Max(0.01f, slideMinSpeed);
            SlideMaxSpeed = Mathf.Max(SlideMinSpeed, slideMaxSpeed);
            SquashFloor = Mathf.Clamp01(squashFloor);
        }

        public float StartSpeed { get; }
        public float Acceleration { get; }
        public float MaxSpeed { get; }
        public float SlideMinSpeed { get; }
        public float SlideMaxSpeed { get; }

        // The share of the full squash a landing at rest gets.
        public float SquashFloor { get; }

        // A piece starting from rest moves at StartSpeed.
        public float Entry(float speed) => Mathf.Max(speed, StartSpeed);

        public float SlideSpeed(float entry) => Mathf.Clamp(entry, SlideMinSpeed, SlideMaxSpeed);

        // Seconds to fall `rows` straight down from `entry` speed, and the speed it arrives with.
        public float FallTime(float rows, float entry, out float exit)
        {
            var v = Entry(entry);
            if (rows <= 0f)
            {
                exit = v;
                return 0f;
            }

            if (Acceleration <= 0f || v >= MaxSpeed)
            {
                exit = Mathf.Min(v, MaxSpeed);
                return rows / exit;
            }

            var toTop = (MaxSpeed - v) / Acceleration;
            var accelerating = v * toTop + 0.5f * Acceleration * toTop * toTop;
            if (rows <= accelerating)
            {
                var t = (Mathf.Sqrt(v * v + 2f * Acceleration * rows) - v) / Acceleration;
                exit = v + Acceleration * t;
                return t;
            }

            exit = MaxSpeed;
            return toTop + (rows - accelerating) / MaxSpeed;
        }

        public float FallTime(float rows) => FallTime(rows, StartSpeed, out _);

        public float SlideTime(float entry) => SlideLength / SlideSpeed(entry);

        // How far a straight fall goes in `seconds` from `speed`, and the speed it ends at.
        public float Advance(float speed, float seconds, out float end)
        {
            var v = Entry(speed);
            if (Acceleration <= 0f || v >= MaxSpeed)
            {
                end = Mathf.Min(v, MaxSpeed);
                return end * seconds;
            }

            var toTop = (MaxSpeed - v) / Acceleration;
            if (seconds <= toTop)
            {
                end = v + Acceleration * seconds;
                return v * seconds + 0.5f * Acceleration * seconds * seconds;
            }

            end = MaxSpeed;
            return v * toTop + 0.5f * Acceleration * toTop * toTop + MaxSpeed * (seconds - toTop);
        }

        // The landing's squash: `full` at top speed, SquashFloor of the way there from rest.
        public Vector2 Squash(Vector2 full, float impact) =>
            Vector2.LerpUnclamped(Vector2.one, full, Mathf.Lerp(SquashFloor, 1f, Mathf.Clamp01(impact / MaxSpeed)));
    }
}
