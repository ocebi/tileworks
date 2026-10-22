using System;
using System.Collections.Generic;

namespace BlockPuzzle.Model
{
    public sealed class Shape
    {
        private readonly int[] _dx;
        private readonly int[] _dy;

        public Shape(int id, string name, int tier, string pattern)
        {
            Id = id;
            Name = name;
            Tier = tier;
            var rows = pattern.Split('/');
            var dx = new List<int>();
            var dy = new List<int>();
            for (var y = 0; y < rows.Length; y++)
            {
                for (var x = 0; x < rows[y].Length; x++)
                {
                    if (rows[y][x] != '#') continue;
                    dx.Add(x);
                    dy.Add(y);
                    Width = Math.Max(Width, x + 1);
                }
            }

            Height = rows.Length;
            _dx = dx.ToArray();
            _dy = dy.ToArray();
        }

        public int Id { get; }
        public string Name { get; }

        // 1 = small pieces only, 3 = the hardest pieces.
        public int Tier { get; }
        public int Width { get; }
        public int Height { get; }
        public int Count => _dx.Length;
        public int Dx(int i) => _dx[i];
        public int Dy(int i) => _dy[i];
    }

    // Every piece the game knows; levels draw from a weighted subset by name. Patterns are rows separated by '/'.
    public sealed class ShapeCatalog
    {
        private readonly Shape[] _shapes;
        private readonly Dictionary<string, Shape> _byName = new Dictionary<string, Shape>(StringComparer.Ordinal);

        public ShapeCatalog()
        {
            var specs = new (string Name, int Tier, string Pattern)[]
            {
                ("dot", 1, "#"),
                ("i2", 1, "##"), ("i2v", 1, "#/#"),
                ("i3", 1, "###"), ("i3v", 1, "#/#/#"),
                ("o2", 1, "##/##"),
                ("c3a", 1, "#./##"), ("c3b", 1, ".#/##"), ("c3c", 1, "##/#."), ("c3d", 1, "##/.#"),
                ("i4", 2, "####"), ("i4v", 2, "#/#/#/#"),
                ("l4a", 2, "#./#./##"), ("l4b", 2, "###/#.."), ("l4c", 2, "##/.#/.#"), ("l4d", 2, "..#/###"),
                ("j4a", 2, ".#/.#/##"), ("j4b", 2, "#../###"), ("j4c", 2, "##/#./#."), ("j4d", 2, "###/..#"),
                ("t4a", 2, "###/.#."), ("t4b", 2, ".#/##/.#"), ("t4c", 2, ".#./###"), ("t4d", 2, "#./##/#."),
                ("r23", 2, "###/###"), ("r32", 2, "##/##/##"),
                ("i5", 3, "#####"), ("i5v", 3, "#/#/#/#/#"),
                ("s4", 3, ".##/##."), ("s4v", 3, "#./##/.#"), ("z4", 3, "##./.##"), ("z4v", 3, ".#/##/#."),
                ("c5a", 3, "#../#../###"), ("c5b", 3, "###/#../#.."), ("c5c", 3, "###/..#/..#"), ("c5d", 3, "..#/..#/###"),
                ("o3", 3, "###/###/###"),
            };

            _shapes = new Shape[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                _shapes[i] = new Shape(i, specs[i].Name, specs[i].Tier, specs[i].Pattern);
                _byName.Add(specs[i].Name, _shapes[i]);
            }
        }

        public int Count => _shapes.Length;
        public Shape this[int id] => _shapes[id];
        public IReadOnlyList<Shape> All => _shapes;

        public Shape Find(string name) =>
            _byName.TryGetValue(name, out var shape) ? shape : throw new ArgumentException($"Unknown shape '{name}'.");
    }
}
