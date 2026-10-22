using System;
using Tileworks.Kit.Core;
using Tileworks.Kit.Unity;
using UnityEngine;

namespace Tileworks.Kit.Audio
{
    // Plays a bank's cues on up to 24 pooled AudioSources. A cue can start later, to line up with a tween, and holds its
    // voice from the moment it is asked for; see VoiceTable for how voices are shared.
    public class SoundPlayer : MonoBehaviour
    {
        private const int MaxSources = 24;
        private const int MaxSteps = 12;
        private const float MinPitch = 0.1f;

        [SerializeField, ReadOnlyInInspector] private SoundBank _sounds;

        private VoiceTable _voices;
        private AudioSource[] _sources;
        private int[] _slots;
        private int[] _turns;
        private Pcg32 _random = new Pcg32(1UL);

        public void Play(int cue, float delay = 0f, float pitch = 1f)
        {
            var slot = Slot(cue);
            if (slot < 0) return;
            var jitter = _sounds.Cues[slot].PitchJitter * (float)(_random.NextDouble() * 2.0 - 1.0);
            Schedule(slot, delay, pitch * (1f + jitter));
        }

        // A semitone higher per step, up to an octave: cascades, goal counts.
        public void PlayStep(int cue, int step, float delay = 0f)
        {
            var slot = Slot(cue);
            if (slot < 0) return;
            Schedule(slot, delay, Mathf.Pow(2f, Mathf.Clamp(step, -MaxSteps, MaxSteps) / (float)MaxSteps));
        }

        private void Schedule(int slot, float delay, float pitch)
        {
            var cue = _sounds.Cues[slot];
            var clips = cue.Clips;
            if (clips == null || clips.Length == 0) return;
            var clip = clips[_turns[slot] % clips.Length];
            if (clip == null) return;
            pitch = Mathf.Max(MinPitch, pitch);
            delay = Mathf.Max(0f, delay);
            var now = Time.realtimeSinceStartupAsDouble;
            var request = new VoiceRequest(slot, cue.Priority, cue.MaxVoices, cue.MinGap, now + delay, clip.length / pitch);
            if (!_voices.TryAssign(request, now, out var plan)) return;
            _turns[slot]++;
            if (plan.Cut >= 0) _sources[plan.Cut].SetScheduledEndTime(AudioSettings.dspTime + (plan.CutAt - now));
            if (plan.IsNew) _sources[plan.Voice] = CreateSource();
            var source = _sources[plan.Voice];
            source.clip = clip;
            source.volume = cue.Volume;
            source.pitch = pitch;
            if (delay > 0f) source.PlayDelayed(delay);
            else source.Play();
        }

        private int Slot(int cue)
        {
            if (_slots == null) Warm();
            return cue >= 0 && cue < _slots.Length ? _slots[cue] : -1;
        }

        private void Warm()
        {
            var cues = _sounds != null ? _sounds.Cues : Array.Empty<SoundCue>();
            var size = 0;
            for (var i = 0; i < cues.Count; i++) size = Math.Max(size, cues[i].Id + 1);
            _slots = new int[size];
            for (var i = 0; i < size; i++) _slots[i] = -1;
            for (var i = 0; i < cues.Count; i++)
            {
                if (_slots[cues[i].Id] < 0) _slots[cues[i].Id] = i;
            }

            _turns = new int[cues.Count];
            _voices = new VoiceTable(MaxSources);
            _sources = new AudioSource[MaxSources];
        }

        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }
    }
}
