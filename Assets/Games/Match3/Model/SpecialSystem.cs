using System;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Match3.Model
{
    // One wave of specials per pass, so chain reactions play out wave by wave.
    public sealed class SpecialSystem : IRuleSystem<Match3State>
    {
        private readonly Match3Board _board;

        public SpecialSystem(Match3Board board) => _board = board;

        public bool Run(Match3State s, RuleContext ctx)
        {
            if (s.PendingCount == 0) return false;
            s.NextCount = 0;
            var targeted = s.NewMark();
            for (var i = 0; i < s.PendingCount; i++) Fire(s, s.Pending[i], ctx, targeted);
            s.SwapPending();
            s.ClearedWave = ctx.Events.Step;
            return true;
        }

        private void Fire(Match3State s, in PendingSpecial p, RuleContext ctx, int targeted)
        {
            var colour = p.Colour == 0 && IsDisco(p.Effect) ? Match3Board.MostCommonColour(s) : p.Colour;
            SpecialFired.Emit(ctx.Events, _board.Ids, p.Id, p.Cell, p.Effect, colour);
            int x = s.X(p.Cell), y = s.Y(p.Cell);
            switch (p.Effect)
            {
                case Effect.Row:
                    Lines(s, ctx, p, x, y, 0, 1);
                    break;
                case Effect.Column:
                    Lines(s, ctx, p, x, y, 1, 0);
                    break;
                case Effect.Cross:
                    Lines(s, ctx, p, x, y, 1, 1);
                    break;
                case Effect.ThickCross:
                    Lines(s, ctx, p, x, y, 3, 3);
                    break;
                case Effect.Bomb:
                    Square(s, ctx, p, 2);
                    break;
                case Effect.BigBomb:
                    Square(s, ctx, p, 3);
                    break;
                case Effect.ClearAll:
                    Square(s, ctx, p, Match3Level.MaxHeight);
                    break;
                case Effect.Disco:
                    Disco(s, ctx, p, colour, Piece.Empty);
                    break;
                case Effect.DiscoRockets:
                    Disco(s, ctx, p, colour, Piece.RocketRow);
                    break;
                case Effect.DiscoBombs:
                    Disco(s, ctx, p, colour, Piece.Bomb);
                    break;
                case Effect.DiscoFlyers:
                    Disco(s, ctx, p, colour, Piece.Flyer);
                    break;
                case Effect.Flyer:
                    Fly(s, ctx, p, Effect.None, 1, targeted);
                    break;
                case Effect.FlyerRow:
                    Fly(s, ctx, p, Effect.Row, 1, targeted);
                    break;
                case Effect.FlyerColumn:
                    Fly(s, ctx, p, Effect.Column, 1, targeted);
                    break;
                case Effect.FlyerBomb:
                    Fly(s, ctx, p, Effect.Bomb, 1, targeted);
                    break;
                case Effect.TripleFlyer:
                    Fly(s, ctx, p, Effect.None, 3, targeted);
                    break;
            }
        }

        private static bool IsDisco(Effect e) => e == Effect.Disco || e == Effect.DiscoRockets || e == Effect.DiscoBombs || e == Effect.DiscoFlyers;

        // `columns` columns centred on x and `rows` rows centred on y.
        private void Lines(Match3State s, RuleContext ctx, in PendingSpecial p, int x, int y, int columns, int rows)
        {
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (!s.IsPlayable(cell)) continue;
                var inColumn = columns > 0 && Math.Abs(s.X(cell) - x) <= columns / 2;
                var inRow = rows > 0 && Math.Abs(s.Y(cell) - y) <= rows / 2;
                if (inColumn || inRow) _board.Hit(s, ctx, cell, Match3Board.Distance(s, p.Cell, cell), p.Effect);
            }
        }

        private void Square(Match3State s, RuleContext ctx, in PendingSpecial p, int radius)
        {
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                var distance = Match3Board.Distance(s, p.Cell, cell);
                if (s.IsPlayable(cell) && distance <= radius) _board.Hit(s, ctx, cell, distance, p.Effect);
            }
        }

        // Clears every gem of the colour, or turns each into `into` to fire next wave (rockets point either way).
        private void Disco(Match3State s, RuleContext ctx, in PendingSpecial p, byte colour, Piece into)
        {
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (s.Pieces[cell] != Piece.Gem || s.Colours[cell] != colour) continue;
                var order = Match3Board.Distance(s, p.Cell, cell);
                if (into == Piece.Empty || s.Chained[cell])
                {
                    _board.Hit(s, ctx, cell, order, p.Effect);
                    continue;
                }

                var piece = into == Piece.RocketRow && ctx.Rng.Get(Match3Rules.SpecialStream).NextInt(2) == 1 ? Piece.RocketColumn : into;
                GemConverted.Emit(ctx.Events, _board.Ids, s.Ids[cell], cell, piece, order);
                _board.Queue(s, cell, Match3Board.EffectOf(piece), 0);
            }
        }

        // Hits the four neighbours, then each flyer picks a target that helps a goal (any piece once the goals are done),
        // hits it on landing and fires what it carries there in the next wave.
        private void Fly(Match3State s, RuleContext ctx, in PendingSpecial p, Effect carried, int flyers, int targeted)
        {
            for (var d = 0; d < 4; d++)
            {
                var n = Match3Board.Neighbour(s, p.Cell, d);
                if (n >= 0) _board.Hit(s, ctx, n, 1, Effect.Flyer);
            }

            ref var rng = ref ctx.Rng.Get(Match3Rules.SpecialStream);
            for (var i = 0; i < flyers; i++)
            {
                var target = Target(s, targeted, ref rng);
                if (target < 0) return;
                var id = i == 0 ? p.Id : Match3Board.NewId(s);
                FlyerLaunched.Emit(ctx.Events, _board.Ids, id, p.Cell, target, carried);
                var pieceId = s.Pieces[target] != Piece.Empty ? s.Ids[target] : -1;
                var floorId = s.Floors[target] > 0 ? s.FloorIds[target] : -1;
                FlyerLanded.Emit(ctx.Events, _board.Ids, id, target, pieceId, floorId);
                _board.Hit(s, ctx, target, 0, Effect.Flyer);
                if (carried != Effect.None) s.Next[s.NextCount++] = new PendingSpecial(target, id, carried, 0);
            }
        }

        private static int Target(Match3State s, int targeted, ref Pcg32 rng)
        {
            var count = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
                if (s.IsPlayable(cell) && s.Marks[cell] != targeted && HelpsGoal(s, cell)) s.Group[count++] = cell;
            if (count == 0)
            {
                for (var cell = 0; cell < s.CellCount; cell++)
                    if (s.IsPlayable(cell) && s.Marks[cell] != targeted && Hittable(s, cell)) s.Group[count++] = cell;
            }

            if (count == 0) return -1;
            var target = s.Group[rng.NextInt(count)];
            s.Marks[target] = targeted;
            return target;
        }

        private static bool HelpsGoal(Match3State s, int cell)
        {
            var goals = s.Level.Goals;
            var piece = s.Pieces[cell];
            for (var i = 0; i < goals.Length; i++)
            {
                if (s.GoalsLeft[i] == 0) continue;
                var helps = goals[i].Kind switch
                {
                    GoalKind.Colour => piece == Piece.Gem && !s.Chained[cell] && s.Colours[cell] == goals[i].Colour,
                    GoalKind.Box => piece == Piece.Box,
                    GoalKind.Balloon => piece == Piece.Balloon,
                    GoalKind.Floor => s.Floors[cell] > 0 && piece != Piece.Box && !s.Chained[cell],
                    GoalKind.Chain => s.Chained[cell],
                    _ => false,
                };
                if (helps) return true;
            }

            return false;
        }

        private static bool Hittable(Match3State s, int cell) => s.Pieces[cell] != Piece.Empty && s.Pieces[cell] != Piece.Duck;
    }
}
