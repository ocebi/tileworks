using Tileworks.Kit.Audio;
using UnityEngine;

namespace Blast.View
{
    public enum BlastCue
    {
        Blast,
        Special,
        Rocket,
        Bomb,
        Disco,
        Box,
        Balloon,
        Duck,
        Refuse,
        Win,
        Lose,
        Hammer,
        Shuffle,
        BombFuse,
        BigBoom,
        DiscoCharge,
        DiscoZap,
        ComboGather,
        ComboBoom,
        Land,
        BoxBreak,
    }

    [CreateAssetMenu(menuName = "Tileworks/Blast/Sounds")]
    public sealed class BlastSounds : SoundBank
    {
    }
}
