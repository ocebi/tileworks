using System;
using System.Collections.Generic;
using System.Threading;
using Blast.Api;
using Cysharp.Threading.Tasks;
using SQLite;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;

namespace Blast.Service
{
    [Table("blast_progress")]
    public sealed class BlastProgressEntity
    {
        [PrimaryKey, Column("id")] public int Id { get; set; }
        [Column("level_index")] public int LevelIndex { get; set; }
        [Column("best_score")] public int BestScore { get; set; }
        [Column("updated_at")] public string UpdatedAt { get; set; }
    }

    [Table("blast_attempts")]
    public sealed class BlastAttemptEntity
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

    public sealed class BlastProgressDao : CachedDao<BlastProgressEntity, int>
    {
        public BlastProgressDao(IDatabase db) : base(db) { }
        protected override int KeyOf(BlastProgressEntity entity) => entity.Id;
    }

    public sealed class BlastAttemptDao : Dao<BlastAttemptEntity, long>
    {
        public BlastAttemptDao(IDatabase db) : base(db) { }
    }

    public sealed class BlastSchema : ISchemaModule
    {
        public string Module => "blast";

        public IReadOnlyList<Migration> Migrations { get; } = new[]
        {
            new Migration(1, "progress and attempt history", new[]
            {
                "CREATE TABLE blast_progress(id INTEGER PRIMARY KEY NOT NULL, level_index INTEGER NOT NULL, best_score INTEGER NOT NULL, updated_at TEXT NOT NULL)",
                "CREATE TABLE blast_attempts(id INTEGER PRIMARY KEY AUTOINCREMENT, level_id TEXT NOT NULL, level_index INTEGER NOT NULL, " +
                "content_hash TEXT NOT NULL, started_at TEXT NOT NULL, duration_ms INTEGER NOT NULL, outcome TEXT NOT NULL, " +
                "score INTEGER NOT NULL, moves INTEGER NOT NULL, continues INTEGER NOT NULL, replay BLOB)",
                "CREATE INDEX ix_blast_attempts_level ON blast_attempts(level_id, started_at)",
            }),
        };
    }

    public sealed class BlastProgressService : IBlastProgress
    {
        private const int RowId = 1;

        private readonly BlastProgressDao _dao;
        private readonly IClock _clock;
        private readonly Observable<int> _current = new Observable<int>();

        public BlastProgressService(BlastProgressDao dao, IClock clock)
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
            _dao.Upsert(new BlastProgressEntity
            {
                Id = RowId, LevelIndex = next, BestScore = Math.Max(row?.BestScore ?? 0, score), UpdatedAt = _clock.UtcNow.ToString("O"),
            });
            var transaction = db.Current;
            if (transaction == null) _current.Value = next;
            else transaction.OnCommitted(() => _current.Value = next);
        }
    }

    public sealed class BlastProgressLoadStep : BootStep
    {
        private static readonly string[] Dependencies = { SaveSteps.Warm };
        private readonly BlastProgressService _progress;

        public BlastProgressLoadStep(BlastProgressService progress) => _progress = progress;

        public override string Name => "Progress";
        public override IReadOnlyList<string> DependsOn => Dependencies;

        public override UniTask RunAsync(CancellationToken ct)
        {
            _progress.Load();
            return UniTask.CompletedTask;
        }
    }
}
