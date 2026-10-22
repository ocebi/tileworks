using System;
using System.Text;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;

namespace Match3.Model
{
    // Payload v1, rows top to bottom. "rows": '#' hole, '_' empty playable cell, '.' random gem, '1'-'6' a gem of that
    // colour, 'h'/'v' row/column rocket, 'b' bomb, '*' disco, 'f' flyer, 'x'/'X' box with 1/2 layers, 'o' balloon, 'd' duck.
    // Optional "floor": '.' none, '1'/'2' layers. Optional "chains": 'c' chains the gem in that cell.
    public sealed class Match3LevelSerializer : ILevelSerializer<Match3Level>
    {
        public const string GameId = "match3";

        private static readonly string[] GoalNames = { "colour", "box", "balloon", "duck", "floor", "chain" };

        public string Game => GameId;
        public int CurrentPayloadVersion => 1;

        public Match3Level Read(LevelEnvelope envelope)
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
            var floors = new byte[count];
            var chains = new bool[count];
            var layer = Cells(envelope.Id, rows, width);
            for (var cell = 0; cell < count; cell++) (playable[cell], pieces[cell], tints[cell], hp[cell]) = Decode(layer[cell]);
            var floorLayer = Cells(envelope.Id, p["floor"] as JArray, width);
            if (floorLayer != null)
                for (var cell = 0; cell < count; cell++) floors[cell] = floorLayer[cell] == '.' || floorLayer[cell] == '#' ? (byte)0 : (byte)(floorLayer[cell] - '0');
            var chainLayer = Cells(envelope.Id, p["chains"] as JArray, width);
            if (chainLayer != null)
                for (var cell = 0; cell < count; cell++) chains[cell] = chainLayer[cell] == 'c';

            var goalsJson = (JArray)p["goals"];
            var goals = new Match3Goal[goalsJson.Count];
            for (var i = 0; i < goals.Length; i++)
            {
                var g = goalsJson[i];
                var kind = Array.IndexOf(GoalNames, (string)g["kind"]);
                if (kind < 0) throw new FormatException($"{envelope.Id}: unknown goal '{g["kind"]}'.");
                goals[i] = new Match3Goal((GoalKind)kind, (int)g["count"], (byte)((int?)g["colour"] ?? 0));
            }

            return new Match3Level(envelope.Id, width, height, (int)p["moves"], (int)p["colours"], playable, pieces, tints, hp, floors, chains, goals);
        }

        public JObject Write(Match3Level level)
        {
            var payload = new JObject
            {
                ["moves"] = level.Moves, ["colours"] = level.Colours, ["goals"] = Goals(level),
                ["rows"] = Layer(level, LayerKind.Rows),
            };
            if (Array.Exists(level.Floors, f => f > 0)) payload["floor"] = Layer(level, LayerKind.Floor);
            if (Array.Exists(level.Chains, c => c)) payload["chains"] = Layer(level, LayerKind.Chains);
            return payload;
        }

        public void Validate(Match3Level level, ValidationReport report)
        {
            int boxes = 0, balloons = 0, ducks = 0, floors = 0, chains = 0;
            for (var cell = 0; cell < level.CellCount; cell++)
            {
                switch (level.Pieces[cell])
                {
                    case Piece.Box: boxes++; break;
                    case Piece.Balloon: balloons++; break;
                    case Piece.Duck: ducks++; break;
                }

                if (level.Floors[cell] > 0) floors++;
                if (level.Chains[cell]) chains++;
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
                    case GoalKind.Floor: report.Require(goal.Count <= floors, $"goal {goal} but only {floors} floor cells"); break;
                    case GoalKind.Chain: report.Require(goal.Count <= chains, $"goal {goal} but only {chains} chains"); break;
                }
            }
        }

        // The layer's characters by cell, or null when the level has no such layer.
        private static char[] Cells(string id, JArray rows, int width)
        {
            if (rows == null) return null;
            var cells = new char[rows.Count * width];
            for (var y = 0; y < rows.Count; y++)
            {
                var row = (string)rows[y];
                if (row.Length != width) throw new FormatException($"{id}: row {y} is {row.Length} wide, expected {width}.");
                row.CopyTo(0, cells, y * width, width);
            }

            return cells;
        }

        private enum LayerKind
        {
            Rows,
            Floor,
            Chains,
        }

        private static JArray Layer(Match3Level level, LayerKind kind)
        {
            var rows = new JArray();
            var row = new StringBuilder(level.Width);
            for (var y = 0; y < level.Height; y++)
            {
                row.Clear();
                for (var x = 0; x < level.Width; x++) row.Append(Encode(level, kind, y * level.Width + x));
                rows.Add(row.ToString());
            }

            return rows;
        }

        private static JArray Goals(Match3Level level)
        {
            var goals = new JArray();
            foreach (var goal in level.Goals)
            {
                var g = new JObject { ["kind"] = GoalNames[(int)goal.Kind], ["count"] = goal.Count };
                if (goal.Kind == GoalKind.Colour) g["colour"] = goal.Colour;
                goals.Add(g);
            }

            return goals;
        }

        private static (bool Playable, Piece Piece, byte Tint, byte Hp) Decode(char c)
        {
            switch (c)
            {
                case '#': return (false, Piece.Empty, 0, 0);
                case '_': return (true, Piece.Empty, 0, 0);
                case '.': return (true, Piece.Gem, 0, 0);
                case 'h': return (true, Piece.RocketRow, 0, 0);
                case 'v': return (true, Piece.RocketColumn, 0, 0);
                case 'b': return (true, Piece.Bomb, 0, 0);
                case '*': return (true, Piece.Disco, 0, 0);
                case 'f': return (true, Piece.Flyer, 0, 0);
                case 'x': return (true, Piece.Box, 0, 1);
                case 'X': return (true, Piece.Box, 0, 2);
                case 'o': return (true, Piece.Balloon, 0, 0);
                case 'd': return (true, Piece.Duck, 0, 0);
            }

            if (c >= '1' && c <= '6') return (true, Piece.Gem, (byte)(c - '0'), 0);
            throw new FormatException($"Unknown cell '{c}'.");
        }

        private static char Encode(Match3Level level, LayerKind kind, int cell) => kind switch
        {
            LayerKind.Floor => level.Floors[cell] == 0 ? '.' : (char)('0' + level.Floors[cell]),
            LayerKind.Chains => level.Chains[cell] ? 'c' : '.',
            _ => Encode(level.Playable[cell], level.Pieces[cell], level.Tints[cell], level.Hp[cell]),
        };

        private static char Encode(bool playable, Piece piece, byte tint, byte hp)
        {
            if (!playable) return '#';
            return piece switch
            {
                Piece.Gem => tint == 0 ? '.' : (char)('0' + tint),
                Piece.RocketRow => 'h',
                Piece.RocketColumn => 'v',
                Piece.Bomb => 'b',
                Piece.Disco => '*',
                Piece.Flyer => 'f',
                Piece.Box => hp > 1 ? 'X' : 'x',
                Piece.Balloon => 'o',
                Piece.Duck => 'd',
                Piece.Empty => '_',
                _ => throw new FormatException($"Can't encode {piece}."),
            };
        }
    }
}
