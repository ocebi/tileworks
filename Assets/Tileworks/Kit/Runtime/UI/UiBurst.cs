using Tileworks.Kit.Core;
using Tileworks.Kit.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworks.Kit.UI
{
    // Screen-space particles made of pooled Images: sparkles from a point, or confetti from both bottom corners. Moves its own
    // bits each frame on unscaled time, so a burst never allocates after the pool has grown and keeps playing during pauses.
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiBurst : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private UiBurstConfig _config;
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private Sprite[] _sprites = new Sprite[0];

        private struct Bit
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Angle;
            public float Spin;
            public float Size;
            public float Phase;
            public Color Colour;
        }

        private Image[] _images = new Image[0];
        private Bit[] _bits = new Bit[0];
        private int _alive;
        private Pcg32 _random = new Pcg32(0x5EEDUL);
        private float _lastTrail = -1f;

        // Sprays from a world position, such as a UI element's.
        public void Burst(Vector3 world) => Emit(((RectTransform)transform).InverseTransformPoint(world), 90f, _config.Spread, Amount());

        // For something moving: sprays from where it is now, at most once per the config's trail interval.
        public void Trail(Vector3 world)
        {
            var now = Time.unscaledTime;
            if (now - _lastTrail < _config.TrailEvery) return;
            _lastTrail = now;
            Burst(world);
        }

        // Two cannons in the bottom corners, aimed up and inwards.
        public void Cannons()
        {
            var rect = ((RectTransform)transform).rect;
            var half = Amount() / 2;
            Emit(new Vector2(rect.xMin, rect.yMin), 62f, _config.Spread, half);
            Emit(new Vector2(rect.xMax, rect.yMin), 118f, _config.Spread, half);
        }

        public void Clear()
        {
            for (var i = 0; i < _alive; i++) _images[i].enabled = false;
            _alive = 0;
        }

        private int Amount()
        {
            var reduced = _uiMotion != null && _uiMotion.Reduced;
            return Mathf.Max(1, Mathf.RoundToInt(_config.Count * (reduced ? _config.ReducedShare : 1f)));
        }

        private void Emit(Vector2 at, float direction, float spread, int count)
        {
            Grow(_alive + count);
            for (var n = 0; n < count; n++)
            {
                var angle = (direction + Between(-0.5f, 0.5f) * spread) * Mathf.Deg2Rad;
                var speed = Between(_config.Speed.x, _config.Speed.y);
                var jitterAngle = Between(0f, Mathf.PI * 2f);
                var jitter = new Vector2(Mathf.Cos(jitterAngle), Mathf.Sin(jitterAngle)) * (Mathf.Sqrt(Between(0f, 1f)) * _config.Jitter);
                var palette = _config.Palette;
                _bits[_alive] = new Bit
                {
                    Position = at + jitter,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    Life = Between(_config.Life.x, _config.Life.y),
                    Angle = Between(0f, 360f),
                    Spin = Between(_config.Spin.x, _config.Spin.y) * (_random.Chance(1, 2) ? -1f : 1f),
                    Size = Between(_config.Size.x, _config.Size.y),
                    Phase = Between(0f, Mathf.PI * 2f),
                    Colour = palette.Length > 0 ? palette[_random.NextInt(palette.Length)] : Color.white,
                };
                var image = _images[_alive];
                image.sprite = _sprites.Length > 0 ? _sprites[_random.NextInt(_sprites.Length)] : null;
                image.enabled = true;
                Place(image, _bits[_alive]);
                _alive++;
            }
        }

        private float Between(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private void Update()
        {
            if (_alive == 0) return;
            var dt = Time.unscaledDeltaTime;
            var damping = Mathf.Max(0f, 1f - _config.Drag * dt);
            for (var i = _alive - 1; i >= 0; i--)
            {
                ref var bit = ref _bits[i];
                bit.Age += dt;
                if (bit.Age >= bit.Life)
                {
                    Retire(i);
                    continue;
                }

                bit.Velocity.y += _config.Gravity * dt;
                bit.Velocity *= damping;
                bit.Position += bit.Velocity * dt;
                bit.Position.x += Mathf.Sin(bit.Age * 7f + bit.Phase) * _config.Sway * dt;
                bit.Angle += bit.Spin * dt;
                Place(_images[i], bit);
            }
        }

        private void Place(Image image, in Bit bit)
        {
            var t = bit.Age / bit.Life;
            var fade = t <= _config.FadeFrom ? 1f : 1f - (t - _config.FadeFrom) / Mathf.Max(0.001f, 1f - _config.FadeFrom);
            var grow = _config.GrowIn <= 0f ? 1f : Mathf.Clamp01(bit.Age / _config.GrowIn);
            var rect = image.rectTransform;
            rect.anchoredPosition = bit.Position;
            rect.localRotation = Quaternion.Euler(0f, 0f, bit.Angle);
            rect.localScale = Vector3.one * grow;
            rect.sizeDelta = new Vector2(bit.Size, bit.Size);
            var colour = bit.Colour;
            colour.a *= fade;
            image.color = colour;
        }

        // Swaps the last live bit into the free slot, so live bits stay packed at the front.
        private void Retire(int index)
        {
            var last = _alive - 1;
            _images[index].enabled = false;
            if (index != last)
            {
                _bits[index] = _bits[last];
                (_images[index], _images[last]) = (_images[last], _images[index]);
            }

            _alive = last;
        }

        private void Grow(int needed)
        {
            if (needed <= _images.Length) return;
            var size = Mathf.Max(needed, _images.Length * 2);
            var images = new Image[size];
            var bits = new Bit[size];
            System.Array.Copy(_images, images, _images.Length);
            System.Array.Copy(_bits, bits, _bits.Length);
            for (var i = _images.Length; i < size; i++)
            {
                var image = new GameObject("Bit", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.enabled = false;
                image.rectTransform.SetParent(transform, false);
                images[i] = image;
            }

            (_images, _bits) = (images, bits);
        }
    }
}
