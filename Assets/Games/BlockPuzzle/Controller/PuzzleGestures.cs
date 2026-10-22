using System;
using Tileworks.Kit.Core;
using Tileworks.Kit.Input;
using UnityEngine;

namespace BlockPuzzle.Controller
{
    // A drag from the offer to the board, in world points.
    public interface IPuzzleDragHandler
    {
        void OnPress(Vector2 world);
        void OnDrag(Vector2 world);
        void OnRelease(Vector2 world);
    }

    // Turns the press, drag and release of a pointer into world points for the game; the input gate already filtered
    // blocked input.
    public sealed class PuzzleGestures : IDisposable
    {
        private readonly IGestureSource _gestures;
        private readonly IPuzzleBoardView _board;
        private readonly IPuzzleDragHandler _handler;

        public PuzzleGestures(IGestureSource gestures, IPuzzleBoardView board, IPuzzleDragHandler handler)
        {
            _gestures = Guard.NotNull(gestures, nameof(gestures));
            _board = Guard.NotNull(board, nameof(board));
            _handler = Guard.NotNull(handler, nameof(handler));
            _gestures.PressStarted += OnPressStarted;
            _gestures.Dragged += OnDragged;
            _gestures.PressEnded += OnPressEnded;
        }

        public void Dispose()
        {
            _gestures.PressStarted -= OnPressStarted;
            _gestures.Dragged -= OnDragged;
            _gestures.PressEnded -= OnPressEnded;
        }

        private void OnPressStarted(Vector2 screen)
        {
            if (TryWorld(screen, out var world)) _handler.OnPress(world);
        }

        private void OnDragged(Vector2 screen)
        {
            if (TryWorld(screen, out var world)) _handler.OnDrag(world);
        }

        private void OnPressEnded(Vector2 screen)
        {
            if (TryWorld(screen, out var world)) _handler.OnRelease(world);
        }

        private bool TryWorld(Vector2 screen, out Vector2 world)
        {
            var camera = _board.Camera;
            world = default;
            if (camera == null) return false;
            world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            return true;
        }
    }
}
