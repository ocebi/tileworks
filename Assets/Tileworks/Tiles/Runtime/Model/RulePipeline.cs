using System;
using System.Collections.Generic;

namespace Tileworks.Tiles.Model
{
    public readonly struct ActionRecord : IEquatable<ActionRecord>
    {
        public readonly ushort Type;
        public readonly int A;
        public readonly int B;

        public ActionRecord(ushort type, int a = 0, int b = 0)
        {
            Type = type;
            A = a;
            B = b;
        }

        public bool Equals(ActionRecord other) => Type == other.Type && A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is ActionRecord other && Equals(other);
        public override int GetHashCode() => (Type * 397 ^ A) * 397 ^ B;
        public override string ToString() => $"{Type}({A},{B})";
    }

    public interface IActionCodec<TAction> where TAction : struct, IGameAction
    {
        ushort TypeId { get; }
        ActionRecord Encode(in TAction action);
        TAction Decode(in ActionRecord record);
    }

    // Built once per game; shared by every engine instance of that game.
    public sealed class RulePipeline<TState> where TState : class, IRuleState<TState>
    {
        internal RulePipeline(Dictionary<Type, ActionEntry> actions, Dictionary<ushort, ActionEntry> byRecordType,
            IRuleSystem<TState>[] systems, IEndCondition<TState>[] endConditions)
        {
            Actions = actions;
            ByRecordType = byRecordType;
            Systems = systems;
            EndConditions = endConditions;
        }

        internal Dictionary<Type, ActionEntry> Actions { get; }
        internal Dictionary<ushort, ActionEntry> ByRecordType { get; }
        internal IRuleSystem<TState>[] Systems { get; }
        internal IEndCondition<TState>[] EndConditions { get; }

        internal abstract class ActionEntry
        {
            public abstract bool HasHandler { get; }
            public abstract ActionResult ApplyRecord(RuleEngine<TState> engine, in ActionRecord record);
        }

        internal sealed class ActionEntry<TAction> : ActionEntry where TAction : struct, IGameAction
        {
            public IActionHandler<TState, TAction> Handler;
            public IActionValidator<TState, TAction>[] Validators = Array.Empty<IActionValidator<TState, TAction>>();
            public IActionCodec<TAction> Codec;

            public override bool HasHandler => Handler != null;

            public override ActionResult ApplyRecord(RuleEngine<TState> engine, in ActionRecord record) =>
                engine.Apply(Codec.Decode(record));
        }
    }

    public sealed class RulePipelineBuilder<TState> where TState : class, IRuleState<TState>
    {
        private readonly Dictionary<Type, RulePipeline<TState>.ActionEntry> _actions = new Dictionary<Type, RulePipeline<TState>.ActionEntry>();
        private readonly Dictionary<ushort, RulePipeline<TState>.ActionEntry> _byRecordType = new Dictionary<ushort, RulePipeline<TState>.ActionEntry>();
        private readonly List<IRuleSystem<TState>> _systems = new List<IRuleSystem<TState>>();
        private readonly List<IEndCondition<TState>> _endConditions = new List<IEndCondition<TState>>();

        public RulePipelineBuilder<TState> Handle<TAction>(IActionHandler<TState, TAction> handler, IActionCodec<TAction> codec = null)
            where TAction : struct, IGameAction
        {
            var entry = Entry<TAction>();
            if (entry.Handler != null) throw new InvalidOperationException($"{typeof(TAction).Name} already has a handler.");
            entry.Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            if (codec != null)
            {
                if (_byRecordType.ContainsKey(codec.TypeId)) throw new InvalidOperationException($"Record type {codec.TypeId} used twice.");
                entry.Codec = codec;
                _byRecordType.Add(codec.TypeId, entry);
            }

            return this;
        }

        public RulePipelineBuilder<TState> Validate<TAction>(IActionValidator<TState, TAction> validator) where TAction : struct, IGameAction
        {
            var entry = Entry<TAction>();
            var list = new List<IActionValidator<TState, TAction>>(entry.Validators) { validator };
            entry.Validators = list.ToArray();
            return this;
        }

        public RulePipelineBuilder<TState> Resolve(IRuleSystem<TState> system)
        {
            _systems.Add(system ?? throw new ArgumentNullException(nameof(system)));
            return this;
        }

        public RulePipelineBuilder<TState> End(IEndCondition<TState> condition)
        {
            _endConditions.Add(condition ?? throw new ArgumentNullException(nameof(condition)));
            return this;
        }

        public RulePipeline<TState> Build()
        {
            foreach (var pair in _actions)
            {
                if (!pair.Value.HasHandler) throw new InvalidOperationException($"{pair.Key.Name} has validators but no handler.");
            }

            return new RulePipeline<TState>(_actions, _byRecordType, _systems.ToArray(), _endConditions.ToArray());
        }

        private RulePipeline<TState>.ActionEntry<TAction> Entry<TAction>() where TAction : struct, IGameAction
        {
            if (_actions.TryGetValue(typeof(TAction), out var existing)) return (RulePipeline<TState>.ActionEntry<TAction>)existing;
            var entry = new RulePipeline<TState>.ActionEntry<TAction>();
            _actions.Add(typeof(TAction), entry);
            return entry;
        }
    }
}
