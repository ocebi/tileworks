using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using Tileworks.Kit.Core;
using Tileworks.Meta.Api;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;
using TileMatch.Api;
using TileMatch.Service;

namespace TileMatch.Controller
{
    public sealed class HomePresenter : ISceneEntry, ITickable, ILevelLauncher, IHomeHandler, IShopHandler, IDisposable
    {
        private readonly IHomeView _view;
        private readonly IShopView _shopView;
        private readonly IStatsView _statsView;
        private readonly ILevelProgress _progress;
        private readonly ILevelSequence _sequence;
        private readonly IAppNavigator _navigator;
        private readonly IEconomy _economy;
        private readonly ILives _lives;
        private readonly ShopService _shop;
        private readonly StatsService _stats;
        private readonly IClock _clock;
        private readonly IMusic _music;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
        private readonly List<ShopEntry> _entries = new List<ShopEntry>();
        private long _lastTickSecond = -1;
        private bool _entered;

        public HomePresenter(IHomeView view, IShopView shopView, IStatsView statsView, ILevelProgress progress, ILevelSequence sequence, IAppNavigator navigator,
            IEconomy economy, ILives lives, ShopService shop, StatsService stats, IClock clock, IMusic music)
        {
            _sequence = Guard.NotNull(sequence, nameof(sequence));
            _view = Guard.NotNull(view, nameof(view));
            _shopView = Guard.NotNull(shopView, nameof(shopView));
            _statsView = Guard.NotNull(statsView, nameof(statsView));
            _progress = progress;
            _navigator = navigator;
            _economy = economy;
            _lives = lives;
            _shop = shop;
            _stats = stats;
            _clock = clock;
            _music = music;
        }

        public UniTask EnterAsync(object args, CancellationToken ct)
        {
            _music.PlayLoop(MusicTrack.Home);
            _subscriptions.Add(_progress.CurrentIndex.Subscribe(index => _view.SetLevel(index + 1)));
            _subscriptions.Add(_economy.Balance(Resources.Coins).Subscribe(_view.SetCoins));
            _view.Bind(this);
            _shopView.Bind(this);
            _entered = true;
            RefreshLives();
            return UniTask.CompletedTask;
        }

        public void Tick()
        {
            if (!_entered) return;
            var second = _clock.UtcNow.Ticks / TimeSpan.TicksPerSecond;
            if (second == _lastTickSecond) return;
            _lastTickSecond = second;
            RefreshLives();
        }

        public void Dispose()
        {
            _view.Bind(null);
            _shopView.Bind(null);
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

        // Past the end the sequence keeps going, so every level reached gets a tile.
        public int LevelCount => Math.Max(_sequence.Count, CurrentIndex + 1);

        public bool CanLaunch(int index) => index < CurrentIndex || _lives.HasLife;

        public void Launch(int index) => _navigator.GoTo(AppStates.Level, new LevelStartArgs(index));

        public void OnPlay()
        {
            if (!_lives.HasLife)
            {
                OnShop();
                return;
            }

            _navigator.GoTo(AppStates.Level, new LevelStartArgs(_progress.CurrentIndex.Value));
        }

        public void OnShop()
        {
            _entries.Clear();
            foreach (var item in _shop.Items) _entries.Add(new ShopEntry(item.Title, item.Cost, _shop.CanBuy(item)));
            _shopView.Show(_entries);
        }

        public void OnBuy(int index)
        {
            if (index < 0 || index >= _shop.Items.Count) return;
            if (_shop.Buy(_shop.Items[index]).IsOk) RefreshLives();
            OnShop();
        }

        public void OnStats() => _statsView.Show("Stats", _stats.Summary());
    }
}
