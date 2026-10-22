using System;
using Tileworks.Kit.Core;
using Tileworks.Kit.Input;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    public interface IBoardTapHandler
    {
        void OnBoardTap(Vector2 world);
    }

    public interface IBoardCamera
    {
        Camera Camera { get; }
    }

    // Turns screen taps into world taps for the game; the input gate already filtered blocked input.
    public sealed class BoardInput : IDisposable
    {
        private readonly IGestureSource _gestures;
        private readonly IBoardCamera _board;
        private readonly IBoardTapHandler _handler;

        public BoardInput(IGestureSource gestures, IBoardCamera board, IBoardTapHandler handler)
        {
            _gestures = Guard.NotNull(gestures, nameof(gestures));
            _board = Guard.NotNull(board, nameof(board));
            _handler = Guard.NotNull(handler, nameof(handler));
            _gestures.Tapped += OnTapped;
        }

        public void Dispose() => _gestures.Tapped -= OnTapped;

        private void OnTapped(Vector2 screen)
        {
            var camera = _board.Camera;
            if (camera == null) return;
            var world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            _handler.OnBoardTap(world);
        }
    }
}
