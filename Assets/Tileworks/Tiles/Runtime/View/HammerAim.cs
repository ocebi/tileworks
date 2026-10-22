using System;
using Tileworks.Kit.Input;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    // A board the hammer aims at. Screen points are in pixels.
    public interface IHammerBoard
    {
        // The board's frame on screen, which stays clear of the dim while the hammer is picked.
        Rect ScreenRect { get; }

        // The playable cell under this screen point, or -1.
        int CellAtScreen(Vector2 screen);

        // Rings a cell; -1 hides the ring.
        void Aim(int cell);

        // A cell the hammer can't hit shakes.
        void RefuseAim(int cell);
    }

    public interface IHammerHandler
    {
        bool CanHit(int cell);
        void OnStruck(int cell);
    }

    // While armed, a finger on the board rings the cell it would hit, and the ring follows it; letting go there strikes.
    // Letting go on a cell it can't hit shakes that cell; letting go off the board does nothing. Either way it stays armed.
    public sealed class HammerAim : IDisposable
    {
        private readonly IGestureSource _gestures;
        private readonly IHammerBoard _board;
        private readonly IHammerHandler _handler;
        private bool _pressed;

        public HammerAim(IGestureSource gestures, IHammerBoard board, IHammerHandler handler)
        {
            _gestures = gestures;
            _board = board;
            _handler = handler;
            _gestures.PressStarted += OnPressStarted;
            _gestures.Dragged += OnDragged;
            _gestures.PressEnded += OnPressEnded;
        }

        public bool Armed { get; private set; }

        // The last release went to the hammer, so the tap that follows it isn't a move.
        public bool Consumed { get; private set; }

        public void Arm(bool armed)
        {
            Armed = armed;
            _pressed = false;
            _board.Aim(-1);
        }

        public void Dispose()
        {
            _gestures.PressStarted -= OnPressStarted;
            _gestures.Dragged -= OnDragged;
            _gestures.PressEnded -= OnPressEnded;
        }

        private void OnPressStarted(Vector2 screen)
        {
            Consumed = false;
            if (!Armed) return;
            _pressed = true;
            Ring(screen);
        }

        private void OnDragged(Vector2 screen)
        {
            if (Armed && _pressed) Ring(screen);
        }

        private void OnPressEnded(Vector2 screen)
        {
            if (!Armed || !_pressed) return;
            _pressed = false;
            Consumed = true;
            _board.Aim(-1);
            var cell = _board.CellAtScreen(screen);
            if (cell < 0) return;
            if (_handler.CanHit(cell)) _handler.OnStruck(cell);
            else _board.RefuseAim(cell);
        }

        private void Ring(Vector2 screen)
        {
            var cell = _board.CellAtScreen(screen);
            _board.Aim(cell >= 0 && _handler.CanHit(cell) ? cell : -1);
        }
    }
}
