using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BlockPuzzle.Editor
{
    // A block puzzle level as the designer edits it: one payload character per cell (see BlockLevelSerializer), the goal and
    // the shape pool in file order, so an untouched level keeps its content hash.
    public sealed class PuzzleLevelDoc
    {
        public readonly List<string> PoolOrder = new List<string>();
        public readonly Dictionary<string, int> Weights = new Dictionary<string, int>();
        public int Width = 8;
        public int Height = 8;
        public string Goal = "score";
        public int Score = 150;
        public char[] Cells = new string('.', 64).ToCharArray();

        public char this[int x, int y]
        {
            get => Cells[y * Width + x];
            set => Cells[y * Width + x] = value;
        }

        public void Load(JObject payload)
        {
            var rows = (JArray)payload["rows"];
            Height = rows.Count;
            Width = ((string)rows[0]).Length;
            Cells = new char[Width * Height];
            for (var y = 0; y < Height; y++)
            {
                var row = (string)rows[y];
                for (var x = 0; x < Width; x++) this[x, y] = x < row.Length ? row[x] : '.';
            }

            Goal = (string)payload["goal"] ?? "score";
            Score = (int?)payload["score"] ?? 150;
            PoolOrder.Clear();
            Weights.Clear();
            if (!(payload["pool"] is JObject pool)) return;
            foreach (var entry in pool)
            {
                PoolOrder.Add(entry.Key);
                Weights[entry.Key] = (int)entry.Value;
            }
        }

        public JObject Save()
        {
            var rows = new JArray();
            for (var y = 0; y < Height; y++) rows.Add(new string(Cells, y * Width, Width));
            var pool = new JObject();
            foreach (var name in PoolOrder)
                if (Weights.TryGetValue(name, out var weight) && weight > 0)
                    pool[name] = weight;
            var payload = new JObject { ["goal"] = Goal, ["rows"] = rows, ["pool"] = pool };
            if (Goal == "score") payload["score"] = Score;
            return payload;
        }

        public int WeightOf(string shape) => Weights.TryGetValue(shape, out var weight) ? weight : 0;

        public void SetWeight(string shape, int weight)
        {
            if (!PoolOrder.Contains(shape)) PoolOrder.Add(shape);
            Weights[shape] = weight;
        }

        public void Resize(int width, int height)
        {
            var cells = new char[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                cells[y * width + x] = x < Width && y < Height ? this[x, y] : '.';
            (Width, Height, Cells) = (width, height, cells);
        }

        public int Count(System.Func<char, bool> match)
        {
            var count = 0;
            foreach (var cell in Cells)
                if (match(cell)) count++;
            return count;
        }
    }
}
