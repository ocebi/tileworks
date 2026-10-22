using System.Collections.Generic;
using Tileworks.Kit.Core;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    // The little loops a special plays while it waits on the board: a rocket wiggles now and then, a bomb's fuse sparks and
    // a disco ball twinkles. A game's idle component calls Begin each frame, then one method per special resting on its cell.
    public sealed class SpecialIdler
    {
        private const float RestTolerance = 0.02f;

        private readonly List<Transform> _tilted = new List<Transform>();
        private BoardFx _fx;
        private float _now;
        private bool _fuse;
        private bool _twinkle;
        private float _nextFuse;
        private float _nextTwinkle;
        private Pcg32 _random = new Pcg32(5UL);

        // False when nothing should idle this frame (no effects, or Reduce Motion).
        public bool Begin(BoardFx fx, float now)
        {
            foreach (var tilted in _tilted)
            {
                if (tilted != null) tilted.localRotation = Quaternion.identity;
            }

            _tilted.Clear();
            _fx = fx;
            _now = now;
            if (fx == null || fx.Config == null || fx.Reduced) return false;
            var config = fx.Config;
            _fuse = now >= _nextFuse;
            if (_fuse) _nextFuse = now + config.FuseSparkEvery;
            _twinkle = now >= _nextTwinkle;
            if (_twinkle) _nextTwinkle = now + config.TwinkleEvery;
            return true;
        }

        // On its cell at full size: not falling, swelling or being pulled into a combo.
        public static bool Resting(Transform piece, Vector3 cell) =>
            (piece.localPosition - cell).sqrMagnitude < RestTolerance * RestTolerance && Mathf.Abs(piece.localScale.x - 1f) < RestTolerance;

        public void Rocket(Transform rocket, int cell)
        {
            var config = _fx.Config;
            var t = (_now + cell * 0.37f) % config.WiggleEvery;
            if (t > config.WiggleFor) return;
            var angle = config.WiggleDegrees * Mathf.Sin(t * config.WiggleRate * Mathf.PI * 2f) * (1f - t / config.WiggleFor);
            rocket.localRotation = Quaternion.Euler(0f, 0f, angle);
            _tilted.Add(rocket);
        }

        public void Bomb(Vector3 at, Color colour)
        {
            if (_fuse) _fx.Glint(at + (Vector3)_fx.Config.FuseOffset, colour, false);
        }

        public void Disco(Vector3 at, Color colour)
        {
            if (!_twinkle) return;
            var angle = (float)_random.NextDouble() * Mathf.PI * 2f;
            var reach = _fx.Config.TwinkleReach * (float)_random.NextDouble();
            _fx.Glint(at + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * reach, colour, true);
        }
    }
}
