using System;
using System.Collections.Generic;
using System.Threading;
using BlockPuzzle.Api;
using Cysharp.Threading.Tasks;
using SQLite;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;

namespace BlockPuzzle.Service
{
    [Table("puzzle_progress")]
    public sealed class PuzzleProgressEntity
    {
        [PrimaryKey, Column("id")] public int Id { get; set; }
        [Column("level_index")] public int LevelIndex { get; set; }
        [Column("best_score")] public int BestScore { get; set; }
        [Column("updated_at")] public string UpdatedAt { get; set; }
    }

    [Table("puzzle_attempts")]
    public sealed class PuzzleAttemptEntity
    {
        [PrimaryKey, AutoIncrement, Column("id")] public long Id { get; set; }
        [Column("level_id")] public string LevelId { get; set; }
        [Column("level_index")] public int LevelIndex { get; set; }
        [Column("content_hash")] public string ContentHash { get; set; }
        [Column("started_at")] public string StartedAt { get; set; }
        [Column("duration_ms")] public int DurationMs { get; set; }
        [Column("outcome")] public string Outcome { get; set; }
        [Column("score")] public int Score { get; set; }
        [Column("placements")] public int Placements { get; set; }
        [Column("continues")] public int Continues { get; set; }
        [Column("replay")] public byte[] Replay { get; set; }
    }

    public sealed class PuzzleProgressDao : CachedDao<PuzzleProgressEntity, int>
    {
        public PuzzleProgressDao(IDatabase db) : base(db) { }
        protected override int KeyOf(PuzzleProgressEntity entity) => entity.Id;
    }

    public sealed class PuzzleAttemptDao : Dao<PuzzleAttemptEntity, long>
    {
        public PuzzleAttemptDao(IDatabase db) : base(db) { }
    }

    public sealed class PuzzleSchema : ISchemaModule
    {
        public string Module => "blockpuzzle";

        public IReadOnlyList<Migration> Migrations { get; } = new[]
        {
            new Migration(1, "progress and attempt history", new[]
            {
                "CREATE TABLE puzzle_progress(id INTEGER PRIMARY KEY NOT NULL, level_index INTEGER NOT NULL, best_score INTEGER NOT NULL, updated_at TEXT NOT NULL)",
                "CREATE TABLE puzzle_attempts(id INTEGER PRIMARY KEY AUTOINCREMENT, level_id TEXT NOT NULL, level_index INTEGER NOT NULL, " +
                "content_hash TEXT NOT NULL, started_at TEXT NOT NULL, duration_ms INTEGER NOT NULL, outcome TEXT NOT NULL, " +
                "score INTEGER NOT NULL, placements INTEGER NOT NULL, continues INTEGER NOT NULL, replay BLOB)",
                "CREATE INDEX ix_puzzle_attempts_level ON puzzle_attempts(level_id, started_at)",
            }),
        };
    }

    public sealed class PuzzleProgressService : IPuzzleProgress
    {
        private const int RowId = 1;

        private readonly PuzzleProgressDao _dao;
        private readonly IClock _clock;
        private readonly Observable<int> _current = new Observable<int>();

        public PuzzleProgressService(PuzzleProgressDao dao, IClock clock)
        {
            _dao = Guard.NotNull(dao, nameof(dao));
            _clock = Guard.NotNull(clock, nameof(clock));
        }

        public IReadOnlyObservable<int> CurrentIndex => _current;
        public int BestScore => _dao.Find(RowId)?.BestScore ?? 0;

        public void Load() => _current.Value = _dao.Find(RowId)?.LevelIndex ?? 0;

        // Called inside the result transaction; the observable updates only after the commit.
        public void Record(IDatabase db, int levelIndex, bool won, int score)
        {
            var row = _dao.Find(RowId);
            var next = won ? Math.Max(_current.Value, levelIndex + 1) : _current.Value;
            _dao.Upsert(new PuzzleProgressEntity
            {
                Id = RowId, LevelIndex = next, BestScore = Math.Max(row?.BestScore ?? 0, score), UpdatedAt = _clock.UtcNow.ToString("O"),
            });
            var transaction = db.Current;
            if (transaction == null) _current.Value = next;
            else transaction.OnCommitted(() => _current.Value = next);
        }
    }

    public sealed class PuzzleProgressLoadStep : BootStep
    {
        private static readonly string[] Dependencies = { SaveSteps.Warm };
        private readonly PuzzleProgressService _progress;

        public PuzzleProgressLoadStep(PuzzleProgressService progress) => _progress = progress;

        public override string Name => "Progress";
        public override IReadOnlyList<string> DependsOn => Dependencies;

        public override UniTask RunAsync(CancellationToken ct)
        {
            _progress.Load();
            return UniTask.CompletedTask;
        }
    }
}
