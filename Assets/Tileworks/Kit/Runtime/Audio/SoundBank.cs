using System.Collections.Generic;
using UnityEngine;

namespace Tileworks.Kit.Audio
{
    // A game's sound cues, filled by its scene builder.
    public abstract class SoundBank : ScriptableObject
    {
        [SerializeField] private SoundCue[] _cues = new SoundCue[0];

        public IReadOnlyList<SoundCue> Cues => _cues;

        public void Configure(SoundCue[] cues) => _cues = cues;
    }
}
