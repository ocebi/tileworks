using System;
using System.Threading;
using NUnit.Framework;
using Tileworks.Tiles.View;

namespace Tileworks.Tiles.Tests
{
    public sealed class LevelListPresenterTests
    {
        private View _view;
        private Launcher _launcher;
        private LevelListPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new View();
            _launcher = new Launcher { CurrentIndex = 5, LevelCount = 20, HasLife = true };
            _presenter = new LevelListPresenter(_view, _launcher);
            _presenter.EnterAsync(null, CancellationToken.None);
        }

        [Test]
        public void Opening_SelectsTheCurrentLevel()
        {
            _view.RequestOpen();
            Assert.That((_view.Count, _view.Current, _view.Selected, _view.Playable), Is.EqualTo((20, 5, 5, true)));
        }

        [Test]
        public void AClearedLevel_IsSelectedThenReplayed()
        {
            _view.RequestOpen();
            _view.Tap(2);
            Assert.That(_view.Selected, Is.EqualTo(2));

            _view.Play();
            Assert.That(_launcher.Launched, Is.EqualTo(2));
            Assert.That(_view.IsOpen, Is.False);
        }

        [Test]
        public void ALockedLevel_Refuses_AndKeepsTheSelection()
        {
            _view.RequestOpen();
            _view.Tap(9);
            Assert.That(_view.Refused, Is.EqualTo(9));
            Assert.That(_view.Selected, Is.EqualTo(5));
        }

        [Test]
        public void WithoutLives_ReplaysStillLaunch_ButTheCurrentLevelDoesNot()
        {
            _launcher.HasLife = false;
            _view.RequestOpen();
            Assert.That(_view.Playable, Is.False);
            _view.Play();
            Assert.That(_launcher.Launched, Is.EqualTo(-1));

            _view.Tap(0);
            Assert.That(_view.Playable, Is.True);
            _view.Play();
            Assert.That(_launcher.Launched, Is.EqualTo(0));
        }

        [Test]
        public void WithEveryLevelCleared_TheLastOneIsSelected()
        {
            _launcher.CurrentIndex = 20;
            _view.RequestOpen();
            Assert.That(_view.Selected, Is.EqualTo(19));
        }

        [Test]
        public void EnteringWithLevelListArgs_OpensTheList()
        {
            var view = new View();
            new LevelListPresenter(view, _launcher).EnterAsync(new LevelListArgs(), CancellationToken.None);
            Assert.That(view.IsOpen, Is.True);
        }

        private sealed class Launcher : ILevelLauncher
        {
            public bool HasLife;
            public int Launched = -1;
            public int CurrentIndex { get; set; }
            public int LevelCount { get; set; }
            public bool CanLaunch(int index) => index < CurrentIndex || HasLife;
            public void Launch(int index) => Launched = index;
        }

        private sealed class View : ILevelListView
        {
            public int Count = -1;
            public int Current = -1;
            public int Selected = -1;
            public int Refused = -1;
            public bool Playable;
            public event Action OpenRequested;
            public event Action<int> TileClicked;
            public event Action PlayClicked;
            public bool IsOpen { get; private set; }

            public void Open(int count, int current, int selected, bool playable)
            {
                (Count, Current, Selected, Playable) = (count, current, selected, playable);
                IsOpen = true;
            }

            public void Select(int index, bool playable) => (Selected, Playable) = (index, playable);
            public void RefuseLocked(int index) => Refused = index;
            public void Close() => IsOpen = false;
            public void RequestOpen() => OpenRequested?.Invoke();
            public void Tap(int index) => TileClicked?.Invoke(index);
            public void Play() => PlayClicked?.Invoke();
        }
    }
}
