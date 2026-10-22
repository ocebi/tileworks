using System.Collections.Generic;
using NUnit.Framework;
using Tileworks.Kit.Audio;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Tileworks.Kit.Tests
{
    public sealed class VoiceTableTests
    {
        private const int A = 0;
        private const int B = 1;
        private const int C = 2;

        private static VoiceRequest Sound(int cue, double start, double length = 1.0, int voices = 16, double gap = 0.04, int priority = 0) =>
            new VoiceRequest(cue, priority, voices, gap, start, length);

        // The old players handed out 8 sources in turn, so the 9th cue of a burst took over one still waiting to start.
        [Test]
        public void ScheduledSounds_SurviveABurst()
        {
            var table = new VoiceTable(24);
            var voices = new HashSet<int>();
            for (var i = 0; i < 20; i++)
            {
                Assert.That(table.TryAssign(Sound(i % 2 == 0 ? A : B, i * 0.05), 0.0, out var plan), Is.True);
                Assert.That(plan.Cut, Is.EqualTo(-1));
                voices.Add(plan.Voice);
            }

            Assert.That(voices.Count, Is.EqualTo(20));
            for (var v = 0; v < table.Count; v++) Assert.That(table.EndOf(v) - table.StartOf(v), Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void AnotherCue_NeverTakesOverASoundWaitingToStart()
        {
            var table = new VoiceTable(4);
            for (var i = 0; i < 4; i++) table.TryAssign(Sound(A, 0.5 + i * 0.1), 0.0, out _);

            Assert.That(table.TryAssign(Sound(B, 0.0, priority: 10), 0.0, out _), Is.False);
            for (var v = 0; v < 4; v++) Assert.That(table.EndOf(v), Is.EqualTo(table.StartOf(v) + 1.0).Within(1e-9));
        }

        [Test]
        public void FullPool_TakesTheOldestStartedSound_OfTheSameCueOrALowerPriority()
        {
            var table = new VoiceTable(2);
            table.TryAssign(Sound(A, 0.0), 0.0, out var first);
            table.TryAssign(Sound(B, 0.1), 0.1, out _);

            Assert.That(table.TryAssign(Sound(C, 0.2), 0.2, out _), Is.False, "same priority, other cue");
            Assert.That(table.TryAssign(Sound(C, 0.2, priority: 1), 0.2, out var plan), Is.True);
            Assert.That(plan.Voice, Is.EqualTo(first.Voice));
            Assert.That(plan.IsNew, Is.False);
            Assert.That(table.TryAssign(Sound(B, 0.3), 0.3, out var again), Is.True, "same cue");
            Assert.That(table.CueOf(again.Voice), Is.EqualTo(B));
        }

        [Test]
        public void OverItsCap_ACueCutsItsOldestCopy_WhenTheNewOneStarts()
        {
            var table = new VoiceTable(8);
            table.TryAssign(Sound(A, 0.0, voices: 2), 0.0, out var first);
            table.TryAssign(Sound(A, 0.1, voices: 2), 0.0, out _);

            Assert.That(table.TryAssign(Sound(A, 0.2, voices: 2), 0.0, out var plan), Is.True);
            Assert.That(plan.Cut, Is.EqualTo(first.Voice));
            Assert.That(plan.CutAt, Is.EqualTo(0.2));
            Assert.That(table.EndOf(first.Voice), Is.EqualTo(0.2));
        }

        [Test]
        public void OverItsCap_ACueStartingNow_ReusesItsOldestCopy()
        {
            var table = new VoiceTable(8);
            table.TryAssign(Sound(A, 0.0, voices: 1), 0.0, out var first);

            Assert.That(table.TryAssign(Sound(A, 0.5, voices: 1), 0.5, out var plan), Is.True);
            Assert.That(plan.Voice, Is.EqualTo(first.Voice));
            Assert.That(plan.Cut, Is.EqualTo(-1));
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void ACopyStartingWithinTheGap_IsDropped()
        {
            var table = new VoiceTable(8);
            table.TryAssign(Sound(A, 0.30), 0.0, out _);

            Assert.That(table.TryAssign(Sound(A, 0.32), 0.0, out _), Is.False);
            Assert.That(table.TryAssign(Sound(A, 0.28), 0.0, out _), Is.False);
            Assert.That(table.TryAssign(Sound(B, 0.30), 0.0, out _), Is.True);
            Assert.That(table.TryAssign(Sound(A, 0.35), 0.0, out _), Is.True);
        }

        [Test]
        public void EndedVoices_AreReused()
        {
            var table = new VoiceTable(8);
            table.TryAssign(Sound(A, 0.0, length: 0.5), 0.0, out _);

            Assert.That(table.TryAssign(Sound(B, 1.0), 1.0, out var plan), Is.True);
            Assert.That(plan.Voice, Is.EqualTo(0));
            Assert.That(plan.IsNew, Is.False);
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void Assigning_DoesNotAllocate()
        {
            var table = new VoiceTable(24);
            for (var i = 0; i < 24; i++) table.TryAssign(Sound(i % 3, i * 0.05, 0.3, voices: 4), 0.0, out _);
            var now = 2.0;
            Assert.That(() =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    now += 0.01;
                    table.TryAssign(Sound(i % 3, now + (i % 5) * 0.05, 0.3, voices: 4), now, out _);
                }
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
