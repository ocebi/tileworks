using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;

namespace Tileworks.Tiles.Tests
{
    public sealed class EventPlayerTests
    {
        private EventTypeRegistry _registry;
        private ushort _fell;
        private ushort _cleared;
        private PresentationClock _clock;
        private Recorder _fellPresenter;
        private Recorder _clearedPresenter;
        private EventPlayer _player;

        [SetUp]
        public void SetUp()
        {
            _registry = new EventTypeRegistry();
            _fell = _registry.Register("Fell", EntityFields.Entity, preempts: true);
            _cleared = _registry.Register("Cleared", EntityFields.Entity);
            _clock = new PresentationClock();
            _fellPresenter = new Recorder(_fell);
            _clearedPresenter = new Recorder(_cleared);
            _player = new EventPlayer(_registry, _clock, new IEventPresenter[] { _fellPresenter, _clearedPresenter }, waveMode: true);
        }

        [Test]
        public void Register_PreemptingBarrier_Throws()
        {
            Assert.Throws<ArgumentException>(() => _registry.Register("Both", EntityFields.None, barrier: true, preempts: true));
            Assert.That(_registry.Get(_fell).Preempts, Is.True);
            Assert.That(_registry.Get(_cleared).Preempts, Is.False);
        }

        [Test]
        public void IsBusy_FollowsTheEntityLane()
        {
            _player.Play(new[] { Event(_cleared, 0, 7, 1f) });

            Assert.That(_player.IsBusy(7), Is.True);
            Assert.That(_player.IsBusy(8), Is.False);
            Assert.That(_player.IsBusy(-1), Is.False);
            Assert.That(_player.IsBusy(100000), Is.False);
            _clock.Advance(1f);
            Assert.That(_player.IsBusy(7), Is.False);
        }

        [Test]
        public void PlainEvents_QueueOnTheirLane()
        {
            _player.Play(new[] { Event(_cleared, 0, 7, 1f) });
            _clock.Advance(0.25f);
            _player.Play(new[] { Event(_cleared, 0, 7, 1f) });

            Assert.That(_clearedPresenter.Delays[1], Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(_player.IdleAt, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void Fall_PreemptsAFallStillInTheAir()
        {
            _player.Play(new[] { Event(_fell, 0, 7, 1f) });
            _clock.Advance(0.25f);
            _player.Play(new[] { Event(_fell, 0, 7, 0.5f) });

            Assert.That(_fellPresenter.Delays[1], Is.EqualTo(0f), "starts now, from wherever the piece is");
            Assert.That(_player.IdleAt, Is.EqualTo(0.75f).Within(1e-5f), "the lane is overwritten, even when it ends sooner");
            _clock.Advance(0.5f);
            Assert.That(_player.IsBusy(7), Is.False);
            Assert.That(_player.IsIdle, Is.True);
        }

        [Test]
        public void Fall_WaitsForAPlainEventOnItsLane()
        {
            _player.Play(new[] { Event(_cleared, 0, 7, 1f) });
            _clock.Advance(0.25f);
            _player.Play(new[] { Event(_fell, 0, 7, 0.5f) });

            Assert.That(_fellPresenter.Delays[0], Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void PlainEvent_WaitsForAFall()
        {
            _player.Play(new[] { Event(_fell, 0, 7, 1f) });
            _clock.Advance(0.25f);
            _player.Play(new[] { Event(_cleared, 0, 7, 0.5f) });

            Assert.That(_clearedPresenter.Delays[0], Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void Fall_WaitsForEveryEarlierStepOfItsOwnAction([Values(false, true)] bool waveMode)
        {
            _player = new EventPlayer(_registry, _clock, new IEventPresenter[] { _fellPresenter, _clearedPresenter }, waveMode);
            _player.Play(new[]
            {
                Event(_fell, 0, 7, 1f),
                Event(_cleared, 1, 8, 0.25f),
                Event(_fell, 2, 7, 0.5f),
            });

            Assert.That(_fellPresenter.Delays[1], Is.GreaterThanOrEqualTo(1f), "the second fall of one action never cuts the first short");
            Assert.That(_player.IdleAt, Is.GreaterThanOrEqualTo(1.5f));
        }

        [Test]
        public void TwoLegsOfOneFall_InOneStep_Queue()
        {
            _player.Play(new[] { Event(_fell, 1, 7, 0.5f), Event(_fell, 1, 7, 0.25f) });

            Assert.That(_fellPresenter.Delays[1], Is.EqualTo(0.5f).Within(1e-5f), "the slide follows the fall");
            Assert.That(_player.IdleAt, Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void Fall_PreemptsOnlyItsOwnLane()
        {
            _player.Play(new[] { Event(_fell, 0, 7, 1f), Event(_fell, 0, 8, 1f) });
            _clock.Advance(0.25f);
            _player.Play(new[] { Event(_fell, 0, 7, 0.25f) });

            Assert.That(_player.IdleAt, Is.EqualTo(1f).Within(1e-5f), "piece 8 is still falling");
            _clock.Advance(0.25f);
            Assert.That(_player.IsBusy(7), Is.False);
            Assert.That(_player.IsBusy(8), Is.True);
        }

        [Test]
        public void Reset_ForgetsWhichTypeHoldsALane()
        {
            _player.Play(new[] { Event(_fell, 0, 7, 1f) });
            _player.Reset();
            _player.Play(new[] { Event(_cleared, 0, 7, 1f) });
            _player.Play(new[] { Event(_fell, 0, 7, 1f) });

            Assert.That(_fellPresenter.Delays[1], Is.EqualTo(1f).Within(1e-5f));
        }

        // The duration rides in A as milliseconds.
        private static BoardEvent Event(ushort type, ushort step, int entity, float seconds) =>
            new BoardEvent(type, step, entity, (int)Math.Round(seconds * 1000f), 0, 0);

        private sealed class Recorder : IEventPresenter
        {
            public Recorder(ushort type) => EventType = type;

            public ushort EventType { get; }
            public List<float> Delays { get; } = new List<float>();

            public float Present(in BoardEvent e, float delay)
            {
                Delays.Add(delay);
                return e.A / 1000f;
            }
        }
    }
}
