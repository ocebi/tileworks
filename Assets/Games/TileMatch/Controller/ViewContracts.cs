using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using TileMatch.Model;
using UnityEngine;

namespace TileMatch.Controller
{
    public interface IBoardView : IBoardCamera
    {
        void Show(TileMatchState state, LayeredBoardMapper mapper, ModifierSet modifiers);
        void Clear();
        void Nope(int tile);
    }

    public interface IHudHandler
    {
        void OnPause();
    }

    public interface IHudView
    {
        // Null unbinds.
        void Bind(IHudHandler handler);
        void SetLevel(int number);
        void SetInteractable(bool interactable);

        // Tags the level as a replay of a cleared one.
        void SetReplay(bool replay);
    }

    public enum ResultChoice
    {
        Next,
        Retry,
        Home,
        Levels,
        Continue,
        Resume,
    }

    public interface IResultView : ISettingsPanel
    {
        bool IsOpen { get; }

        // The level the player is still on while replaying a cleared one, else 0; win and lose read it.
        void SetReplay(int currentLevel);

        // Each popup completes with the button the player chose.
        UniTask<ResultChoice> ShowWin(int levelNumber, int coins, CancellationToken ct);
        UniTask<ResultChoice> ShowLose(LoseReason reason, bool canContinue, int continuePrice, bool canAfford, CancellationToken ct);
        UniTask<ResultChoice> ShowPause(CancellationToken ct);
        void Hide();
    }

    public interface IHomeHandler
    {
        void OnPlay();
        void OnShop();
        void OnStats();
    }

    public interface IHomeView : IGameSwitchView
    {
        void Bind(IHomeHandler handler);
        void SetLevel(int number);
        void SetCoins(long coins);
        void SetLives(int lives, in LivesTimer timer);
        void SetPlayable(bool playable);
    }

    public readonly struct ShopEntry
    {
        public readonly string Title;
        public readonly int Cost;
        public readonly bool Affordable;

        public ShopEntry(string title, int cost, bool affordable)
        {
            Title = title;
            Cost = cost;
            Affordable = affordable;
        }
    }

    public interface IShopHandler
    {
        void OnBuy(int index);
    }

    public interface IShopView
    {
        void Bind(IShopHandler handler);
        void Show(System.Collections.Generic.IReadOnlyList<ShopEntry> entries);
        void Hide();
        bool IsOpen { get; }
    }

    public interface IStatsView
    {
        void Show(string title, string body);
        void Hide();
    }

    // Created per attempt by the installer layer; disposing it disposes the level scope.
    public interface ILevelContext : IDisposable
    {
        TileMatch.Service.LevelSession Session { get; }
        IEventPlayer Player { get; }
    }

    public interface ILevelContextFactory
    {
        ILevelContext Create(Tileworks.Tiles.Levels.LevelRef level);
    }
}
