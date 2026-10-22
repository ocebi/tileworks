using System;

namespace Tileworks.Kit.Audio
{
    // Levels (0..1) of two loop channels that crossfade, and a duck applied over both. No engine calls, so it can be tested.
    public sealed class MusicMix
    {
        private readonly float[] _level = new float[2];
        private readonly float[] _target = new float[2];
        private readonly float[] _rate = new float[2];
        private float _duck = 1f;
        private float _duckTarget = 1f;
        private float _duckLeft;
        private float _duckRate;

        public MusicTrack Track { get; private set; }
        public int Live { get; private set; }
        public float Duck => _duck;

        public float LevelOf(int channel) => _level[channel];

        // Silent and staying so: the caller can stop its source.
        public bool Faded(int channel) => _level[channel] <= 0f && _target[channel] <= 0f;

        // True when the caller should start `track` on the Live channel.
        public bool PlayLoop(MusicTrack track, float fade)
        {
            if (track == Track) return false;
            FadeTo(Live, 0f, fade);
            Live = 1 - Live;
            Track = track;
            _level[Live] = 0f;
            FadeTo(Live, 1f, fade);
            return true;
        }

        public void StopLoop(float fade)
        {
            FadeTo(Live, 0f, fade);
            Track = MusicTrack.None;
        }

        public void StartDuck(float volume, float seconds, float fade)
        {
            _duckTarget = Math.Clamp(volume, 0f, 1f);
            _duckLeft = seconds;
            _duckRate = Rate(fade);
        }

        public void Tick(float dt)
        {
            for (var c = 0; c < 2; c++) _level[c] = MoveTowards(_level[c], _target[c], _rate[c] * dt);
            if (_duckLeft > 0f)
            {
                _duckLeft -= dt;
                if (_duckLeft <= 0f) _duckTarget = 1f;
            }

            _duck = MoveTowards(_duck, _duckTarget, _duckRate * dt);
        }

        private void FadeTo(int channel, float level, float fade)
        {
            _target[channel] = level;
            _rate[channel] = Rate(fade);
        }

        // Per second; a fade of zero jumps on the next tick.
        private static float Rate(float fade) => fade > 0f ? 1f / fade : float.MaxValue;

        private static float MoveTowards(float value, float target, float step) =>
            value < target ? Math.Min(value + step, target) : Math.Max(value - step, target);
    }
}
