using System;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.App
{
    public interface IGameSwitchHandler
    {
        void OnSwitchGame();
    }

    public interface IGameSwitchView
    {
        // Null unbinds.
        void Bind(IGameSwitchHandler handler);

        // Null hides the option.
        void ShowSwitchGame(string label);
    }

    public sealed class GameSwitchPresenter : IInitializable, IGameSwitchHandler, IDisposable
    {
        private readonly IGameSwitchView _view;
        private readonly IGameSwitcher _games;

        public GameSwitchPresenter(IGameSwitchView view, IGameSwitcher games)
        {
            _view = Guard.NotNull(view, nameof(view));
            _games = Guard.NotNull(games, nameof(games));
        }

        public void Initialize()
        {
            _view.ShowSwitchGame(_games.HasHub ? "All Games" : _games.Others.Count > 0 ? "Play " + _games.Others[0].Title : null);
            _view.Bind(this);
        }

        public void Dispose() => _view.Bind(null);

        public void OnSwitchGame()
        {
            if (_games.HasHub) _games.ToHub();
            else if (_games.Others.Count > 0) _games.SwitchTo(_games.Others[0]);
        }
    }
}
