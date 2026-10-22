using Match3.Model;
using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Match3.View
{
    // The dragged piece passes in front and swells halfway; the board holds its clears until both pieces arrive.
    public sealed class PiecesSwappedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public PiecesSwappedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.PiecesSwapped;

        public float Present(in BoardEvent e, float delay)
        {
            var swapped = PiecesSwapped.From(e);
            var board = _kit.Board;
            var feel = _kit.Feel;
            var duration = _kit.Duration(feel.SwapDuration);
            var from = board.CellPosition(swapped.Source);
            var to = board.CellPosition(swapped.Target);
            var moved = board.Get(swapped.Id);
            var other = board.Get(swapped.OtherId);
            board.HideHint();
            if (other != null) Tween.StopAll(other.transform);
            if (moved != null)
            {
                Tween.StopAll(moved.transform);
                board.Lift(moved);
                Tween.LocalPosition(moved.transform, from, to, duration, Ease.InOutQuad, startDelay: delay).OnComplete(board, b => b.Land(moved));
                Tween.Scale(moved.transform, feel.SwapSwell, duration * 0.5f, Ease.OutQuad, cycles: 2, cycleMode: CycleMode.Yoyo, startDelay: delay);
            }

            if (other != null) Tween.LocalPosition(other.transform, to, from, duration, Ease.InOutQuad, startDelay: delay);
            _kit.Feedback(Match3Cue.Swap, HapticKind.Light, delay);
            _kit.Matches = 0;
            _kit.HoldUntil = _kit.Clock.Now + delay + duration;
            return duration;
        }
    }

    // A fall comes as legs, moved frame by frame by the kit's FallMover: straight runs speed up down their column,
    // diagonal steps slide at an even pace. A fall that starts from rest waits out the column stagger; a leg for a piece
    // already moving joins its queue, so the piece keeps its speed. The squash and the landing report come from
    // PieceLandings, on the last leg only.
    public sealed class PieceFellPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public PieceFellPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.PieceFell;

        public float Present(in BoardEvent e, float delay)
        {
            var fell = PieceFell.From(e);
            var board = _kit.Board;
            var piece = board.Get(fell.Id);
            if (piece == null) return 0f;
            return Fall(_kit, fell.Id, piece, board.CellPosition(fell.Source), board.CellPosition(fell.Target), fell.Target % board.Width, fell.Lands, delay);
        }

        public static float Fall(Match3PresenterKit kit, int id, SpriteRenderer piece, Vector3 from, Vector3 to, int column, bool lands, float delay)
        {
            var start = kit.Mover.IsMoving(id) ? delay : delay + kit.Stagger(column);
            kit.Board.Lift(piece);
            var fall = kit.Mover.Add(id, piece.transform, from, to, lands, start);

            // The lane frees up when the landing is reported, so a clear the landing makes pops out of the squash.
            return start - delay + fall + (lands ? kit.Duration(kit.Feel.LandClearGap) : 0f);
        }
    }

    public sealed class PieceSpawnedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public PieceSpawnedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.PieceSpawned;

        public float Present(in BoardEvent e, float delay)
        {
            var spawned = PieceSpawned.From(e);
            var board = _kit.Board;
            var piece = board.Add(spawned.Id, spawned.Cell, Piece.Gem, spawned.Colour, 0);
            // It waits above the board, under the mask and behind the pieces still coming down its column, until its fall
            // starts.
            var from = _kit.Mover.Spawn(piece.transform, board.SpawnPosition(spawned.Cell, spawned.Drop));
            return PieceFellPresenter.Fall(_kit, spawned.Id, piece, from, board.CellPosition(spawned.Cell), spawned.Cell % board.Width, spawned.Lands, delay);
        }
    }

    // A piece touching down: it squashes harder the faster it hit, thumps, and is reported landed once a clear it
    // makes is due, so the pop swells out of the squash.
    public sealed class PieceLandings : IFallLandingHandler
    {
        private readonly Match3PresenterKit _kit;

        public PieceLandings(Match3PresenterKit kit) => _kit = kit;

        public void Landed(int id, Transform piece, float impact)
        {
            var feel = _kit.Feel;
            var board = _kit.Board;
            var renderer = board.Get(id);
            if (renderer != null) board.Land(renderer);
            var land = _kit.Duration(feel.LandDuration);
            var squash = _kit.Mover.Motion.Squash(feel.LandSquash, impact);
            Tween.Scale(piece, new Vector3(squash.x, squash.y, 1f), land * 0.35f, Ease.OutQuad);
            Tween.Scale(piece, Vector3.one, land * 0.65f, Ease.OutBack, startDelay: land * 0.35f);
            _kit.Sfx.Play(Match3Cue.Land, 0f);
            _kit.Squashed(land);
            _kit.At(_kit, _kit.Duration(feel.LandClearGap), k => k.ReportLanded(id));
        }
    }

    // The No moves ribbon, or the shuffle booster flying into the board; the pieces fly after it.
    public sealed class BoardShuffledPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public BoardShuffledPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.BoardShuffled;

        public float Present(in BoardEvent e, float delay)
        {
            var booster = BoardShuffled.From(e).Booster;
            var board = _kit.Boosters;
            var length = booster
                ? BoosterFx.ShuffleLeadIn(board, board.Config, _kit.Clock, _kit.BoosterBar.ButtonScreenPoint(BoardBoosters.Shuffle), Vector3.zero, delay)
                : _kit.Duration(_kit.Feel.ShuffleBanner);
            if (!booster) _kit.At(_kit.Hud, delay, hud => hud.ShowNoMoves());
            _kit.Feedback(Match3Cue.Shuffle, HapticKind.Medium, delay);
            _kit.HoldUntil = _kit.Clock.Now + delay + length;
            return _kit.Hold(delay, length);
        }
    }

    // Each gem lifts, flies to its new cell with a full turn and lands; starts are staggered a little by id.
    public sealed class PieceShuffledPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public PieceShuffledPresenter(Match3PresenterKit kit) => _kit = kit;
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
