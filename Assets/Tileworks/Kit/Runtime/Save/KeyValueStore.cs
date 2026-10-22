using System.Collections.Generic;
using System.Globalization;
using SQLite;

namespace Tileworks.Kit.Save
{
    public interface IKeyValueStore
    {
        string Get(string key, string fallback = null);
        void Set(string key, string value);
        long GetLong(string key, long fallback = 0);
        void SetLong(string key, long value);
        bool GetBool(string key, bool fallback = false);
        void SetBool(string key, bool value);
    }

    [Table("settings")]
    public sealed class SettingEntity
    {
        [PrimaryKey, Column("key")] public string Key { get; set; }
        [Column("value")] public string Value { get; set; }
    }

    public sealed class SettingsDao : CachedDao<SettingEntity, string>
    {
        public SettingsDao(IDatabase db) : base(db) { }
        protected override string KeyOf(SettingEntity entity) => entity.Key;
    }

    // Small typed values (settings, clock guard) in one kit-owned table.
    public sealed class KeyValueStore : IKeyValueStore
    {
        private readonly SettingsDao _dao;

        public KeyValueStore(SettingsDao dao) => _dao = dao;

        public string Get(string key, string fallback = null) => _dao.IsLoaded ? _dao.Find(key)?.Value ?? fallback : fallback;
        public void Set(string key, string value) => _dao.Upsert(new SettingEntity { Key = key, Value = value });

        public long GetLong(string key, long fallback = 0) =>
            long.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

        public void SetLong(string key, long value) => Set(key, value.ToString(CultureInfo.InvariantCulture));
        public bool GetBool(string key, bool fallback = false) => Get(key) is { } value ? value == "1" : fallback;
        public void SetBool(string key, bool value) => Set(key, value ? "1" : "0");
    }

    public sealed class KitSchema : ISchemaModule
    {
        public string Module => "kit.settings";

        public IReadOnlyList<Migration> Migrations { get; } = new[]
        {
            new Migration(1, "settings", new[] { "CREATE TABLE settings(key TEXT PRIMARY KEY NOT NULL, value TEXT NOT NULL)" }),
        };
    }
}
