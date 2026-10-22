using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Match3.Editor
{
    // A match-3 level as the designer edits it: one payload character per cell for the pieces, the floor and the chains
    // (see Match3LevelSerializer), and the settings.
    public sealed class Match3LevelDoc
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
        public int Colours = 5;
        public char[] Cells = new string('.', 8 * 9).ToCharArray();
        public char[] Floor = new string('.', 8 * 9).ToCharArray();
        public char[] Chains = new string('.', 8 * 9).ToCharArray();

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
            Cells = Read(rows);
            Floor = Read(payload["floor"] as JArray);
            Chains = Read(payload["chains"] as JArray);
            Moves = (int?)payload["moves"] ?? 25;
            Colours = (int?)payload["colours"] ?? 5;
            Goals.Clear();
            if (payload["goals"] is JArray goals)
                foreach (var g in goals)
                    Goals.Add(new Goal { Kind = (string)g["kind"], Colour = (int?)g["colour"] ?? 1, Count = (int?)g["count"] ?? 1 });
        }

        // Same keys and order as Match3LevelSerializer.Write, so an untouched level keeps its content hash.
        public JObject Save()
        {
            var goals = new JArray();
            foreach (var goal in Goals)
            {
                var g = new JObject { ["kind"] = goal.Kind, ["count"] = goal.Count };
                if (goal.Kind == "colour") g["colour"] = goal.Colour;
                goals.Add(g);
            }

            var payload = new JObject { ["moves"] = Moves, ["colours"] = Colours, ["goals"] = goals, ["rows"] = Write(Cells) };
            if (Array.Exists(Floor, c => c != '.' && c != '#')) payload["floor"] = Write(Floor);
            if (Array.Exists(Chains, c => c == 'c')) payload["chains"] = Write(Chains);
            return payload;
        }

        // Keeps the top-left part; new cells are random gems with no floor or chain.
        public void Resize(int width, int height)
        {
            Cells = Crop(Cells, width, height);
            Floor = Crop(Floor, width, height);
            Chains = Crop(Chains, width, height);
            (Width, Height) = (width, height);
        }

        public int Count(char code)
        {
            var count = 0;
            foreach (var cell in Cells)
                if (cell == code) count++;
            return count;
        }

        public int FloorCells() => Array.FindAll(Floor, c => c == '1' || c == '2').Length;

        public int ChainCount() => Array.FindAll(Chains, c => c == 'c').Length;

        private char[] Read(JArray rows)
        {
            var cells = new string('.', Width * Height).ToCharArray();
            if (rows == null) return cells;
            for (var y = 0; y < Math.Min(Height, rows.Count); y++)
            {
                var row = (string)rows[y];
                for (var x = 0; x < Math.Min(Width, row.Length); x++) cells[y * Width + x] = row[x];
            }

            return cells;
        }

        private JArray Write(char[] layer)
        {
            var rows = new JArray();
            for (var y = 0; y < Height; y++) rows.Add(new string(layer, y * Width, Width));
            return rows;
        }

        private char[] Crop(char[] layer, int width, int height)
        {
            var cells = new char[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                cells[y * width + x] = x < Width && y < Height ? layer[y * Width + x] : '.';
            return cells;
        }
    }
}
