using System;
using System.Collections.Generic;
using System.Threading;
using Match3.Model;
using Match3.Service;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Match3.Controller
{
    // A level id plays that level instead of the sequence's level at the index (the level editor's Play).
    public sealed class Match3StartArgs
    {
        public Match3StartArgs(int levelIndex, string levelId = null)
        {
            LevelIndex = levelIndex;
            LevelId = levelId;
        }

        public int LevelIndex { get; }
        public string LevelId { get; }
    }

    public interface IPieceLandingHandler
    {
        // The view saw the piece come to rest (a clear it makes is due now).
        void OnLanded(int id);
    }

    public interface IMatch3BoardView : IBoardCamera
    {
        void Show(Match3State state);
        void BindLandings(IPieceLandingHandler handler);

        // Where the next move is made: the refill ripples out from this cell's column.
        void SetOrigin(int cell);

        // A special, combo, shuffle or booster is playing; moves wait for it. Falls don't hold the board.
        bool EffectsBusy { get; }

        // The playable cell under this world point, or -1.
        int CellAt(Vector2 world);
        Vector2 CellCentre(int cell);
        float CellSize { get; }

        // Rings the selected piece; -1 clears it.
        void Select(int cell);

        // Feedback for a swap the rules refused, by its reason.
        void Refuse(int from, int to, RejectReason reason);

        // Nudges the two pieces of the hinted swap until HideHint.
        void ShowHint(int from, int to);
        void HideHint();

        float HintDelay { get; }

        // How long a swipe onto a falling gem waits for it to land.
        float TapBuffer { get; }

        // The level-start drop: HidePieces before the card, DropIn when it closes; DropIn returns its seconds.
        void HidePieces();
        float DropIn();

        // After a win: leftover moves become rockets that pop what they pass; returns its seconds. SkipBonus stops it.
        float PlayBonus(int moves, float delay);
        void SkipBonus();
    }

    public interface IMatch3HudHandler
    {
        void OnPause();
    }

    // Goal and move counters change from the event presenters, in step with the pieces.
    public interface IMatch3HudView
    {
        // Null unbinds.
        void Bind(IMatch3HudHandler handler);
        void SetGoals(IReadOnlyList<Match3Goal> goals);
        void SetGoalLeft(int goal, int left);

        // Flies a cleared goal piece from `screen` (its width on screen is `screenSize`) to its goal; the count drops on landing.
        void Collect(int goal, int left, Sprite sprite, Vector2 screen, float screenSize, in GoalFlight flight);
        void SetMoves(int moves);
        void SetInteractable(bool interactable);

        // Tags the level as a replay of a cleared one.
        void SetReplay(bool replay);

        // "Nice!", "Great!" or "Amazing!" for the Count-th match in one move.
        void ShowCascade(int count);

        // The level-start card with this level's goals, which then fly into the goal chips; completes when they have landed.
        UniTask ShowIntro(int level, LevelBadge badge, CancellationToken ct);

        // The banner before leftover moves turn into rockets; returns its seconds on screen.
        float ShowBonus() => 0f;

        // The "No moves!" ribbon before a shuffle.
        void ShowNoMoves();
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

    public interface IMatch3ResultView : ISettingsPanel
    {
        bool IsOpen { get; }

        // The level the player is still on while replaying a cleared one, else 0; win and lose read it.
        void SetReplay(int currentLevel);

        // Each popup completes with the button the player chose.
        UniTask<ResultChoice> ShowWin(int levelNumber, int coins, CancellationToken ct);

        // goalsLeft runs parallel to goals; a goal at 0 shows as done.
        UniTask<ResultChoice> ShowLose(IReadOnlyList<Match3Goal> goals, IReadOnlyList<int> goalsLeft, bool canContinue, int extraMoves, int continuePrice,
            bool canAfford, CancellationToken ct);
        UniTask<ResultChoice> ShowPause(CancellationToken ct);
        void Hide();
    }

    public interface IMatch3HomeHandler
    {
        void OnPlay();
    }

    public interface IMatch3HomeView : IGameSwitchView
    {
        // Null unbinds.
        void Bind(IMatch3HomeHandler handler);
        void SetLevel(int number);

        // A Hard or Super Hard sticker on the Play button.
        void SetBadge(LevelBadge badge);
        void SetCoins(long coins);
        void SetLives(int lives, in LivesTimer timer);
        void SetPlayable(bool playable);
    }

    public interface IMatch3Context : IDisposable
    {
        Match3Session Session { get; }

        // Every frame: moves the falls and watches for landings the view never reported.
        void Tick();
    }

    // One child scope per attempt, so a retry or exit disposes everything the attempt created.
    public interface IMatch3ContextFactory
    {
        IMatch3Context Create(LevelRef level);
    }
}
