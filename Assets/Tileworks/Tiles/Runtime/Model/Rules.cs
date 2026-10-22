using System;

namespace Tileworks.Tiles.Model
{
    public interface IGameAction
    {
    }

    public enum Outcome : byte
    {
        Ongoing,
        Won,
        Lost,
    }

    // Codes below 100 are framework-wide; games define their own from 100.
    public readonly struct RejectReason : IEquatable<RejectReason>
    {
        public static readonly RejectReason None = new RejectReason(0);
        public static readonly RejectReason Unknown = new RejectReason(1);
        public static readonly RejectReason GameOver = new RejectReason(2);
        public static readonly RejectReason InvalidTarget = new RejectReason(3);
        public static readonly RejectReason NotFree = new RejectReason(4);
        public static readonly RejectReason Vetoed = new RejectReason(5);
        public static readonly RejectReason NoRoom = new RejectReason(6);
        public static readonly RejectReason NotApplicable = new RejectReason(7);
        public static readonly RejectReason NoHandler = new RejectReason(8);
        public static readonly RejectReason NotInFlight = new RejectReason(9);

        public readonly ushort Code;

        public RejectReason(ushort code) => Code = code;

        public bool IsNone => Code == 0;
        public bool Equals(RejectReason other) => Code == other.Code;
        public override bool Equals(object obj) => obj is RejectReason other && Equals(other);
        public override int GetHashCode() => Code;
        public static bool operator ==(RejectReason a, RejectReason b) => a.Code == b.Code;
        public static bool operator !=(RejectReason a, RejectReason b) => a.Code != b.Code;
        public override string ToString() => IsNone ? "None" : $"Reject({Code})";
    }

    public interface IRuleState<in TState>
    {
        Outcome Outcome { get; set; }
        void CopyTo(TState target);
        ulong ComputeHash();
    }

    public interface IActionHandler<in TState, TAction> where TAction : struct, IGameAction
    {
        RejectReason Validate(TState state, in TAction action);
        void Apply(TState state, in TAction action, RuleContext ctx);
    }

    // Lets plug-ins (modifiers, tutorials) veto an action without the handler knowing about them.
    public interface IActionValidator<in TState, TAction> where TAction : struct, IGameAction
    {
        RejectReason Validate(TState state, in TAction action);
    }

    public interface IRuleSystem<in TState>
    {
        // True if it changed state; resolution repeats until a full pass changes nothing.
        bool Run(TState state, RuleContext ctx);
    }

    public interface IEndCondition<in TState>
    {
        Outcome Evaluate(TState state, RuleContext ctx);
    }

    public sealed class RuleContext
    {
        public RuleContext(EventWriter events, RngStreams rng)
        {
            Events = events;
            Rng = rng;
        }

        public EventWriter Events { get; }
        public RngStreams Rng { get; }
        public int ActionIndex { get; internal set; }

        // Whether a system earlier in the current resolution pass changed the state.
        public bool PassChanged { get; internal set; }
    }

    public readonly struct ActionResult
    {
        public readonly RejectReason Rejected;
        public readonly Outcome Outcome;
        public readonly EventRange Events;

        public ActionResult(RejectReason rejected, Outcome outcome, EventRange events)
        {
            Rejected = rejected;
            Outcome = outcome;
            Events = events;
        }

        public bool Accepted => Rejected.IsNone;
        public override string ToString() => Accepted ? $"Accepted({Outcome}, {Events.Count} events)" : Rejected.ToString();
    }
}
