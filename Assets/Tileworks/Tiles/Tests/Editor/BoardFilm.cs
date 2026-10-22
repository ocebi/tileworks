using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PrimeTween;
using UnityEngine;

namespace Tileworks.Tiles.Tests
{
    // Steps a board's tweens by hand, a frame at a time, and records every piece, so a test can check what a player sees:
    // no piece jumps, nothing falls into a cell before the piece clearing there is gone, and the board is still once idle.
    // Positions are board-local, one cell per unit.
    public sealed class BoardFilm
    {
        public const float FrameTime = 1f / 120f;

        // Faster than any fall or slide a board plays, in cells per frame.
        private const float MaxStep = 0.75f;

        // Closer than any two pieces come when one slides past another's corner (0.71).
        private const float MinApart = 0.6f;
        private const float Near = 0.02f;

        private static readonly MethodInfo ManualUpdate = typeof(PrimeTweenConfig).GetMethod("ManualUpdate", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly object[] FrameArgs = { UpdateType.Update, (float?)FrameTime, (float?)FrameTime };

        private readonly Func<int, SpriteRenderer> _piece;
        private readonly Func<int, Vector3> _cell;
        private readonly int _width;
        private readonly Dictionary<int, List<Shot>> _shots = new Dictionary<int, List<Shot>>();
        private readonly List<Leg> _falls = new List<Leg>();
        private readonly List<Leg> _clears = new List<Leg>();
        private readonly HashSet<int> _free = new HashSet<int>();
        private float _idleAt;
        private int _samples;

        public BoardFilm(Func<int, SpriteRenderer> piece, Func<int, Vector3> cell, int width)
        {
            _piece = piece;
            _cell = cell;
            _width = width;
        }

        public float Length { get; private set; }

        // Starts a move: call before its events play, so a piece moved while they are scheduled shows as a jump.
        public void Begin(IEnumerable<int> ids)
        {
            _shots.Clear();
            _falls.Clear();
            _clears.Clear();
            _free.Clear();
            Length = 0f;
            _samples = 0;
            foreach (var id in ids) _shots[id] = new List<Shot>();
            Sample();
        }

        // A piece that moves on a path of its own (a swap, a shuffle, a special flying off) and may cross others.
        public void Free(int id) => _free.Add(id);

        // A piece that appears after Begin, such as one spawned by a later move.
        public void Track(int id)
        {
            if (_shots.ContainsKey(id)) return;
            var shots = new List<Shot>();
            for (var i = 0; i < _samples; i++) shots.Add(new Shot(0f, false, default, 0f));
            _shots[id] = shots;
        }

        // Facts come in the model's event order: a fall must wait for every clear the model made before it on its path.
        // One leg of a piece's way down; `from` is -1 for a piece that drops in from above the board.
        public void Fell(int id, int from, int to) => _falls.Add(new Leg(id, from, to, _falls.Count + _clears.Count));

        public void Cleared(int id, int cell) => _clears.Add(new Leg(id, cell, cell, _falls.Count + _clears.Count));

        // Plays until `idleAt` (seconds from Begin, when the board says it is done) and a little longer.
        public void Run(float idleAt, float tail = 0.5f)
        {
            _idleAt = idleAt;
            for (var frames = Mathf.CeilToInt((idleAt + tail) / FrameTime); frames > 0; frames--) Frame(null);
        }

        // Plays a board whose moves go on while it plays (landings reported by the view set off clears): `frame` runs at
        // the start of every frame with its length (advance the clock, tick the movers), until `idle` holds; then `tail`
        // longer, in which nothing may change. Fails past `limit` seconds.
        public void Run(Func<bool> idle, Action<float> frame, float tail = 0.5f, float limit = 30f)
        {
            while (!idle())
            {
                if (Length > limit) throw new InvalidOperationException($"the board was still busy after {limit} s");
                Frame(frame);
            }

            _idleAt = Length;
            for (var frames = Mathf.CeilToInt(tail / FrameTime); frames > 0; frames--) Frame(frame);
        }

        private void Frame(Action<float> frame)
        {
            frame?.Invoke(FrameTime);
            ManualUpdate.Invoke(null, FrameArgs);
            Length += FrameTime;
            Sample();
        }

        public IEnumerable<string> Problems() => Jumps().Concat(EarlyFalls()).Concat(Leftovers()).Concat(Unsettled());

        // Each piece the board should hold sits on its cell at full size, upright.
        public IEnumerable<string> Misplaced(IEnumerable<(int Id, int Cell)> pieces)
        {
            foreach (var (id, cell) in pieces)
            {
                var renderer = _piece(id);
                if (renderer == null || !Shown(renderer))
                {
                    yield return $"piece {id} at {Where(cell)} is missing";
                    continue;
                }

                var t = renderer.transform;
                if (Vector3.Distance(t.localPosition, _cell(cell)) > Near || Mathf.Abs(t.localScale.x - 1f) > Near || Mathf.Abs(Mathf.DeltaAngle(t.localEulerAngles.z, 0f)) > 1f)
                    yield return $"piece {id} rests at {t.localPosition} (scale {t.localScale.x:0.00}, {t.localEulerAngles.z:0}°) instead of {Where(cell)}";
            }
        }

        // No two pieces that only ever fall come within a fraction of a cell of each other where it shows: one passing
        // through another shows up here. An overlap above the board's top edge is hidden by its mask (new pieces wait
        // there), and cleared and free pieces are left out.
        public IEnumerable<string> Overlaps()
        {
            var top = _cell(0).y + 0.5f;
            var cleared = new HashSet<int>(_clears.Select(c => c.Id));
            var ids = _shots.Keys.Where(id => !_free.Contains(id) && !cleared.Contains(id)).OrderBy(id => id).ToArray();
            for (var a = 0; a < ids.Length; a++)
            for (var b = a + 1; b < ids.Length; b++)
            {
                var first = _shots[ids[a]];
                var second = _shots[ids[b]];
                for (var i = 0; i < Math.Min(first.Count, second.Count); i++)
                {
                    var p = first[i];
                    var q = second[i];
                    if (!p.Shown || !q.Shown || p.Scale < 0.95f || q.Scale < 0.95f) continue;
                    if (Mathf.Max(p.Position.y, q.Position.y) - 0.5f >= top - Near) continue;
                    if (Vector3.Distance(p.Position, q.Position) >= MinApart) continue;
                    yield return $"pieces {ids[a]} and {ids[b]} overlap at {p.Time:0.000}s, at {p.Position} and {q.Position}";
                    break;
                }
            }
        }

        private IEnumerable<string> Jumps()
        {
            foreach (var pair in _shots)
            {
                var shots = pair.Value;
                for (var i = 1; i < shots.Count; i++)
                {
                    if (!shots[i].Shown || !shots[i - 1].Shown) continue;
                    var step = Vector3.Distance(shots[i].Position, shots[i - 1].Position);
                    if (step <= MaxStep) continue;
                    yield return $"piece {pair.Key} jumped {step:0.00} cells at {shots[i].Time:0.000}s";
                    break;
                }
            }
        }

        private IEnumerable<string> EarlyFalls()
        {
            foreach (var fall in _falls)
            {
                var (started, at) = Started(fall);
                if (float.IsNaN(started)) continue;
                foreach (var clear in _clears)
                {
                    if (clear.Order > fall.Order || !OnPath(fall, clear.From)) continue;
                    var gone = Gone(clear.Id);
                    if (gone - started > FrameTime * 0.5f)
                        yield return $"piece {fall.Id} fell {Where(fall.From)}→{Where(fall.To)} from {at} at {started:0.000}s, {(gone - started) * 1000f:0} ms before piece {clear.Id} at {Where(clear.From)} was gone";
                }
            }
        }

        private IEnumerable<string> Leftovers() =>
            _clears.Where(c => _shots.TryGetValue(c.Id, out var shots) && shots[shots.Count - 1].Shown).Select(c => $"piece {c.Id} cleared at {Where(c.From)} is still showing");

        private IEnumerable<string> Unsettled()
        {
            foreach (var pair in _shots)
            {
                var shots = pair.Value;
                var idle = shots.FindIndex(s => s.Time >= _idleAt);
                if (idle < 0) continue;
                for (var i = idle + 1; i < shots.Count; i++)
                {
                    if (shots[i].Shown == shots[idle].Shown && (!shots[i].Shown || (Vector3.Distance(shots[i].Position, shots[idle].Position) <= 0.01f &&
                                                                                    Mathf.Abs(shots[i].Scale - shots[idle].Scale) <= 0.01f))) continue;
                    yield return $"piece {pair.Key} still changing {(shots[i].Time - _idleAt) * 1000f:0} ms after the board went idle";
                    break;
                }
            }
        }

        // When the piece first shows on the leg: once it has been on its start cell, past it towards the end one; for a new
        // piece, inside the board.
        private (float Time, Vector3 At) Started(Leg fall)
        {
            if (!_shots.TryGetValue(fall.Id, out var shots)) return (float.NaN, default);
            var to = _cell(fall.To);
            var top = _cell(fall.To % _width).y + 0.5f;
            var from = fall.From < 0 ? default : _cell(fall.From);
            var span = Vector3.Distance(from, to);
            var reached = fall.From < 0;
            foreach (var shot in shots)
            {
                if (!shot.Shown) continue;
                reached |= Vector3.Distance(shot.Position, from) < 0.05f;
                if (reached && (fall.From < 0 ? shot.Position.y < top : Vector3.Distance(shot.Position, to) < span - 0.05f)) return (shot.Time, shot.Position);
            }

            return (float.NaN, default);
        }

        // The first time after which the piece no longer shows.
        private float Gone(int id)
        {
            if (!_shots.TryGetValue(id, out var shots)) return 0f;
            for (var i = shots.Count - 1; i >= 0; i--)
                if (shots[i].Shown) return i + 1 < shots.Count ? shots[i + 1].Time : float.PositiveInfinity;
            return 0f;
        }

        // The cells a leg passes through or ends on.
        private bool OnPath(Leg fall, int cell)
        {
            if (cell % _width != fall.To % _width) return false;
            var row = cell / _width;
            if (fall.From < 0) return row <= fall.To / _width;
            if (fall.From % _width != fall.To % _width) return cell == fall.To;
            return row > fall.From / _width && row <= fall.To / _width;
        }

        private void Sample()
        {
            _samples++;
            foreach (var pair in _shots)
            {
                var renderer = _piece(pair.Key);
                pair.Value.Add(renderer == null
                    ? new Shot(Length, false, default, 0f)
                    : new Shot(Length, Shown(renderer), renderer.transform.localPosition, renderer.transform.localScale.x));
            }
        }

        private static bool Shown(SpriteRenderer r) => r.enabled && r.gameObject.activeInHierarchy && r.color.a > 0.05f && Mathf.Abs(r.transform.localScale.x) > 0.05f;

        private string Where(int cell) => cell < 0 ? "above" : $"({cell % _width},{cell / _width})";

        private readonly struct Shot
        {
            public readonly float Time;
            public readonly bool Shown;
            public readonly Vector3 Position;
            public readonly float Scale;

            public Shot(float time, bool shown, Vector3 position, float scale)
            {
                Time = time;
                Shown = shown;
                Position = position;
                Scale = scale;
            }
        }

        private readonly struct Leg
        {
            public readonly int Id;
            public readonly int From;
            public readonly int To;
            public readonly int Order;

            public Leg(int id, int from, int to, int order)
            {
                Id = id;
                From = from;
                To = to;
                Order = order;
            }
        }
    }
}
