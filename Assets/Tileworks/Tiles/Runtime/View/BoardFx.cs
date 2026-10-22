using PrimeTween;
using Tileworks.Kit.Core;
using Tileworks.Kit.Unity;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    // Board particles in four shared ParticleSystems, so a burst is a few Emit calls and costs no objects; plus the board
    // shake and zoom punch. Positions are board-local, in cells. Anything can start later, to line up with a tween.
    public sealed class BoardFx : MonoBehaviour
    {
        private const int MaxPending = 256;
        private const int MaxTrails = 16;
        private const int MaxParticles = 1000;

        private enum Kind : byte
        {
            Shatter,
            Smoke,
            Sparks,
            Stars,
            Explosion,
        }

        private struct Pending
        {
            public double Time;
            public Kind Kind;
            public Vector3 At;
            public Color Colour;
            public float Size;
        }

        private struct Trail
        {
            public Transform Target;
            public double From;
            public double Until;
            public Vector3 Last;
            public float OwedSmoke;
            public float OwedSparks;
            public bool Started;
            public Color Colour;
        }

        [SerializeField, ReadOnlyInInspector] private BoardFxConfig _config;
        [SerializeField, ReadOnlyInInspector, Tooltip("Moved by the shake and scaled by the punch.")] private Transform _board;

        private readonly Pending[] _pending = new Pending[MaxPending];
        private readonly Trail[] _trails = new Trail[MaxTrails];
        private int _pendingCount;
        private ParticleSystem _shards;
        private ParticleSystem _smoke;
        private ParticleSystem _sparks;
        private ParticleSystem _stars;
        private Tween _shake;
        private Tween _punch;
        private Pcg32 _random = new Pcg32(7UL);
        private PresentationClock _clock;

        // Reduce Motion: fewer particles, no trails, no shake or punch.
        public bool Reduced => _clock != null && _clock.ReduceMotion;

        public BoardFxConfig Config => _config;

        // Times follow the presentation clock, like the tweens they line up with.
        public void Bind(PresentationClock clock) => _clock = clock;

        private double Now => _clock != null ? _clock.Now : Time.unscaledTimeAsDouble;

        public void Shatter(Vector3 at, Color colour, float delay = 0f) => Queue(Kind.Shatter, at, colour, 1f, delay);

        public void Smoke(Vector3 at, float size, float delay = 0f) => Queue(Kind.Smoke, at, Color.white, size, delay);

        public void Sparks(Vector3 at, Color colour, float delay = 0f) => Queue(Kind.Sparks, at, colour, 1f, delay);

        public void Stars(Vector3 at, Color colour, float delay = 0f) => Queue(Kind.Stars, at, colour, 1f, delay);

        // A single slow spark (a fuse) or star (a twinkle), right away.
        public void Glint(Vector3 at, Color colour, bool star)
        {
            if (_config == null) return;
            EnsureSystems();
            if (star) Emit(_stars, _config.Star, at, colour, 1, 0.2f, 0.6f);
            else Emit(_sparks, _config.Spark, at, colour, 1, 0.15f, 1f, 0.8f);
        }

        // Debris and smoke sized to a blast `radius` in cells, with a board shake to match.
        public void Explode(Vector3 at, float radius, Color colour, float delay = 0f)
        {
            Queue(Kind.Explosion, at, colour, radius, delay);
            Shake(radius, delay);
        }

        // Smoke and sparks behind a moving piece, such as a rocket half, from `delay` for `seconds`.
        public void Follow(Transform target, Color colour, float delay, float seconds)
        {
            if (Reduced || target == null) return;
            for (var i = 0; i < _trails.Length; i++)
            {
                if (_trails[i].Target != null) continue;
                var now = Now;
                _trails[i] = new Trail { Target = target, From = now + delay, Until = now + delay + seconds, Colour = colour };
                return;
            }
        }

        public void Shake(float radius, float delay = 0f)
        {
            if (Reduced || _board == null || _config == null) return;
            if (_shake.isAlive) _shake.Complete();
            var strength = _config.ShakePerCell * radius;
            _shake = Tween.ShakeLocalPosition(_board, new Vector3(strength, strength, 0f), _config.ShakeDuration, _config.ShakeFrequency,
                startDelay: delay, useUnscaledTime: true);
        }

        public void Punch(float delay = 0f)
        {
            if (Reduced || _board == null || _config == null) return;
            if (_punch.isAlive) _punch.Complete();
            var strength = _config.PunchScale;
            _punch = Tween.PunchScale(_board, new Vector3(strength, strength, 0f), _config.PunchDuration, _config.PunchFrequency, startDelay: delay, useUnscaledTime: true);
        }

        private void Queue(Kind kind, Vector3 at, Color colour, float size, float delay)
        {
            if (_config == null || _pendingCount == _pending.Length) return;
            _pending[_pendingCount++] = new Pending
            {
                Time = Now + delay, Kind = kind, At = at, Colour = colour, Size = size,
            };
        }

        private void LateUpdate()
        {
            if (_config == null) return;
            var now = Now;
            for (var i = 0; i < _pendingCount;)
            {
                if (_pending[i].Time > now)
                {
                    i++;
                    continue;
                }

                Fire(in _pending[i]);
                _pending[i] = _pending[--_pendingCount];
            }

            for (var i = 0; i < _trails.Length; i++)
            {
                if (_trails[i].Target != null) Advance(ref _trails[i], now);
            }
        }

        private void Fire(in Pending p)
        {
            EnsureSystems();
            switch (p.Kind)
            {
                case Kind.Shatter:
                    Emit(_shards, _config.Shard, p.At, p.Colour, Amount(Range(_config.ShardsPerPiece)), 1f, 1f);
                    break;
                case Kind.Smoke:
                    Emit(_smoke, _config.Smoke, p.At, p.Colour, Amount(Range(_config.SmokePerPuff)), p.Size, p.Size);
                    break;
                case Kind.Sparks:
                    Emit(_sparks, _config.Spark, p.At, p.Colour, Amount(Range(_config.SparksPerBurst)), 1f, 1f);
                    break;
                case Kind.Stars:
                    Emit(_stars, _config.Star, p.At, p.Colour, Amount(Range(_config.SparksPerBurst) / 2), 1f, 1f);
                    break;
                case Kind.Explosion:
                    Emit(_shards, _config.Shard, p.At, p.Colour, Amount(Mathf.RoundToInt(_config.DebrisPerCell * p.Size)), 1.5f, 1.2f);
                    Emit(_smoke, _config.Smoke, p.At, Color.white, Amount(Range(_config.SmokePerPuff) * Mathf.CeilToInt(p.Size)), p.Size * 0.6f,
                        1f + p.Size * 0.25f);
                    Emit(_sparks, _config.Spark, p.At, p.Colour, Amount(Range(_config.SparksPerBurst)), 1.2f, 1f);
                    break;
            }
        }

        // Emits by distance travelled, spread along the path, so a fast rocket leaves an even trail.
        private void Advance(ref Trail trail, double now)
        {
            if (now < trail.From) return;
            var target = trail.Target;
            if (now > trail.Until || !target.gameObject.activeInHierarchy)
            {
                trail.Target = null;
                return;
            }

            var position = transform.InverseTransformPoint(target.position);
            if (!trail.Started)
            {
                trail.Started = true;
                trail.Last = position;
                return;
            }

            EnsureSystems();
            var travelled = Vector3.Distance(position, trail.Last);
            trail.OwedSmoke += travelled * _config.TrailPerCell.x;
            trail.OwedSparks += travelled * _config.TrailPerCell.y;
            for (; trail.OwedSmoke >= 1f; trail.OwedSmoke -= 1f)
                Emit(_smoke, _config.Smoke, Vector3.Lerp(trail.Last, position, Next()), Color.white, 1, 0.25f, 0.75f, 0.5f);
            for (; trail.OwedSparks >= 1f; trail.OwedSparks -= 1f)
                Emit(_sparks, _config.Spark, Vector3.Lerp(trail.Last, position, Next()), trail.Colour, 1, 0.4f, 1f);
            trail.Last = position;
        }

        private void Emit(ParticleSystem system, in ParticleLook look, Vector3 at, Color colour, int count, float speedScale, float sizeScale,
            float lifeScale = 1f)
        {
            var p = new ParticleSystem.EmitParams();
            for (var i = 0; i < count; i++)
            {
                var angle = Next() * Mathf.PI * 2f;
                var speed = Range(look.Speed) * speedScale;
                p.position = at;
                p.velocity = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + look.Lift, 0f);
                p.startSize = Range(look.Size) * sizeScale;
                p.startLifetime = Range(look.Life) * lifeScale;
                p.startColor = colour;
                p.rotation = Next() * 360f;
                p.angularVelocity = (Next() * 2f - 1f) * look.Spin;
                system.Emit(p, 1);
            }
        }

        private int Amount(int count) => Reduced ? Mathf.Max(1, Mathf.RoundToInt(count * _config.ReducedShare)) : count;

        private float Next() => (float)_random.NextDouble();

        private float Range(Vector2 range) => Mathf.Lerp(range.x, range.y, Next());

        private int Range(Vector2Int range) => range.x + _random.NextInt(Mathf.Max(1, range.y - range.x + 1));

        private void EnsureSystems()
        {
            if (_shards != null) return;
            _shards = Create("Shards", _config.Shard);
            _smoke = Create("Smoke", _config.Smoke);
            _sparks = Create("Sparks", _config.Spark);
            _stars = Create("Stars", _config.Star);
        }

        private ParticleSystem Create(string name, in ParticleLook look)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = MaxParticles;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var force = system.forceOverLifetime;
            force.enabled = look.Gravity != 0f;
            force.space = ParticleSystemSimulationSpace.Local;
            force.x = new ParticleSystem.MinMaxCurve(0f);
            force.y = new ParticleSystem.MinMaxCurve(-look.Gravity);
            force.z = new ParticleSystem.MinMaxCurve(0f);

            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f - look.Fade), new GradientAlphaKey(0f, 1f) });
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(fade);

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, look.EndSize));

            if (look.Sprite != null)
            {
                var sheet = system.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Sprites;
                sheet.AddSprite(look.Sprite);
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _config.Material;
            renderer.sortingOrder = look.Order;
            system.Play();
            return system;
        }
    }
}
