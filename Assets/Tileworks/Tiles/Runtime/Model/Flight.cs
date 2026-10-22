using System;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Model
{
    // The view reports that the piece with this id came to rest in the cell; costs no move.
    public readonly struct Land : IGameAction
    {
        public readonly int Cell;
        public readonly int Id;

        public Land(int cell, int id)
        {
            Cell = cell;
            Id = id;
        }

        public override string ToString() => $"Land({Cell}, #{Id})";
    }

    // Lands every piece in flight, one at a time in canonical order, resolving between landings. Bots, replays and the
    // session's watchdog use it.
    public readonly struct LandAll : IGameAction
    {
    }

    // A state whose pieces fly after gravity moves them until the view reports them landed.
    public interface IFlightState
    {
        Outcome Outcome { get; }
        FlightMarks Flight { get; }
        int IdAt(int cell);
    }

    // Which cells hold a piece in flight. Part of the state: copy it in CopyTo and mix it into ComputeHash.
    public sealed class FlightMarks
    {
        private readonly bool[] _cells;
        private readonly int _width;

        public FlightMarks(int width, int cellCount)
        {
            _width = width;
            _cells = new bool[cellCount];
        }

        public int Count { get; private set; }
        public int Length => _cells.Length;
        public bool this[int cell] => _cells[cell];

        // Set by LandAll for the rest of its action; not part of the state.
        public bool LandingAll { get; set; }

        public void Launch(int cell)
        {
            if (_cells[cell]) return;
            _cells[cell] = true;
            Count++;
        }

        public void Land(int cell)
        {
            if (!_cells[cell]) return;
            _cells[cell] = false;
            Count--;
        }

        // Keeps the flag with a piece that moves; the source cell becomes empty and grounded.
        public void Move(int from, int to)
        {
            var flying = _cells[from];
            Land(from);
            if (flying) Launch(to);
            else Land(to);
        }

        // Lowest row first, then left to right; -1 when nothing is in flight.
        public int Next()
        {
            if (Count == 0) return -1;
            for (var row = _cells.Length / _width - 1; row >= 0; row--)
            {
                for (int x = 0, cell = row * _width; x < _width; x++, cell++)
                    if (_cells[cell]) return cell;
            }

            return -1;
        }

        public void Clear()
        {
            Array.Clear(_cells, 0, _cells.Length);
            Count = 0;
            LandingAll = false;
        }

        public void CopyTo(FlightMarks target)
        {
            Array.Copy(_cells, target._cells, _cells.Length);
            target.Count = Count;
            target.LandingAll = false;
        }

        public ulong Hash(ulong hash)
        {
            hash = StableHash.Combine(hash, (ulong)Count);
            for (var cell = 0; cell < _cells.Length; cell++)
                if (_cells[cell]) hash = StableHash.Combine(hash, (ulong)cell);
            return hash;
        }
    }

    public static class FlightRules
    {
        // Land is valid only for the piece the view saw land, while it is still in flight and the level is going on.
        public static RejectReason Check<TState>(TState s, in Land action) where TState : IFlightState
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            var cell = action.Cell;
            if (cell < 0 || cell >= s.Flight.Length || !s.Flight[cell] || s.IdAt(cell) != action.Id) return RejectReason.NotInFlight;
            return RejectReason.None;
        }

        public static RejectReason Check<TState>(TState s) where TState : IFlightState
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            return s.Flight.Count == 0 ? RejectReason.NotInFlight : RejectReason.None;
        }
    }

    // The plain handlers: a game whose landing needs more than the flag cleared writes its own around FlightRules.
    public sealed class LandHandler<TState> : IActionHandler<TState, Land> where TState : IFlightState
    {
        public RejectReason Validate(TState s, in Land action) => FlightRules.Check(s, action);
        public void Apply(TState s, in Land action, RuleContext ctx) => s.Flight.Land(action.Cell);
    }

    public sealed class LandAllHandler<TState> : IActionHandler<TState, LandAll> where TState : IFlightState
    {
        public RejectReason Validate(TState s, in LandAll action) => FlightRules.Check(s);
        public void Apply(TState s, in LandAll action, RuleContext ctx) => s.Flight.LandingAll = true;
    }

    // Lands the pieces LandAll asked for, one per quiet pass. Register it after every other system: it lands only in a
    // pass where nothing else changed, so each landing resolves fully before the next.
    public sealed class LandAllSystem<TState> : IRuleSystem<TState> where TState : IFlightState
    {
        public bool Run(TState s, RuleContext ctx)
        {
            var flight = s.Flight;
            if (!flight.LandingAll || ctx.PassChanged) return false;
            var cell = flight.Next();
            if (cell < 0)
            {
                flight.LandingAll = false;
                return false;
            }

            flight.Land(cell);
            return true;
        }
    }
}
