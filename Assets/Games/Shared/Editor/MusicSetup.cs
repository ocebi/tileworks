using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using UnityEditor;
using UnityEngine;

namespace Tileworlds.Games.Editor
{
    // The app scene's music player and the music config every game shares.
    public static class MusicSetup
    {
        private const string ConfigPath = "Assets/Games/Shared/Config/Music.asset";

        public static void Add(AppRoot root)
        {
            var config = SceneParts.LoadOrCreate<MusicConfig>(ConfigPath);
            // Clips are content (the Shared.Music group); a clip referenced here would ship inside every app scene's bundle.
            config.Configure(null, null, null, null, null, null);
            config.ConfigureKeys("music_home", "music_game_1", "music_game_2", "music_hard", "music_clear", "music_fail");
            EditorUtility.SetDirty(config);
            var player = new GameObject("Music").AddComponent<MusicPlayer>();
            player.transform.SetParent(root.transform, false);
            SceneParts.Assign(player, "_config", config);
            SceneParts.Assign(root, "_music", player);
        }

    }
}
