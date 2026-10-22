using UnityEngine;

namespace Tileworlds.Games.Editor
{
    // The shared white effect sprites from Tools/art/fx, tinted at run time. Loading one sets its sprite import settings.
    public static class FxArt
    {
        public const string Folder = "Assets/Games/Shared/Art/Fx";

        public static Sprite Load(string name) => ArtImport.Sprite($"{Folder}/{name}.png", 100);
    }
}
