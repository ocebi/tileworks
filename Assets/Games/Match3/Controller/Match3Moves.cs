using System;
using Match3.Service;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Match3.Controller
{
    // Moves made while gems fall, and the view's landing reports. A swap between landed gems goes through at once; one touching a falling gem waits up to
    // TapBuffer for it to land, retried on every landing. When the time is up, a swap between landed gems the rules still
    // refuse gets the refusal and one still waiting on a falling gem is dropped. Nothing goes through or waits while a
    // special, combo, shuffle or booster holds the board. Each move tells the board where it was made, so the refill
    // ripples out from there.
    public sealed class Match3Moves : IPieceLandingHandler
    {
        private readonly IMatch3BoardView _board;
        private readonly IClock _clock;
        private Match3Session _session;
        private int _from = -1;
        private int _to = -1;
        private DateTime _at;

        public Match3Moves(IMatch3BoardView board, IClock clock)
        {
            _board = board;
            _clock = clock;
        }

        public void Begin(Match3Session session)
        {
            _session = session;
            Drop();
        }

        public void Swap(int from, int to)
        {
            Drop();
            if (_board.EffectsBusy) return;
            if (_session.CanActOn(from) && _session.CanActOn(to))
            {
                Submit(from, to);
                return;
            }

            if (_board.TapBuffer > 0f) (_from, _to, _at) = (from, to, _clock.UtcNow);
        }

        public void Activate(int cell)
        {
            if (_board.EffectsBusy || !_session.CanActOn(cell)) return;
            _board.SetOrigin(cell);
            _session.Activate(cell);
        }

        // The view saw this gem land; a clear it makes ripples out from its cell.
        public void OnLanded(int id)
        {
            if (_session == null) return;
            var cell = _session.CellOf(id);
            if (cell < 0) return;
            _board.SetOrigin(cell);
            _session.Land(cell, id);
            Retry();
        }

        public ActionResult Hammer(int cell)
        {
            _board.SetOrigin(cell);
            return _session.Hammer(cell);
        }

        // The pieces fly from the middle of the board.
        public ActionResult Shuffle()
        {
            var s = _session.State;
            _board.SetOrigin(s.CellAt(s.Width / 2, s.Height / 2));
            return _session.Shuffle();
        }

        // A waiting swap is retried every frame while the level plays, and dropped once it doesn't.
        public void Tick(bool playing)
        {
            if (playing) Retry();
            else Drop();
        }

        public void Drop() => _from = _to = -1;

        private void Submit(int from, int to)
        {
            var refused = _session.Check(from, to);
            if (!refused.IsNone)
            {
                _board.Refuse(from, to, refused);
                return;
            }

            _board.SetOrigin(to);
            _session.Swap(from, to);
        }

        private void Retry()
        {
            if (_from < 0) return;
            if (_board.EffectsBusy)
            {
                Drop();
                return;
            }

            var (from, to) = (_from, _to);
            var landed = _session.CanActOn(from) && _session.CanActOn(to);
            if (landed && _session.Check(from, to).IsNone)
            {
                Drop();
                _board.SetOrigin(to);
                _session.Swap(from, to);
                return;
            }

            if ((_clock.UtcNow - _at).TotalSeconds < _board.TapBuffer) return;
            Drop();
            if (landed) _board.Refuse(from, to, _session.Check(from, to));
        }
    }
}
