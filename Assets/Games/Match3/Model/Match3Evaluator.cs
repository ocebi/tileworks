using System;
using Tileworks.Tiles.Search;

namespace Match3.Model
{
    // The skilled bot's judgement. A swap is worth its matched gems, the special it makes (4-line, 2×2, L/T, 5-line) or
    // the special or combo it fires, and the goals and blockers its matches hit. A position is worth its goal progress
    // (more so when behind the move budget), specials waiting on the board and next to each other, swaps that would make
    // a special, blockers next to gems, and how low the ducks are.
    public sealed class Match3Evaluator : IPositionEvaluator<Match3State, Swap>
    {
        private const int GoalWeight = 4000;
        private const int MaxSetups = 4;

        public int Score(Match3State s, in Swap move)
        {
            var a = s.Pieces[move.From];
            var b = s.Pieces[move.To];
            var specialA = Pieces.IsSpecial(a);
            var specialB = Pieces.IsSpecial(b);
            if (specialA && specialB) return 300 + ComboWorth(Match3Board.Combine(a, b));
            if (specialA || specialB) return 300 + FireWorth(specialA ? a : b);

            Match3Board.Exchange(s, move.From, move.To);
            var score = MatchWorth(s, move.To) + MatchWorth(s, move.From);
            Match3Board.Exchange(s, move.From, move.To);
            return score;
        }

        public int Value(Match3State s)
        {
            var value = GoalValue(s);
            int setups = 0, bestSetup = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                var piece = s.Pieces[cell];
                if (Pieces.IsSpecial(piece))
                {
                    value += BoardWorth(piece);
                    for (var d = 0; d < 2; d++)
                    {
                        var n = Match3Board.Neighbour(s, cell, d);
                        if (n >= 0 && Pieces.IsSpecial(s.Pieces[n])) value += 200;
                    }
                }
                else if (piece == Piece.Duck)
                {
                    value -= 15 * (s.Height - 1 - s.Y(cell));
                }
                else if (piece == Piece.Box || piece == Piece.Balloon)
                {
                    if (NextToGem(s, cell)) value += 25;
                }

                if (piece != Piece.Gem || !Matches.CanSwap(s, cell)) continue;
                for (var d = 0; d < 2; d++)
                {
                    var n = Match3Board.Neighbour(s, cell, d);
                    if (n < 0 || s.Pieces[n] != Piece.Gem || !Matches.CanSwap(s, n)) continue;
                    var made = Match3Hints.Value(s, cell, n);
                    if (made < 1000) continue;
                    setups++;
                    bestSetup = Math.Max(bestSetup, made);
                }
            }

            value += Math.Min(setups, MaxSetups) * 60;
            if (bestSetup > 0) value += CreationWorth(RankOfSetup(bestSetup)) / 2;
            return value;
        }

        // The gems a match through `cell` clears, the special it leaves, and the goals and blockers it works on.
        private static int MatchWorth(Match3State s, int cell)
        {
            var made = Matches.SpecialAt(s, cell, out var size);
            if (size == 0) return 0;
            var score = size * 10 + (made == Piece.Empty ? 0 : CreationWorth(Rank(made)));
            var goals = s.Level.Goals;
            for (var i = 0; i < size; i++)
            {
                var matched = s.Group[i];
                if (s.Floors[matched] > 0) score += 30;
                if (s.Chained[matched]) score += 30;
                for (var g = 0; g < goals.Length; g++)
                    if (goals[g].Kind == GoalKind.Colour && goals[g].Colour == s.Colours[matched] && s.GoalsLeft[g] > 0) score += 20;
                for (var d = 0; d < 4; d++)
                {
                    var n = Match3Board.Neighbour(s, matched, d);
                    if (n >= 0 && (s.Pieces[n] == Piece.Balloon || s.Pieces[n] == Piece.Box)) score += 40;
                }
            }

            return score;
        }

        // Minus the goal left, weighted up to double when the goal left outruns the moves left.
        private static int GoalValue(Match3State s)
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

        private static bool NextToGem(Match3State s, int cell)
        {
            for (var d = 0; d < 4; d++)
            {
                var n = Match3Board.Neighbour(s, cell, d);
                if (n >= 0 && s.Pieces[n] == Piece.Gem) return true;
            }

            return false;
        }

        // Match3Hints.Value of a special-making swap is 1000 + 100 × rank + the matched gems.
        private static int RankOfSetup(int hintValue) => (hintValue - 1000) / 100;

        private static int Rank(Piece p) => p switch
        {
            Piece.Disco => 4,
            Piece.Bomb => 3,
            Piece.Flyer => 2,
            _ => Pieces.IsSpecial(p) ? 1 : 0,
        };

        private static int CreationWorth(int rank) => rank switch
        {
            4 => 800,
            3 => 500,
            2 => 350,
            1 => 250,
            _ => 0,
        };

        private static int BoardWorth(Piece piece) => piece switch
        {
            Piece.Disco => 550,
            Piece.Bomb => 350,
            Piece.Flyer => 250,
            _ => 200,
        };

        private static int FireWorth(Piece piece) => piece switch
        {
            Piece.Disco => 800,
            Piece.Bomb => 500,
            Piece.Flyer => 450,
            _ => 400,
        };

        private static int ComboWorth(Effect combo) => combo switch
        {
            Effect.ClearAll => 3000,
            Effect.DiscoBombs => 2400,
            Effect.DiscoFlyers => 2200,
            Effect.DiscoRockets => 2000,
            Effect.BigBomb => 1600,
            Effect.FlyerBomb => 1400,
            Effect.ThickCross => 1300,
            Effect.TripleFlyer => 1200,
            Effect.FlyerRow or Effect.FlyerColumn => 1100,
            _ => 1000,
        };
    }
}
