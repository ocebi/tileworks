using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;
using TileMatch.Model;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace TileMatch.Tests
{
    // Random playouts (including boosters and continues) with invariants checked after every action (§16.3).
    public sealed class PropertyTests
    {
        private object _sink;

        private static IEnumerable<ulong> Seeds()
        {
            for (ulong seed = 1; seed <= 40; seed++) yield return seed;
        }

        [TestCaseSource(nameof(Seeds))]
        public void RandomPlayouts_KeepInvariants_AndReplayDeterministically(ulong seed)
        {
            var rng = new Pcg32(seed * 7919);
            var level = RulesFixture.RandomLevel(seed, 8 + rng.NextInt(20), 3 + rng.NextInt(6), 2 + rng.NextInt(3));
            var f = new RulesFixture(level, seed);
            var initialKinds = Histogram(f.S);

            var continued = false;
            for (var step = 0; step < 400 && f.S.Outcome != Outcome.Won; step++)
            {
                if (f.S.Outcome == Outcome.Lost)
                {
                    if (continued) break;
                    continued = true;
                    Assert.That(f.Engine.Apply(new ContinueAfterFail(1, 3)).Accepted, Is.True);
                }
                else if (rng.Chance(1, 12))
                {
                    f.Booster((BoosterKind)(1 + rng.NextInt(4)));
                }
                else
                {
                    var tile = RandomSelectable(f, ref rng);
                    if (tile < 0) break;
                    Assert.That(f.Select(tile).Accepted, Is.True);
                }

                AssertInvariants(f.S, initialKinds);
            }

            var replay = new Replay { LevelId = level.Id, Seed = seed, Actions = new List<ActionRecord>(f.Engine.Log).ToArray() };
            var again = RulesFixture.Rules.CreateEngine(level, seed);
            Assert.That(replay.Play(again), Is.EqualTo(f.S.ComputeHash()), "same seed and actions give the same state");
        }

        [Test]
        public void Replay_RoundTripsThroughBytes()
        {
            var replay = new Replay
            {
                LevelId = "tm_0001", ContentHash = "abc", RulesVersion = TileMatchRules.Version, Seed = 99, FinalHash = 1234,
                Actions = new[] { new ActionRecord(1, 5), new ActionRecord(2, 3), new ActionRecord(3, 1, 3) },
            };

            var copy = Replay.FromBytes(replay.ToBytes());

            Assert.That(copy.LevelId, Is.EqualTo("tm_0001"));
            Assert.That(copy.Seed, Is.EqualTo(99));
            Assert.That(copy.FinalHash, Is.EqualTo(1234));
            Assert.That(copy.Actions, Is.EqualTo(replay.Actions));
        }

        [Test]
        public void SnapshotRing_RestoresExactState()
        {
            var f = new RulesFixture(RulesFixture.RandomLevel(3, 12, 4));
            var scratch = new TileMatchState[8];
            for (var i = 0; i < scratch.Length; i++) scratch[i] = RulesFixture.Rules.CreateScratch(f.Level);
            var ring = new SnapshotRing<TileMatchState>(scratch);
            var rng = new Pcg32(5);

            ring.Push(f.S);
            var before = f.S.ComputeHash();
            f.Select(RandomSelectable(f, ref rng));
            Assert.That(f.S.ComputeHash(), Is.Not.EqualTo(before));

            Assert.That(ring.TryPop(f.S), Is.True);
            Assert.That(f.S.ComputeHash(), Is.EqualTo(before));
        }

        [Test]
        public void SelectTile_DoesNotAllocate_AfterWarmUp()
        {
            var level = RulesFixture.RandomLevel(11, 30, 6, 3);
            var warm = new RulesFixture(level, 1);
            var rng = new Pcg32(2);
            PlayRandom(warm, ref rng, 40);

            var f = new RulesFixture(level, 1);
            var tiles = new List<int>();
            var probe = new RulesFixture(level, 1);
            var pick = new Pcg32(3);
            for (var i = 0; i < 25 && probe.S.Outcome == Outcome.Ongoing; i++)
            {
                var t = RandomSelectable(probe, ref pick);
                if (t < 0) break;
                probe.Select(t);
                tiles.Add(t);
            }

            f.Select(tiles[0]);
            Assert.That(() =>
            {
                for (var i = 1; i < tiles.Count; i++) f.Select(tiles[i]);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AllocationConstraint_DetectsAllocations()
        {
            Assert.Throws<AssertionException>(() =>
                Assert.That((TestDelegate)(() => _sink = new int[64]), Is.Not.AllocatingGCMemory()));
        }

        internal static int RandomSelectable(RulesFixture f, ref Pcg32 rng)
        {
            Span<int> group = stackalloc int[SelectTileHandler.MaxGroup];
            var candidates = new List<int>();
            for (var tile = 0; tile < f.S.TileCount; tile++)
            {
                if (SelectTileHandler.ValidateTile(f.S, RulesFixture.Rules.Modifiers, tile, group).IsNone) candidates.Add(tile);
            }

            return candidates.Count == 0 ? -1 : candidates[rng.NextInt(candidates.Count)];
        }

        private static void PlayRandom(RulesFixture f, ref Pcg32 rng, int steps)
        {
            for (var i = 0; i < steps && f.S.Outcome == Outcome.Ongoing; i++)
            {
                var tile = RandomSelectable(f, ref rng);
                if (tile < 0) return;
                f.Select(tile);
            }
        }

        private static void AssertInvariants(TileMatchState s, int[] initialKinds)
        {
            Assert.That(s.PendingCount, Is.Zero, "nothing left in flight after an action");
            Assert.That(s.TrayCount, Is.LessThanOrEqualTo(s.TrayCapacity));
            Assert.That(Histogram(s), Is.EqualTo(initialKinds), "kinds are conserved");

            var inTray = 0;
            foreach (var location in s.Locations) if (location == TileLocation.Tray) inTray++;
            Assert.That(inTray, Is.EqualTo(s.TrayCount));

            var seen = new HashSet<ushort>();
            for (var i = 0; i < s.TrayCount; i++)
            {
                var kind = s.Kinds[s.Tray[i]];
                if (i > 0 && kind != s.Kinds[s.Tray[i - 1]]) Assert.That(seen.Add(kind), Is.True, "tray kinds stay grouped");
                if (i == 0) seen.Add(kind);
                Assert.That(s.CountInTray(kind), Is.LessThan(3), "no triple survives resolution");
            }

            for (var slot = 0; slot < s.Level.SlotCount; slot++)
            {
                var expected = 0;
                foreach (var above in s.Level.Cover.CoveredBy(slot)) if (s.Occupant[above] >= 0) expected++;
                Assert.That(s.Cover.Covering[slot], Is.EqualTo(expected), $"cover count of slot {slot}");
                var occupant = s.Occupant[slot];
                if (occupant >= 0)
                {
                    Assert.That(s.Locations[occupant], Is.EqualTo(TileLocation.Board));
                    Assert.That(s.SlotOf(occupant), Is.EqualTo(slot));
                }
            }

            for (var tile = 0; tile < s.TileCount; tile++)
            {
                if (s.Locations[tile] == TileLocation.Board) Assert.That(s.Occupant[s.SlotOf(tile)], Is.EqualTo(tile));
            }
        }

        private static int[] Histogram(TileMatchState s)
        {
            var counts = new int[s.Level.KindCount];
            foreach (var kind in s.Kinds) counts[kind]++;
            return counts;
        }
    }
}
