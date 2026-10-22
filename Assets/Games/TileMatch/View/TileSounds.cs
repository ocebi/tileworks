using Tileworks.Kit.Audio;
using UnityEngine;

namespace TileMatch.View
{
    public enum Sfx
    {
        Tap,
        Land,
        Triple,
        Nope,
        Uncover,
        Crack,
        Spawn,
        Booster,
        Win,
        Lose,
        Button,
    }

    [CreateAssetMenu(menuName = "Tileworks/Tile Match/Sounds")]
    public sealed class TileSounds : SoundBank
    {
    }
}
