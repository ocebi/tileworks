using System;
using Tileworks.Tiles.Levels;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    // Where published levels come from and how hard the client tries (docs/REMOTE-LEVELS.md). One asset serves every game.
    [CreateAssetMenu(menuName = "Tileworks/Remote Levels")]
    public sealed class RemoteLevelsConfig : ScriptableObject
    {
        [SerializeField, Tooltip("Each game's manifest; {game} becomes the game id. Empty turns updates off.")]
        private string _manifestUrl = "https://ocebi.github.io/tessera/levels/{game}/manifest.json";

        [SerializeField, Tooltip("Off by default so the Editor plays the level files on disk, not a published copy.")]
        private bool _inEditor;

        [SerializeField, Min(0), Tooltip("How long boot waits for an update before going on; a later one lands on the next return to Home.")]
        private int _bootWaitMs = 1500;

        [SerializeField, Min(1)] private int _timeoutSeconds = 8;
        [SerializeField, Tooltip("Waits between retries of a failed request.")] private int[] _retryDelaysMs = { 1000, 3000 };
        [SerializeField, Min(1)] private int _maxParallel = 4;
        [SerializeField, Min(0), Tooltip("Check again when the app comes back after this long; 0 never.")] private int _refreshMinutes = 30;

        public bool Enabled => !string.IsNullOrEmpty(_manifestUrl) && (_inEditor || !Application.isEditor);
        public int BootWaitMs => _bootWaitMs;
        public TimeSpan RefreshAfter => TimeSpan.FromMinutes(_refreshMinutes);

        public LevelUpdateOptions OptionsFor(string game) => new LevelUpdateOptions
        {
            ManifestUrl = _manifestUrl.Replace("{game}", game),
            TimeoutSeconds = _timeoutSeconds,
            RetryDelaysMs = (int[])_retryDelaysMs.Clone(),
            MaxParallel = _maxParallel,
        };
    }
}
