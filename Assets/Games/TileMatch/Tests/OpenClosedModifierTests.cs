using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tileworks.Tiles.Model;
using TileMatch.Model;

namespace TileMatch.Tests
{
    // A new blocker added from outside the model assembly runs through validation, resolution, events, snapshots and
    // hashing without any change to core files (§10.6).
    public sealed class OpenClosedModifierTests
    {
        private const ushort ChainType = 99;
        private static readonly RejectReason Chained = new RejectReason(199);

        // "Chain": a tile stays locked until `value` other tiles have left the board.
        // data[tile] = links left; data[n + tile] = the original requirement.
        private sealed class ChainModifier : ITileModifier
        {
            public ushort TypeId => ChainType;

            public int[] CreateState(TileMatchLevel level)
            {
                var n = level.TileCount;
                var data = new int[n * 2];
                foreach (var spec in level.Modifiers)
                {
                    if (spec.Type != TypeId) continue;
                    data[spec.Tile] = spec.Value;
                    data[n + spec.Tile] = spec.Value;
                }

                return data;
            }

            public RejectReason CanSelect(TileMatchState s, int[] data, int tile) => data[tile] > 0 ? Chained : RejectReason.None;

            public TileTraits Traits(TileMatchState s, int[] data, int tile) => data[tile] > 0 ? TileTraits.Locked : TileTraits.None;

            public bool OnReact(TileMatchState s, int[] data, RuleContext ctx, in TileMatchEventIds ids)
            {
                var gone = 0;
                foreach (var location in s.Locations)
                {
                    if (location != TileLocation.Board && location != TileLocation.Queued) gone++;
                }

                var changed = false;
                var n = s.TileCount;
                for (var tile = 0; tile < n; tile++)
                {
                    if (data[tile] == 0) continue;
                    var remaining = Math.Max(0, data[n + tile] - gone);
                    if (remaining == data[tile]) continue;
                    data[tile] = remaining;
                    ModifierChanged.Emit(ctx.Events, ids, tile, TypeId, remaining);
                    changed = true;
                }

                return changed;
            }
        }

        private static TileMatchRules RulesWithChain()
        {
            var modifiers = new List<ITileModifier>(TileMatchRules.BuiltInModifiers()) { new ChainModifier() };
            return new TileMatchRules(modifiers.ToArray());
        }

        [Test]
        public void PluginModifier_VetoesUntilItsConditionHolds()
        {
            var rules = RulesWithChain();
            var level = TileMatchLevelBuilder.FromAscii(@"
                layer 0:
                A A A B B B").Modifier(ChainType, 3, 2).Build("chain");
            var engine = rules.CreateEngine(level, 1);

            Assert.That(engine.Apply(new SelectTile(3)).Rejected, Is.EqualTo(Chained));
            engine.Apply(new SelectTile(0));
            Assert.That(engine.Apply(new SelectTile(3)).Rejected, Is.EqualTo(Chained));
            engine.Apply(new SelectTile(1));

            Assert.That(engine.Apply(new SelectTile(3)).Accepted, Is.True);
        }

        [Test]
        public void PluginModifierState_IsCopiedAndHashed()
        {
            var rules = RulesWithChain();
            var level = TileMatchLevelBuilder.FromAscii(@"
                layer 0:
                A A A B B B").Modifier(ChainType, 3, 2).Build("chain");
            var engine = rules.CreateEngine(level, 1);
            var snapshot = rules.CreateScratch(level);
            engine.State.CopyTo(snapshot);
            var before = engine.State.ComputeHash();

            engine.Apply(new SelectTile(0));
            engine.Apply(new SelectTile(5));

            Assert.That(engine.State.ComputeHash(), Is.Not.EqualTo(before));
            snapshot.CopyTo(engine.State);
            Assert.That(engine.State.ComputeHash(), Is.EqualTo(before));
        }

        [Test]
        public void UnknownModifier_IsRejectedAtLoad()
        {
            var level = TileMatchLevelBuilder.FromAscii(@"
                layer 0:
                A A A").Modifier(ChainType, 0, 1).Build("unknown");
            Assert.Throws<ArgumentException>(() => RulesFixture.Rules.CreateState(level));
        }
    }
}
