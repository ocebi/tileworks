using System.Collections.Generic;
using System.Linq;
using Match3.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Match3.Tests
{
    public sealed class Match3PlayoutTests
    {
        private static readonly Match3Rules Rules = new Match3Rules();

        private static readonly string[] Busy = { "#...v..#", "..x.o...", "...d....", ".X....b.", "........", ".o..x...", "...*....", "....f..." };
        private static readonly string[] Chains = { "........", "........", "........", "...c....", "........", "........", "c.......", "........" };
        private static readonly string[] Floor = { "#......#", "........", "........", "........", "..1111..", "..2222..", "........", "........" };
        private const string Goals = "[{\"kind\":\"colour\",\"colour\":1,\"count\":500}]";

        internal static Match3Level Level() => Match3RulesTests.Level(Busy, Goals, 40, 4, Floor, Chains);

        internal static Swap[] Legal(RuleEngine<Match3State> engine)
        {
            var s = engine.State;
            var legal = new List<Swap>();
            for (var cell = 0; cell < s.CellCount; cell++)
            for (var d = 0; d < 2; d++)
            {
                var n = Match3Board.Neighbour(s, cell, d);
                if (n >= 0 && engine.Check(new Swap(cell, n)).IsNone) legal.Add(new Swap(cell, n));
            }

            return legal.ToArray();
        }

        // Random swaps on a busy board, each followed by its landings: every move settles with no match left, ids stay
        // unique, and a replay gives the same hash.
        [Test]
        public void RandomPlayouts_SettleWithNoMatchLeft_AndReplayExactly()
        {
            var level = Level();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var engine = Rules.CreateEngine(level, seed);
                var s = engine.State;
                var pick = new Pcg32(seed * 7919);
                while (s.Outcome == Outcome.Ongoing)
                {
                    var legal = Legal(engine);
                    Assert.That(legal, Is.Not.Empty, "an ongoing level always has a move");
                    var moves = s.MovesLeft;
                    Assert.That(engine.Apply(legal[pick.NextInt(legal.Length)]).Accepted, Is.True);
                    Match3RulesTests.LandAll(engine);
                    Assert.That(s.MovesLeft, Is.EqualTo(moves - 1));
                    Assert.That(Matches.AnyMatch(s), Is.False, $"seed {seed}: a match was left on the board");
                    var ids = Enumerable.Range(0, s.CellCount).Where(c => s.Pieces[c] != Piece.Empty).Select(c => s.Ids[c]).ToList();
                    Assert.That(ids, Is.Unique);
                }

                var replay = Rules.CreateEngine(level, seed);
                foreach (var record in engine.Log) replay.ApplyRecord(record);
                Assert.That(replay.State.ComputeHash(), Is.EqualTo(s.ComputeHash()), $"seed {seed}");
            }
        }

        [Test]
        public void Swaps_DoNotAllocate_AfterWarmUp()
        {
            var level = Level();
            var probe = Rules.CreateEngine(level, 9);
            var pick = new Pcg32(4);
            var swaps = new List<Swap>();
            while (probe.State.Outcome == Outcome.Ongoing && swaps.Count < 30)
            {
                var legal = Legal(probe);
                var swap = legal[pick.NextInt(legal.Length)];
                probe.Apply(swap);
                Match3RulesTests.LandAll(probe);
                swaps.Add(swap);
            }

            var engine = Rules.CreateEngine(level, 9, new EventBuffer(4096));
            engine.RecordActions = false;
            engine.Apply(swaps[0]);
            engine.Apply(new LandAll());
            Assert.That(() =>
            {
                for (var i = 1; i < swaps.Count; i++)
                {
                    engine.Apply(swaps[i]);
                    if (engine.State.Flight.Count > 0) engine.Apply(new LandAll());
                }
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
