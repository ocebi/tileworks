using System;
using Tileworks.Kit.Core;
using UnityEngine;

namespace Tileworks.Kit.Input
{
    public interface IGestureSource
    {
        event Action<Vector2> Tapped;
        event Action<Vector2> PressStarted;
        event Action<Vector2> PressEnded;
        event Action<Vector2> DragStarted;
        event Action<Vector2> Dragged;
        event Action<Vector2> DragEnded;
    }

    // Taps and drags from the pointer surface, one pointer at a time.
    public sealed class GestureSource : IGestureSource, IDisposable
    {
        private const int NoPointer = int.MinValue;

        private readonly IInputGate _gate;
        private readonly IPointerSurface _surface;
        private int _pointer = NoPointer;
        private bool _dragging;

        public GestureSource(IInputGate gate, IPointerSurface surface)
        {
            _gate = Guard.NotNull(gate, nameof(gate));
            _surface = Guard.NotNull(surface, nameof(surface));
            _surface.Pressed += OnPressed;
            _surface.DragStarted += OnDragStarted;
            _surface.Dragged += OnDragged;
            _surface.Released += OnReleased;
        }

        public event Action<Vector2> Tapped;
        public event Action<Vector2> PressStarted;
        public event Action<Vector2> PressEnded;
        public event Action<Vector2> DragStarted;
        public event Action<Vector2> Dragged;
        public event Action<Vector2> DragEnded;

        public void Dispose()
        {
            _surface.Pressed -= OnPressed;
            _surface.DragStarted -= OnDragStarted;
            _surface.Dragged -= OnDragged;
            _surface.Released -= OnReleased;
        }

        private void OnPressed(int pointer, Vector2 position)
        {
            if ((_pointer != NoPointer && _pointer != pointer) || _gate.IsBlocked) return;
            _pointer = pointer;
            _dragging = false;
            PressStarted?.Invoke(position);
        }

        private void OnDragStarted(int pointer, Vector2 pressPosition)
        {
            if (pointer != _pointer) return;
            _dragging = true;
            DragStarted?.Invoke(pressPosition);
        }

        private void OnDragged(int pointer, Vector2 position)
        {
            if (pointer == _pointer && _dragging) Dragged?.Invoke(position);
        }

        private void OnReleased(int pointer, Vector2 position)
        {
            if (pointer != _pointer) return;
            _pointer = NoPointer;
            PressEnded?.Invoke(position);
            if (_dragging) DragEnded?.Invoke(position);
            else if (!_gate.IsBlocked) Tapped?.Invoke(position);
        }
    }
}
