using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Content;
using Tileworks.Kit.Core;
using Tileworks.Kit.Unity;
using UnityEngine.SceneManagement;

namespace Tileworks.Kit.App
{
    public interface IGameSwitcher
    {
        // Every game whose scenes are in this build, in catalog order.
        IReadOnlyList<GameEntry> Games { get; }

        // The other games, starting with the next one in the catalog and wrapping round, so offering the first one cycles
        // through every game. Empty in a single-game build.
        IReadOnlyList<GameEntry> Others { get; }

        // False in a build without the hub scene, and in the hub itself.
        bool HasHub { get; }

        void SwitchTo(GameEntry game);
        void ToHub();
    }

    // What outlives the app scene on a game switch, carried by the app root: the loading screen and the music.
    public interface ICarryOver
    {
        void CarryOver();
    }

    public sealed class GameSwitcher : IGameSwitcher
    {
        private readonly GameCatalog _catalog;
        private readonly string _currentBootScene;
        private readonly ICarryOver _carry;
        private readonly IContent _content;
        private readonly IProgress<float> _progress;
        private readonly ILog _log;
        private readonly List<GameEntry> _games = new List<GameEntry>();
        private readonly List<GameEntry> _others = new List<GameEntry>();
        private string _hub;
        private bool _resolved;
        private bool _switching;

        public GameSwitcher(GameCatalog catalog, string currentBootScene, ICarryOver carry, IContent content, IProgress<float> progress, ILog log)
        {
            _catalog = catalog;
            _currentBootScene = currentBootScene;
            _carry = carry;
            _content = Guard.NotNull(content, nameof(content));
            _progress = progress;
            _log = Guard.NotNull(log, nameof(log));
        }

        public IReadOnlyList<GameEntry> Games => Resolved()._games;
        public IReadOnlyList<GameEntry> Others => Resolved()._others;
        public bool HasHub => Resolved()._hub != null;

        public void SwitchTo(GameEntry game)
        {
            _log.Info(LogTag.App, "Switching to {0}", game.Title);
            Load(game.BootScene);
        }

        public void ToHub()
        {
            if (!HasHub) return;
            _log.Info(LogTag.App, "Back to the games");
            Load(_hub);
        }

        // The content system answers only once it is up, so the lists are built on the first ask after boot: this app's
        // own boot scene is always in the build, so it tells when the answers are real.
        private GameSwitcher Resolved()
        {
            if (_resolved || _catalog == null || !_content.Exists(_currentBootScene)) return this;
            _resolved = true;
            var games = _catalog.Games;
            var current = -1;
            for (var i = 0; i < games.Count; i++)
            {
                if (games[i].BootScene == _currentBootScene) current = i;
                if (_content.Exists(games[i].BootScene)) _games.Add(games[i]);
            }

            for (var step = 1; step <= games.Count; step++)
            {
                var game = games[(current + step + games.Count) % games.Count];
                if (game.BootScene != _currentBootScene && _games.Contains(game)) _others.Add(game);
            }

            if (!string.IsNullOrEmpty(_catalog.Hub) && _catalog.Hub != _currentBootScene && _content.Exists(_catalog.Hub)) _hub = _catalog.Hub;
            return this;
        }

        private void Load(string bootScene)
        {
            if (_switching) return;
            _switching = true;
            LoadAsync(bootScene).Forget(_log, "Game switch");
        }

        // Loading another boot scene on its own unloads this app, which disposes its scope and closes its save. The
        // loading screen and the music carry over to the next app; the screen shows the fetch of a game not yet cached.
        private async UniTask LoadAsync(string bootScene)
        {
            _carry?.CarryOver();
            var bytes = await _content.DownloadSizeAsync(bootScene, CancellationToken.None);
            if (bytes > 0)
            {
                _log.Info(LogTag.Assets, "Fetching {0} KB for {1}", bytes / 1024, bootScene);
                _progress?.Report(0f);
                await _content.DownloadAsync(bootScene, _progress, CancellationToken.None);
            }

            await _content.LoadSceneAsync(bootScene, LoadSceneMode.Single, CancellationToken.None);
        }
    }
}
