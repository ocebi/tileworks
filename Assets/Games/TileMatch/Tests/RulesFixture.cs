using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;
using TileMatch.Model;

namespace TileMatch.Tests
{
    public sealed class RulesFixture
    {
        public static readonly TileMatchRules Rules = TileMatchRules.CreateDefault();

        public RulesFixture(TileMatchLevel level, ulong seed = 1)
        {
            Level = level;
            Engine = Rules.CreateEngine(level, seed);
        }

        public static RulesFixture Ascii(string text, Action<TileMatchLevelBuilder> configure = null)
        {
            var builder = TileMatchLevelBuilder.FromAscii(text);
            configure?.Invoke(builder);
            return new RulesFixture(builder.Build("test"));
        }

        public TileMatchLevel Level { get; }
        public RuleEngine<TileMatchState> Engine { get; }
        public TileMatchState S => Engine.State;
        public TileMatchEventIds Ids => Rules.Events;

        public ActionResult Select(int tile) => Engine.Apply(new SelectTile(tile));
        public ActionResult Booster(BoosterKind kind) => Engine.Apply(new UseBooster(kind));

        public List<string> EventNames()
        {
            var names = new List<string>();
            foreach (var e in Engine.Events.AsSpan()) names.Add(Rules.Registry.NameOf(e.Type));
            return names;
        }

        public int[] TrayTiles()
        {
            var tiles = new int[S.TrayCount];
            Array.Copy(S.Tray, tiles, S.TrayCount);
            return tiles;
        }

        // Random but valid level: stacked pyramids, kinds dealt in triples.
        public static TileMatchLevel RandomLevel(ulong seed, int triples, int kinds, int layers = 3)
        {
            var rng = new Pcg32(seed);
            var builder = new TileMatchLevelBuilder();
            var positions = new List<(int X, int Y, int L)>();
            var size = 6;
            for (var layer = 0; layer < layers && positions.Count < triples * 3; layer++)
            for (var y = 0; y < size && positions.Count < triples * 3; y++)
            for (var x = 0; x < size && positions.Count < triples * 3; x++)
                positions.Add((x * 2 + (layer & 1), y * 2 + (layer & 1), layer));

            var deck = new ushort[positions.Count - positions.Count % 3];
            for (var i = 0; i < deck.Length; i++) deck[i] = (ushort)((i / 3) % kinds);
            rng.Shuffle(deck.AsSpan());
            for (var i = 0; i < deck.Length; i++) builder.Add(positions[i].X, positions[i].Y, positions[i].L, deck[i]);
            return builder.Build($"random-{seed}");
        }
    }
}
