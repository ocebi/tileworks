using System;
using System.Threading;
using BlockPuzzle.Model;
using BlockPuzzle.Service;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;
using UnityEngine;

namespace BlockPuzzle.Controller
{
    // A level id plays that level instead of the sequence's level at the index (the level editor's Play).
    public sealed class PuzzleStartArgs
    {
        public PuzzleStartArgs(int levelIndex, string levelId = null)
        {
            LevelIndex = levelIndex;
            LevelId = levelId;
        }

        public int LevelIndex { get; }
        public string LevelId { get; }
    }

    public interface IPuzzleBoardView : IBoardCamera
    {
        void Show(BlockState state, ShapeCatalog shapes);

        // Offer slot whose piece is under this world point, or -1.
        int SlotAt(Vector2 world);

        // Moves the lifted piece with the pointer; returns the cell under its top-left block, or -1 off the board.
        int DragTo(int slot, Vector2 world);

        // Shows where the piece would land and which lines it would clear; -1 hides it.
        void Preview(int slot, int anchor);

        // Sends a lifted piece back to the offer.
        void Return(int slot);
    }

    public interface IPuzzleHudHandler
    {
        void OnPause();
    }

    public interface IPuzzleHudView
    {
        // Null unbinds.
        void Bind(IPuzzleHudHandler handler);
        void SetLevel(int number);
        void SetGoal(BlockGoal goal, int gems, int scoreTarget);
        void SetScore(int score);
        void SetGemsLeft(int gems);
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

    public interface IPuzzleResultView : ISettingsPanel
    {
        bool IsOpen { get; }

        // The level the player is still on while replaying a cleared one, else 0; win and lose read it.
        void SetReplay(int currentLevel);

        // Each popup completes with the button the player chose.
        UniTask<ResultChoice> ShowWin(int levelNumber, int coins, int score, CancellationToken ct);
        UniTask<ResultChoice> ShowLose(bool canContinue, int continuePrice, bool canAfford, CancellationToken ct);
        UniTask<ResultChoice> ShowPause(CancellationToken ct);
        void Hide();
    }

    public interface IPuzzleHomeHandler
    {
        void OnPlay();
    }

    public interface IPuzzleHomeView : IGameSwitchView
    {
        // Null unbinds.
        void Bind(IPuzzleHomeHandler handler);
        void SetLevel(int number, int levelCount);
        void SetCoins(long coins);
        void SetLives(int lives, in LivesTimer timer);
        void SetPlayable(bool playable);
    }

    public interface IPuzzleContext : IDisposable
    {
        PuzzleSession Session { get; }
    }

    // One child scope per attempt, so a retry or exit disposes everything the attempt created.
    public interface IPuzzleContextFactory
    {
        IPuzzleContext Create(LevelRef level);
    }
}
