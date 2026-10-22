using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tileworks.Kit.App
{
    [Serializable]
    public struct GameEntry
    {
        public string Title;
        public string BootScene;
    }

    // Every game built from this project; scene builders add their game here.
    [CreateAssetMenu(menuName = "Tileworks/Game Catalog")]
    public sealed class GameCatalog : ScriptableObject
    {
        [SerializeField] private GameEntry[] _games = new GameEntry[0];
        [SerializeField] private string _hub;

        public IReadOnlyList<GameEntry> Games => _games;

        // The boot scene of the screen that lists every game; empty when there is none.
        public string Hub => _hub;

        public void SetHub(string bootScene) => _hub = bootScene;

        public void Set(string title, string bootScene)
        {
            var games = new List<GameEntry>(_games);
            games.RemoveAll(g => g.BootScene == bootScene);
            games.Add(new GameEntry { Title = title, BootScene = bootScene });
            games.Sort((a, b) => string.CompareOrdinal(a.Title, b.Title));
            _games = games.ToArray();
        }
    }
}
