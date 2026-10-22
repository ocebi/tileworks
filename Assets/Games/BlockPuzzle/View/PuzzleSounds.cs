using Tileworks.Kit.Audio;
using UnityEngine;

namespace BlockPuzzle.View
{
    public enum PuzzleCue
    {
        Pick,
        Place,
        Clear,
        Gem,
        Deal,
        Nope,
        Win,
        Lose,
    }

    [CreateAssetMenu(menuName = "Tileworks/Block Puzzle/Sounds")]
    public sealed class PuzzleSounds : SoundBank
    {
    }
}
