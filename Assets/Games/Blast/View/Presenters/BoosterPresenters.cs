using Blast.Model;
using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Blast.View
{
    // The hammer flies in from its button and swings onto the piece, whose clear waits on its lane until impact. The dim
    // around the board lifts at impact.
    public sealed class HammerStruckPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public HammerStruckPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.HammerStruck;

        public float Present(in BoardEvent e, float delay)
        {
            var struck = HammerStruck.From(e);
            var board = _kit.Board;
            var bar = _kit.BoosterBar;
            var impact = BoosterFx.Hammer(board, board.BoosterFx, _kit.Clock, bar.ButtonScreenPoint(BoardBoosters.Hammer), board.CellPosition(struck.Cell), delay);
            _kit.Feedback(BlastCue.Hammer, HapticKind.Medium, delay + impact);
            _kit.At(bar, delay + impact, b => b.SetPicked(-1, default));
            return _kit.Hold(delay, impact);
        }
    }

    // The shuffle icon flies into the board and bursts; the pieces fly once it has.
    public sealed class BoardShuffledPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public BoardShuffledPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.BoardShuffled;

        public float Present(in BoardEvent e, float delay)
        {
            var board = _kit.Board;
            var length = BoosterFx.ShuffleLeadIn(board, board.BoosterFx, _kit.Clock, _kit.BoosterBar.ButtonScreenPoint(BoardBoosters.Shuffle), Vector3.zero, delay);
            _kit.Feedback(BlastCue.Shuffle, HapticKind.Medium, delay);
            _kit.HoldUntil = _kit.Clock.Now + delay + length;
            return _kit.Hold(delay, length);
        }
    }

    // Each piece lifts, flies to its new cell with a full turn and lands; starts are staggered a little by id.
    public sealed class PieceShuffledPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public PieceShuffledPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.PieceShuffled;

        public float Present(in BoardEvent e, float delay)
        {
            var shuffled = PieceShuffled.From(e);
            var board = _kit.Board;
            var feel = _kit.Feel;
            var piece = board.Get(shuffled.Id);
            if (piece == null) return 0f;
            var start = _kit.AfterHold(delay) + _kit.Duration(feel.ShuffleStagger * (shuffled.Id * 37 % 11) / 10f);
            var lift = _kit.Duration(feel.ShuffleLift);
            var flight = _kit.Duration(feel.ShuffleFlight);
            var land = _kit.Duration(feel.LandDuration);
            var squash = new Vector3(feel.LandSquash.x, feel.LandSquash.y, 1f);
            board.Lift(piece);
            var flies = start + lift;
            var lands = flies + flight;
            Tween.Scale(piece.transform, feel.ShuffleLiftScale, lift, Ease.OutQuad, startDelay: start);
            Tween.LocalPosition(piece.transform, board.CellPosition(shuffled.Source), board.CellPosition(shuffled.Target), flight, Ease.InOutCubic,
                startDelay: flies);
            Tween.LocalEulerAngles(piece.transform, Vector3.zero, new Vector3(0f, 0f, -360f), flight, Ease.InOutCubic, startDelay: flies);
            Tween.Scale(piece.transform, squash, land * 0.35f, Ease.OutQuad, startDelay: lands);
            Tween.Scale(piece.transform, Vector3.one, land * 0.65f, Ease.OutBack, startDelay: lands + land * 0.35f).OnComplete(board, b => b.Land(piece));
            return _kit.Hold(delay, start - delay + lift + flight + land);
        }
    }
}
