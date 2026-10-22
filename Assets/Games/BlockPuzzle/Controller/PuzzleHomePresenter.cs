using System;
using System.Threading;
using BlockPuzzle.Api;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using Tileworks.Kit.Core;
using Tileworks.Meta.Api;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;

namespace BlockPuzzle.Controller
{
    public sealed class PuzzleHomePresenter : ISceneEntry, ITickable, ILevelLauncher, IPuzzleHomeHandler, IDisposable
    {
        private readonly IPuzzleHomeView _view;
        private readonly IPuzzleProgress _progress;
        private readonly ILevelSequence _sequence;
        private readonly IAppNavigator _navigator;
        private readonly IEconomy _economy;
        private readonly ILives _lives;
        private readonly IClock _clock;
        private readonly IMusic _music;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
        private long _lastSecond = -1;
        private bool _entered;

        public PuzzleHomePresenter(IPuzzleHomeView view, IPuzzleProgress progress, ILevelSequence sequence, IAppNavigator navigator, IEconomy economy,
            ILives lives, IClock clock, IMusic music)
        {
            _view = Guard.NotNull(view, nameof(view));
            _progress = progress;
            _sequence = sequence;
            _navigator = navigator;
            _economy = economy;
            _lives = lives;
            _clock = clock;
            _music = music;
        }

        public UniTask EnterAsync(object args, CancellationToken ct)
        {
            _music.PlayLoop(MusicTrack.Home);
            _subscriptions.Add(_progress.CurrentIndex.Subscribe(index => _view.SetLevel(Math.Min(index, _sequence.Count - 1) + 1, _sequence.Count)));
            _subscriptions.Add(_economy.Balance(Resources.Coins).Subscribe(_view.SetCoins));
            _view.Bind(this);
            _entered = true;
            RefreshLives();
            return UniTask.CompletedTask;
        }

        public void Tick()
        {
            if (!_entered) return;
            var second = _clock.UtcNow.Ticks / TimeSpan.TicksPerSecond;
            if (second == _lastSecond) return;
            _lastSecond = second;
            RefreshLives();
        }

        public void Dispose()
        {
            _view.Bind(null);
            _subscriptions.Dispose();
        }

        private void RefreshLives()
        {
            _lives.Refresh();
            var count = _lives.Count.Value;
            var timer = LivesTimer.Of(count, _lives.Max, _lives.IsInfinite, _lives.UntilNext);
            _view.SetLives(count, timer);
            _view.SetPlayable(_lives.HasLife);
        }

        public int CurrentIndex => _progress.CurrentIndex.Value;

        public int LevelCount => _sequence.Count;

        public bool CanLaunch(int index) => index < CurrentIndex || _lives.HasLife;

        public void Launch(int index) => _navigator.GoTo(AppStates.Level, new PuzzleStartArgs(index));

        public void OnPlay()
        {
            if (_lives.HasLife) _navigator.GoTo(AppStates.Level, new PuzzleStartArgs(_progress.CurrentIndex.Value));
        }
    }
}
