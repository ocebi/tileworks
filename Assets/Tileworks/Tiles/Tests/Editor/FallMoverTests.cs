using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Tileworks.Tiles.Tests
{
    public sealed class FallMoverTests
    {
        private const float Frame = 1f / 120f;
        private static readonly FallMotion Motion = new FallMotion(4f, 60f, 24f, 8f, 12f, 0.35f);

        private PresentationClock _clock;
        private Landings _landings;
        private FallMover _mover;
        private readonly List<GameObject> _pieces = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _clock = new PresentationClock();
            _landings = new Landings();
            _mover = new FallMover(_clock, _landings, Motion);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var piece in _pieces) Object.DestroyImmediate(piece);
            _pieces.Clear();
        }

        [TestCase(1f, 0.13f)]
        [TestCase(4f, 0.30f)]
        [TestCase(9f, 0.51f)]
        public void FallTimes_FromRest_MatchTheSpec(float rows, float seconds)
        {
            Assert.That(Motion.FallTime(rows), Is.EqualTo(seconds).Within(0.01f));
        }

        [Test]
        public void Advance_AndFallTime_Agree()
        {
            foreach (var entry in new[] { 0f, 6f, 30f })
            foreach (var rows in new[] { 0.5f, 2f, 7f })
            {
                var t = Motion.FallTime(rows, entry, out var exit);
                Assert.That(Motion.Advance(entry, t, out var end), Is.EqualTo(rows).Within(1e-3f), $"entry {entry}, rows {rows}");
                Assert.That(end, Is.EqualTo(exit).Within(1e-3f));
            }
        }

        [Test]
        public void ALandingSquashesMore_TheHarderItHits()
        {
            var full = new Vector2(1.2f, 0.8f);
            Assert.That(Motion.Squash(full, Motion.MaxSpeed), Is.EqualTo(full));
            Assert.That(Motion.Squash(full, 0f).y, Is.EqualTo(Mathf.Lerp(1f, 0.8f, 0.35f)).Within(1e-5f));
        }

        [Test]
        public void AFall_EndsOnItsCell_WhenPlanned_AndLandsOnce()
        {
            var piece = Piece(0f, 0f);
            var planned = _mover.Add(1, piece, new Vector3(0f, 0f), new Vector3(0f, -4f), true, 0.1f);

            var trace = Run(piece, 1f);

            Assert.That(planned, Is.EqualTo(Motion.FallTime(4f)).Within(1e-4f), "the leg's own time, after its delay");
            Assert.That(piece.localPosition, Is.EqualTo(new Vector3(0f, -4f)));
            Assert.That(_landings.Ids, Is.EqualTo(new[] { 1 }));
            Assert.That(_landings.At[0], Is.EqualTo(0.1f + planned).Within(Frame * 1.5f));
            Assert.That(_landings.Impact[0], Is.GreaterThan(Motion.StartSpeed));
            AssertSmoothDownward(trace);
            Assert.That(_mover.Count, Is.Zero);
        }

        [Test]
        public void AFallExtendedMidAir_KeepsItsSpeed_AndLandsOnlyAtTheEnd()
        {
            var piece = Piece(0f, 0f);
            var first = _mover.Add(1, piece, new Vector3(0f, 0f), new Vector3(0f, -3f), true, 0f);
            var trace = Run(piece, first * 0.7f);
            var speed = trace[trace.Count - 1].y - trace[trace.Count - 2].y;

            var more = _mover.Add(1, piece, new Vector3(0f, -3f), new Vector3(0f, -6f), true, 0f);
            var extended = trace.Count;
            trace.AddRange(Run(piece, 1f).Skip(1));

            Assert.That(more, Is.GreaterThan(first * 0.3f), "the plan runs to the end of the new leg");
            Assert.That(trace[extended].y - trace[extended - 1].y, Is.LessThanOrEqualTo(speed + 1e-4f), "no restart from rest");
            Assert.That(piece.localPosition, Is.EqualTo(new Vector3(0f, -6f)));
            Assert.That(_landings.Ids, Is.EqualTo(new[] { 1 }), "no landing at the old target");
            AssertSmoothDownward(trace);
            AssertNeverStops(trace, -6f);
        }

        [Test]
        public void APieceAbove_NeverOvertakesOrTouches_ThePieceBelow()
        {
            var lower = Piece(0f, -2f);
            var upper = Piece(0f, 0f);
            _mover.Add(1, lower, lower.localPosition, new Vector3(0f, -7f), true, 0.2f);
            _mover.Add(2, upper, upper.localPosition, new Vector3(0f, -6f), true, 0f);

            for (var t = 0f; t < 1.5f; t += Frame)
            {
                Step();
                Assert.That(upper.localPosition.y - lower.localPosition.y, Is.GreaterThanOrEqualTo(1f - 1e-4f), $"at {t:0.000}s");
            }

            Assert.That((lower.localPosition.y, upper.localPosition.y), Is.EqualTo((-7f, -6f)));
            Assert.That(_landings.Ids, Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public void ASlide_RunsAtAnEvenClampedPace_ThenTheFallContinues()
        {
            var piece = Piece(0f, 0f);
            _mover.Add(1, piece, new Vector3(0f, 0f), new Vector3(1f, -1f), false, 0f);
            var planned = _mover.Add(1, piece, new Vector3(1f, -1f), new Vector3(1f, -3f), true, 0f);

            var trace = Run(piece, 1f);

            Assert.That(piece.localPosition, Is.EqualTo(new Vector3(1f, -3f)));
            var slideFrames = 0;
            for (var i = 1; i < trace.Count && trace[i].x < 1f; i++, slideFrames++)
                Assert.That(Vector3.Distance(trace[i], trace[i - 1]), Is.EqualTo(Motion.SlideMinSpeed * Frame).Within(1e-3f));
            Assert.That(slideFrames, Is.GreaterThan(5));
            Assert.That(_landings.At[0], Is.EqualTo(planned).Within(Frame * 1.5f));
        }

        // A piece one cell above a waiting piece at the end of its fall turns into its slide without slowing down.
        [Test]
        public void ATurn_OverAWaitingPiece_KeepsTheSpeed()
        {
            var waiting = Piece(0f, -3f);
            var piece = Piece(0f, 0f);
            _mover.Add(1, waiting, waiting.localPosition, new Vector3(0f, -5f), true, 10f);
            _mover.Add(2, piece, piece.localPosition, new Vector3(0f, -2f), false, 0f);
            _mover.Add(2, piece, new Vector3(0f, -2f), new Vector3(1f, -3f), true, 0f);

            var trace = Run(piece, 1f);

            Assert.That(piece.localPosition, Is.EqualTo(new Vector3(1f, -3f)));
            var fast = trace.FindIndex(p => p.y < -1f);
            for (var i = fast + 1; i < trace.Count && trace[i] != piece.localPosition; i++)
                Assert.That(Vector3.Distance(trace[i], trace[i - 1]), Is.GreaterThan(Motion.SlideMinSpeed * Frame * 0.9f), $"slowed at frame {i}, {trace[i]}");
        }

        // The next leg starts the moment the piece arrives, even when it arrives ahead of the plan.
        [Test]
        public void ALegQueuedBehindAnother_FollowsOnAtOnce_WhenThePieceIsAheadOfThePlan()
        {
            var piece = Piece(0f, 0f);
            _mover.Add(1, piece, piece.localPosition, new Vector3(0f, -2f), false, 0f);
            _mover.Add(1, piece, new Vector3(0f, -2f), new Vector3(1f, -3f), false, 0f);
            _mover.Add(1, piece, new Vector3(1f, -3f), new Vector3(1f, -5f), true, 0f);
            _clock.Speed = 2f;

            var trace = Run(piece, 1f);

            Assert.That(piece.localPosition, Is.EqualTo(new Vector3(1f, -5f)));
            AssertNeverStops(trace, -5f);
            for (var i = 2; i < trace.Count && trace[i] != piece.localPosition; i++)
                Assert.That(Vector3.Distance(trace[i], trace[i - 1]), Is.GreaterThan(Motion.StartSpeed * 2f * Frame), $"slowed at frame {i}, {trace[i]}");
        }

        // A piece sliding into a cell waits for a piece that started falling earlier to pass through that cell.
        [Test]
        public void ASlide_WaitsForAnEarlierFallThroughItsTarget()
        {
            var faller = Piece(1f, 2f);
            var slider = Piece(0f, 0f);
            _mover.Add(1, faller, faller.localPosition, new Vector3(1f, -3f), true, 0f);
            _mover.Add(2, slider, slider.localPosition, new Vector3(1f, -1f), true, 0f);

            var closest = float.MaxValue;
            for (var t = 0f; t < 1.5f; t += Frame)
            {
                Step();
                closest = Mathf.Min(closest, Vector3.Distance(faller.localPosition, slider.localPosition));
            }

            Assert.That(closest, Is.GreaterThan(0.99f), "the two pieces overlapped");
            Assert.That((faller.localPosition, slider.localPosition), Is.EqualTo((new Vector3(1f, -3f), new Vector3(1f, -1f))));
            Assert.That(_landings.Ids, Is.EquivalentTo(new[] { 1, 2 }));
        }

        // Pieces queued at a pen opening slide in one after another without stopping on their cell: each slide starts as
        // soon as the piece ahead has reached the opening and will be a cell past it when the slide gets there.
        [Test]
        public void PiecesQueuedAtAnOpening_SlideInOneAfterAnother_WithoutHolding()
        {
            var first = Piece(1f, 0f);
            var second = Piece(1f, 1f);
            var third = Piece(1f, 2f);
            _mover.Add(1, first, first.localPosition, new Vector3(0f, -1f), false, 0f);
            _mover.Add(1, first, new Vector3(0f, -1f), new Vector3(0f, -4f), true, 0f);
            _mover.Add(2, second, second.localPosition, new Vector3(1f, 0f), false, 0f);
            _mover.Add(3, third, third.localPosition, new Vector3(1f, 1f), false, 0f);
            _mover.Add(2, second, new Vector3(1f, 0f), new Vector3(0f, -1f), false, 0f);
            _mover.Add(2, second, new Vector3(0f, -1f), new Vector3(0f, -3f), true, 0f);
            _mover.Add(3, third, new Vector3(1f, 1f), new Vector3(1f, 0f), false, 0f);
            _mover.Add(3, third, new Vector3(1f, 0f), new Vector3(0f, -1f), false, 0f);
            _mover.Add(3, third, new Vector3(0f, -1f), new Vector3(0f, -2f), true, 0f);

            var pieces = new[] { first, second, third };
            var traces = pieces.Select(p => new List<Vector3> { p.localPosition }).ToArray();
            var closest = float.MaxValue;
            for (var t = 0f; t < 1.5f; t += Frame)
            {
                Step();
                for (var i = 0; i < pieces.Length; i++)
                {
                    traces[i].Add(pieces[i].localPosition);
                    for (var j = i + 1; j < pieces.Length; j++) closest = Mathf.Min(closest, Vector3.Distance(pieces[i].localPosition, pieces[j].localPosition));
                }
            }

            Assert.That(pieces.Select(p => p.localPosition), Is.EqualTo(new[] { new Vector3(0f, -4f), new Vector3(0f, -3f), new Vector3(0f, -2f) }));
            Assert.That(closest, Is.GreaterThan(0.99f), "two pieces came closer than a cell");
            AssertNeverStops(traces[1], -3f);
            AssertNeverStops(traces[2], -2f);
        }

        // A move's new pieces in a column are lifted together until the lowest is a cell above every piece an earlier
        // move has falling there, keeping their spacing; the next move's, even in the same frame, come in behind them.
        [Test]
        public void NewPieces_EnterBehindEarlierMoves_KeepingTheirSpacing()
        {
            var earlier = Piece(0f, 5.2f);
            _mover.Add(1, earlier, earlier.localPosition, new Vector3(0f, 0f), true, 1f);
            _mover.NextMove();

            var upper = Piece(0f, 0f);
            var lower = Piece(0f, 0f);
            var aside = Piece(1f, 0f);
            Assert.That(_mover.Spawn(upper, new Vector3(0f, 6f)).y, Is.EqualTo(6.2f).Within(1e-4f));
            Assert.That(_mover.Spawn(lower, new Vector3(0f, 5f)).y, Is.EqualTo(6.2f).Within(1e-4f));
            Assert.That(upper.localPosition.y, Is.EqualTo(7.2f).Within(1e-4f), "lifted again with the lower one");
            Assert.That(_mover.Spawn(aside, new Vector3(1f, 5f)).y, Is.EqualTo(5f), "another column");
            _mover.Add(2, upper, upper.localPosition, new Vector3(0f, 2f), true, 1f);
            _mover.Add(3, lower, lower.localPosition, new Vector3(0f, 1f), true, 1f);

            _mover.NextMove();
            var next = Piece(0f, 0f);
            Assert.That(_mover.Spawn(next, new Vector3(0f, 6f)).y, Is.EqualTo(8.2f).Within(1e-4f), "behind the move before, played this frame");
        }

        [Test]
        public void Stop_LeavesThePieceWhereItIs()
        {
            var piece = Piece(0f, 0f);
            _mover.Add(1, piece, piece.localPosition, new Vector3(0f, -5f), true, 0f);
            Run(piece, 0.1f);
            var at = piece.localPosition;

            _mover.Stop(1);
            Run(piece, 0.5f);

            Assert.That(piece.localPosition, Is.EqualTo(at));
            Assert.That(_mover.IsMoving(1), Is.False);
            Assert.That(_landings.Ids, Is.Empty);
        }

        private Transform Piece(float x, float y)
        {
            var go = new GameObject("piece");
            _pieces.Add(go);
            go.transform.localPosition = new Vector3(x, y);
            return go.transform;
        }

        private void Step()
        {
            _clock.Advance(Frame);
            _landings.Now = _clock.Now;
            _mover.Tick();
        }

        private List<Vector3> Run(Transform piece, float seconds)
        {
            var trace = new List<Vector3> { piece.localPosition };
            for (var t = 0f; t < seconds; t += Frame)
            {
                Step();
                trace.Add(piece.localPosition);
            }

            return trace;
        }

        private static void AssertSmoothDownward(List<Vector3> trace)
        {
            for (var i = 1; i < trace.Count; i++)
            {
                Assert.That(trace[i].y, Is.LessThanOrEqualTo(trace[i - 1].y + 1e-5f), $"moved up at frame {i}");
                Assert.That(Vector3.Distance(trace[i], trace[i - 1]), Is.LessThanOrEqualTo(Motion.MaxSpeed * Frame + 1e-4f), $"jumped at frame {i}");
            }
        }

        // Once it starts, the piece moves every frame until it rests on `bottom`.
        private static void AssertNeverStops(List<Vector3> trace, float bottom)
        {
            var started = false;
            for (var i = 1; i < trace.Count; i++)
            {
                var moved = trace[i] != trace[i - 1];
                started |= moved;
                if (started && !moved && trace[i].y > bottom) Assert.Fail($"stopped mid-air at {trace[i]} (frame {i})");
            }
        }

        private sealed class Landings : IFallLandingHandler
        {
            public float Now;
            public readonly List<int> Ids = new List<int>();
            public readonly List<float> At = new List<float>();
            public readonly List<float> Impact = new List<float>();

            public void Landed(int id, Transform piece, float impact)
            {
                Ids.Add(id);
                At.Add(Now);
                Impact.Add(impact);
            }
        }
    }
}
