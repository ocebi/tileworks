using Tileworks.Kit.Audio;
using UnityEngine;

namespace Match3.View
{
    public enum Match3Cue
    {
        Match,
        Swap,
        Special,
        Rocket,
        Bomb,
        Disco,
        Flyer,
        Box,
        Floor,
        Chain,
        Balloon,
        Duck,
        Refuse,
        Shuffle,
        Win,
        Lose,
        Hammer,
        BombFuse,
        BigBoom,
        DiscoCharge,
        DiscoZap,
        ComboGather,
        ComboBoom,
        Land,
        BoxBreak,
        ChainBreak,
    }

    [CreateAssetMenu(menuName = "Tileworks/Match3/Sounds")]
    public sealed class Match3Sounds : SoundBank
    {
    }
}
