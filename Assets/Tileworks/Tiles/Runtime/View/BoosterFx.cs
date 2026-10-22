using PrimeTween;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    // What the booster effects need from a board. Positions are board-local, one cell per unit.
    public interface IBoosterFxBoard
    {
        // What the impact punches and shakes, in its parent's units.
        Transform Root { get; }
        Sprite FlashSprite { get; }
        Sprite RingSprite { get; }
        Sprite SparkSprite { get; }
        SpriteRenderer Fx(Sprite sprite, Vector3 local, Vector2 scale, Color colour);
        void Release(SpriteRenderer fx);
        Vector3 LocalAt(Vector2 screen);

        // Root's parent units per screen pixel.
        float UnitsPerPixel { get; }
    }

    // The Hammer's and Shuffle's motion, the same on every board. Delays are from now and already on the clock's scale.
    public static class BoosterFx
    {
        private const int OverPieces = 20;

        // Flies the hammer in from the button, swings it onto `target` and sends it off; returns the delay until impact.
        public static float Hammer(IBoosterFxBoard board, BoosterFxConfig fx, PresentationClock clock, Vector2 button, Vector3 target, float delay)
        {
            var times = fx.HammerTimes;
            var angles = fx.HammerAngles;
            var lifts = fx.HammerLifts;
            var size = fx.HammerSize / fx.Hammer.bounds.size.x;
            var start = board.LocalAt(button) + (Vector3)(fx.FlyInOffset * (fx.HammerSize * fx.HammerScales.x));
            var rest = target + new Vector3(-fx.HammerReach, fx.FaceAboveCentre + fx.HammerFace, 0f);

            var hammer = board.Fx(fx.Hammer, start, Vector2.one * (size * fx.HammerScales.x), new Color(1f, 1f, 1f, 0f));
            hammer.sortingOrder += OverPieces;
            var t = hammer.transform;
            t.localRotation = Quaternion.Euler(0f, 0f, angles[0]);
            Tween.Alpha(hammer, 1f, 0.03f, startDelay: delay);
            Tween.Scale(t, size, clock.Scale(times[0]), Ease.OutQuad, startDelay: delay);

            var at = delay;
            var eases = new[] { Ease.OutQuad, Ease.OutSine, Ease.InQuad, Ease.OutQuad, Ease.OutQuad };
            var impact = 0f;
            for (var beat = 0; beat < times.Length; beat++)
            {
                var duration = clock.Scale(times[beat]);
                Tween.LocalPosition(t, rest + Vector3.up * lifts[beat], duration, eases[beat], startDelay: at);
                if (beat > 0) Tween.LocalRotation(t, Quaternion.Euler(0f, 0f, angles[beat]), duration, eases[beat], startDelay: at);
                at += duration;
                if (beat == 2) impact = at;
            }

            var leave = clock.Scale(times[times.Length - 1]);
            Tween.Scale(t, size * fx.HammerScales.y, leave, Ease.OutQuad, startDelay: at - leave);
            Tween.Alpha(hammer, 0f, leave, startDelay: at - leave).OnComplete(board, b => b.Release(hammer));
            Impact(board, fx, clock, target, impact);
            return impact - delay;
        }

        // The shuffle icon flies to the board's centre turning once, then swells and fades as a ring bursts; returns how long.
        public static float ShuffleLeadIn(IBoosterFxBoard board, BoosterFxConfig fx, PresentationClock clock, Vector2 button, Vector3 centre, float delay)
        {
            var fly = clock.Scale(fx.ShuffleFly);
            var fade = clock.Scale(fx.ShuffleFade);
            var unit = 1f / fx.Shuffle.bounds.size.x;
            var icon = board.Fx(fx.Shuffle, board.LocalAt(button), Vector2.one * (unit * fx.ShuffleScales.x), new Color(1f, 1f, 1f, 0f));
            icon.sortingOrder += OverPieces;
            var t = icon.transform;
            Tween.Alpha(icon, 1f, 0.03f, startDelay: delay);
            Tween.LocalPosition(t, centre, fly, Ease.OutQuad, startDelay: delay);
            Tween.LocalEulerAngles(t, Vector3.zero, new Vector3(0f, 0f, -360f), fly, Ease.OutQuad, startDelay: delay);
            Tween.Scale(t, unit * fx.ShuffleScales.y, fly, Ease.OutQuad, startDelay: delay);
            Tween.Scale(t, unit * fx.ShuffleSwell, fade, Ease.OutQuad, startDelay: delay + fly);
            Tween.Alpha(icon, 0f, fade, Ease.InQuad, startDelay: delay + fly).OnComplete(board, b => b.Release(icon));
            Burst(board, board.RingSprite, centre, 0.2f, fx.ShuffleRing, fade, delay + fly);
            return fly + fade;
        }

        private static void Impact(IBoosterFxBoard board, BoosterFxConfig fx, PresentationClock clock, Vector3 at, float delay)
        {
            var duration = clock.Scale(fx.ImpactDuration);
            Burst(board, board.FlashSprite, at, 0.4f, fx.FlashSize, duration * 0.7f, delay);
            Burst(board, board.RingSprite, at, 0.3f, fx.RingSize, duration, delay);
            for (var i = 0; i < fx.Sparks; i++)
            {
                var angle = (i + 0.5f) * Mathf.PI * 2f / fx.Sparks;
                var spark = board.Fx(board.SparkSprite, at, Vector2.one * Size(board.SparkSprite, 0.3f), Color.clear);
                Tween.Color(spark, Color.white, 0.01f, startDelay: delay);
                Tween.LocalPosition(spark.transform, at + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * fx.SparkReach, duration, Ease.OutQuad, startDelay: delay);
                Tween.Alpha(spark, 1f, 0f, duration, Ease.InQuad, startDelay: delay).OnComplete(board, b => b.Release(spark));
            }

            var root = board.Root;
            var (scale, x) = (root.localScale, root.localPosition.x);
            var step = clock.Scale(fx.BoardShakeStep);
            Tween.Scale(root, scale * fx.BoardPunch, step, Ease.OutQuad, startDelay: delay);
            Tween.Scale(root, scale, step * 3f, Ease.OutBack, startDelay: delay + step);
            var shake = fx.BoardShake;
            for (var i = 0; i < shake.Length; i++)
                Tween.LocalPositionX(root, x + shake[i] * board.UnitsPerPixel, step, Ease.OutSine, startDelay: delay + i * step);
            if (shake.Length > 0 && shake[shake.Length - 1] != 0f) Tween.LocalPositionX(root, x, step, startDelay: delay + shake.Length * step);
        }

        private static void Burst(IBoosterFxBoard board, Sprite sprite, Vector3 at, float from, float to, float duration, float delay)
        {
            var fx = board.Fx(sprite, at, Vector2.one * Size(sprite, from), Color.clear);
            Tween.Color(fx, Color.white, 0.01f, startDelay: delay);
            Tween.Scale(fx.transform, Size(sprite, to), duration, Ease.OutQuad, startDelay: delay);
            Tween.Alpha(fx, 1f, 0f, duration, Ease.InQuad, startDelay: delay).OnComplete(board, b => b.Release(fx));
        }

        private static float Size(Sprite sprite, float cells) => cells / sprite.bounds.size.x;
    }
}
