using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Tileworks.Tiles.Tests
{
    public sealed class ModelTests
    {
        [Test]
        public void CoverGraph_MatchesBruteForce()
        {
            var rng = new Pcg32(17);
            var slots = new List<Slot>();
            var seen = new HashSet<Slot>();
            while (slots.Count < 150)
            {
                var slot = new Slot(rng.NextInt(16), rng.NextInt(16), rng.NextInt(5));
                if (seen.Add(slot)) slots.Add(slot);
            }

            var array = slots.ToArray();
            var graph = CoverGraph.Build(array);
            for (var i = 0; i < array.Length; i++)
            {
                var expected = new List<int>();
                for (var j = 0; j < array.Length; j++)
                {
                    if (array[i].Layer > array[j].Layer && Math.Abs(array[i].X - array[j].X) < 2 && Math.Abs(array[i].Y - array[j].Y) < 2)
                        expected.Add(j);
                }

                Assert.That(graph.Covers(i).ToArray(), Is.EqualTo(expected.ToArray()), $"slot {i}");
            }
        }

        [Test]
        public void CoverCounts_TrackOccupancy()
        {
            var slots = new[] { new Slot(0, 0, 0), new Slot(2, 0, 0), new Slot(1, 0, 1) };
            var graph = CoverGraph.Build(slots);
            var counts = CoverCounts.Create(graph, new[] { true, true, true });
            Span<int> changed = stackalloc int[4];

            Assert.That(counts.Covering, Is.EqualTo(new[] { 1, 1, 0 }));
            Assert.That(counts.OnVacated(graph, 2, changed), Is.EqualTo(2));
            Assert.That(counts.OnOccupied(graph, 2, changed), Is.EqualTo(2));
            Assert.That(counts.Covering, Is.EqualTo(new[] { 1, 1, 0 }));
        }

        [Test]
        public void Topologies_ReportNeighboursInsideBounds()
        {
            Span<Cell> cells = stackalloc Cell[8];
            Assert.That(new SquareTopology(3, 3).GetNeighbours(new Cell(0, 0), cells), Is.EqualTo(2));
            Assert.That(new SquareTopology(3, 3, diagonal: true).GetNeighbours(new Cell(1, 1), cells), Is.EqualTo(8));

            Span<Hex> hexes = stackalloc Hex[6];
            var hex = new HexTopology(2);
            Assert.That(hex.GetNeighbours(new Hex(0, 0), hexes), Is.EqualTo(6));
            Assert.That(hex.GetNeighbours(new Hex(2, 0), hexes), Is.EqualTo(3));
            Assert.That(hex.CoordOf(hex.IndexOf(new Hex(-1, 2))), Is.EqualTo(new Hex(-1, 2)));

            Span<StackPos> stack = stackalloc StackPos[2];
            Assert.That(new StackTopology(4, 4).GetNeighbours(new StackPos(0, 0), stack), Is.EqualTo(1));
        }

        [Test]
        public void RngStreams_AreIndependent()
        {
            var a = new RngStreams(5);
            var shuffleA = a.Register("shuffle");
            var spawnA = a.Register("spawn");
            var b = new RngStreams(5);
            var shuffleB = b.Register("shuffle");
            b.Register("spawn");

            a.Get(spawnA).NextUInt();
            a.Get(spawnA).NextUInt();
            Assert.That(a.Get(shuffleA).NextUInt(), Is.EqualTo(b.Get(shuffleB).NextUInt()), "draws on spawn never shift shuffle");
        }

        [Test]
        public void Pcg32_IsUnbiasedEnough_AndDeterministic()
        {
            var rng = new Pcg32(1);
            var buckets = new int[6];
            for (var i = 0; i < 60000; i++) buckets[rng.NextInt(6)]++;
            foreach (var count in buckets) Assert.That(count, Is.InRange(9500, 10500));
            Assert.That(new Pcg32(9).NextUInt(), Is.EqualTo(new Pcg32(9).NextUInt()));
        }

        [Test]
        public void EventRegistry_RejectsDuplicates()
        {
            var registry = new EventTypeRegistry();
            var id = registry.Register("Moved", EntityFields.Entity);
            Assert.That(registry.Get(id).Name, Is.EqualTo("Moved"));
            Assert.Throws<ArgumentException>(() => registry.Register("Moved", EntityFields.None));
        }
    }
}
