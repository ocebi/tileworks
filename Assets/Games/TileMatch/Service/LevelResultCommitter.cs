using System.Collections.Generic;
using System.Diagnostics;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using Tileworks.Meta.Api;
using TileMatch.Api;

namespace TileMatch.Service
{
    public sealed class LevelResult
    {
        public string LevelId;
        public string ContentHash;
        public int LevelIndex;
        public bool Won;
        public int Reward;
        public int Moves;
        public int Continues;
        public int DurationMs;
        public string StartedAt;
        public string BoostersJson = "{}";
        public byte[] Replay;
    }

    // Everything a finished attempt changes is written in one transaction: all of it or none of it (§10.11).
    public sealed class LevelResultCommitter
    {
        private readonly IDatabase _db;
        private readonly LevelAttemptDao _attempts;
        private readonly ProgressService _progress;
        private readonly IEconomy _economy;
        private readonly ILives _lives;
        private readonly IReadOnlyList<ILevelResultListener> _listeners;
        private readonly IMessageBus _bus;
        private readonly ILog _log;

        public LevelResultCommitter(IDatabase db, LevelAttemptDao attempts, ProgressService progress, IEconomy economy, ILives lives,
            IReadOnlyList<ILevelResultListener> listeners, IMessageBus bus, ILog log)
        {
            _db = db;
            _attempts = attempts;
            _progress = progress;
            _economy = economy;
            _lives = lives;
            _listeners = listeners;
            _bus = bus;
            _log = log;
        }

        public void Commit(LevelResult result)
        {
            var watch = Stopwatch.StartNew();
            using (var transaction = _db.Begin("LevelResult"))
            {
                _attempts.Insert(new LevelAttemptEntity
                {
                    LevelId = result.LevelId,
                    LevelIndex = result.LevelIndex,
                    Attempt = _attempts.CountAttempts(result.LevelId) + 1,
                    ContentHash = result.ContentHash ?? string.Empty,
                    StartedAt = result.StartedAt ?? string.Empty,
                    DurationMs = result.DurationMs,
                    Outcome = result.Won ? "won" : "lost",
                    Moves = result.Moves,
                    BoostersJson = result.BoostersJson,
                    Continues = result.Continues,
                    Replay = result.Replay,
                });
                _attempts.TrimReplays(result.LevelId);
                // A replay of a cleared level is for fun: no progress, coins or lost life.
                if (!IsReplay(result.LevelIndex))
                {
                    if (result.Won)
                    {
                        _progress.RecordWin(_db, result.LevelIndex);
                        if (result.Reward > 0)
                            _economy.Apply(EconomyTransaction.Grant(TransactionSource.LevelWin, result.LevelId, Resources.Coins, result.Reward));
                    }
                    else
                    {
                        _lives.TryConsume();
                    }

                    foreach (var listener in _listeners) listener.OnLevelResult(result.LevelId, result.Won);
                }

                transaction.Commit();
            }

            _log.Info(LogTag.Save, "Level result committed in {0:0.00} ms", watch.Elapsed.TotalMilliseconds);
            _bus.Publish(new LevelCompleted(result.LevelId, result.LevelIndex, result.Won, result.Moves));
        }

        public bool IsReplay(int levelIndex) => levelIndex < _progress.CurrentIndex.Value;
    }
}
