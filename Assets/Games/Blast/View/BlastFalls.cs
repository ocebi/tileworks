using System.Collections.Generic;
using PrimeTween;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Blast.View
{
    // Falling pieces move frame by frame (FallMover): a fall for a piece still in the air continues from where it is. On
    // its last leg a piece squashes by how hard it hit and plays the land cue; a short gap later, unless it has started
    // falling again, it is reported to the controller as landed, so what the landing sets off pops out of the squash.
    public sealed class BlastFalls : IFallLandingHandler
    {
        private readonly BlastBoardView _board;
        private readonly PresentationClock _clock;
        private readonly FallMover _mover;
        private readonly Dictionary<int, int> _landsOn = new Dictionary<int, int>(64);
        private readonly Stack<Report> _reports = new Stack<Report>(16);

        public BlastFalls(BlastBoardView board, PresentationClock clock)
        {
            _board = board;
            _clock = clock;
            _mover = new FallMover(clock, this, board.Feel.Motion);
        }

        public bool IsMoving(int id) => _mover.IsMoving(id);
        public int Count => _mover.Count;

        // Presentation clock time the last landing squash ends.
        public float SquashUntil { get; private set; }

        // Queues a leg after `delay`; returns the seconds after `delay` its lane stays busy: to touch-down, plus the gap
        // before the landing is reported.
        public float Fall(int id, SpriteRenderer piece, Vector3 from, Vector3 to, int cell, bool lands, float delay)
        {
            _mover.Motion = _board.Feel.Motion;
            _board.Lift(piece);
            var seconds = _mover.Add(id, piece.transform, from, to, lands, delay);
            if (!lands) return seconds;
            _landsOn[id] = cell;
            return seconds + _clock.Scale(_board.Feel.LandClearGap);
        }

        public void Tick() => _mover.Tick();

        // Called before each move's events play, so new pieces of a later move enter behind this one's.
        public void NextMove() => _mover.NextMove();

        // Places a new piece at its spawn point, this move's new pieces in the column lifted together above every piece an
        // earlier move still has falling there; returns where it waits.
        public Vector3 Spawn(SpriteRenderer piece, Vector3 from) => _mover.Spawn(piece.transform, from);

        public void Stop(int id)
        {
            _mover.Stop(id);
            _landsOn.Remove(id);
        }

        public void Clear()
        {
            _mover.Clear();
            _landsOn.Clear();
        }

        void IFallLandingHandler.Landed(int id, Transform piece, float impact)
        {
            var feel = _board.Feel;
            var land = _clock.Scale(feel.LandDuration);
            SquashUntil = Mathf.Max(SquashUntil, _clock.Now + Mathf.Max(land, _clock.Scale(feel.LandClearGap)));
            var full = _mover.Motion.Squash(feel.LandSquash, impact);
            // Two plain tweens rather than a sequence, so a tap's refusal or a clear can stop them mid-squash.
            var squash = new Vector3(full.x, full.y, 1f);
            Tween.Scale(piece, squash, land * 0.35f, Ease.OutQuad);
            Tween.Scale(piece, squash, Vector3.one, land * 0.65f, Ease.OutBack, startDelay: land * 0.35f);
            _board.Sfx.Play(BlastCue.Land);
            if (piece.TryGetComponent<SpriteRenderer>(out var renderer)) _board.Land(renderer);
            if (!_landsOn.Remove(id, out var cell)) return;
            var report = _reports.Count > 0 ? _reports.Pop() : new Report(this);
            report.Set(id, cell);
            Tween.Delay(report, _clock.Scale(feel.LandClearGap), r => r.Send(), useUnscaledTime: true);
        }

        private void Send(Report report)
        {
            _reports.Push(report);
            if (!_mover.IsMoving(report.Id)) _board.SendLanded(report.Id, report.Cell);
        }

        private sealed class Report
        {
            private readonly BlastFalls _falls;

            public Report(BlastFalls falls) => _falls = falls;

            public int Id { get; private set; }
            public int Cell { get; private set; }

            public void Set(int id, int cell)
            {
                Id = id;
                Cell = cell;
            }

            public void Send() => _falls.Send(this);
        }
    }
}
