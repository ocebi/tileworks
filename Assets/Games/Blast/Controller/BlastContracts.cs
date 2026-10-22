using System;
using System.Collections.Generic;
using System.Threading;
using Blast.Model;
using Blast.Service;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Blast.Controller
{
    // A level id plays that level instead of the sequence's level at the index (the level editor's Play).
    public sealed class BlastStartArgs
    {
        public BlastStartArgs(int levelIndex, string levelId = null)
        {
            LevelIndex = levelIndex;
            LevelId = levelId;
        }

        public int LevelIndex { get; }
        public string LevelId { get; }
    }

    // The view reports each piece it has seen land on its cell.
    public interface IBoardLandingHandler
    {
        void OnLanded(int id, int cell);
    }

    public interface IBlastBoardView : IHammerBoard, IBoardCamera
    {
        void Show(BlastState state);

        // Null unbinds.
        void BindLandings(IBoardLandingHandler handler);

        // Where the next action starts (the tapped or struck cell, a landing that set something off); falls stagger by
        // column away from it. -1 is the board's centre.
        void SetOrigin(int cell);

        // A special, combo, shuffle or booster is still playing; taps wait. Plain falls never hold the board.
        bool EffectsBusy { get; }

        float TapBuffer { get; }

        // Moves the falling pieces; the controller ticks it every frame.
        void Tick();

        // The playable cell under this world point, or -1.
        int CellAt(Vector2 world);

        // Feedback for a tap the rules refused: a lone cube or a blocker.
        void Refuse(int cell);

        // Pulses these cells until the next hint or HideHint.
        void ShowHint(int[] cells, int count);
        void HideHint();

        float HintDelay { get; }

        // The level-start drop: HidePieces before the card, DropIn when it closes; DropIn returns its seconds.
        void HidePieces();
        float DropIn();

        // After a win: leftover moves become rockets that pop what they pass; returns its seconds. SkipBonus stops it.
        float PlayBonus(int moves, float delay);
        void SkipBonus();
    }

    public interface IBlastHudHandler
    {
        void OnPause();
    }

    // Goal and move counters change from the event presenters, in step with the pieces.
    public interface IBlastHudView
    {
        // Null unbinds.
        void Bind(IBlastHudHandler handler);
        void SetGoals(IReadOnlyList<BlastGoal> goals);
        void SetGoalLeft(int goal, int left);

        // Flies a cleared goal piece from `screen` (its width on screen is `screenSize`) to its goal; the count drops on landing.
        void Collect(int goal, int left, Sprite sprite, Vector2 screen, float screenSize, in GoalFlight flight);
        void SetMoves(int moves);
        void SetInteractable(bool interactable);

        // Tags the level as a replay of a cleared one.
        void SetReplay(bool replay);

        // "Nice!", "Great!" or "Amazing!" over the board for the count-th big moment in one move.
        void ShowCascade(int count);

        // The level-start card with this level's goals, which then fly into the goal chips; completes when they have landed.
        UniTask ShowIntro(int level, LevelBadge badge, CancellationToken ct);

        // The banner before leftover moves turn into rockets; returns its seconds on screen.
        float ShowBonus() => 0f;
    }

    public readonly struct GoalFlight
    {
        public readonly float Lift;
        public readonly float LiftScale;
        public readonly float Duration;
        public readonly float Arc;

        public GoalFlight(float lift, float liftScale, float duration, float arc)
        {
            Lift = lift;
            LiftScale = liftScale;
            Duration = duration;
            Arc = arc;
        }
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

    public interface IBlastResultView : ISettingsPanel
    {
        bool IsOpen { get; }

        // The level the player is still on while replaying a cleared one, else 0; win and lose read it.
        void SetReplay(int currentLevel);

        // Each popup completes with the button the player chose.
        UniTask<ResultChoice> ShowWin(int levelNumber, int coins, CancellationToken ct);

        // goalsLeft runs parallel to goals; a goal at 0 shows as done.
        UniTask<ResultChoice> ShowLose(IReadOnlyList<BlastGoal> goals, IReadOnlyList<int> goalsLeft, bool canContinue, int extraMoves, int continuePrice,
            bool canAfford, CancellationToken ct);
        UniTask<ResultChoice> ShowPause(CancellationToken ct);
        void Hide();
    }

    public interface IBlastHomeHandler
    {
        void OnPlay();
    }

    public interface IBlastHomeView : IGameSwitchView
    {
        // Null unbinds.
        void Bind(IBlastHomeHandler handler);
        void SetLevel(int number);

        // A Hard or Super Hard sticker on the Play button.
        void SetBadge(LevelBadge badge);
        void SetCoins(long coins);
        void SetLives(int lives, in LivesTimer timer);
        void SetPlayable(bool playable);
    }

    public interface IBlastContext : IDisposable
    {
        BlastSession Session { get; }
    }

    // One child scope per attempt, so a retry or exit disposes everything the attempt created.
    public interface IBlastContextFactory
    {
        IBlastContext Create(LevelRef level);
    }
}
