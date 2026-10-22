using System;
using Tileworks.Tiles.Search;

namespace Blast.Model
{
    // The skilled bot's judgement. A move is worth its cubes, the special it makes (5 / 7 / 9 cubes) or the combo it
    // fires, goal cubes and blockers it hits, and cubes right under a duck. A position is worth its goal progress (more
    // so when behind the move budget), specials waiting on the board and next to each other, groups big enough for a
    // special, blockers that a group can reach, and how low the ducks are and whether the cube under each can go next.
    public sealed class BlastEvaluator : IPositionEvaluator<BlastState, Tap>
    {
        private const int GoalWeight = 4000;

        // BlastBoard.Neighbour's direction for the cell below.
        private const int Down = 1;

        public int Score(BlastState s, in Tap move)
        {
            var cell = move.Cell;
            var size = BlastBoard.FindGroup(s, cell);
            if (Pieces.IsSpecial(s.Pieces[cell])) return 300 + (size >= 2 ? ComboWorth(s, size) : FireWorth(s.Pieces[cell]));

            var colour = s.Colours[cell];
            var score = size * 10 + CreationWorth(size);
            var goals = s.Level.Goals;
            for (var i = 0; i < goals.Length; i++)
                if (goals[i].Kind == GoalKind.Colour && goals[i].Colour == colour && s.GoalsLeft[i] > 0) score += size * 20;

            for (var i = 0; i < size; i++)
            {
                var member = s.Group[i];
                if (HasDuckAbove(s, member)) score += 30;
                if (member >= s.Width && s.Pieces[member - s.Width] == Piece.Duck) score += 200;
                for (var d = 0; d < 4; d++)
                {
                    var n = BlastBoard.Neighbour(s, member, d);
                    if (n >= 0 && (s.Pieces[n] == Piece.Balloon || s.Pieces[n] == Piece.Box)) score += 40;
                }
            }

            return score;
        }

        public int Value(BlastState s)
        {
            var value = GoalValue(s);
            Span<bool> seen = stackalloc bool[s.CellCount];
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                var piece = s.Pieces[cell];
                if (Pieces.IsSpecial(piece))
                {
                    value += BoardWorth(piece);
                    for (var d = 0; d < 2; d++)
                    {
                        var n = BlastBoard.Neighbour(s, cell, d);
                        if (n >= 0 && Pieces.IsSpecial(s.Pieces[n])) value += 200;
                    }

                    continue;
                }

                if (piece == Piece.Duck)
                {
                    value -= 15 * (s.Height - 1 - s.Y(cell));
                    var below = BlastBoard.Neighbour(s, cell, Down);
                    if (below >= 0 && s.Pieces[below] == Piece.Cube && BlastBoard.FindGroup(s, below) >= 2) value += 150;
                }
                if (piece == Piece.Box || piece == Piece.Balloon)
                {
                    for (var d = 0; d < 4; d++)
                    {
                        var n = BlastBoard.Neighbour(s, cell, d);
                        if (n < 0 || s.Pieces[n] != Piece.Cube) continue;
                        value += 25;
                        break;
                    }
                }

                if (piece != Piece.Cube || seen[cell]) continue;
                var size = BlastBoard.FindGroup(s, cell);
                for (var i = 0; i < size; i++) seen[s.Group[i]] = true;
                value += CreationWorth(size) / 2;
            }

            return value;
        }

        // Minus the goal left, weighted up to double when the goal left outruns the moves left.
        private static int GoalValue(BlastState s)
        {
            var goals = s.Level.Goals;
            int total = 0, left = 0;
            for (var i = 0; i < goals.Length; i++)
            {
                total += goals[i].Count;
                left += Math.Max(0, s.GoalsLeft[i]);
            }

            if (total == 0) return 0;
            var goalLeft = (float)left / total;
            var movesLeft = s.Level.Moves == 0 ? 0f : (float)s.MovesLeft / s.Level.Moves;
            var behind = Math.Clamp((goalLeft - movesLeft) * 2f, 0f, 1f);
            return -(int)(goalLeft * GoalWeight * (1f + behind));
        }

        private static int CreationWorth(int size) =>
            size >= TapHandler.DiscoAt ? 800 : size >= TapHandler.BombAt ? 500 : size >= TapHandler.RocketAt ? 250 : 0;

        private static int BoardWorth(Piece piece) => piece switch
        {
            Piece.Disco => 550,
            Piece.Bomb => 350,
            _ => 200,
        };

        private static int FireWorth(Piece piece) => piece switch
        {
            Piece.Disco => 800,
            Piece.Bomb => 500,
            _ => 400,
        };

        // The two strongest specials in s.Group decide the combo.
        private static int ComboWorth(BlastState s, int size)
        {
            Piece first = Piece.Empty, second = Piece.Empty;
            for (var i = 0; i < size; i++)
            {
                var piece = s.Pieces[s.Group[i]];
                if (Rank(piece) > Rank(first)) (first, second) = (piece, first);
                else if (Rank(piece) > Rank(second)) second = piece;
            }

            return BlastBoard.Combine(first, second) switch
            {
                Effect.ClearAll => 3000,
                Effect.DiscoBombs => 2400,
                Effect.DiscoRockets => 2000,
                Effect.BigBomb => 1600,
                Effect.ThickCross => 1300,
                _ => 1000,
            };
        }

        private static int Rank(Piece p) => p == Piece.Disco ? 3 : p == Piece.Bomb ? 2 : Pieces.IsSpecial(p) ? 1 : 0;

        private static bool HasDuckAbove(BlastState s, int cell)
        {
            for (var above = cell - s.Width; above >= 0; above -= s.Width)
                if (s.Pieces[above] == Piece.Duck) return true;
            return false;
        }
    }
}
