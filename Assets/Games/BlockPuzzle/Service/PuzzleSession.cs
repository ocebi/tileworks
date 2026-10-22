using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BlockPuzzle.Model;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using Tileworks.Meta.Api;
using Tileworks.Meta.Config;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;

namespace BlockPuzzle.Service
{
    [ConfigSection("puzzleContinue")]
    public sealed class PuzzleContinueConfig : IValidatable
    {
        public int[] CoinPrices = { 120, 240 };

        public int MaxContinues => CoinPrices.Length;
        public int PriceFor(int used) => CoinPrices[Math.Min(used, CoinPrices.Length - 1)];

        public void Validate(ValidationReport report) => report.Require(CoinPrices != null && CoinPrices.Length > 0, "coinPrices must not be empty");
    }

    public sealed class PuzzleResult
    {
        public string LevelId;
        public string ContentHash;
        public int LevelIndex;
        public bool Won;
        public int Reward;
        public int Score;
        public int Placements;
        public int Continues;
        public int DurationMs;
        public string StartedAt;
        public byte[] Replay;
    }

    // Everything a finished attempt changes is written in one transaction: all of it or none of it.
    public sealed class PuzzleResultCommitter
    {
        private readonly IDatabase _db;
        private readonly PuzzleAttemptDao _attempts;
        private readonly PuzzleProgressService _progress;
        private readonly IEconomy _economy;
        private readonly ILives _lives;
        private readonly IReadOnlyList<ILevelResultListener> _listeners;
        private readonly ILog _log;

        public PuzzleResultCommitter(IDatabase db, PuzzleAttemptDao attempts, PuzzleProgressService progress, IEconomy economy, ILives lives,
            IReadOnlyList<ILevelResultListener> listeners, ILog log)
        {
            _db = db;
            _attempts = attempts;
            _progress = progress;
            _economy = economy;
            _lives = lives;
            _listeners = listeners;
            _log = log;
        }

        public void Commit(PuzzleResult result)
        {
            using (var transaction = _db.Begin("PuzzleResult"))
            {
                _attempts.Insert(new PuzzleAttemptEntity
                {
                    LevelId = result.LevelId, LevelIndex = result.LevelIndex, ContentHash = result.ContentHash ?? string.Empty,
                    StartedAt = result.StartedAt ?? string.Empty, DurationMs = result.DurationMs, Outcome = result.Won ? "won" : "lost",
                    Score = result.Score, Placements = result.Placements, Continues = result.Continues, Replay = result.Replay,
                });
                // A replay of a cleared level is for fun: no progress, coins or lost life.
                if (!IsReplay(result.LevelIndex))
                {
                    _progress.Record(_db, result.LevelIndex, result.Won, result.Score);
                    if (result.Won && result.Reward > 0)
                        _economy.Apply(EconomyTransaction.Grant(TransactionSource.LevelWin, result.LevelId, Resources.Coins, result.Reward));
                    if (!result.Won) _lives.TryConsume();
                    foreach (var listener in _listeners) listener.OnLevelResult(result.LevelId, result.Won);
                }

                transaction.Commit();
            }

            _log.Info(LogTag.Save, "Puzzle {0} committed: {1}, score {2}", result.LevelId, result.Won ? "won" : "lost", result.Score);
        }

        public bool IsReplay(int levelIndex) => levelIndex < _progress.CurrentIndex.Value;
    }

    // One attempt: owns the rules engine, forwards player intents, plays the events and reports the end only after
    // the presentation has caught up.
    public sealed class PuzzleSession : IDisposable
    {
        private readonly LevelRef _ref;
        private readonly LevelRepository _repository;
        private readonly BlockRules _rules;
        private readonly IEventPlayer _player;
        private readonly PuzzleResultCommitter _committer;
        private readonly IClock _clock;
        private readonly IEconomy _economy;
        private readonly PuzzleContinueConfig _continues;
        private readonly ILog _log;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Stopwatch _duration = new Stopwatch();
        private LevelEnvelope _envelope;
        private DateTime _startedAt;
        private bool _finishing;

        public PuzzleSession(LevelRef levelRef, LevelRepository repository, BlockRules rules, IEventPlayer player, PuzzleResultCommitter committer,
            IClock clock, IEconomy economy, IConfig config, ILog log)
        {
            _ref = levelRef;
            _repository = Guard.NotNull(repository, nameof(repository));
            _rules = Guard.NotNull(rules, nameof(rules));
            _player = Guard.NotNull(player, nameof(player));
            _committer = Guard.NotNull(committer, nameof(committer));
            _clock = Guard.NotNull(clock, nameof(clock));
            _economy = Guard.NotNull(economy, nameof(economy));
            _continues = Guard.NotNull(config, nameof(config)).Get<PuzzleContinueConfig>();
            _log = Guard.NotNull(log, nameof(log));
        }

        public LevelRef Ref => _ref;
        public BlockLevel Level { get; private set; }
        public RuleEngine<BlockState> Engine { get; private set; }
        public BlockState State => Engine.State;
        public BlockRules Rules => _rules;
        public int ContinuesLeft => _continues.MaxContinues - State.ContinuesUsed;
        public int ContinuePrice => _continues.PriceFor(State.ContinuesUsed);
        public bool CanContinue => ContinuesLeft > 0 && Engine.Check(new RefreshOffer()).IsNone;
        public bool CanAffordContinue => _economy.CanAfford(ContinueCost());

        // Fires once the presentation is idle after a win or loss.
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
            Level = new BlockLevelSerializer(_rules.Shapes).Read(_envelope);
            Engine = _rules.CreateEngine(Level, seed);
            _startedAt = _clock.UtcNow;
            _duration.Restart();
            _log.Info(LogTag.Level, "Puzzle {0} ({1}) started", _ref.Index + 1, _ref.LevelId);
            return Result.Ok();
        }

        public RejectReason Check(int slot, int anchor) => Engine.Check(new PlacePiece(slot, anchor));

        public ActionResult Place(int slot, int anchor) => Submit(new PlacePiece(slot, anchor));

        public ActionResult Continue()
        {
            if (!CanContinue || !_economy.Apply(ContinueCost()).IsOk) return new ActionResult(RejectReason.NotApplicable, State.Outcome, default);
            _finishing = false;
            return Submit(new RefreshOffer());
        }

        // A declined continue ends the attempt; a win ends it immediately.
        public void Conclude()
        {
            if (State.Outcome == Outcome.Ongoing) return;
            _duration.Stop();
            var replay = new Replay
            {
                LevelId = Level.Id, ContentHash = _envelope.ContentHash, RulesVersion = BlockRules.Version, Seed = Engine.Rng.Seed,
                Actions = new List<ActionRecord>(Engine.Log).ToArray(), FinalHash = State.ComputeHash(),
            };
            _committer.Commit(new PuzzleResult
            {
                LevelId = Level.Id, ContentHash = _envelope.ContentHash, LevelIndex = _ref.Index, Won = State.Outcome == Outcome.Won, Reward = _ref.Reward,
                Score = State.Score, Placements = State.Placements, Continues = State.ContinuesUsed, DurationMs = (int)_duration.ElapsedMilliseconds,
                StartedAt = _startedAt.ToString("O"), Replay = replay.ToBytes(),
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
                _log.Error(LogTag.Level, "Finishing the puzzle failed", e);
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

        private EconomyTransaction ContinueCost() =>
            EconomyTransaction.Spend(TransactionSource.Continue, Level?.Id, Resources.Coins, ContinuePrice);
    }
}
