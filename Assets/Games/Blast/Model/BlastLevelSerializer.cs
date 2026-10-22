using System;
using System.Text;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;

namespace Blast.Model
{
    // Payload v1: rows top to bottom. '#' hole, '.' random cube, '1'-'6' a cube of that colour, 'h'/'v' row/column rocket,
    // 'b' bomb, '*' disco ball, 'x'/'X' box with 1/2 layers, 'o' balloon, 'd' duck, '_' an empty cell (a pen behind boxes that
    // fills once its wall breaks; the board doesn't settle at the start).
    public sealed class BlastLevelSerializer : ILevelSerializer<BlastLevel>
    {
        public const string GameId = "blast";

        private static readonly string[] GoalNames = { "colour", "box", "balloon", "duck" };

        public string Game => GameId;
        public int CurrentPayloadVersion => 1;

        public BlastLevel Read(LevelEnvelope envelope)
        {
            if (envelope.Game != GameId) throw new FormatException($"{envelope.Id} is a '{envelope.Game}' level.");
            if (envelope.PayloadVersion > CurrentPayloadVersion) throw new FormatException($"{envelope.Id} payload v{envelope.PayloadVersion} is newer than this build.");

            var p = envelope.Payload;
            var rows = (JArray)p["rows"];
            var height = rows.Count;
            var width = ((string)rows[0]).Length;
            var count = width * height;
            var playable = new bool[count];
            var pieces = new Piece[count];
            var tints = new byte[count];
            var hp = new byte[count];
            for (var y = 0; y < height; y++)
            {
                var row = (string)rows[y];
                if (row.Length != width) throw new FormatException($"{envelope.Id}: row {y} is {row.Length} wide, expected {width}.");
                for (var x = 0; x < width; x++)
                {
                    var cell = y * width + x;
                    (playable[cell], pieces[cell], tints[cell], hp[cell]) = Decode(row[x]);
                }
            }

            var goalsJson = (JArray)p["goals"];
            var goals = new BlastGoal[goalsJson.Count];
            for (var i = 0; i < goals.Length; i++)
            {
                var g = goalsJson[i];
                var kind = (GoalKind)Array.IndexOf(GoalNames, (string)g["kind"]);
                if ((int)kind < 0) throw new FormatException($"{envelope.Id}: unknown goal '{g["kind"]}'.");
                goals[i] = new BlastGoal(kind, (int)g["count"], (byte)((int?)g["colour"] ?? 0));
            }

            return new BlastLevel(envelope.Id, width, height, (int)p["moves"], (int)p["colours"], playable, pieces, tints, hp, goals);
        }

        public JObject Write(BlastLevel level)
        {
            var rows = new JArray();
            var row = new StringBuilder(level.Width);
            for (var y = 0; y < level.Height; y++)
            {
                row.Clear();
                for (var x = 0; x < level.Width; x++)
                {
                    var cell = y * level.Width + x;
                    row.Append(Encode(level.Playable[cell], level.Pieces[cell], level.Tints[cell], level.Hp[cell]));
                }

                rows.Add(row.ToString());
            }

            var goals = new JArray();
            foreach (var goal in level.Goals)
            {
                var g = new JObject { ["kind"] = GoalNames[(int)goal.Kind], ["count"] = goal.Count };
                if (goal.Kind == GoalKind.Colour) g["colour"] = goal.Colour;
                goals.Add(g);
            }

            return new JObject { ["moves"] = level.Moves, ["colours"] = level.Colours, ["goals"] = goals, ["rows"] = rows };
        }

        public void Validate(BlastLevel level, ValidationReport report)
        {
            int boxes = 0, balloons = 0, ducks = 0;
            for (var cell = 0; cell < level.CellCount; cell++)
            {
                switch (level.Pieces[cell])
                {
                    case Piece.Box: boxes++; break;
                    case Piece.Balloon: balloons++; break;
                    case Piece.Duck: ducks++; break;
                }

                report.Require(level.Tints[cell] <= level.Colours, $"cell {cell} uses colour {level.Tints[cell]} of {level.Colours}");
            }

            foreach (var goal in level.Goals)
            {
                report.Require(goal.Count > 0, $"goal {goal} needs a count");
                switch (goal.Kind)
                {
                    case GoalKind.Colour: report.Require(goal.Colour >= 1 && goal.Colour <= level.Colours, $"goal {goal} names a colour the level doesn't use"); break;
                    case GoalKind.Box: report.Require(goal.Count <= boxes, $"goal {goal} but only {boxes} boxes"); break;
                    case GoalKind.Balloon: report.Require(goal.Count <= balloons, $"goal {goal} but only {balloons} balloons"); break;
                    case GoalKind.Duck: report.Require(goal.Count <= ducks, $"goal {goal} but only {ducks} ducks"); break;
                }
            }
        }

        private static (bool Playable, Piece Piece, byte Tint, byte Hp) Decode(char c)
        {
            switch (c)
            {
                case '#': return (false, Piece.Empty, 0, 0);
                case '_': return (true, Piece.Empty, 0, 0);
                case '.': return (true, Piece.Cube, 0, 0);
                case 'h': return (true, Piece.RocketRow, 0, 0);
                case 'v': return (true, Piece.RocketColumn, 0, 0);
                case 'b': return (true, Piece.Bomb, 0, 0);
                case '*': return (true, Piece.Disco, 0, 0);
                case 'x': return (true, Piece.Box, 0, 1);
                case 'X': return (true, Piece.Box, 0, 2);
                case 'o': return (true, Piece.Balloon, 0, 0);
                case 'd': return (true, Piece.Duck, 0, 0);
            }

            if (c >= '1' && c <= '6') return (true, Piece.Cube, (byte)(c - '0'), 0);
            throw new FormatException($"Unknown cell '{c}'.");
        }

        private static char Encode(bool playable, Piece piece, byte tint, byte hp)
        {
            if (!playable) return '#';
            return piece switch
            {
                Piece.Empty => '_',
                Piece.Cube => tint == 0 ? '.' : (char)('0' + tint),
                Piece.RocketRow => 'h',
                Piece.RocketColumn => 'v',
                Piece.Bomb => 'b',
                Piece.Disco => '*',
                Piece.Box => hp > 1 ? 'X' : 'x',
                Piece.Balloon => 'o',
                Piece.Duck => 'd',
                _ => throw new FormatException($"Can't encode {piece}."),
            };
        }
    }
}
