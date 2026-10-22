using System;

namespace BlockPuzzle.Model
{
    public enum BlockGoal : byte
    {
        Score = 0,
        Gems = 1,
    }

    // Immutable level definition. Cells are row-major with row 0 at the top; colour 0 is empty.
    public sealed class BlockLevel
    {
        public BlockLevel(string id, int width, int height, byte[] colours, bool[] gems, BlockGoal goal, int scoreTarget,
            int[] pool, int[] weights)
        {
            if (width < 4 || height < 4 || width > 12 || height > 12) throw new ArgumentException($"Board {width}x{height} is out of range.");
            if (colours.Length != width * height || gems.Length != colours.Length) throw new ArgumentException("Cell arrays do not match the board.");
            if (pool.Length == 0 || pool.Length != weights.Length) throw new ArgumentException("The shape pool needs one weight per shape.");
            Id = id;
            Width = width;
            Height = height;
            Colours = colours;
            Gems = gems;
            Goal = goal;
            ScoreTarget = scoreTarget;
            Pool = pool;
            Weights = weights;
            for (var i = 0; i < gems.Length; i++)
            {
                if (!gems[i]) continue;
                if (colours[i] == 0) throw new ArgumentException($"Gem at cell {i} sits on an empty cell.");
                GemCount++;
            }

            if (goal == BlockGoal.Gems && GemCount == 0) throw new ArgumentException("A gem goal needs gems.");
        }

        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public int CellCount => Width * Height;
        public byte[] Colours { get; }
        public bool[] Gems { get; }
        public int GemCount { get; }
        public BlockGoal Goal { get; }
        public int ScoreTarget { get; }
        public int[] Pool { get; }
        public int[] Weights { get; }
    }
}
