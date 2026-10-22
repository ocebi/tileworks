using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;

namespace Hub.Controller
{
    // Lists the games in this build with the last one played selected. Only Play starts a game, so a quick double tap while
    // browsing doesn't launch one.
    public sealed class HubPresenter : ISceneEntry, IHubHandler, IDisposable
    {
        public const string LastPlayedKey = "hub.lastPlayed";

        private readonly IHubView _view;
        private readonly IGameSwitcher _games;
        private readonly IKeyValueStore _store;
        private readonly IMusic _music;
        private int _selected;
        private bool _leaving;

        public HubPresenter(IHubView view, IGameSwitcher games, IKeyValueStore store, IMusic music)
        {
            _view = Guard.NotNull(view, nameof(view));
            _games = Guard.NotNull(games, nameof(games));
            _store = Guard.NotNull(store, nameof(store));
            _music = music;
        }

        public UniTask EnterAsync(object args, CancellationToken ct)
        {
            _music.PlayLoop(MusicTrack.Home);
            var games = _games.Games;
            var last = _store.Get(LastPlayedKey);
            for (var i = 0; i < games.Count; i++)
                if (games[i].BootScene == last) _selected = i;
            _view.Open(games, _selected);
            _view.Bind(this);
            return UniTask.CompletedTask;
        }

        public void Dispose() => _view.Bind(null);

        public void OnCard(int index)
        {
            if (_leaving || index < 0 || index >= _games.Games.Count) return;
            if (index == _selected)
            {
                _view.Nudge(index);
                return;
            }

            _selected = index;
            _view.Select(index);
        }

        public void OnPlay()
        {
            if (_leaving || _games.Games.Count == 0) return;
            _leaving = true;
            PlayAsync(_games.Games[_selected]).Forget();
        }

        private async UniTaskVoid PlayAsync(GameEntry game)
        {
            _store.Set(LastPlayedKey, game.BootScene);
            await _view.LeaveAsync();
            _games.SwitchTo(game);
        }
    }
}
