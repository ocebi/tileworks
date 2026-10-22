using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hub.Controller;
using NUnit.Framework;
using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using Tileworks.Kit.Save;

namespace Hub.Tests
{
    public sealed class HubPresenterTests
    {
        private static readonly GameEntry Blast = new GameEntry { Title = "Blast", BootScene = "blast" };
        private static readonly GameEntry Block = new GameEntry { Title = "Block Puzzle", BootScene = "block" };
        private static readonly GameEntry Tiles = new GameEntry { Title = "Tile Match", BootScene = "tiles" };

        private View _view;
        private Switcher _games;
        private Store _store;

        [SetUp]
        public void SetUp()
        {
            _view = new View();
            _games = new Switcher { Games = { Blast, Block, Tiles } };
            _store = new Store();
        }

        [Test]
        public void Entering_ListsTheGames_AndSelectsTheLastOnePlayed()
        {
            _store.Set(HubPresenter.LastPlayedKey, "tiles");
            Enter();
            Assert.That(_view.Shown, Is.EqualTo(new[] { Blast, Block, Tiles }));
            Assert.That(_view.Selected, Is.EqualTo(2));
        }

        [Test]
        public void Entering_SelectsTheFirstGame_WhenNoneWasPlayed()
        {
            Enter();
            Assert.That(_view.Selected, Is.EqualTo(0));
        }

        [Test]
        public void TappingAnotherCard_SelectsIt_AndTappingItAgainOnlyNudgesIt()
        {
            Enter();
            _view.Tap(1);
            Assert.That(_view.Selected, Is.EqualTo(1));

            _view.Tap(1);
            Assert.That(_view.Nudged, Is.EqualTo(1));
            Assert.That(_games.SwitchedTo, Is.Empty);
        }

        [Test]
        public void Play_RemembersTheGame_AndSwitchesOnceTheViewHasLeft()
        {
            Enter();
            _view.Tap(1);
            _view.Play();
            Assert.That(_store.Get(HubPresenter.LastPlayedKey), Is.EqualTo("block"));
            Assert.That(_games.SwitchedTo, Is.Empty);

            _view.Left.TrySetResult();
            Assert.That(_games.SwitchedTo, Is.EqualTo(new[] { "block" }));
        }

        [Test]
        public void AfterPlay_FurtherTapsAreIgnored()
        {
            Enter();
            _view.Play();
            _view.Play();
            _view.Tap(2);
            _view.Left.TrySetResult();
            Assert.That(_games.SwitchedTo, Is.EqualTo(new[] { "blast" }));
            Assert.That(_view.Selected, Is.EqualTo(0));
        }

        [Test]
        public void Disposing_StopsListening()
        {
            var presenter = Enter();
            presenter.Dispose();
            _view.Play();
            Assert.That(_games.SwitchedTo, Is.Empty);
        }

        private HubPresenter Enter()
        {
            var presenter = new HubPresenter(_view, _games, _store, new SilentMusic());
            presenter.EnterAsync(null, CancellationToken.None);
            return presenter;
        }

        private sealed class View : IHubView
        {
            public readonly UniTaskCompletionSource Left = new UniTaskCompletionSource();
            public IReadOnlyList<GameEntry> Shown;
            public int Selected = -1;
            public int Nudged = -1;
            private IHubHandler _handler;

            public void Bind(IHubHandler handler) => _handler = handler;

            public void Open(IReadOnlyList<GameEntry> games, int selected)
            {
                Shown = games;
                Selected = selected;
            }

            public void Select(int index) => Selected = index;
            public void Nudge(int index) => Nudged = index;
            public UniTask LeaveAsync() => Left.Task;

            public void Tap(int index) => _handler?.OnCard(index);
            public void Play() => _handler?.OnPlay();
        }

        private sealed class Switcher : IGameSwitcher
        {
            public readonly List<GameEntry> Games = new List<GameEntry>();
            public readonly List<string> SwitchedTo = new List<string>();
            IReadOnlyList<GameEntry> IGameSwitcher.Games => Games;
            public IReadOnlyList<GameEntry> Others => Games;
            public bool HasHub => false;
            public void SwitchTo(GameEntry game) => SwitchedTo.Add(game.BootScene);
            public void ToHub() { }
        }

        private sealed class Store : IKeyValueStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string Get(string key, string fallback = null) => _values.TryGetValue(key, out var value) ? value : fallback;
            public void Set(string key, string value) => _values[key] = value;
            public long GetLong(string key, long fallback = 0) => fallback;
            public void SetLong(string key, long value) { }
            public bool GetBool(string key, bool fallback = false) => fallback;
            public void SetBool(string key, bool value) { }
        }
    }
}
