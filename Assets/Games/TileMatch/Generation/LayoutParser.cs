using System;
using System.Collections.Generic;
using TileMatch.Model;

namespace TileMatch.Generation
{
    public sealed class LayoutFile
    {
        public string Name;
        public TileMatchLevel Layout;
        public int Kinds = 6;
        public DifficultyBand Band = DifficultyBand.Normal;
        public int Margin = 1;
    }

    // Kind-less authoring format. Directives: tray N, kinds N, band <label>, margin N, spawner N, then
    // "layer N: [dy=1]" blocks whose rows mark tiles: x plain, f/F frozen (1/2 layers), h hidden, s spawner,
    // digits pair linked tiles. Column = x in half-units; row r = y 2r (+dy).
    public static class LayoutParser
    {
        public static LayoutFile Parse(string name, string text)
        {
            var file = new LayoutFile { Name = name };
            var builder = new TileMatchLevelBuilder();
            var spawnerQueue = 3;
            var layer = -1;
            var row = 0;
            var dy = 0;
            var links = new Dictionary<char, int>();
            var spawners = new List<int>();
            var rows = new List<(string Line, int Layer, int Y)>();

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal)) continue;
                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "tray": builder.Tray(int.Parse(parts[1])); continue;
                    case "kinds": file.Kinds = int.Parse(parts[1]); continue;
                    case "band": file.Band = DifficultyBands.Parse(parts[1]); continue;
                    case "margin": file.Margin = int.Parse(parts[1]); continue;
                    case "spawner": spawnerQueue = int.Parse(parts[1]); continue;
                    case "time": builder.TimeLimit(int.Parse(parts[1])); continue;
                }

                if (parts[0] == "layer")
                {
                    layer = int.Parse(parts[1].TrimEnd(':'));
                    dy = parts.Length > 2 && parts[2].StartsWith("dy=", StringComparison.Ordinal) ? int.Parse(parts[2].Substring(3)) : 0;
                    row = 0;
                    continue;
                }

                if (layer < 0) throw new FormatException($"{name}: rows must follow a 'layer N:' header.");
                rows.Add((line, layer, row++ * 2 + dy));
            }

            var indent = int.MaxValue;
            foreach (var r in rows) indent = Math.Min(indent, r.Line.Length - r.Line.TrimStart().Length);

            foreach (var (line, rowLayer, y) in rows)
            {
                var body = line.Substring(indent);
                for (var c = 0; c < body.Length; c++)
                {
                    var mark = body[c];
                    if (mark == ' ' || mark == '.') continue;
                    var tile = builder.Add(c, y, rowLayer, 0);
                    if (mark == 'f') builder.Frozen(tile, 1);
                    else if (mark == 'F') builder.Frozen(tile, 2);
                    else if (mark == 'h') builder.Hidden(tile);
                    else if (mark == 's') spawners.Add(tile);
                    else if (char.IsDigit(mark))
                    {
                        if (links.TryGetValue(mark, out var partner))
                        {
                            builder.Link(partner, tile);
                            links.Remove(mark);
                        }
                        else
                        {
                            links[mark] = tile;
                        }
                    }
                    else if (mark != 'x') throw new FormatException($"{name}: unknown mark '{mark}'.");
                }
            }

            if (links.Count > 0) throw new FormatException($"{name}: unpaired link marks.");
            foreach (var tile in spawners) builder.Spawner(tile, new ushort[spawnerQueue]);
            var total = builder.TileCount + spawners.Count * spawnerQueue;
            if (total % 3 != 0) throw new FormatException($"{name}: {total} tiles; the total must be a multiple of 3.");
            file.Layout = builder.Build(name);
            return file;
        }
    }
}
