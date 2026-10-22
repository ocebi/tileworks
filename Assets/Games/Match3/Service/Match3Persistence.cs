using System;
using System.Collections.Generic;
using System.Threading;
using Match3.Api;
using Cysharp.Threading.Tasks;
using SQLite;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;

namespace Match3.Service
{
    [Table("match3_progress")]
    public sealed class Match3ProgressEntity
    {
        [PrimaryKey, Column("id")] public int Id { get; set; }
        [Column("level_index")] public int LevelIndex { get; set; }
        [Column("best_score")] public int BestScore { get; set; }
        [Column("updated_at")] public string UpdatedAt { get; set; }
    }

    [Table("match3_attempts")]
    public sealed class Match3AttemptEntity
    {
        [PrimaryKey, AutoIncrement, Column("id")] public long Id { get; set; }
        [Column("level_id")] public string LevelId { get; set; }
        [Column("level_index")] public int LevelIndex { get; set; }
        [Column("content_hash")] public string ContentHash { get; set; }
        [Column("started_at")] public string StartedAt { get; set; }
        [Column("duration_ms")] public int DurationMs { get; set; }
        [Column("outcome")] public string Outcome { get; set; }
        [Column("score")] public int Score { get; set; }
        [Column("moves")] public int Moves { get; set; }
        [Column("continues")] public int Continues { get; set; }
        [Column("replay")] public byte[] Replay { get; set; }
    }

    public sealed class Match3ProgressDao : CachedDao<Match3ProgressEntity, int>
    {
        public Match3ProgressDao(IDatabase db) : base(db) { }
        protected override int KeyOf(Match3ProgressEntity entity) => entity.Id;
    }

    public sealed class Match3AttemptDao : Dao<Match3AttemptEntity, long>
    {
        public Match3AttemptDao(IDatabase db) : base(db) { }
    }

    public sealed class Match3Schema : ISchemaModule
    {
        public string Module => "match3";

        public IReadOnlyList<Migration> Migrations { get; } = new[]
        {
            new Migration(1, "progress and attempt history", new[]
            {
                "CREATE TABLE match3_progress(id INTEGER PRIMARY KEY NOT NULL, level_index INTEGER NOT NULL, best_score INTEGER NOT NULL, updated_at TEXT NOT NULL)",
                "CREATE TABLE match3_attempts(id INTEGER PRIMARY KEY AUTOINCREMENT, level_id TEXT NOT NULL, level_index INTEGER NOT NULL, " +
                "content_hash TEXT NOT NULL, started_at TEXT NOT NULL, duration_ms INTEGER NOT NULL, outcome TEXT NOT NULL, " +
                "score INTEGER NOT NULL, moves INTEGER NOT NULL, continues INTEGER NOT NULL, replay BLOB)",
                "CREATE INDEX ix_match3_attempts_level ON match3_attempts(level_id, started_at)",
            }),
        };
    }

    public sealed class Match3ProgressService : IMatch3Progress
    {
        private const int RowId = 1;

        private readonly Match3ProgressDao _dao;
        private readonly IClock _clock;
        private readonly Observable<int> _current = new Observable<int>();

        public Match3ProgressService(Match3ProgressDao dao, IClock clock)
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
            _dao.Upsert(new Match3ProgressEntity
            {
                Id = RowId, LevelIndex = next, BestScore = Math.Max(row?.BestScore ?? 0, score), UpdatedAt = _clock.UtcNow.ToString("O"),
            });
            var transaction = db.Current;
            if (transaction == null) _current.Value = next;
            else transaction.OnCommitted(() => _current.Value = next);
        }
    }

    public sealed class Match3ProgressLoadStep : BootStep
    {
        private static readonly string[] Dependencies = { SaveSteps.Warm };
        private readonly Match3ProgressService _progress;

        public Match3ProgressLoadStep(Match3ProgressService progress) => _progress = progress;

        public override string Name => "Progress";
        public override IReadOnlyList<string> DependsOn => Dependencies;

        public override UniTask RunAsync(CancellationToken ct)
        {
            _progress.Load();
            return UniTask.CompletedTask;
        }
    }
}
