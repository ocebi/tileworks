using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Tileworks.Tiles.View
{
    public interface IEventPresenter
    {
        ushort EventType { get; }

        // Schedules tweens to start after `delay` and returns how long the lanes it touches stay busy.
        float Present(in BoardEvent e, float delay);
    }

    // Lane scheduler (§9.10.1): one lane per entity plus a global lane. An event starts when every lane it touches
    // is free; barriers wait for all lanes; in wave mode a step waits for the earlier steps to finish. A preempting
    // event doesn't wait for a lane an earlier action's preempting event holds (a fall redirects a fall still in the
    // air); it starts once the earlier steps of its own action have played, and takes the lane over. Within one action
    // a piece's legs queue as usual.
    public sealed class EventPlayer : IEventPlayer
    {
        private readonly EventTypeRegistry _registry;
        private readonly PresentationClock _clock;
        private readonly IEventPresenter[] _presenters;
        private readonly bool _waveMode;
        private readonly List<float> _stepEnds = new List<float>(8);
        private float[] _lanes = new float[256];
        private bool[] _preemptible = new bool[256];
        private int[] _laneOwner = new int[256];
        private int _play;
        private float _globalLane;
        private float _allLanes;

        public EventPlayer(EventTypeRegistry registry, PresentationClock clock, IReadOnlyList<IEventPresenter> presenters, bool waveMode = false)
        {
            _registry = Guard.NotNull(registry, nameof(registry));
            _clock = Guard.NotNull(clock, nameof(clock));
            _waveMode = waveMode;
            _presenters = new IEventPresenter[registry.Count];
            foreach (var presenter in presenters)
            {
                if (presenter.EventType >= _presenters.Length) throw new ArgumentException($"Presenter for unknown event type {presenter.EventType}.");
                if (_presenters[presenter.EventType] != null) throw new ArgumentException($"Two presenters for {registry.NameOf(presenter.EventType)}.");
                _presenters[presenter.EventType] = presenter;
            }
        }

        public float IdleAt => _allLanes;
        public bool IsIdle => _clock.Now >= _allLanes;
        public bool IsBusy(int entity) => _clock.Now < Lane(entity);

        public void Play(ReadOnlySpan<BoardEvent> events)
        {
            var now = _clock.Now;
            _play++;
            _stepEnds.Clear();
            for (var i = 0; i < events.Length; i++) Schedule(events[i], now);
        }

        public async UniTask WaitIdleAsync(CancellationToken ct)
        {
            while (!IsIdle) await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        public void Reset()
        {
            Array.Clear(_lanes, 0, _lanes.Length);
            Array.Clear(_preemptible, 0, _preemptible.Length);
            _globalLane = 0;
            _allLanes = 0;
        }

        private void Schedule(in BoardEvent e, float now)
        {
            var info = _registry.Get(e.Type);
            var start = info.Entities == EntityFields.None ? Math.Max(now, _globalLane) : Math.Max(now, LanesBusyUntil(e, info.Entities, info.Preempts));
            if (_waveMode || info.Preempts) start = Math.Max(start, EarlierStepsEnd(e.Step, now));
            if (info.Barrier) start = Math.Max(start, _allLanes);

            var presenter = _presenters[e.Type];
            var end = start + (presenter?.Present(e, start - now) ?? 0f);

            if (info.Entities == EntityFields.None || info.Barrier) _globalLane = Math.Max(_globalLane, end);
            if (info.Entities != EntityFields.None && OccupyLanes(e, info.Entities, end, info.Preempts)) _allLanes = AllLanesEnd();
            _allLanes = Math.Max(_allLanes, end);
            while (_stepEnds.Count <= e.Step) _stepEnds.Add(now);
            _stepEnds[e.Step] = Math.Max(_stepEnds[e.Step], end);
        }

        private float LanesBusyUntil(in BoardEvent e, EntityFields fields, bool preempts)
        {
            var busy = 0f;
            if ((fields & EntityFields.Entity) != 0) busy = Math.Max(busy, Lane(e.Entity, preempts));
            if ((fields & EntityFields.A) != 0) busy = Math.Max(busy, Lane(e.A, preempts));
            if ((fields & EntityFields.B) != 0) busy = Math.Max(busy, Lane(e.B, preempts));
            if ((fields & EntityFields.C) != 0) busy = Math.Max(busy, Lane(e.C, preempts));
            return busy;
        }

        // Every earlier step of this Play, not only the previous one: a step without events (a landing) leaves a gap. It
        // also keeps a preempting event from cutting short a fall from its own action.
        private float EarlierStepsEnd(int step, float now)
        {
            var end = now;
            for (var i = 0; i < step && i < _stepEnds.Count; i++) end = Math.Max(end, _stepEnds[i]);
            return end;
        }

        // True when a preempting event cut a lane short, so the player may go idle sooner than it said.
        private bool OccupyLanes(in BoardEvent e, EntityFields fields, float end, bool preempts)
        {
            var shortened = false;
            if ((fields & EntityFields.Entity) != 0) shortened |= SetLane(e.Entity, end, preempts);
            if ((fields & EntityFields.A) != 0) shortened |= SetLane(e.A, end, preempts);
            if ((fields & EntityFields.B) != 0) shortened |= SetLane(e.B, end, preempts);
            if ((fields & EntityFields.C) != 0) shortened |= SetLane(e.C, end, preempts);
            return shortened;
        }

        private float AllLanesEnd()
        {
            var end = _globalLane;
            for (var i = 0; i < _lanes.Length; i++) end = Math.Max(end, _lanes[i]);
            return end;
        }

        private float Lane(int entity) => entity >= 0 && entity < _lanes.Length ? _lanes[entity] : 0f;

        // A lane an earlier Play's preempting event holds is free to another preempting event.
        private float Lane(int entity, bool preempts) =>
            preempts && entity >= 0 && entity < _preemptible.Length && _preemptible[entity] && _laneOwner[entity] != _play ? 0f : Lane(entity);

        private bool SetLane(int entity, float end, bool preempts)
        {
            if (entity < 0) return false;
            if (entity >= _lanes.Length)
            {
                var size = Math.Max(entity + 1, _lanes.Length * 2);
                Array.Resize(ref _lanes, size);
                Array.Resize(ref _preemptible, size);
                Array.Resize(ref _laneOwner, size);
            }

            var shortened = end < _lanes[entity];
            _lanes[entity] = end;
            _preemptible[entity] = preempts;
            _laneOwner[entity] = _play;
            return shortened;
        }
    }
}
