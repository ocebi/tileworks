using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SQLite;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;

namespace Tileworks.Kit.Tests
{
    public sealed class SaveTests
    {
        [Table("wallet")]
        public sealed class WalletRow
        {
            [PrimaryKey, Column("resource_id")] public string ResourceId { get; set; }
            [Column("balance")] public long Balance { get; set; }
        }

        public sealed class WalletDao : CachedDao<WalletRow, string>
        {
            public WalletDao(IDatabase db) : base(db) { }
            protected override string KeyOf(WalletRow entity) => entity.ResourceId;
        }

        public sealed class PlainWalletDao : Dao<WalletRow, string>
        {
            public PlainWalletDao(IDatabase db) : base(db) { }
        }

        private sealed class EconomySchema : ISchemaModule
        {
            public string Module => "test.economy";

            public IReadOnlyList<Migration> Migrations { get; } = new[]
            {
                new Migration(1, "wallet", new[]
                {
                    "CREATE TABLE wallet(resource_id TEXT PRIMARY KEY NOT NULL, balance INTEGER NOT NULL)",
                }),
                new Migration(2, "seed coins", Array.Empty<string>(),
                    db => db.Execute("INSERT INTO wallet(resource_id, balance) VALUES (?, ?)", "coins", 100)),
            };
        }

        private sealed class BrokenSchema : ISchemaModule
        {
            public string Module => "test.broken";

            public IReadOnlyList<Migration> Migrations { get; } = new[]
            {
                new Migration(1, "ok", new[] { "CREATE TABLE a(id INTEGER PRIMARY KEY)" }),
                new Migration(2, "fails halfway", new[] { "CREATE TABLE b(id INTEGER PRIMARY KEY)", "THIS IS NOT SQL" }),
            };
        }

        private SqliteDatabase _db;

        [SetUp]
        public void SetUp()
        {
            _db = new SqliteDatabase(DatabaseOptions.InMemory(), new NullLog());
            _db.Open();
        }

        [TearDown]
        public void TearDown() => _db.Dispose();

        private SchemaMigrator Migrator() => new SchemaMigrator(_db, new NullLog());

        [Test]
        public void Migrate_AppliesPendingMigrations_OnceOnly()
        {
            var modules = new ISchemaModule[] { new EconomySchema() };
            var first = Migrator().Migrate(modules);
            var second = Migrator().Migrate(modules);

            Assert.That(first.Count, Is.EqualTo(2));
            Assert.That(second, Is.Empty);
            Assert.That(Migrator().GetVersion("test.economy"), Is.EqualTo(2));
            Assert.That(_db.Scalar<long>("SELECT balance FROM wallet WHERE resource_id = ?", "coins"), Is.EqualTo(100));
            Assert.That(_db.Scalar<int>("PRAGMA user_version"), Is.EqualTo(SchemaMigrator.FileFormatVersion));
        }

        [Test]
        public void Migrate_FailureRollsBackThatMigration_AndKeepsEarlierOnes()
        {
            var e = Assert.Throws<MigrationException>(() => Migrator().Migrate(new ISchemaModule[] { new BrokenSchema() }));
            Assert.That(e.Version, Is.EqualTo(2));
            Assert.That(Migrator().GetVersion("test.broken"), Is.EqualTo(1));
            Assert.That(_db.Scalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'b'"), Is.EqualTo(0), "table b rolled back");
            Assert.That(_db.Current, Is.Null);
        }

        [Test]
        public void Migrate_RejectsDatabaseFromNewerBuild()
        {
            Migrator().Migrate(new ISchemaModule[] { new EconomySchema() });
            _db.Execute("UPDATE schema_versions SET version = 9 WHERE module = ?", "test.economy");
            Assert.Throws<MigrationException>(() => Migrator().Migrate(new ISchemaModule[] { new EconomySchema() }));
        }

        // A module dropped from the build leaves its row and tables behind; they are never read, so old saves still open.
        [Test]
        public void Migrate_IgnoresARowForAnUnregisteredModule()
        {
            Migrator().Migrate(new ISchemaModule[] { new EconomySchema() });
            _db.Execute("CREATE TABLE removed_entries(id TEXT PRIMARY KEY NOT NULL)");
            _db.Execute("INSERT INTO schema_versions(module, version, applied_at) VALUES (?, ?, ?)", "test.removed", 3, "2026-01-01");

            Assert.That(Migrator().Migrate(new ISchemaModule[] { new EconomySchema() }), Is.Empty);
            Assert.That(Migrator().GetVersion("test.removed"), Is.EqualTo(3));
            Assert.That(_db.Scalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'removed_entries'"), Is.EqualTo(1));
        }

        [Test]
        public void Transaction_CommitPersists_DisposeWithoutCommitRollsBack()
        {
            Migrator().Migrate(new ISchemaModule[] { new EconomySchema() });
            var dao = new PlainWalletDao(_db);
            var before = _db.RecentTransactions.Count;

            using (var tx = _db.Begin("commit"))
            {
                dao.Upsert(new WalletRow { ResourceId = "gems", Balance = 5 });
                tx.Commit();
            }

            using (_db.Begin("rollback"))
            {
                dao.Upsert(new WalletRow { ResourceId = "gems", Balance = 999 });
            }

            Assert.That(dao.Find("gems").Balance, Is.EqualTo(5));
            Assert.That(_db.RecentTransactions.Count, Is.EqualTo(before + 2));
            Assert.That(_db.RecentTransactions[before + 1].Committed, Is.False);
        }

        [Test]
        public void NestedTransaction_IsASavepoint_ThatCanRollBackAlone()
        {
            Migrator().Migrate(new ISchemaModule[] { new EconomySchema() });
            var dao = new PlainWalletDao(_db);

            using (var outer = _db.Begin("outer"))
            {
                dao.Upsert(new WalletRow { ResourceId = "a", Balance = 1 });
                using (_db.Begin("inner"))
                {
                    dao.Upsert(new WalletRow { ResourceId = "b", Balance = 2 });
                }

                outer.Commit();
            }

            Assert.That(dao.Find("a"), Is.Not.Null);
            Assert.That(dao.Find("b"), Is.Null);
        }

        [Test]
        public void CompletingOuterBeforeInner_Throws()
        {
            var outer = _db.Begin("outer");
            var inner = _db.Begin("inner");
            Assert.Throws<InvalidOperationException>(() => outer.Commit());
            inner.Dispose();
            outer.Dispose();
        }

        [Test]
        public void CachedDao_UpdatesCacheOnlyAfterCommit()
        {
            Migrator().Migrate(new ISchemaModule[] { new EconomySchema() });
            var dao = new WalletDao(_db);
            dao.Load();

            using (var tx = _db.Begin("spend"))
            {
                dao.Upsert(new WalletRow { ResourceId = "coins", Balance = 40 });
                Assert.That(dao.Find("coins").Balance, Is.EqualTo(100), "not visible before commit");
                tx.Commit();
            }

            Assert.That(dao.Find("coins").Balance, Is.EqualTo(40));

            using (_db.Begin("rolled back"))
            {
                dao.Upsert(new WalletRow { ResourceId = "coins", Balance = 0 });
            }

            Assert.That(dao.Find("coins").Balance, Is.EqualTo(40), "rolled-back write never reaches the cache");
            Assert.That(new PlainWalletDao(_db).Find("coins").Balance, Is.EqualTo(40));
        }

        [Test]
        public void CachedDao_RolledBackSavepoint_DropsItsCacheUpdates()
        {
            Migrator().Migrate(new ISchemaModule[] { new EconomySchema() });
            var dao = new WalletDao(_db);
            dao.Load();

            using (var outer = _db.Begin("outer"))
            {
                dao.Upsert(new WalletRow { ResourceId = "gems", Balance = 1 });
                using (_db.Begin("inner"))
                {
                    dao.Upsert(new WalletRow { ResourceId = "coins", Balance = 1 });
                }

                outer.Commit();
            }

            Assert.That(dao.Find("gems").Balance, Is.EqualTo(1));
            Assert.That(dao.Find("coins").Balance, Is.EqualTo(100));
        }

        [Test]
        public void CachedDao_StoresACopy_SoCallerMutationsDoNotLeak()
        {
            Migrator().Migrate(new ISchemaModule[] { new EconomySchema() });
            var dao = new WalletDao(_db);
            dao.Load();

            var row = new WalletRow { ResourceId = "gems", Balance = 3 };
            dao.Upsert(row);
            row.Balance = 1000;

            Assert.That(dao.Find("gems").Balance, Is.EqualTo(3));
        }

        [Test]
        public void FileDatabase_Backup_Restore_AndQuickCheck()
        {
            var root = Path.Combine(Path.GetTempPath(), "tileworks-save-" + Guid.NewGuid().ToString("N"));
            try
            {
                var profile = new ProfileStore(root);
                profile.Resolve();
                var db = new SqliteDatabase(new DatabaseOptions { Path = profile.DatabasePath, UseWal = true }, new NullLog());
                db.Open();
                Assert.That(db.QuickCheck(), Is.True);
                new SchemaMigrator(db, new NullLog()).Migrate(new ISchemaModule[] { new EconomySchema() });
                db.BackupTo(profile.BackupPath);
                db.Execute("UPDATE wallet SET balance = 0");
                db.Close();

                profile.RestoreBackup();
                db.Open();
                Assert.That(db.Scalar<long>("SELECT balance FROM wallet WHERE resource_id = 'coins'"), Is.EqualTo(100));
                db.Close();

                Assert.That(new ProfileStore(root).Resolve(), Is.EqualTo(profile.ProfileId), "profile id is stable");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void QueryText_ReturnsHeaderAndRows()
        {
            Migrator().Migrate(new ISchemaModule[] { new EconomySchema() });
            var rows = _db.QueryText("SELECT resource_id, balance FROM wallet");
            Assert.That(rows[0], Is.EqualTo(new[] { "resource_id", "balance" }));
            Assert.That(rows[1], Is.EqualTo(new[] { "coins", "100" }));
        }
    }
}
