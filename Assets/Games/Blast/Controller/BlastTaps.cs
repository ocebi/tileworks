using System;
using Blast.Model;
using Blast.Service;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Blast.Controller
{
    // Taps while pieces fall: a tap on a landed group goes straight to the rules. One that touches a falling piece, or a
    // lone cube whose neighbour is still falling, waits up to the Feel's tap buffer and is retried as pieces land; on
    // expiry a landed cube gets the refusal, a falling one nothing. Nothing waits while an effect holds the board. Landings
    // the view reports go to the session here, so a waiting tap is retried the moment its piece lands.
    public sealed class BlastTaps : IBoardLandingHandler
    {
        private const int None = -1;

        private readonly IBlastBoardView _board;
        private readonly IClock _clock;
        private BlastSession _session;
        private int _waiting = None;
        private DateTime _expires;

        public BlastTaps(IBlastBoardView board, IClock clock)
        {
            _board = Guard.NotNull(board, nameof(board));
            _clock = Guard.NotNull(clock, nameof(clock));
        }

        public void Begin(BlastSession session)
        {
            _session = session;
            _waiting = None;
        }

        public void Tap(int cell)
        {
            _waiting = None;
            if (_board.EffectsBusy || Settle(cell)) return;
            _waiting = cell;
            _expires = _clock.UtcNow.AddSeconds(_board.TapBuffer);
        }

        // On each landing report and each frame until the waiting tap is taken or expires.
        public void Retry()
        {
            if (_waiting == None) return;
            if (_board.EffectsBusy || _session.State.Outcome != Outcome.Ongoing)
            {
                _waiting = None;
                return;
            }

            var cell = _waiting;
            if (Settle(cell))
            {
                _waiting = None;
                return;
            }

            if (_clock.UtcNow < _expires) return;
            _waiting = None;
            if (!_session.State.Flight[cell]) _board.Refuse(cell);
        }

        public void Cancel() => _waiting = None;

        // A landing can set something off (a duck reaching the bottom); its falls stagger away from that cell.
        public void OnLanded(int id, int cell)
        {
            if (_session == null) return;
            _board.SetOrigin(cell);
            _session.Land(cell, id);
            Retry();
        }

        // True once the tap is decided: taken by the rules, or refused for good with feedback.
        private bool Settle(int cell)
        {
            if (!_session.CanActOn(cell)) return false;
            var result = _session.Tap(cell);
            if (result.Accepted) return true;
            if (result.Rejected == BlastRejects.Falling) return false;
            if (result.Rejected == BlastRejects.GroupTooSmall && _session.NextToFlight(cell)) return false;
            _board.Refuse(cell);
            return true;
        }
    }
}
