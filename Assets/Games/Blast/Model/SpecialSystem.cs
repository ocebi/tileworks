using System;
using Tileworks.Tiles.Model;

namespace Blast.Model
{
    // One wave of specials per pass, so chain reactions play out wave by wave.
    public sealed class SpecialSystem : IRuleSystem<BlastState>
    {
        private readonly BlastBoard _board;

        public SpecialSystem(BlastBoard board) => _board = board;

        public bool Run(BlastState s, RuleContext ctx)
        {
            if (s.PendingCount == 0) return false;
            s.NextCount = 0;
            for (var i = 0; i < s.PendingCount; i++) Fire(s, s.Pending[i], ctx);
            s.SwapPending();
            s.FiredAt = BlastBoard.Wave(ctx);
            return true;
        }

        private void Fire(BlastState s, in PendingSpecial p, RuleContext ctx)
        {
            SpecialFired.Emit(ctx.Events, _board.Ids, p.Id, p.Cell, p.Effect, p.Colour, s.NextChain(ctx.ActionIndex));
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
                    Square(s, ctx, p, BlastLevel.MaxSize);
                    break;
                case Effect.Disco:
                case Effect.DiscoRockets:
                case Effect.DiscoBombs:
                    Disco(s, ctx, p);
                    break;
            }
        }

        // `columns` columns centred on x and `rows` rows centred on y.
        private void Lines(BlastState s, RuleContext ctx, in PendingSpecial p, int x, int y, int columns, int rows)
        {
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (!s.IsPlayable(cell)) continue;
                var inColumn = Math.Abs(s.X(cell) - x) <= columns / 2 && columns > 0;
                var inRow = Math.Abs(s.Y(cell) - y) <= rows / 2 && rows > 0;
                if (inColumn || inRow) _board.Hit(s, ctx, cell, BlastBoard.Distance(s, p.Cell, cell), p.Effect);
            }
        }

        private void Square(BlastState s, RuleContext ctx, in PendingSpecial p, int radius)
        {
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                var distance = BlastBoard.Distance(s, p.Cell, cell);
                if (s.IsPlayable(cell) && distance <= radius) _board.Hit(s, ctx, cell, distance, p.Effect);
            }
        }

        private void Disco(BlastState s, RuleContext ctx, in PendingSpecial p)
        {
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (s.Pieces[cell] != Piece.Cube || s.Colours[cell] != p.Colour) continue;
                var order = BlastBoard.Distance(s, p.Cell, cell);
                if (p.Effect == Effect.Disco)
                {
                    _board.Remove(s, ctx, cell, order, p.Effect);
                    continue;
                }

                var piece = p.Effect == Effect.DiscoBombs ? Piece.Bomb : ctx.Rng.Get(BlastRules.SpecialStream).NextInt(2) == 0 ? Piece.RocketRow : Piece.RocketColumn;
                CubeConverted.Emit(ctx.Events, _board.Ids, s.Ids[cell], cell, piece, order);
                _board.Queue(s, cell, BlastBoard.EffectOf(piece));
            }
        }
    }
}
