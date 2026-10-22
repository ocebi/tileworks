using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tileworks.Kit.App;

namespace Tileworks.Kit.Tests
{
    public sealed class GameSwitchTests
    {
        [Test]
        public void WithAHub_TheButtonSaysAllGames_AndGoesBackToTheHub()
        {
            var games = new Switcher { HasHub = true, Others = { new GameEntry { Title = "Blast" } } };
            var view = new View();
            new GameSwitchPresenter(view, games).Initialize();

            view.Click();

            Assert.That(view.Label, Is.EqualTo("All Games"));
            Assert.That(games.WentToHub, Is.True);
            Assert.That(games.SwitchedTo, Is.Null);
        }

        [Test]
        public void WithoutAHub_TheButtonOffersTheNextGame()
        {
            var games = new Switcher { Others = { new GameEntry { Title = "Blast" } } };
            var view = new View();
            new GameSwitchPresenter(view, games).Initialize();

            view.Click();

            Assert.That(view.Label, Is.EqualTo("Play Blast"));
            Assert.That(games.SwitchedTo, Is.EqualTo("Blast"));
        }

        [Test]
        public void ASingleGameBuild_HidesTheButton()
        {
            var view = new View { Label = "shown" };
            new GameSwitchPresenter(view, new Switcher()).Initialize();
            Assert.That(view.Label, Is.Null);
        }

        private sealed class View : IGameSwitchView
        {
            public string Label;
            private IGameSwitchHandler _handler;
            public void Bind(IGameSwitchHandler handler) => _handler = handler;
            public void ShowSwitchGame(string label) => Label = label;
            public void Click() => _handler?.OnSwitchGame();
        }

        private sealed class Switcher : IGameSwitcher
        {
            public readonly List<GameEntry> Others = new List<GameEntry>();
            public bool HasHub { get; set; }
            public bool WentToHub;
            public string SwitchedTo;
            public IReadOnlyList<GameEntry> Games => Others;
            IReadOnlyList<GameEntry> IGameSwitcher.Others => Others;
            public void SwitchTo(GameEntry game) => SwitchedTo = game.Title;
            public void ToHub() => WentToHub = true;
        }
    }
}
