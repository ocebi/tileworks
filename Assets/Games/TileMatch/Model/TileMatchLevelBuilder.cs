using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    public sealed class TileMatchLevelBuilder
    {
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<ushort> _kinds = new List<ushort>();
        private readonly List<ModifierSpec> _modifiers = new List<ModifierSpec>();
        private readonly List<(int Tile, ushort[] Queue)> _spawners = new List<(int, ushort[])>();
        private int _trayCapacity = 7;
        private int _timeLimitSec;

        public int TileCount => _slots.Count;

        public int Add(int x, int y, int layer, ushort kind)
        {
            _slots.Add(new Slot(x, y, layer));
            _kinds.Add(kind);
            return _slots.Count - 1;
        }

        public TileMatchLevelBuilder Tray(int capacity)
        {
            _trayCapacity = capacity;
            return this;
        }

        public TileMatchLevelBuilder TimeLimit(int seconds)
        {
            _timeLimitSec = seconds;
            return this;
        }

        public TileMatchLevelBuilder Frozen(int tile, int layers = 1) => Modifier(ModifierTypes.Frozen, tile, layers);
        public TileMatchLevelBuilder Hidden(int tile) => Modifier(ModifierTypes.Hidden, tile);
        public TileMatchLevelBuilder Link(int a, int b) => Modifier(ModifierTypes.Linked, a, b);

        public TileMatchLevelBuilder Modifier(ushort type, int tile, int value = 0)
        {
            _modifiers.Add(new ModifierSpec(type, tile, value));
            return this;
        }

        // Queued tiles are numbered after every board tile, in spawner order.
        public TileMatchLevelBuilder Spawner(int boardTile, params ushort[] queuedKinds)
        {
            _spawners.Add((boardTile, queuedKinds));
            return this;
        }

        public TileMatchLevel Build(string id)
        {
            var slots = _slots.ToArray();
            var layout = new LayeredLayout(slots);
            var kinds = new List<ushort>(_kinds);
            var tileSlots = new List<int>();
            for (var i = 0; i < slots.Length; i++) tileSlots.Add(i);

            var spawners = new SpawnerSpec[_spawners.Count];
            for (var i = 0; i < _spawners.Count; i++)
            {
                var (tile, queue) = _spawners[i];
                var queued = new int[queue.Length];
                for (var q = 0; q < queue.Length; q++)
                {
                    queued[q] = kinds.Count;
                    kinds.Add(queue[q]);
                    tileSlots.Add(tile);
                }

                spawners[i] = new SpawnerSpec(tile, queued);
            }

            var kindCount = 0;
            foreach (var kind in kinds) kindCount = Math.Max(kindCount, kind + 1);
            return new TileMatchLevel(id, layout, kinds.ToArray(), tileSlots.ToArray(), slots.Length, _trayCapacity, kindCount,
                _modifiers.ToArray(), spawners, _timeLimitSec);
        }

        // "layer N:" starts a layer; each following line is a row (y = row × 2); a letter at column c is a tile at
        // x = c (half-units), kind = letter − 'A'; '.' and spaces are empty. "tray N" sets the capacity.
        public static TileMatchLevelBuilder FromAscii(string text)
        {
            var builder = new TileMatchLevelBuilder();
            var layer = -1;
            var row = 0;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("layer", StringComparison.OrdinalIgnoreCase))
                {
                    layer = int.Parse(trimmed.Substring(5).Trim().TrimEnd(':'));
                    row = 0;
                    continue;
                }

                if (trimmed.StartsWith("tray", StringComparison.OrdinalIgnoreCase))
                {
                    builder.Tray(int.Parse(trimmed.Substring(4).Trim()));
                    continue;
                }

                if (layer < 0) throw new FormatException("Rows must follow a 'layer N:' header.");
                var indent = line.Length - line.TrimStart().Length;
                var body = line.Substring(Math.Min(indent, CommonIndent(text)));
                for (var c = 0; c < body.Length; c++)
                {
                    if (body[c] >= 'A' && body[c] <= 'Z') builder.Add(c, row * 2, layer, (ushort)(body[c] - 'A'));
                }

                row++;
            }

            return builder;
        }

        private static int CommonIndent(string text)
        {
            var min = int.MaxValue;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("layer", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("tray", StringComparison.OrdinalIgnoreCase)) continue;
                min = Math.Min(min, line.Length - line.TrimStart().Length);
            }

            return min == int.MaxValue ? 0 : min;
        }

        public static void Validate(TileMatchLevel level, ValidationReport report)
        {
            var counts = new int[Math.Max(1, level.KindCount)];
            foreach (var kind in level.Kinds) counts[kind]++;
            for (var k = 0; k < counts.Length; k++)
                report.Require(counts[k] % 3 == 0, $"kind {k} has {counts[k]} tiles; every kind needs a multiple of 3");
            report.Require(level.TrayCapacity >= 3, "tray capacity must be at least 3");
            report.Require(level.BoardTileCount > 0, "level has no tiles");
        }
    }
}
