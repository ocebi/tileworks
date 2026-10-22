using System;
using Match3.Model;
using Tileworks.Kit.Core;
using Tileworks.Kit.Input;
using UnityEngine;

namespace Match3.Controller
{
    public interface IBoardMoveHandler
    {
        void OnSwap(int from, int to);
        void OnActivate(int cell);

        // Any touch on the board, so the idle hint can reset.
        void OnTouch();
    }

    // What the gestures need to know about the level in play.
    public interface IBoardGestureContext
    {
        Match3State State { get; }
        bool Enabled { get; }

        // False while a special, combo, shuffle or booster plays; falls don't stop gestures.
        bool CanMove { get; }
    }

    // Turns pointer gestures into swaps: drag a piece past a third of a cell towards a neighbour, or tap a piece and then
    // a neighbour. Tapping a selected special again quickly fires it. Gestures work while pieces fall; the move handler
    // decides what to do with a swipe that touches a falling gem.
    public sealed class BoardGestures : IDisposable
    {
        public const float DragThreshold = 1f / 3f;
        public const double DoubleTapSeconds = 0.4;

        private readonly IGestureSource _gestures;
        private readonly IMatch3BoardView _board;
        private readonly IClock _clock;
        private readonly IBoardGestureContext _context;
        private readonly IBoardMoveHandler _handler;
        private int _pressed = -1;
        private bool _swiped;
        private int _selected = -1;
        private DateTime _selectedAt;

        public BoardGestures(IGestureSource gestures, IMatch3BoardView board, IClock clock, IBoardGestureContext context, IBoardMoveHandler handler)
        {
            _gestures = gestures;
            _board = board;
            _clock = clock;
            _context = context;
            _handler = handler;
            _gestures.PressStarted += OnPressStarted;
            _gestures.Dragged += OnDragged;
            _gestures.Tapped += OnTapped;
        }

        public void Clear() => Select(-1);

        public void Dispose()
        {
            _gestures.PressStarted -= OnPressStarted;
            _gestures.Dragged -= OnDragged;
            _gestures.Tapped -= OnTapped;
        }

        private void OnPressStarted(Vector2 screen)
        {
            _swiped = false;
            _pressed = _context.Enabled ? CellAt(screen) : -1;
            if (_pressed >= 0) _handler.OnTouch();
        }

        private void OnDragged(Vector2 screen)
        {
            if (_pressed < 0 || _swiped || !_context.Enabled || !_context.CanMove) return;
            var delta = World(screen) - _board.CellCentre(_pressed);
            if (delta.magnitude < _board.CellSize * DragThreshold) return;
            _swiped = true;
            var s = _context.State;
            var horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            var x = s.X(_pressed) + (horizontal ? Math.Sign(delta.x) : 0);
            var y = s.Y(_pressed) + (horizontal ? 0 : delta.y > 0 ? -1 : 1);
            var target = s.CellAt(x, y);
            Select(-1);
            if (target >= 0) _handler.OnSwap(_pressed, target);
        }

        private void OnTapped(Vector2 screen)
        {
            if (!_context.Enabled || !_context.CanMove) return;
            var cell = CellAt(screen);
            if (cell < 0)
            {
                Select(-1);
                return;
            }

            _handler.OnTouch();
            var s = _context.State;
            if (_selected < 0)
            {
                Pick(s, cell);
                return;
            }

            if (cell == _selected)
            {
                var quick = (_clock.UtcNow - _selectedAt).TotalSeconds <= DoubleTapSeconds;
                Select(-1);
                if (quick && Pieces.IsSpecial(s.Pieces[cell])) _handler.OnActivate(cell);
                return;
            }

            if (Match3Board.AreNeighbours(s, _selected, cell))
            {
                var from = _selected;
                Select(-1);
                _handler.OnSwap(from, cell);
                return;
            }

            Pick(s, cell);
        }

        // Only a piece that can move takes the ring; a chained gem shakes its chain instead.
        private void Pick(Match3State s, int cell)
        {
            if (Matches.CanSwap(s, cell))
            {
                Select(cell);
                return;
            }

            Select(-1);
            if (s.Chained[cell]) _board.Refuse(cell, cell, Match3Rejects.Chained);
        }

        private void Select(int cell)
        {
            _selected = cell;
            _selectedAt = _clock.UtcNow;
            _board.Select(cell);
        }

        private int CellAt(Vector2 screen) => _board.CellAt(World(screen));

        private Vector2 World(Vector2 screen)
        {
            var camera = _board.Camera;
            return camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
        }
    }
}
