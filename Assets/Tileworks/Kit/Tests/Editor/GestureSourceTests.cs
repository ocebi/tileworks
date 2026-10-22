using System;
using NUnit.Framework;
using Tileworks.Kit.Input;
using UnityEngine;

namespace Tileworks.Kit.Tests
{
    public sealed class GestureSourceTests
    {
        private sealed class FakeSurface : IPointerSurface
        {
            public event Action<int, Vector2> Pressed;
            public event Action<int, Vector2> DragStarted;
            public event Action<int, Vector2> Dragged;
            public event Action<int, Vector2> Released;

            public void Press(int pointer) => Pressed?.Invoke(pointer, Vector2.zero);
            public void Drag(int pointer)
            {
                DragStarted?.Invoke(pointer, Vector2.zero);
                Dragged?.Invoke(pointer, Vector2.one);
            }

            public void Release(int pointer) => Released?.Invoke(pointer, Vector2.one);
        }

        private FakeSurface _surface;
        private InputGate _gate;
        private GestureSource _source;
        private int _taps;
        private int _drags;

        [SetUp]
        public void SetUp()
        {
            _surface = new FakeSurface();
            _gate = new InputGate();
            _source = new GestureSource(_gate, _surface);
            _taps = 0;
            _drags = 0;
            _source.Tapped += _ => _taps++;
            _source.DragEnded += _ => _drags++;
        }

        [Test]
        public void PressAndRelease_IsATap()
        {
            _surface.Press(1);
            _surface.Release(1);

            Assert.That(_taps, Is.EqualTo(1));
        }

        [Test]
        public void PressThatDrags_IsADrag_NotATap()
        {
            _surface.Press(1);
            _surface.Drag(1);
            _surface.Release(1);

            Assert.That((_taps, _drags), Is.EqualTo((0, 1)));
        }

        [Test]
        public void SecondPointer_IsIgnoredWhileTheFirstIsDown()
        {
            _surface.Press(1);
            _surface.Press(2);
            _surface.Release(2);
            _surface.Release(1);

            Assert.That(_taps, Is.EqualTo(1));
        }

        [Test]
        public void BlockedGate_SwallowsTheTap()
        {
            _surface.Press(1);
            using (_gate.Block("test")) _surface.Release(1);

            Assert.That(_taps, Is.Zero);
        }

        [Test]
        public void Disposed_StopsListening()
        {
            _source.Dispose();
            _surface.Press(1);
            _surface.Release(1);

            Assert.That(_taps, Is.Zero);
        }
    }
}
