using System;
using UnityEngine;

namespace Tileworks.Kit.UI
{
    public enum UiSound
    {
        PopupOpen,
        PopupClose,
        CountTick,
        StarPop,
        Confetti,
        Callout,
        GoalArrive,
        GoalDone,
        MovesLow,
        Badge,
    }

    [Serializable]
    public struct UiSoundClip
    {
        public UiSound Sound;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume;
    }
}
