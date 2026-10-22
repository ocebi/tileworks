using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    public interface ILandingSound
    {
        // A piece touches down at `at` seconds from now.
        void Land(float at);
    }

    public readonly struct DropInStyle
    {
        public DropInStyle(float rowStagger, float columnStagger, float startScale, FallMotion motion, float land, Vector2 squash)
        {
            RowStagger = rowStagger;
            ColumnStagger = columnStagger;
            StartScale = startScale;
            Motion = motion;
            Land = land;
            Squash = squash;
        }

        public float RowStagger { get; }
        public float ColumnStagger { get; }
        public float StartScale { get; }
        public float Land { get; }
        public Vector2 Squash { get; }
        public FallMotion Motion { get; }

        // Seconds to fall `rows` from rest.
        public float FallTime(float rows) => Motion.FallTime(rows);
    }

    // The level-start drop of a board game: pieces wait out of sight behind the level card, then come in at the board's
    // top edge, small and fading in, fall to their cells, the bottom row first, and land with a squash. Nothing is drawn
    // above the board, so the HUD stays clear.
    public static class BoardDropIn
    {
        private const float FadeIn = 0.08f;

        public static void Hide(IEnumerable<SpriteRenderer> pieces)
        {
            foreach (var piece in pieces) piece.enabled = false;
        }

        public static float Show(IEnumerable<SpriteRenderer> pieces)
        {
            foreach (var piece in pieces) piece.enabled = true;
            return 0f;
        }

        // `origin` is the top-left cell's position; `landed` hears each landing's delay. Returns the seconds until the last.
        public static float Play(IEnumerable<SpriteRenderer> pieces, Vector2 origin, int rows, in DropInStyle style, ILandingSound landing)
        {
            var squash = new Vector3(style.Squash.x, style.Squash.y, 1f);
            var top = origin.y + 0.5f;
            var last = 0f;
            foreach (var piece in pieces)
            {
                piece.enabled = true;
                var target = piece.transform.localPosition;
                var row = Mathf.RoundToInt(origin.y - target.y);
                var column = Mathf.RoundToInt(target.x - origin.x);
                var start = style.RowStagger * (rows - 1 - row) + style.ColumnStagger * column;
                var from = new Vector3(target.x, top, target.z);
                var fall = style.FallTime(top - target.y);
                piece.transform.localPosition = from;
                piece.transform.localScale = Vector3.one * style.StartScale;
                Tween.Alpha(piece, 0f, 1f, FadeIn, startDelay: start);
                Tween.Scale(piece.transform, style.StartScale, 1f, fall, Ease.OutQuad, startDelay: start);
                Tween.LocalPosition(piece.transform, from, target, fall, Ease.InQuad, startDelay: start)
                    .Chain(Tween.Scale(piece.transform, squash, style.Land * 0.35f, Ease.OutQuad))
                    .Chain(Tween.Scale(piece.transform, Vector3.one, style.Land * 0.65f, Ease.OutBack));
                landing?.Land(start + fall);
                last = Mathf.Max(last, start + fall + style.Land);
            }

            return last;
        }
    }
}
