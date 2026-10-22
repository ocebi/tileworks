using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Blast.Model
{
    // Pieces fall within their column segment, the top segment refills and cut-off cells take diagonal slides (see
    // Gravity). Each piece's path goes out as legs; every piece that moved stays in flight until the view lands it.
    public sealed class SettleSystem : IRuleSystem<BlastState>
    {
        private readonly BlastEventIds _ids;

        public SettleSystem(BlastEventIds ids) => _ids = ids;

        public bool Run(BlastState s, RuleContext ctx)
        {
            if (s.PendingCount > 0 || s.FiredAt == BlastBoard.Wave(ctx)) return false;
            if (!Gravity.Settle(s, ref ctx.Rng.Get(BlastRules.RefillStream))) return false;
            var legs = s.Legs;
            for (var i = 0; i < legs.Count; i++)
            {
                ref readonly var leg = ref legs[i];
                if (leg.From < 0) PieceSpawned.Emit(ctx.Events, _ids, leg.Id, leg.To, leg.Colour, leg.Drop, leg.Lands);
                else PieceFell.Emit(ctx.Events, _ids, leg.Id, leg.From, leg.To, leg.Lands);
            }

            return true;
        }
    }

    // A duck that has landed on the lowest playable cell of its column is collected.
    public sealed class DuckSystem : IRuleSystem<BlastState>
    {
        private readonly BlastBoard _board;

        public DuckSystem(BlastBoard board) => _board = board;

        public bool Run(BlastState s, RuleContext ctx)
        {
            if (s.PendingCount > 0 || s.FiredAt == BlastBoard.Wave(ctx)) return false;
            var collected = false;
            for (var x = 0; x < s.Width; x++)
            {
                for (var y = s.Height - 1; y >= 0; y--)
                {
                    var cell = y * s.Width + x;
                    if (!s.IsPlayable(cell)) continue;
                    if (s.Pieces[cell] == Piece.Duck && !s.Flight[cell])
                    {
                        _board.Remove(s, ctx, cell, 0);
                        collected = true;
                    }

                    break;
                }
            }

            return collected;
        }
    }

    // When the board has settled with nothing in flight, no group and no special, the cubes are reshuffled until one
    // appears. Register it after the systems that settle the board: it runs only in a pass where they changed nothing.
    public sealed class ShuffleSystem : IRuleSystem<BlastState>
    {
        private const int Attempts = 20;

        private readonly BlastEventIds _ids;

        public ShuffleSystem(BlastEventIds ids) => _ids = ids;

        public bool Run(BlastState s, RuleContext ctx)
        {
            if (s.PendingCount > 0 || ctx.PassChanged || s.Flight.Count > 0 || BlastBoard.HasMove(s)) return false;
            if (!Shuffle(s, ref ctx.Rng.Get(BlastRules.ShuffleStream))) return false;
            s.Shuffles++;
            for (var cell = 0; cell < s.CellCount; cell++)
                if (s.Pieces[cell] == Piece.Cube) CubeRecoloured.Emit(ctx.Events, _ids, s.Ids[cell], cell, s.Colours[cell]);
            return true;
        }

        // False when there aren't two touching cubes to make a group from.
        public static bool Shuffle(BlastState s, ref Pcg32 rng)
        {
            var count = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
                if (s.Pieces[cell] == Piece.Cube) s.Group[count++] = cell;
            if (count < 2) return false;

            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                for (var i = count - 1; i > 0; i--)
                {
                    var j = rng.NextInt(i + 1);
                    (s.Colours[s.Group[i]], s.Colours[s.Group[j]]) = (s.Colours[s.Group[j]], s.Colours[s.Group[i]]);
                }

                if (BlastBoard.HasMove(s)) return true;
            }

            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                for (var d = 0; d < 4; d++)
                {
                    var n = BlastBoard.Neighbour(s, cell, d);
                    if (n < 0 || s.Pieces[n] != Piece.Cube) continue;
                    s.Colours[n] = s.Colours[cell];
                    return true;
                }
            }

            return false;
        }
    }
}
