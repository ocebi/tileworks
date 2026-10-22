using System;
using System.Collections.Generic;
using SQLite;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Save
{
    // One transaction per migration; a failure stops the run.
    public sealed class SchemaMigrator
    {
        public const int FileFormatVersion = 1;

        private readonly IDatabase _db;
        private readonly ILog _log;

        public SchemaMigrator(IDatabase db, ILog log)
        {
            _db = Guard.NotNull(db, nameof(db));
            _log = Guard.NotNull(log, nameof(log));
        }

        public IReadOnlyList<AppliedMigration> Migrate(IReadOnlyList<ISchemaModule> modules)
        {
            EnsureBookkeeping();
            ValidateModules(modules);

            var applied = new List<AppliedMigration>();
            foreach (var module in modules)
            {
                var current = GetVersion(module.Module);
                var latest = module.Migrations.Count == 0 ? 0 : module.Migrations[module.Migrations.Count - 1].Version;
                if (current > latest)
                    throw new MigrationException(module.Module, $"database is at v{current} but this build only knows v{latest}");

                foreach (var migration in module.Migrations)
                {
                    if (migration.Version <= current) continue;
                    Apply(module.Module, migration);
                    applied.Add(new AppliedMigration(module.Module, migration.Version, migration.Description));
                }
            }

            if (applied.Count > 0) _log.Info(LogTag.Save, "Applied {0} migration(s)", applied.Count);
            return applied;
        }

        public int GetVersion(string module)
        {
            var rows = _db.Query<SchemaVersionRow>("SELECT module, version, applied_at FROM schema_versions WHERE module = ?", module);
            return rows.Count == 0 ? 0 : rows[0].Version;
        }

        private void Apply(string module, Migration migration)
        {
            using var transaction = _db.Begin($"migrate:{module}:v{migration.Version}");
            try
            {
                foreach (var statement in migration.Sql) _db.Execute(statement);
                migration.Data?.Invoke(_db);
                _db.Execute("INSERT OR REPLACE INTO schema_versions(module, version, applied_at) VALUES (?, ?, ?)",
                    module, migration.Version, DateTime.UtcNow.ToString("O"));
                transaction.Commit();
            }
            catch (Exception e) when (!(e is MigrationException))
            {
                throw new MigrationException(module, migration.Version, e);
            }
        }

        private void EnsureBookkeeping()
        {
            _db.Execute("CREATE TABLE IF NOT EXISTS schema_versions(module TEXT PRIMARY KEY NOT NULL, version INTEGER NOT NULL, applied_at TEXT NOT NULL)");
            var format = _db.Scalar<int>("PRAGMA user_version");
            if (format == 0) _db.Execute("PRAGMA user_version = " + FileFormatVersion);
            else if (format > FileFormatVersion)
                throw new MigrationException("file", $"file format v{format} is newer than this build (v{FileFormatVersion})");
        }

        private static void ValidateModules(IReadOnlyList<ISchemaModule> modules)
        {
            var seen = new HashSet<string>();
            foreach (var module in modules)
            {
                if (!seen.Add(module.Module)) throw new MigrationException(module.Module, "module registered twice");
                for (var i = 0; i < module.Migrations.Count; i++)
                {
                    if (module.Migrations[i].Version != i + 1)
                        throw new MigrationException(module.Module, $"migration #{i} has version {module.Migrations[i].Version}; versions must be 1, 2, 3…");
                }
            }
        }

        [Table("schema_versions")]
        private sealed class SchemaVersionRow
        {
            [Column("module")] public string Module { get; set; }
            [Column("version")] public int Version { get; set; }
            [Column("applied_at")] public string AppliedAt { get; set; }
        }
    }
}
