using System;
using System.IO;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Save
{
    public sealed class ProfileStore
    {
        private readonly string _saveRoot;

        public ProfileStore(string persistentRoot, string folder = "save")
        {
            _saveRoot = Path.Combine(Guard.NotEmpty(persistentRoot, nameof(persistentRoot)), Guard.NotEmpty(folder, nameof(folder)));
        }

        public string ProfileId { get; private set; }
        public string DatabasePath => Path.Combine(ProfileDirectory, "player.db");
        public string BackupPath => Path.Combine(ProfileDirectory, "player.bak.db");
        public string ProfileDirectory => Path.Combine(_saveRoot, ProfileId ?? throw new InvalidOperationException("Call Resolve first."));

        public string Resolve()
        {
            Directory.CreateDirectory(_saveRoot);
            var pointer = Path.Combine(_saveRoot, "profile.txt");
            if (File.Exists(pointer))
            {
                var id = File.ReadAllText(pointer).Trim();
                if (Guid.TryParse(id, out _))
                {
                    ProfileId = id;
                    return id;
                }
            }

            ProfileId = Guid.NewGuid().ToString("N");
            File.WriteAllText(pointer, ProfileId);
            return ProfileId;
        }

        public bool HasBackup => File.Exists(BackupPath);

        public void RestoreBackup()
        {
            DeleteDatabaseFiles(keepBackup: true);
            File.Copy(BackupPath, DatabasePath, overwrite: true);
        }

        public string Quarantine()
        {
            var target = Path.Combine(ProfileDirectory, $"player.broken-{DateTime.UtcNow:yyyyMMddHHmmss}.db");
            if (File.Exists(DatabasePath)) File.Move(DatabasePath, target);
            DeleteDatabaseFiles(keepBackup: true);
            return target;
        }

        public void DeleteDatabaseFiles(bool keepBackup = false)
        {
            foreach (var path in new[] { DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm", DatabasePath + "-journal" })
            {
                if (File.Exists(path)) File.Delete(path);
            }

            if (!keepBackup && File.Exists(BackupPath)) File.Delete(BackupPath);
        }
    }
}
