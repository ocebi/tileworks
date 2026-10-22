using System;
using System.IO;
using Tileworks.Kit.Audio;
using UnityEditor;
using UnityEngine;

namespace Tileworlds.Games.Editor
{
    // Builds sound cues for the scene builders. Each clip name is a file name without extension, found under Roots.
    public static class SoundCues
    {
        public static readonly string[] Roots = { "Assets/Games/Shared/Audio", "Assets/ThirdParty/Kenney/Audio" };

        private static readonly string[] Extensions = { ".ogg", ".wav", ".mp3" };

        public static SoundCue Clip<TCue>(TCue cue, float volume, params string[] names) where TCue : struct, Enum
        {
            var clips = new AudioClip[names.Length];
            for (var i = 0; i < names.Length; i++) clips[i] = Load(names[i]);
            return new SoundCue(cue.ToString(), Convert.ToInt32(cue), clips, volume);
        }

        public static AudioClip Load(string name)
        {
            var path = PathOf(name) ?? throw new FileNotFoundException($"No sound named '{name}' under {string.Join(", ", Roots)}");
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path) ?? throw new InvalidOperationException($"{path} is not imported as an AudioClip");
        }

        // Null, with a warning, while the file isn't there yet.
        public static AudioClip TryLoad(string name)
        {
            var path = PathOf(name);
            if (path != null) return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Debug.LogWarning($"[Tileworks] Missing sound {name}");
            return null;
        }

        private static string PathOf(string name)
        {
            foreach (var root in Roots)
            {
                foreach (var extension in Extensions)
                {
                    var path = $"{root}/{name}{extension}";
                    if (File.Exists(path)) return path;
                }
            }

            return null;
        }
    }
}
