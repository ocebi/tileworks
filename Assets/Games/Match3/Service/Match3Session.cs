using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Match3.Model;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using Tileworks.Meta.Api;
using Tileworks.Meta.Boosters;
using Tileworks.Meta.Config;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;

namespace Match3.Service
{
    [ConfigSection("match3Continue")]
    public sealed class Match3ContinueConfig : IValidatable
    {
        public int[] CoinPrices = { 100, 200 };
        public int ExtraMoves = 5;

        public int MaxContinues => CoinPrices.Length;
        public int PriceFor(int used) => CoinPrices[Math.Min(used, CoinPrices.Length - 1)];

        public void Validate(ValidationReport report)
        {
            report.Require(CoinPrices != null && CoinPrices.Length > 0, "coinPrices must not be empty");
            report.Require(ExtraMoves > 0, "extraMoves must be positive");
        }
    }

    public sealed class Match3Result
    {
        public string LevelId;
        public string ContentHash;
        public int LevelIndex;
        public bool Won;
        public int Reward;
        public int Score;
        public int Moves;
        public int Continues;
        public int DurationMs;
        public string StartedAt;
        public byte[] Replay;
    }

    // Everything a finished attempt changes is written in one transaction: all of it or none of it.
    public sealed class Match3ResultCommitter
    {
        private readonly IDatabase _db;
        private readonly Match3AttemptDao _attempts;
        private readonly Match3ProgressService _progress;
        private readonly IEconomy _economy;
        private readonly ILives _lives;
        private readonly IReadOnlyList<ILevelResultListener> _listeners;
        private readonly ILog _log;

        public Match3ResultCommitter(IDatabase db, Match3AttemptDao attempts, Match3ProgressService progress, IEconomy economy, ILives lives,
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

        public void Commit(Match3Result result)
        {
            using (var transaction = _db.Begin("Match3Result"))
            {
                _attempts.Insert(new Match3AttemptEntity
                {
                    LevelId = result.LevelId, LevelIndex = result.LevelIndex, ContentHash = result.ContentHash ?? string.Empty,
                    StartedAt = result.StartedAt ?? string.Empty, DurationMs = result.DurationMs, Outcome = result.Won ? "won" : "lost",
                    Score = result.Score, Moves = result.Moves, Continues = result.Continues, Replay = result.Replay,
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

            _log.Info(LogTag.Save, "Match3 {0} committed: {1} in {2} moves", result.LevelId, result.Won ? "won" : "lost", result.Moves);
        }

        public bool IsReplay(int levelIndex) => levelIndex < _progress.CurrentIndex.Value;
    }

    // One attempt: owns the rules engine, forwards moves and the view's landing reports, plays the events and reports the
    // end only after the presentation has caught up. If the view goes quiet with pieces still in flight, everything lands.
    public sealed class Match3Session : IDisposable
    {
        private const double WatchdogSeconds = 0.5;

        private readonly LevelRef _ref;
        private readonly LevelRepository _repository;
        private readonly Match3Rules _rules;
        private readonly IEventPlayer _player;
        private readonly Match3ResultCommitter _committer;
        private readonly IClock _clock;
        private readonly IEconomy _economy;
        private readonly Match3ContinueConfig _continues;
        private readonly BoosterService _boosters;
        private readonly ILog _log;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Stopwatch _duration = new Stopwatch();
        private DateTime _quietSince;
        private bool _quiet;
        private LevelEnvelope _envelope;
        private DateTime _startedAt;
        private bool _finishing;

        public Match3Session(LevelRef levelRef, LevelRepository repository, Match3Rules rules, IEventPlayer player, Match3ResultCommitter committer,
            IClock clock, IEconomy economy, IConfig config, BoosterService boosters, ILog log)
        {
            _ref = levelRef;
            _repository = Guard.NotNull(repository, nameof(repository));
            _rules = Guard.NotNull(rules, nameof(rules));
            _player = Guard.NotNull(player, nameof(player));
            _committer = Guard.NotNull(committer, nameof(committer));
            _clock = Guard.NotNull(clock, nameof(clock));
            _economy = Guard.NotNull(economy, nameof(economy));
            _continues = Guard.NotNull(config, nameof(config)).Get<Match3ContinueConfig>();
            _boosters = Guard.NotNull(boosters, nameof(boosters));
            _log = Guard.NotNull(log, nameof(log));
        }

        public LevelRef Ref => _ref;
        public Match3Level Level { get; private set; }
        public RuleEngine<Match3State> Engine { get; private set; }
        public Match3State State => Engine.State;
        public Match3Rules Rules => _rules;
        public int ContinuesLeft => _continues.MaxContinues - State.ContinuesUsed;
        public int ContinuePrice => _continues.PriceFor(State.ContinuesUsed);
        public int ExtraMoves => _continues.ExtraMoves;
        public bool CanContinue => ContinuesLeft > 0 && Engine.Check(new AddMoves(_continues.ExtraMoves)).IsNone;
        public bool CanAffordContinue => _economy.CanAfford(ContinueCost());

        // The presentation has caught up with the rules and every piece has landed (after the level ends, landings no
        // longer count).
        public bool IsSettled => _player.IsIdle && (Engine == null || State.Flight.Count == 0 || State.Outcome != Outcome.Ongoing);

        // A move may touch this cell: its piece has landed and isn't animating.
        public bool CanActOn(int cell) => cell >= 0 && cell < State.CellCount && !State.Flight[cell] && !_player.IsBusy(State.Ids[cell]);

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
            Level = new Match3LevelSerializer().Read(_envelope);
            Engine = _rules.CreateEngine(Level, seed);
            _startedAt = _clock.UtcNow;
            _duration.Restart();
            _log.Info(LogTag.Level, "Match3 {0} ({1}) started", _ref.Index + 1, _ref.LevelId);
            return Result.Ok();
        }

        public RejectReason Check(int from, int to) => Engine.Check(new Swap(from, to));

        public ActionResult Swap(int from, int to) => Submit(new Swap(from, to));

        public ActionResult Activate(int cell) => Submit(new Activate(cell));

        // The view saw this piece land. Reports for a piece that has since left or moved on, or after the level ended,
        // are dropped.
        public void Land(int cell, int id)
        {
            if (State.Outcome == Outcome.Ongoing) Submit(new Land(cell, id));
        }

        // The cell of the piece in flight with this id, or -1.
        public int CellOf(int id)
        {
            var s = State;
            for (var cell = 0; cell < s.CellCount; cell++)
                if (s.Ids[cell] == id && s.Flight[cell] && s.Pieces[cell] != Piece.Empty) return cell;
            return -1;
        }

        // Called every frame by the game presenter.
        public void Tick()
        {
            if (Engine == null || State.Outcome != Outcome.Ongoing || State.Flight.Count == 0 || !_player.IsIdle)
            {
                _quiet = false;
                return;
            }

            if (!_quiet)
            {
                _quiet = true;
                _quietSince = _clock.UtcNow;
                return;
            }

            if ((_clock.UtcNow - _quietSince).TotalSeconds < WatchdogSeconds) return;
            _log.Warn(LogTag.Level, "Match3 {0}: {1} pieces never reported landing; landing them", Level.Id, State.Flight.Count);
            _quiet = false;
            Submit(new LandAll());
        }

        public bool CanHammer(int cell) => HammerHandler.CanHit(State, cell);

        public ActionResult Hammer(int cell) => UseBooster(BoardBoosters.Hammer, new Hammer(cell));

        public ActionResult Shuffle() => UseBooster(BoardBoosters.Shuffle, new ShuffleBoard());

        public ActionResult Continue()
        {
            if (!CanContinue || !_economy.Apply(ContinueCost()).IsOk) return new ActionResult(RejectReason.NotApplicable, State.Outcome, default);
            _finishing = false;
            return Submit(new AddMoves(_continues.ExtraMoves));
        }

        // A declined continue ends the attempt; a win ends it immediately.
        public void Conclude()
        {
            if (State.Outcome == Outcome.Ongoing) return;
            _duration.Stop();
            var replay = new Replay
            {
                LevelId = Level.Id, ContentHash = _envelope.ContentHash, RulesVersion = Match3Rules.Version, Seed = Engine.Rng.Seed,
                Actions = new List<ActionRecord>(Engine.Log).ToArray(), FinalHash = State.ComputeHash(),
            };
            _committer.Commit(new Match3Result
            {
                LevelId = Level.Id, ContentHash = _envelope.ContentHash, LevelIndex = _ref.Index, Won = State.Outcome == Outcome.Won, Reward = _ref.Reward,
                Score = State.Score, Moves = State.MovesUsed, Continues = State.ContinuesUsed, DurationMs = (int)_duration.ElapsedMilliseconds,
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

        // A booster is paid for only once the rules accept it.
        private ActionResult UseBooster<TAction>(int slot, in TAction action) where TAction : struct, IGameAction
        {
            var rejected = Engine.Check(action);
            if (rejected.IsNone && !_boosters.Spend(slot, Level.Id).IsOk) rejected = RejectReason.NotApplicable;
            if (!rejected.IsNone) return new ActionResult(rejected, State.Outcome, default);
            _log.Info(LogTag.Level, "Match3 {0}: booster {1}", Level.Id, slot);
            return Submit(action);
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

        private EconomyTransaction ContinueCost() =>
            EconomyTransaction.Spend(TransactionSource.Continue, Level?.Id, Resources.Coins, ContinuePrice);
    }
}
