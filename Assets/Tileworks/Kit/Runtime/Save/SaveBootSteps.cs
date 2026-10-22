using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Save
{
    public static class SaveSteps
    {
        public const string Open = "DatabaseOpen";
        public const string Migrate = "Migration";
        public const string Warm = "CacheWarm";
    }

    // Opens the profile database; a damaged file is replaced by its backup, or by a fresh profile as a last resort.
    public sealed class DatabaseOpenStep : BootStep
    {
        private readonly SqliteDatabase _db;
        private readonly ProfileStore _profile;
        private readonly ILog _log;

        public DatabaseOpenStep(SqliteDatabase db, ProfileStore profile, ILog log)
        {
            _db = db;
            _profile = profile;
            _log = log;
        }

        public override string Name => SaveSteps.Open;

        public override UniTask RunAsync(CancellationToken ct)
        {
            if (_db.IsInMemory)
            {
                _db.Open();
                return UniTask.CompletedTask;
            }

            _db.Open();
            if (_db.QuickCheck()) return UniTask.CompletedTask;

            _db.Close();
            if (_profile.HasBackup)
            {
                _log.Warn(LogTag.Save, "Database failed quick_check; restoring backup");
                _profile.RestoreBackup();
                _db.Open();
                if (_db.QuickCheck()) return UniTask.CompletedTask;
                _db.Close();
            }

            var kept = _profile.Quarantine();
            _log.Error(LogTag.Save, $"Database unrecoverable; kept at {kept}, starting a fresh profile");
            _db.Open();
            return UniTask.CompletedTask;
        }
    }

    public sealed class MigrationStep : BootStep
    {
        private static readonly string[] Dependencies = { SaveSteps.Open };

        private readonly SqliteDatabase _db;
        private readonly IReadOnlyList<ISchemaModule> _modules;
        private readonly ProfileStore _profile;
        private readonly ILog _log;

        public MigrationStep(SqliteDatabase db, IReadOnlyList<ISchemaModule> modules, ProfileStore profile, ILog log)
        {
            _db = db;
            _modules = modules;
            _profile = profile;
            _log = log;
        }

        public override string Name => SaveSteps.Migrate;
        public override IReadOnlyList<string> DependsOn => Dependencies;

        public override UniTask RunAsync(CancellationToken ct)
        {
            var applied = new SchemaMigrator(_db, _log).Migrate(_modules);
            if (applied.Count > 0 && !_db.IsInMemory)
            {
                try
                {
                    _db.BackupTo(_profile.BackupPath);
                }
                catch (Exception e)
                {
                    _log.Warn(LogTag.Save, "Backup after migration failed: {0}", e.Message);
                }
            }

            return UniTask.CompletedTask;
        }
    }

    public sealed class CacheWarmStep : BootStep
    {
        private static readonly string[] Dependencies = { SaveSteps.Migrate };

        private readonly IReadOnlyList<ICachedDao> _daos;

        public CacheWarmStep(IReadOnlyList<ICachedDao> daos) => _daos = daos;

        public override string Name => SaveSteps.Warm;
        public override IReadOnlyList<string> DependsOn => Dependencies;

        public override UniTask RunAsync(CancellationToken ct)
        {
            foreach (var dao in _daos) dao.Load();
            return UniTask.CompletedTask;
        }
    }
}
