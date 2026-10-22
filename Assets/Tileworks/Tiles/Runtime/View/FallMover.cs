using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    public interface IFallLandingHandler
    {
        // The piece came to rest on the last leg of its fall at `impact` cells per second.
        void Landed(int id, Transform piece, float impact);
    }

    // Moves falling pieces frame by frame on the presentation clock. Each piece keeps a queue of legs: a new fall for a
    // piece still in the air appends to it, so the piece keeps its position and speed. A piece never comes closer than
    // one cell to a falling or waiting piece below it in its column, and takes that piece's speed when it catches up. A
    // slide waits until every piece that started falling before it and still has to pass its target cell has reached
    // that cell and will be a cell past it when the slide gets there: in the rules those falls came first (often in an
    // earlier move), so in the view they go first too. Behind a piece sliding the same way into the same cell it starts a
    // cell back and goes no faster.
    // Positions are board-local, one cell per unit, rows going down in -y.
    public sealed class FallMover : ITickable
    {
        private const float Gap = 1f;
        private const float Column = 0.5f;

        // Below this a piece has reached a position: float error must not read as being held.
        private const float Near = 1e-3f;

        private readonly PresentationClock _clock;
        private readonly IFallLandingHandler _handler;
        private readonly List<FallRunner> _running = new List<FallRunner>(64);
        private readonly Dictionary<int, FallRunner> _byId = new Dictionary<int, FallRunner>(64);
        private readonly Stack<FallRunner> _pool = new Stack<FallRunner>(64);
        private readonly Dictionary<int, SpawnBatch> _batches = new Dictionary<int, SpawnBatch>();
        private float _last;
        private int _move;
        private int _legs;

        public FallMover(PresentationClock clock, IFallLandingHandler handler, FallMotion motion)
        {
            _clock = Guard.NotNull(clock, nameof(clock));
            _handler = Guard.NotNull(handler, nameof(handler));
            Motion = motion;
            _last = clock.Now;
        }

        public FallMotion Motion { get; set; }
        public int Count => _running.Count;
        public bool IsMoving(int id) => _byId.ContainsKey(id);

        // Queues a leg from `from` to `to` that starts no earlier than `delay` seconds from now. Returns the planned
        // seconds from now until the leg ends, without the follow rule's holdups, for the event player's lanes.
        public float Add(int id, Transform piece, Vector3 from, Vector3 to, bool lands, float delay)
        {
            var now = _clock.Now;
            if (!_byId.TryGetValue(id, out var runner))
            {
                runner = _pool.Count > 0 ? _pool.Pop() : new FallRunner();
                runner.Begin(id, piece, now, _move);
                _byId.Add(id, runner);
                _running.Add(runner);
            }

            var scale = _clock.Scale(1f);
            var requested = now + delay;
            var start = Mathf.Max(requested, runner.PlannedEnd);

            // A leg that waits for nothing but the legs before it follows on the moment they end, however their real
            // timing drifts from the plan; one that waits for something else (a clear under it) keeps its start.
            var follows = runner.Count > 0 && requested <= runner.PlannedEnd + Near;
            var slide = !Mathf.Approximately(from.x, to.x);
            float seconds;
            if (slide)
            {
                seconds = Motion.SlideTime(runner.PlannedExit);
                runner.PlannedExit = Motion.SlideSpeed(Motion.Entry(runner.PlannedExit));
            }
            else
            {
                seconds = Motion.FallTime(Mathf.Abs(from.y - to.y), runner.PlannedExit, out var exit);
                runner.PlannedExit = exit;
            }

            runner.PlannedEnd = start + seconds * scale;
            runner.Append(new FallLeg(to, slide, lands, follows ? float.NegativeInfinity : start, ++_legs));
            return runner.PlannedEnd - (now + delay);
        }

        // Call before each move's events play: new pieces of later moves enter behind this move's.
        public void NextMove()
        {
            _move++;
            foreach (var batch in _batches.Values) batch.Clear();
        }

        // Places a new piece at its spawn point `from`, all of this move's new pieces in a column lifted together, so the
        // lowest stays a cell above every piece an earlier move still has falling there (in the rules new pieces are the
        // topmost of their column) and their spacing by drop height is kept. Returns where the piece now waits.
        public Vector3 Spawn(Transform piece, Vector3 from)
        {
            var column = Mathf.RoundToInt(from.x * 2f);
            if (!_batches.TryGetValue(column, out var batch)) _batches.Add(column, batch = new SpawnBatch());
            var floor = float.NegativeInfinity;
            for (var i = 0; i < _running.Count; i++)
            {
                var other = _running[i];
                if (other.Move < _move && Mathf.Abs(other.Position.x - from.x) <= Column) floor = Mathf.Max(floor, other.Position.y + Gap);
            }

            var lift = Mathf.Max(batch.Lift, floor - from.y);
            if (lift > batch.Lift)
            {
                foreach (var placed in batch.Pieces)
                    if (placed != null) placed.localPosition += Vector3.up * (lift - batch.Lift);
                batch.Lift = lift;
            }

            from.y += batch.Lift;
            piece.localPosition = from;
            batch.Pieces.Add(piece);
            return from;
        }

        // The piece leaves the board or is taken over by another animation: it stops where it is.
        public void Stop(int id)
        {
            if (!_byId.TryGetValue(id, out var runner)) return;
            Release(runner);
        }

        public void Clear()
        {
            while (_running.Count > 0) Release(_running[_running.Count - 1]);
            _last = _clock.Now;
        }

        public void Tick()
        {
            var now = _clock.Now;
            var elapsed = now - _last;
            _last = now;
            if (elapsed <= 0f || _running.Count == 0) return;
            var motionTime = elapsed / _clock.Scale(1f);
            SortLowestFirst();
            for (var i = 0; i < _running.Count; i++)
            {
                var runner = _running[i];

                // A piece the board removed mid-fall simply leaves.
                if (runner.Piece == null)
                {
                    Release(runner);
                    i--;
                    continue;
                }

                if (Move(runner, now, elapsed, motionTime)) continue;
                var (id, piece, impact, lands) = (runner.Id, runner.Piece, runner.Speed, runner.LandsAtEnd);
                Release(runner);
                i--;
                if (lands) _handler.Landed(id, piece, impact);
            }
        }

        // Advances one piece by this frame; false once its last leg is done. Until its first leg starts the piece is left
        // to whatever else animates it (a swap), and the fall starts from wherever it then is.
        private bool Move(FallRunner r, float now, float elapsed, float motionTime)
        {
            var frameStart = now - elapsed;
            var time = motionTime;
            if (!r.Moving) r.Position = r.Piece.localPosition;
            while (r.Count > 0)
            {
                ref var leg = ref r.Head;
                if (!leg.Started)
                {
                    var lead = float.PositiveInfinity;
                    if (leg.StartAt >= now || leg.Slide && Crossed(r, leg, out lead)) break;
                    if (leg.StartAt > frameStart) time = Mathf.Min(time, (now - leg.StartAt) / _clock.Scale(1f));
                    leg.Started = true;
                    r.Moving = true;
                    r.Speed = leg.Slide ? Mathf.Min(Motion.SlideSpeed(Motion.Entry(r.Speed)), lead) : Motion.Entry(r.Speed);
                }

                if (time <= 0f) break;
                time = leg.Slide ? Slide(r, leg, time) : Fall(r, leg, time);
                if (time < 0f) break;
                r.LandsAtEnd = leg.Lands;
                r.Pop();
            }

            if (r.Moving) r.Piece.localPosition = r.Position;
            return r.Count > 0;
        }

        // Moves along a straight leg; returns the time left once it arrives, or -1 while still on the way. A piece below
        // in the column holds it a cell above; catching up, it takes that piece's speed.
        private float Fall(FallRunner r, in FallLeg leg, float time)
        {
            var below = Below(r, out var held);
            var left = r.Position.y - leg.To.y;
            var room = r.Position.y - Mathf.Max(leg.To.y, below + Gap);
            var distance = Motion.Advance(r.Speed, time, out var end);
            if (room < left - Near && distance >= room)
            {
                r.Position.y -= Mathf.Max(0f, room);
                r.Speed = Mathf.Min(end, held);
                return -1f;
            }

            if (distance < left)
            {
                r.Position.y -= distance;
                r.Speed = end;
                return -1f;
            }

            var used = Motion.FallTime(left, r.Speed, out var exit);
            r.Position = leg.To;
            r.Speed = exit;
            return Mathf.Max(0f, time - used);
        }

        private float Slide(FallRunner r, in FallLeg leg, float time)
        {
            var offset = leg.To - r.Position;
            var left = offset.magnitude;
            var distance = r.Speed * time;
            if (distance < left)
            {
                r.Position += offset * (distance / left);
                return -1f;
            }

            r.Position = leg.To;
            return time - left / r.Speed;
        }

        // Whether a piece on a leg queued before the slide still has to pass its target cell, and either hasn't reached
        // it or won't be a cell below it when the slide gets there: in the rules that fall came first, so in the view it
        // goes first too. One walk up the target column (the runners are sorted lowest first): each piece is held a cell
        // above where the piece under it will be. A piece a cell ahead sliding the same way into the cell leads instead:
        // `lead` is its speed.
        private bool Crossed(FallRunner r, in FallLeg slide, out float lead)
        {
            var cell = slide.To;
            lead = float.PositiveInfinity;
            var seconds = Vector3.Distance(r.Position, cell) / Motion.SlideSpeed(Motion.Entry(r.Speed));
            var under = float.NegativeInfinity;
            for (var i = 0; i < _running.Count; i++)
            {
                var other = _running[i];
                if (other == r || Mathf.Abs(other.Position.x - cell.x) > Column) continue;
                var after = Mathf.Max(After(other, seconds), under + Gap);
                under = after;
                if (other.Count == 0 || !other.PassesBelow(cell.y - Near, slide.Seq)) continue;
                if (Leads(other, r, cell))
                {
                    lead = Mathf.Min(lead, other.Speed);
                    continue;
                }

                if (other.Position.y > cell.y + Near || after > cell.y - Gap + Near) return true;
            }

            return false;
        }

        // A follower starts only a full cell behind a piece sliding the same way into the same cell, and never faster than
        // it; a piece sliding in from the other side or falling through the cell still blocks.
        private static bool Leads(FallRunner other, FallRunner r, Vector3 cell)
        {
            ref readonly var leg = ref other.Head;
            return leg.Started && leg.Slide && Vector3.Distance(leg.To, cell) < Near && (other.Position.x - cell.x) * (r.Position.x - cell.x) > 0f &&
                   Vector3.Distance(other.Position, r.Position) >= Gap - Near;
        }

        // Where a piece's fall takes it in `seconds`, no further than the end of its current leg; a piece that is sliding
        // or waiting to start stays put.
        private float After(FallRunner r, float seconds)
        {
            if (r.Count == 0) return r.Position.y;
            ref readonly var leg = ref r.Head;
            if (!leg.Started || leg.Slide) return r.Position.y;
            return Mathf.Max(r.Position.y - Motion.Advance(r.Speed, seconds, out _), leg.To.y);
        }

        // The highest piece under this one in its column, and its speed; -infinity when none.
        private float Below(FallRunner r, out float speed)
        {
            var top = float.NegativeInfinity;
            speed = 0f;
            for (var i = 0; i < _running.Count; i++)
            {
                var other = _running[i];
                if (other == r || Mathf.Abs(other.Position.x - r.Position.x) > Column || other.Position.y >= r.Position.y || other.Position.y <= top) continue;
                top = other.Position.y;
                speed = other.Speed;
            }

            return top;
        }

        // The lowest piece moves first, so the one above it follows its new position.
        private void SortLowestFirst()
        {
            for (var i = 1; i < _running.Count; i++)
            {
                var runner = _running[i];
                var j = i - 1;
                while (j >= 0 && _running[j].Position.y > runner.Position.y)
                {
                    _running[j + 1] = _running[j];
                    j--;
                }

                _running[j + 1] = runner;
            }
        }

        private void Release(FallRunner runner)
        {
            _running.Remove(runner);
            _byId.Remove(runner.Id);
            runner.Piece = null;
            _pool.Push(runner);
        }

        // One move's new pieces in one column and how far they were lifted.
        private sealed class SpawnBatch
        {
            public readonly List<Transform> Pieces = new List<Transform>();
            public float Lift;

            public void Clear()
            {
                Pieces.Clear();
                Lift = 0f;
            }
        }
    }
}
