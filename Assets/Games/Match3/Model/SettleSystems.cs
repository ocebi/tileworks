using Tileworks.Tiles.Model;

namespace Match3.Model
{
    // The board settles under the shared gravity in the wave after a clear, and sends each piece's path as legs. Every
    // piece that moved or arrived is in flight until the view reports it landed.
    public sealed class SettleSystem : IRuleSystem<Match3State>
    {
        private readonly Match3EventIds _ids;

        public SettleSystem(Match3EventIds ids) => _ids = ids;

        public bool Run(Match3State s, RuleContext ctx)
        {
            var wave = ctx.Events.Step;
            if (s.PendingCount > 0 || s.ClearedWave == wave) return false;
            if (!Gravity.Settle(s, ref ctx.Rng.Get(Match3Rules.RefillStream))) return false;

            var legs = s.Legs;
            for (var i = 0; i < legs.Count; i++)
            {
                ref readonly var leg = ref legs[i];
                if (leg.From < 0) PieceSpawned.Emit(ctx.Events, _ids, leg.Id, leg.To, leg.Colour, leg.Drop, leg.Lands);
                else PieceFell.Emit(ctx.Events, _ids, leg.Id, leg.From, leg.To, leg.Lands);
            }

            s.MovedWave = wave;
            return true;
        }
    }

    // A duck that has landed on the lowest playable cell of its column is collected.
    public sealed class DuckSystem : IRuleSystem<Match3State>
    {
        private readonly Match3Board _board;

        public DuckSystem(Match3Board board) => _board = board;

        public bool Run(Match3State s, RuleContext ctx)
        {
            if (s.PendingCount > 0 || s.ClearedWave == ctx.Events.Step) return false;
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

            if (collected) s.ClearedWave = ctx.Events.Step;
            return collected;
        }
    }

    // Once a wave neither cleared nor moved anything, the matches the falls made clear in the next one.
    public sealed class MatchSystem : IRuleSystem<Match3State>
    {
        private readonly MatchClearer _matches;
        private readonly Match3EventIds _ids;

        public MatchSystem(MatchClearer matches, Match3EventIds ids)
        {
            _matches = matches;
            _ids = ids;
        }

        public bool Run(Match3State s, RuleContext ctx)
        {
            var wave = ctx.Events.Step;
            if (s.PendingCount > 0 || s.ClearedWave == wave || s.MovedWave == wave) return false;
            if (!_matches.ClearAll(s, ctx)) return false;
            if (s.Matches >= 2) Cascade.Emit(ctx.Events, _ids, s.Matches);
            s.ClearedWave = wave;
            return true;
        }
    }

    // Lands the pieces LandAll asked for, one per quiet pass, so each landing resolves fully before the next. Runs last.
    public sealed class LandingSystem : IRuleSystem<Match3State>
    {
        public bool Run(Match3State s, RuleContext ctx)
        {
            var flight = s.Flight;
            if (!flight.LandingAll || ctx.PassChanged) return false;
            var cell = flight.Next();
            if (cell < 0)
            {
                flight.LandingAll = false;
                return false;
            }

            Match3Board.Land(s, cell);
            return true;
        }
    }
}
