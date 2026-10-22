using System.Collections.Generic;
using SQLite;
using Tileworks.Kit.Save;

namespace Tileworks.Meta.Economy
{
    [Table("wallet")]
    public sealed class WalletEntity
    {
        [PrimaryKey, Column("resource_id")] public string ResourceId { get; set; }
        [Column("balance")] public long Balance { get; set; }
    }

    [Table("transactions")]
    public sealed class TransactionEntity
    {
        [PrimaryKey, AutoIncrement, Column("id")] public long Id { get; set; }
        [Column("source")] public string Source { get; set; }
        [Column("context")] public string Context { get; set; }
        [Column("deltas")] public string Deltas { get; set; }
        [Column("created_at")] public string CreatedAt { get; set; }
    }

    [Table("grants")]
    public sealed class GrantEntity
    {
        [PrimaryKey, Column("id")] public string Id { get; set; }
        [Column("created_at")] public string CreatedAt { get; set; }
    }

    [Table("lives")]
    public sealed class LivesEntity
    {
        [PrimaryKey, Column("id")] public int Id { get; set; }
        [Column("stored")] public int Stored { get; set; }
        [Column("last_regen_at")] public long LastRegenAtTicks { get; set; }
        [Column("infinite_until")] public long InfiniteUntilTicks { get; set; }
    }

    // Reads go to the database so a balance seen inside an open transaction includes its own writes.
    public sealed class WalletDao : Dao<WalletEntity, string>
    {
        public WalletDao(IDatabase db) : base(db) { }
    }

    public sealed class TransactionLogDao : Dao<TransactionEntity, long>
    {
        public const int Kept = 500;
        private const string TrimSql = "DELETE FROM transactions WHERE id <= (SELECT MAX(id) FROM transactions) - ?";

        public TransactionLogDao(IDatabase db) : base(db) { }

        public void Trim() => Db.Execute(TrimSql, Kept);

        public List<TransactionEntity> Recent(int limit) => Db.Query<TransactionEntity>("SELECT * FROM transactions ORDER BY id DESC LIMIT ?", limit);
    }

    public sealed class GrantDao : Dao<GrantEntity, string>
    {
        public GrantDao(IDatabase db) : base(db) { }
    }

    public sealed class LivesDao : Dao<LivesEntity, int>
    {
        public LivesDao(IDatabase db) : base(db) { }
    }

    public sealed class EconomySchema : ISchemaModule
    {
        public const long StarterCoins = 500;

        public string Module => "meta.economy";

        public IReadOnlyList<Migration> Migrations { get; } = new[]
        {
            new Migration(1, "wallet, transaction log and lives", new[]
            {
                "CREATE TABLE wallet(resource_id TEXT PRIMARY KEY NOT NULL, balance INTEGER NOT NULL CHECK (balance >= 0))",
                "CREATE TABLE transactions(id INTEGER PRIMARY KEY AUTOINCREMENT, source TEXT NOT NULL, context TEXT NOT NULL, deltas TEXT NOT NULL, created_at TEXT NOT NULL)",
                "CREATE TABLE lives(id INTEGER PRIMARY KEY NOT NULL, stored INTEGER NOT NULL, last_regen_at INTEGER NOT NULL, infinite_until INTEGER NOT NULL)",
            }, db => db.Execute("INSERT INTO wallet(resource_id, balance) VALUES (?, ?)", Api.Resources.Coins, StarterCoins)),
            new Migration(2, "one-time grants", new[]
            {
                "CREATE TABLE grants(id TEXT PRIMARY KEY NOT NULL, created_at TEXT NOT NULL)",
            }),
        };
    }
}
