using System;
using System.Collections.Generic;
using SQLite;
using Tileworks.Kit.Save;

namespace TileMatch.Service
{
    [Table("progress")]
    public sealed class ProgressEntity
    {
        [PrimaryKey, Column("id")] public int Id { get; set; }
        [Column("level_index")] public int LevelIndex { get; set; }
        [Column("updated_at")] public string UpdatedAt { get; set; }
        [Column("starter_granted")] public bool StarterGranted { get; set; }
    }

    [Table("level_attempts")]
    public sealed class LevelAttemptEntity
    {
        [PrimaryKey, AutoIncrement, Column("id")] public long Id { get; set; }
        [Column("level_id")] public string LevelId { get; set; }
        [Column("level_index")] public int LevelIndex { get; set; }
        [Column("attempt")] public int Attempt { get; set; }
        [Column("content_hash")] public string ContentHash { get; set; }
        [Column("started_at")] public string StartedAt { get; set; }
        [Column("duration_ms")] public int DurationMs { get; set; }
        [Column("outcome")] public string Outcome { get; set; }
        [Column("moves")] public int Moves { get; set; }
        [Column("boosters_json")] public string BoostersJson { get; set; }
        [Column("continues")] public int Continues { get; set; }
        [Column("replay")] public byte[] Replay { get; set; }
    }

    public sealed class ProgressDao : CachedDao<ProgressEntity, int>
    {
        public ProgressDao(IDatabase db) : base(db) { }
        protected override int KeyOf(ProgressEntity entity) => entity.Id;
    }

    public sealed class LevelAttemptDao : Dao<LevelAttemptEntity, long>
    {
        public const int ReplaysKeptPerLevel = 20;

        public LevelAttemptDao(IDatabase db) : base(db) { }

        public int CountAttempts(string levelId) => Db.Scalar<int>("SELECT COUNT(*) FROM level_attempts WHERE level_id = ?", levelId);

        // Keeps replays (the heavy column) only for the most recent attempts of each level.
        private const string TrimSql = @"UPDATE level_attempts SET replay = NULL
            WHERE level_id = ? AND id NOT IN (SELECT id FROM level_attempts WHERE level_id = ? ORDER BY id DESC LIMIT ?)";

        public void TrimReplays(string levelId) => Db.Execute(TrimSql, levelId, levelId, ReplaysKeptPerLevel);

        public List<LevelAttemptEntity> Recent(int limit) =>
            Db.Query<LevelAttemptEntity>("SELECT * FROM level_attempts ORDER BY id DESC LIMIT ?", limit);
    }

    public sealed class TileMatchSchema : ISchemaModule
    {
        public string Module => "tilematch";

        public IReadOnlyList<Migration> Migrations { get; } = new[]
        {
            new Migration(1, "progress and attempt history", new[]
            {
                "CREATE TABLE progress(id INTEGER PRIMARY KEY NOT NULL, level_index INTEGER NOT NULL, updated_at TEXT NOT NULL)",
                "CREATE TABLE level_attempts(id INTEGER PRIMARY KEY AUTOINCREMENT, level_id TEXT NOT NULL, level_index INTEGER NOT NULL, " +
                "attempt INTEGER NOT NULL, content_hash TEXT NOT NULL, started_at TEXT NOT NULL, duration_ms INTEGER NOT NULL, " +
                "outcome TEXT NOT NULL, moves INTEGER NOT NULL, boosters_json TEXT NOT NULL, continues INTEGER NOT NULL, replay BLOB)",
                "CREATE INDEX ix_level_attempts_level ON level_attempts(level_id, started_at)",
            }),
            new Migration(2, "starter bundle flag", new[]
            {
                "ALTER TABLE progress ADD COLUMN starter_granted INTEGER NOT NULL DEFAULT 0",
            }),
        };
    }
}
