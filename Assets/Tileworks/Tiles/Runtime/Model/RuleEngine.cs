using System;
using System.Collections.Generic;

namespace Tileworks.Tiles.Model
{
    // Validate → apply → resolve until stable → evaluate end conditions → record. Allocation-free after warm-up.
    public sealed class RuleEngine<TState> where TState : class, IRuleState<TState>
    {
        // A safety cap, not a budget: LandAll lands one piece per pass, so a full 10×10 board with cascades needs hundreds.
        public const int MaxResolutionPasses = 4096;

        private readonly RulePipeline<TState> _pipeline;
        private readonly RuleContext _context;
        private readonly EventWriter _writer;
        private readonly List<ActionRecord> _log = new List<ActionRecord>(1024);

        public RuleEngine(TState state, RulePipeline<TState> pipeline, RngStreams rng, EventBuffer buffer = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            Rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Events = buffer ?? new EventBuffer();
            _writer = new EventWriter(Events);
            _context = new RuleContext(_writer, rng);
        }

        public TState State { get; private set; }
        public RngStreams Rng { get; }
        public EventBuffer Events { get; }
        public IReadOnlyList<ActionRecord> Log => _log;
        public bool RecordActions { get; set; } = true;

        // Points the engine at another state of the same level (solver and playouts reuse one engine).
        public void Attach(TState state) => State = state ?? throw new ArgumentNullException(nameof(state));

        public bool CanHandle<TAction>() where TAction : struct, IGameAction => _pipeline.Actions.ContainsKey(typeof(TAction));

        public RejectReason Check<TAction>(in TAction action) where TAction : struct, IGameAction
        {
            if (!_pipeline.Actions.TryGetValue(typeof(TAction), out var boxed)) return RejectReason.NoHandler;
            var entry = (RulePipeline<TState>.ActionEntry<TAction>)boxed;
            var rejected = entry.Handler.Validate(State, action);
            if (!rejected.IsNone) return rejected;
            for (var i = 0; i < entry.Validators.Length; i++)
            {
                rejected = entry.Validators[i].Validate(State, action);
                if (!rejected.IsNone) return rejected;
            }

            return RejectReason.None;
        }

        public ActionResult Apply<TAction>(in TAction action) where TAction : struct, IGameAction
        {
            Events.Clear();
            var rejected = Check(action);
            if (!rejected.IsNone) return new ActionResult(rejected, State.Outcome, default);

            var entry = (RulePipeline<TState>.ActionEntry<TAction>)_pipeline.Actions[typeof(TAction)];
            _writer.Step = 0;
            entry.Handler.Apply(State, action, _context);
            Resolve();
            EvaluateEnd();

            if (RecordActions && entry.Codec != null) _log.Add(entry.Codec.Encode(action));
            _context.ActionIndex++;
            return new ActionResult(RejectReason.None, State.Outcome, new EventRange(0, Events.Count));
        }

        public ActionResult ApplyRecord(in ActionRecord record)
        {
            if (!_pipeline.ByRecordType.TryGetValue(record.Type, out var entry))
                return new ActionResult(RejectReason.NoHandler, State.Outcome, default);
            return entry.ApplyRecord(this, record);
        }

        private void Resolve()
        {
            var systems = _pipeline.Systems;
            for (var pass = 1; pass <= MaxResolutionPasses; pass++)
            {
                _writer.Step = (ushort)pass;
                _context.PassChanged = false;
                for (var i = 0; i < systems.Length; i++)
                    if (systems[i].Run(State, _context)) _context.PassChanged = true;
                if (!_context.PassChanged) return;
            }

            throw new InvalidOperationException($"Resolution did not settle within {MaxResolutionPasses} passes.");
        }

        private void EvaluateEnd()
        {
            var conditions = _pipeline.EndConditions;
            for (var i = 0; i < conditions.Length; i++)
            {
                var outcome = conditions[i].Evaluate(State, _context);
                if (outcome == Outcome.Ongoing) continue;
                State.Outcome = outcome;
                return;
            }
        }
    }
}
