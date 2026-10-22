using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TileMatch.Editor
{
    // A tile match level as the designer edits it. Positions are in half tiles (a tile covers 2 × 2). Modifiers and spawners
    // hang off their tiles, so removing a tile can't leave them pointing at the wrong one; indices are rebuilt on save.
    public sealed class TileLevelDoc
    {
        public sealed class Tile
        {
            public int X;
            public int Y;
            public int Layer;
            public int Kind;
            public int Ice;
            public bool Hidden;
            public Tile Link;
            public List<int> Queue;

            // Where each modifier and the spawner sat in the file, so saving keeps the file's order; new ones go last.
            public int IceOrder = int.MaxValue;
            public int HiddenOrder = int.MaxValue;
            public int LinkOrder = int.MaxValue;
            public int QueueOrder = int.MaxValue;
        }

        public const int Span = 20;
        public const int MaxLayer = 7;

        public readonly List<Tile> Tiles = new List<Tile>();
        public int Tray = 7;
        public int TimeLimit;

        public void Load(JObject payload)
        {
            Tiles.Clear();
            Tray = (int?)payload["tray"] ?? 7;
            TimeLimit = (int?)payload["timeLimit"] ?? 0;
            foreach (var row in (JArray)payload["tiles"])
                Tiles.Add(new Tile { X = (int)row[0], Y = (int)row[1], Layer = (int)row[2], Kind = (int?)row[3] ?? 0 });
            if (payload["modifiers"] is JArray modifiers)
            {
                for (var order = 0; order < modifiers.Count; order++)
                {
                    var m = modifiers[order];
                    var index = (int)m["tile"];
                    if (index < 0 || index >= Tiles.Count) continue;
                    var tile = Tiles[index];
                    var value = (int?)m["value"] ?? 0;
                    switch ((string)m["type"])
                    {
                        case "frozen":
                            (tile.Ice, tile.IceOrder) = (value, order);
                            break;
                        case "hidden":
                            (tile.Hidden, tile.HiddenOrder) = (true, order);
                            break;
                        case "linked":
                            (tile.Link, tile.LinkOrder) = (value >= 0 && value < Tiles.Count ? Tiles[value] : null, order);
                            break;
                    }
                }
            }

            if (!(payload["spawners"] is JArray spawners)) return;
            for (var order = 0; order < spawners.Count; order++)
            {
                var index = (int)spawners[order]["tile"];
                if (index < 0 || index >= Tiles.Count) continue;
                var tile = Tiles[index];
                (tile.Queue, tile.QueueOrder) = (new List<int>(), order);
                foreach (var kind in (JArray)spawners[order]["queue"]) tile.Queue.Add((int)kind);
            }
        }

        // Same keys and order as TileMatchLevelSerializer.Write.
        public JObject Save()
        {
            var tiles = new JArray();
            var modifiers = new List<(int Order, int Rank, JObject Entry)>();
            var spawners = new List<(int Order, int Rank, JObject Entry)>();
            for (var i = 0; i < Tiles.Count; i++)
            {
                var t = Tiles[i];
                tiles.Add(new JArray(t.X, t.Y, t.Layer, t.Kind));
                if (t.Ice > 0) modifiers.Add((t.IceOrder, i, new JObject { ["type"] = "frozen", ["tile"] = i, ["value"] = t.Ice }));
                if (t.Hidden) modifiers.Add((t.HiddenOrder, Tiles.Count + i, new JObject { ["type"] = "hidden", ["tile"] = i }));
                var partner = t.Link != null ? Tiles.IndexOf(t.Link) : -1;
                if (partner >= 0)
                {
                    var link = new JObject { ["type"] = "linked", ["tile"] = i };
                    if (partner != 0) link["value"] = partner;
                    modifiers.Add((t.LinkOrder, 2 * Tiles.Count + i, link));
                }

                if (t.Queue != null) spawners.Add((t.QueueOrder, i, new JObject { ["tile"] = i, ["queue"] = new JArray(t.Queue) }));
            }

            var payload = new JObject { ["tray"] = Tray, ["timeLimit"] = TimeLimit, ["tiles"] = tiles };
            if (modifiers.Count > 0) payload["modifiers"] = InFileOrder(modifiers);
            if (spawners.Count > 0) payload["spawners"] = InFileOrder(spawners);
            return payload;
        }

        private static JArray InFileOrder(List<(int Order, int Rank, JObject Entry)> entries)
        {
            entries.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Rank.CompareTo(b.Rank));
            var array = new JArray();
            foreach (var entry in entries) array.Add(entry.Entry);
            return array;
        }

        // The tile on `layer` that covers half cell (x, y).
        public Tile At(int layer, int x, int y)
        {
            for (var i = Tiles.Count - 1; i >= 0; i--)
            {
                var t = Tiles[i];
                if (t.Layer == layer && x >= t.X && x < t.X + 2 && y >= t.Y && y < t.Y + 2) return t;
            }

            return null;
        }

        public bool Overlaps(int layer, int x, int y)
        {
            foreach (var t in Tiles)
                if (t.Layer == layer && System.Math.Abs(t.X - x) < 2 && System.Math.Abs(t.Y - y) < 2)
                    return true;
            return false;
        }

        public void Remove(Tile tile)
        {
            Tiles.Remove(tile);
            foreach (var t in Tiles)
                if (t.Link == tile)
                    t.Link = null;
        }

        public Dictionary<int, int> KindCounts()
        {
            var counts = new Dictionary<int, int>();
            foreach (var t in Tiles)
            {
                counts[t.Kind] = counts.TryGetValue(t.Kind, out var n) ? n + 1 : 1;
                if (t.Queue == null) continue;
                foreach (var kind in t.Queue) counts[kind] = counts.TryGetValue(kind, out var q) ? q + 1 : 1;
            }

            return counts;
        }
    }
}
