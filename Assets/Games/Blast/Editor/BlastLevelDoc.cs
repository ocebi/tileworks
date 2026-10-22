using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Blast.Editor
{
    // A blast level as the designer edits it: one payload character per cell (see BlastLevelSerializer) and the settings.
    public sealed class BlastLevelDoc
    {
        public sealed class Goal
        {
            public string Kind = "colour";
            public int Colour = 1;
            public int Count = 20;
        }

        public readonly List<Goal> Goals = new List<Goal>();
        public int Width = 8;
        public int Height = 9;
        public int Moves = 25;
        public int Colours = 4;
        public char[] Cells = new string('.', 8 * 9).ToCharArray();

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

            Moves = (int?)payload["moves"] ?? 25;
            Colours = (int?)payload["colours"] ?? 4;
            Goals.Clear();
            if (payload["goals"] is JArray goals)
                foreach (var g in goals)
                    Goals.Add(new Goal { Kind = (string)g["kind"], Colour = (int?)g["colour"] ?? 1, Count = (int?)g["count"] ?? 1 });
        }

        // Same keys and order as BlastLevelSerializer.Write, so an untouched level keeps its content hash.
        public JObject Save()
        {
            var goals = new JArray();
            foreach (var goal in Goals)
            {
                var g = new JObject { ["kind"] = goal.Kind, ["count"] = goal.Count };
                if (goal.Kind == "colour") g["colour"] = goal.Colour;
                goals.Add(g);
            }

            var rows = new JArray();
            for (var y = 0; y < Height; y++) rows.Add(new string(Cells, y * Width, Width));
            return new JObject { ["moves"] = Moves, ["colours"] = Colours, ["goals"] = goals, ["rows"] = rows };
        }

        // Keeps the top-left part; new cells are random cubes.
        public void Resize(int width, int height)
        {
            var cells = new char[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                cells[y * width + x] = x < Width && y < Height ? this[x, y] : '.';
            (Width, Height, Cells) = (width, height, cells);
        }

        public int Count(char code)
        {
            var count = 0;
            foreach (var cell in Cells)
                if (cell == code) count++;
            return count;
        }
    }
}
