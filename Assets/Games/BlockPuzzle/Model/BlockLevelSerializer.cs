using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;

namespace BlockPuzzle.Model
{
    // Payload v1: rows top to bottom, '.' empty, 'A'-'G' a colour, 'a'-'g' that colour with a gem; pool as name → weight.
    public sealed class BlockLevelSerializer : ILevelSerializer<BlockLevel>
    {
        public const string GameId = "blockpuzzle";

        private readonly ShapeCatalog _shapes;

        public BlockLevelSerializer(ShapeCatalog shapes) => _shapes = shapes;

        public string Game => GameId;
        public int CurrentPayloadVersion => 1;

        public BlockLevel Read(LevelEnvelope envelope)
        {
            if (envelope.Game != GameId) throw new FormatException($"{envelope.Id} is a '{envelope.Game}' level.");
            if (envelope.PayloadVersion > CurrentPayloadVersion) throw new FormatException($"{envelope.Id} payload v{envelope.PayloadVersion} is newer than this build.");

            var p = envelope.Payload;
            var rows = (JArray)p["rows"];
            var height = rows.Count;
            var width = ((string)rows[0]).Length;
            var colours = new byte[width * height];
            var gems = new bool[width * height];
            for (var y = 0; y < height; y++)
            {
                var row = (string)rows[y];
                if (row.Length != width) throw new FormatException($"{envelope.Id}: row {y} is {row.Length} wide, expected {width}.");
                for (var x = 0; x < width; x++) (colours[y * width + x], gems[y * width + x]) = Decode(row[x]);
            }

            var pool = new List<int>();
            var weights = new List<int>();
            foreach (var entry in (JObject)p["pool"])
            {
                pool.Add(_shapes.Find(entry.Key).Id);
                weights.Add((int)entry.Value);
            }

            var goal = (string)p["goal"] == "gems" ? BlockGoal.Gems : BlockGoal.Score;
            return new BlockLevel(envelope.Id, width, height, colours, gems, goal, (int?)p["score"] ?? 0, pool.ToArray(), weights.ToArray());
        }

        public JObject Write(BlockLevel level)
        {
            var rows = new JArray();
            var row = new StringBuilder(level.Width);
            for (var y = 0; y < level.Height; y++)
            {
                row.Clear();
                for (var x = 0; x < level.Width; x++) row.Append(Encode(level.Colours[y * level.Width + x], level.Gems[y * level.Width + x]));
                rows.Add(row.ToString());
            }

            var pool = new JObject();
            for (var i = 0; i < level.Pool.Length; i++) pool[_shapes[level.Pool[i]].Name] = level.Weights[i];
            var payload = new JObject { ["goal"] = level.Goal == BlockGoal.Gems ? "gems" : "score", ["rows"] = rows, ["pool"] = pool };
            if (level.Goal == BlockGoal.Score) payload["score"] = level.ScoreTarget;
            return payload;
        }

        public void Validate(BlockLevel level, ValidationReport report)
        {
            report.Require(level.Width <= 32 && level.Height <= 32, "board wider or taller than 32");
            report.Require(level.Goal != BlockGoal.Score || level.ScoreTarget > 0, "score goal without a target");
            for (var y = 0; y < level.Height; y++)
            {
                var full = true;
                for (var x = 0; x < level.Width; x++) full &= level.Colours[y * level.Width + x] != 0;
                report.Require(!full, $"row {y} starts full");
            }

            for (var x = 0; x < level.Width; x++)
            {
                var full = true;
                for (var y = 0; y < level.Height; y++) full &= level.Colours[y * level.Width + x] != 0;
                report.Require(!full, $"column {x} starts full");
            }
        }

        private static (byte Colour, bool Gem) Decode(char c)
        {
            if (c == '.') return (0, false);
            if (c >= 'A' && c <= 'G') return ((byte)(c - 'A' + 1), false);
            if (c >= 'a' && c <= 'g') return ((byte)(c - 'a' + 1), true);
            throw new FormatException($"Unknown cell '{c}'.");
        }

        private static char Encode(byte colour, bool gem) => colour == 0 ? '.' : (char)((gem ? 'a' : 'A') + colour - 1);
    }
}
