using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Meta.Api;
using Tileworks.Meta.Config;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using TileMatch.Model;

namespace TileMatch.Service
{
    // One attempt: owns the rules engine, forwards player intents, plays the resulting events and reports the end
    // only after the presentation has caught up.
    public sealed class LevelSession : IDisposable
    {
        private readonly LevelRef _ref;
        private readonly LevelRepository _repository;
        private readonly TileMatchRules _rules;
        private readonly IEventPlayer _player;
        private readonly LevelResultCommitter _committer;
        private readonly IClock _clock;
        private readonly IEconomy _economy;
        private readonly ContinueConfig _continues;
        private readonly ILog _log;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Dictionary<BoosterKind, int> _boostersUsed = new Dictionary<BoosterKind, int>();
        private readonly Stopwatch _duration = new Stopwatch();
        private LevelEnvelope _envelope;
        private DateTime _startedAt;
        private bool _finishing;

        public LevelSession(LevelRef levelRef, LevelRepository repository, TileMatchRules rules, IEventPlayer player,
            LevelResultCommitter committer, IClock clock, IEconomy economy, IConfig config, ILog log)
        {
            _ref = levelRef;
            _repository = Guard.NotNull(repository, nameof(repository));
            _rules = Guard.NotNull(rules, nameof(rules));
            _player = Guard.NotNull(player, nameof(player));
            _committer = Guard.NotNull(committer, nameof(committer));
            _clock = Guard.NotNull(clock, nameof(clock));
            _economy = Guard.NotNull(economy, nameof(economy));
            _continues = Guard.NotNull(config, nameof(config)).Get<ContinueConfig>();
            _log = Guard.NotNull(log, nameof(log));
        }

        public LevelRef Ref => _ref;
        public TileMatchLevel Level { get; private set; }
        public RuleEngine<TileMatchState> Engine { get; private set; }
        public TileMatchState State => Engine.State;
        public TileMatchRules Rules => _rules;
        public int ContinuesLeft => _continues.MaxContinues - State.ContinuesUsed;
        public int ContinuePrice => _continues.PriceFor(State.ContinuesUsed);
        public bool CanAffordContinue => _economy.CanAfford(ContinueCost());
        public IReadOnlyObservable<long> BoosterCount(BoosterKind kind) => _economy.Balance(BoosterResources.Of(kind));

        private UniTaskCompletionSource<Outcome> _finished;
        private bool _finishedTaken;

        // Completes once the presentation is idle after a win or loss; each outcome is handed over once.
        public UniTask<Outcome> WhenFinishedAsync(CancellationToken ct)
        {
            if (_finished == null || _finishedTaken) _finished = new UniTaskCompletionSource<Outcome>();
            _finishedTaken = true;
            return _finished.Task.AttachExternalCancellation(ct);
        }

        public Result Load(ulong seed)
        {
            var loaded = _repository.Get(_ref.LevelId);
            if (!loaded.TryGet(out _envelope)) return loaded.Error;
            Level = new TileMatchLevelSerializer().Read(_envelope);
            Engine = _rules.CreateEngine(Level, seed);
            _startedAt = _clock.UtcNow;
            _duration.Restart();
            _log.Info(LogTag.Level, "Level {0} ({1}) started", _ref.Index + 1, _ref.LevelId);
            return Result.Ok();
        }

        public ActionResult Select(int tile) => Submit(new SelectTile(tile));

        // The rules decide applicability first, so a booster is only spent when it will do something.
        public ActionResult UseBooster(BoosterKind kind)
        {
            var action = new UseBooster(kind);
            var check = Engine.Check(action);
            if (!check.IsNone) return new ActionResult(check, State.Outcome, default);
            var spent = _economy.Apply(EconomyTransaction.Spend(TransactionSource.BoosterUse, Level.Id, BoosterResources.Of(kind), 1));
            if (!spent.IsOk) return new ActionResult(RejectReason.NotApplicable, State.Outcome, default);
            _boostersUsed.TryGetValue(kind, out var used);
            _boostersUsed[kind] = used + 1;
            return Submit(action);
        }

        public ActionResult Continue()
        {
            var action = new ContinueAfterFail(_continues.ExtraSlots, _continues.ReturnCount);
            var check = Engine.Check(action);
            if (ContinuesLeft <= 0 || !check.IsNone) return new ActionResult(check.IsNone ? RejectReason.NotApplicable : check, State.Outcome, default);
            if (!_economy.Apply(ContinueCost()).IsOk) return new ActionResult(RejectReason.NotApplicable, State.Outcome, default);
            _finishing = false;
            return Submit(action);
        }

        // A declined continue ends the attempt; a win ends it immediately.
        public void Conclude()
        {
            if (State.Outcome == Outcome.Ongoing) return;
            _duration.Stop();
            var replay = new Replay
            {
                LevelId = Level.Id, ContentHash = _envelope.ContentHash, RulesVersion = TileMatchRules.Version, Seed = Engine.Rng.Seed,
                Actions = new List<ActionRecord>(Engine.Log).ToArray(), FinalHash = State.ComputeHash(),
            };
            _committer.Commit(new LevelResult
            {
                LevelId = Level.Id, ContentHash = _envelope.ContentHash, LevelIndex = _ref.Index, Won = State.Outcome == Outcome.Won, Reward = _ref.Reward,
                Moves = State.ActionsTaken, Continues = State.ContinuesUsed, DurationMs = (int)_duration.ElapsedMilliseconds,
                StartedAt = _startedAt.ToString("O"), BoostersJson = BoostersJson(), Replay = replay.ToBytes(),
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private ActionResult Submit<TAction>(in TAction action) where TAction : struct, IGameAction
        {
            var result = Engine.Apply(action);
            if (!result.Accepted) return result;
            _player.Play(Engine.Events.Slice(result.Events));
            if (result.Outcome != Outcome.Ongoing && !_finishing)
            {
                _finishing = true;
                FinishAsync(result.Outcome, _cts.Token).Forget();
            }

            return result;
        }

        private async UniTaskVoid FinishAsync(Outcome outcome, CancellationToken ct)
        {
            try
            {
                await _player.WaitIdleAsync(ct);
                Finish(outcome);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Error(LogTag.Level, "Finishing the level failed", e);
            }
        }

        private void Finish(Outcome outcome)
        {
            if (_finished == null || _finished.Task.Status.IsCompleted())
            {
                _finished = new UniTaskCompletionSource<Outcome>();
                _finishedTaken = false;
            }

            _finished.TrySetResult(outcome);
        }

        private string BoostersJson()
        {
            if (_boostersUsed.Count == 0) return "{}";
            var parts = new List<string>();
            foreach (var pair in _boostersUsed) parts.Add($"\"{pair.Key}\":{pair.Value}");
            return "{" + string.Join(",", parts) + "}";
        }

        private EconomyTransaction ContinueCost() =>
            EconomyTransaction.Spend(TransactionSource.Continue, Level?.Id, Resources.Coins, ContinuePrice);
    }
}
