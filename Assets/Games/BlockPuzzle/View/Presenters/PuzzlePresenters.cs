using BlockPuzzle.Model;
using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using UnityEngine;

namespace BlockPuzzle.View
{
    public sealed class PiecePlacedPresenter : IEventPresenter
    {
        private readonly PuzzlePresenterKit _kit;
        public PiecePlacedPresenter(PuzzlePresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.PiecePlaced;

        public float Present(in BoardEvent e, float delay)
        {
            _kit.Board.Take(PiecePlaced.From(e).Slot)?.Destroy();
            _kit.Board.RefreshFits();
            _kit.Feedback(PuzzleCue.Place, HapticKind.Light, delay);
            return 0f;
        }
    }

    public sealed class CellFilledPresenter : IEventPresenter
    {
        private readonly PuzzlePresenterKit _kit;
        public CellFilledPresenter(PuzzlePresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.CellFilled;

        public float Present(in BoardEvent e, float delay)
        {
            var filled = CellFilled.From(e);
            var duration = _kit.Duration(0.12f);
            _kit.At(_kit.Board, delay, board =>
            {
                board.SetBlock(filled.Cell, filled.Colour);
                Tween.PunchScale(board.BlockAt(filled.Cell).transform, new Vector3(0.15f, 0.15f, 0f), duration);
            });
            return duration;
        }
    }

    // Blocks pop one after another along the cleared lines; a gem pops bigger and counts down the goal.
    public sealed class CellClearedPresenter : IEventPresenter
    {
        private readonly PuzzlePresenterKit _kit;
        public CellClearedPresenter(PuzzlePresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.CellCleared;

        public float Present(in BoardEvent e, float delay)
        {
            var cleared = CellCleared.From(e);
            var board = _kit.Board;
            var start = delay + _kit.Duration(_kit.Feel.ClearStagger * cleared.Order);
            var duration = _kit.Duration(_kit.Feel.ClearDuration);
            var block = board.BlockAt(cleared.Cell);
            Tween.Scale(block.transform, 0f, duration, Ease.InBack, startDelay: start)
                .OnComplete(board, b => b.SetBlock(cleared.Cell, 0));
            if (cleared.Gem)
            {
                var gem = board.GemAt(cleared.Cell);
                Tween.Scale(gem.transform, 1.6f, duration, Ease.OutQuad, startDelay: start);
                Tween.Alpha(gem, 1f, 0f, duration, startDelay: start).OnComplete(gem, g =>
                {
                    g.enabled = false;
                    g.transform.localScale = Vector3.one;
                    g.color = Color.white;
                });
                board.GemsLeft--;
                var left = board.GemsLeft;
                _kit.At(_kit.Hud, start + duration, hud => hud.SetGemsLeft(left));
                _kit.Feedback(PuzzleCue.Gem, HapticKind.Light, start);
            }

            return start - delay + duration;
        }
    }

    public sealed class LinesClearedPresenter : IEventPresenter
    {
        private readonly PuzzlePresenterKit _kit;
        public LinesClearedPresenter(PuzzlePresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.LinesCleared;

        public float Present(in BoardEvent e, float delay)
        {
            var lines = LinesCleared.From(e);
            _kit.Feedback(PuzzleCue.Clear, HapticKind.Medium, delay, 1f + 0.08f * Mathf.Min(lines.Streak, 6));
            var text = lines.Lines >= 3 ? "Amazing!" : lines.Lines == 2 ? "Great!" : lines.Streak > 1 ? $"Combo x{lines.Streak}" : null;
            if (text != null) _kit.Board.Callout(text, delay);
            return 0f;
        }
    }

    public sealed class ScoreChangedPresenter : IEventPresenter
    {
        private readonly PuzzlePresenterKit _kit;
        public ScoreChangedPresenter(PuzzlePresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.ScoreChanged;

        public float Present(in BoardEvent e, float delay)
        {
            var score = ScoreChanged.From(e).Score;
            _kit.At(_kit.Hud, delay, hud => hud.SetScore(score));
            return 0f;
        }
    }

    public sealed class OfferDealtPresenter : IEventPresenter
    {
        private readonly PuzzlePresenterKit _kit;
        public OfferDealtPresenter(PuzzlePresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.OfferDealt;

        public float Present(in BoardEvent e, float delay)
        {
            var dealt = OfferDealt.From(e);
            var board = _kit.Board;
            var start = delay + _kit.Duration(0.06f * dealt.Slot);
            var duration = _kit.Duration(_kit.Feel.DealDuration);
            var piece = board.Deal(dealt.Slot, dealt.Shape, dealt.Colour);
            Tween.Scale(piece.Root, _kit.Feel.OfferScale, duration, Ease.OutBack, startDelay: start);
            board.RefreshFits();
            if (dealt.Slot == 0) _kit.Sfx.Play(PuzzleCue.Deal, start);
            return start - delay + duration;
        }
    }

    public sealed class LevelWonPresenter : IEventPresenter
    {
        private readonly PuzzlePresenterKit _kit;
        public LevelWonPresenter(PuzzlePresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.LevelWon;

        public float Present(in BoardEvent e, float delay)
        {
            _kit.Feedback(PuzzleCue.Win, HapticKind.Success, delay);
            return _kit.Duration(_kit.Feel.ResultDelay);
        }
    }

    public sealed class LevelLostPresenter : IEventPresenter
    {
        private readonly PuzzlePresenterKit _kit;
        public LevelLostPresenter(PuzzlePresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.LevelLost;

        public float Present(in BoardEvent e, float delay)
        {
            _kit.Feedback(PuzzleCue.Lose, HapticKind.Failure, delay);
            return _kit.Duration(_kit.Feel.ResultDelay);
        }
    }
}
