using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TileMatch.Model;

namespace TileMatch.Generation
{
    public readonly struct LayoutCell : IEquatable<LayoutCell>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Layer;
        public readonly char Mark;

        public LayoutCell(int x, int y, int layer, char mark = 'x')
        {
            X = x;
            Y = y;
            Layer = layer;
            Mark = mark;
        }

        public LayoutCell WithMark(char mark) => new LayoutCell(X, Y, Layer, mark);
        public bool SamePosition(int x, int y, int layer) => X == x && Y == y && Layer == layer;
        public bool Equals(LayoutCell o) => X == o.X && Y == o.Y && Layer == o.Layer && Mark == o.Mark;
        public override bool Equals(object obj) => obj is LayoutCell o && Equals(o);
        public override int GetHashCode() => (X * 397 ^ Y) * 31 ^ Layer;
    }

    // Editable, kind-less layout: what the level editor manipulates and the ASCII layout files store.
    public sealed class LayoutDocument
    {
        public readonly List<LayoutCell> Cells = new List<LayoutCell>();
        public int Tray = 7;
        public int Kinds = 6;
        public int Margin = 1;
        public int SpawnerQueue = 3;
        public int TimeLimit;
        public DifficultyBand Band = DifficultyBand.Normal;
        public string Comment = string.Empty;

        public int SpawnerCount => Cells.Count(c => c.Mark == 's');
        public int TotalTiles => Cells.Count + SpawnerCount * SpawnerQueue;
        public bool IsValidCount => TotalTiles > 0 && TotalTiles % 3 == 0;

        public int IndexAt(int x, int y, int layer) => Cells.FindIndex(c => c.SamePosition(x, y, layer));

        public void Set(int x, int y, int layer, char mark)
        {
            var index = IndexAt(x, y, layer);
            if (index >= 0) Cells[index] = new LayoutCell(x, y, layer, mark);
            else Cells.Add(new LayoutCell(x, y, layer, mark));
        }

        public void Remove(int x, int y, int layer)
        {
            var index = IndexAt(x, y, layer);
            if (index >= 0) Cells.RemoveAt(index);
        }

        public static LayoutDocument Parse(string text)
        {
            var doc = new LayoutDocument();
            var layer = -1;
            var row = 0;
            var dy = 0;
            var rows = new List<(string Line, int Layer, int Y)>();
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    if (doc.Comment.Length == 0) doc.Comment = trimmed.Substring(1).Trim();
                    continue;
                }

                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "tray": doc.Tray = int.Parse(parts[1]); continue;
                    case "kinds": doc.Kinds = int.Parse(parts[1]); continue;
                    case "band": doc.Band = DifficultyBands.Parse(parts[1]); continue;
                    case "margin": doc.Margin = int.Parse(parts[1]); continue;
                    case "spawner": doc.SpawnerQueue = int.Parse(parts[1]); continue;
                    case "time": doc.TimeLimit = int.Parse(parts[1]); continue;
                    case "layer":
                        layer = int.Parse(parts[1].TrimEnd(':'));
                        dy = parts.Length > 2 && parts[2].StartsWith("dy=", StringComparison.Ordinal) ? int.Parse(parts[2].Substring(3)) : 0;
                        row = 0;
                        continue;
                }

                if (layer < 0) throw new FormatException("Rows must follow a 'layer N:' header.");
                rows.Add((line, layer, row++ * 2 + dy));
            }

            var indent = rows.Count == 0 ? 0 : rows.Min(r => r.Line.Length - r.Line.TrimStart().Length);
            foreach (var (line, rowLayer, y) in rows)
            {
                var body = line.Substring(indent);
                for (var c = 0; c < body.Length; c++)
                {
                    if (body[c] != ' ' && body[c] != '.') doc.Cells.Add(new LayoutCell(c, y, rowLayer, body[c]));
                }
            }

            return doc;
        }

        public string ToText()
        {
            var text = new StringBuilder();
            if (Comment.Length > 0) text.Append("# ").AppendLine(Comment);
            text.AppendLine($"tray {Tray}").AppendLine($"kinds {Kinds}").AppendLine($"band {DifficultyBands.Label(Band)}");
            if (Margin != 1) text.AppendLine($"margin {Margin}");
            if (SpawnerCount > 0) text.AppendLine($"spawner {SpawnerQueue}");
            if (TimeLimit > 0) text.AppendLine($"time {TimeLimit}");

            var minX = Cells.Count == 0 ? 0 : Cells.Min(c => c.X);
            foreach (var layer in Cells.Select(c => c.Layer).Distinct().OrderBy(l => l))
            {
                var cells = Cells.Where(c => c.Layer == layer).ToList();
                var dy = cells.Min(c => c.Y) & 1;
                text.AppendLine(dy == 0 ? $"layer {layer}:" : $"layer {layer}: dy={dy}");
                var maxY = cells.Max(c => c.Y);
                for (var y = dy; y <= maxY; y += 2)
                {
                    var row = cells.Where(c => c.Y == y).ToList();
                    if (row.Count == 0)
                    {
                        text.AppendLine(".");
                        continue;
                    }

                    var chars = new char[row.Max(c => c.X) - minX + 1];
                    for (var i = 0; i < chars.Length; i++) chars[i] = ' ';
                    foreach (var cell in row) chars[cell.X - minX] = cell.Mark;
                    text.AppendLine(new string(chars).TrimEnd());
                }
            }

            return text.ToString();
        }

        public LayoutFile ToLayoutFile(string name) => LayoutParser.Parse(name, ToText());
    }
}
