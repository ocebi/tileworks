using System;
using UnityEngine;

namespace Tileworks.Kit.Audio
{
    // One sound a game can play: its clip variants (used in turn) and how copies of it share the voices.
    [Serializable]
    public struct SoundCue
    {
        public const float DefaultJitter = 0.05f;
        public const int DefaultVoices = 4;
        public const float DefaultGap = 0.04f;

        [SerializeField] private string _name;
        [SerializeField, Min(0)] private int _id;
        [SerializeField] private AudioClip[] _clips;
        [SerializeField, Range(0f, 1f)] private float _volume;
        [SerializeField, Range(0f, 0.25f), Tooltip("Random pitch change either way, as a fraction. Steps skip it.")]
        private float _pitchJitter;
        [SerializeField, Range(1, 16), Tooltip("Most copies sounding at once; one more cuts the oldest.")]
        private int _maxVoices;
        [SerializeField, Range(0f, 0.5f), Tooltip("Seconds; a copy starting closer than this to another is dropped.")]
        private float _minGap;
        [SerializeField, Range(0, 10), Tooltip("When every voice is busy, a cue may take one from a lower priority.")]
        private int _priority;

        public SoundCue(string name, int id, AudioClip[] clips, float volume)
        {
            _name = name;
            _id = id;
            _clips = clips;
            _volume = volume;
            _pitchJitter = DefaultJitter;
            _maxVoices = DefaultVoices;
            _minGap = DefaultGap;
            _priority = 0;
        }

        public string Name => _name;
        public int Id => _id;
        public AudioClip[] Clips => _clips;
        public float Volume => _volume;
        public float PitchJitter => _pitchJitter;
        public int MaxVoices => _maxVoices;
        public float MinGap => _minGap;
        public int Priority => _priority;

        public SoundCue WithJitter(float pitchJitter)
        {
            var cue = this;
            cue._pitchJitter = pitchJitter;
            return cue;
        }

        public SoundCue WithVoices(int maxVoices)
        {
            var cue = this;
            cue._maxVoices = maxVoices;
            return cue;
        }

        public SoundCue WithGap(float seconds)
        {
            var cue = this;
            cue._minGap = seconds;
            return cue;
        }

        public SoundCue WithPriority(int priority)
        {
            var cue = this;
            cue._priority = priority;
            return cue;
        }
    }
}
