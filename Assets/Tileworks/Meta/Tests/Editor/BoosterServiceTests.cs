using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using Tileworks.Meta.Boosters;
using Tileworks.Meta.Config;
using Tileworks.Meta.Economy;

namespace Tileworks.Meta.Tests
{
    public sealed class BoosterServiceTests
    {
        private SqliteDatabase _db;
        private EconomyService _economy;
        private BoosterService _boosters;

        [SetUp]
        public void SetUp()
        {
            _db = new SqliteDatabase(DatabaseOptions.InMemory(), new NullLog());
            _db.Open();
            new SchemaMigrator(_db, new NullLog()).Migrate(new ISchemaModule[] { new EconomySchema() });
            var clock = new FakeClock(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));
            _economy = new EconomyService(_db, new WalletDao(_db), new TransactionLogDao(_db), new GrantDao(_db), new MessageBus(), clock);
            _economy.Load();
            var config = new ConfigService(new IConfigSource[] { new JsonConfigSource("{\"boosters\":{\"unlockLevels\":[4,7],\"starterStock\":3}}") }, new NullLog());
            _boosters = new BoosterService(config, _economy, new Store());
        }

        [TearDown]
        public void TearDown() => _db.Dispose();

        [Test]
        public void TheStarterStock_IsGrantedOncePerSave()
        {
            _boosters.GrantStarter();
            _boosters.GrantStarter();
            Assert.That(_boosters.Stock(0).Value, Is.EqualTo(3));
            Assert.That(_boosters.Stock(1).Value, Is.EqualTo(3));
        }

        [Test]
        public void Spending_TakesOne_AndFailsAtZero()
        {
            _boosters.GrantStarter();
            for (var i = 0; i < 3; i++) Assert.That(_boosters.Spend(0, "level").IsOk, Is.True);
            Assert.That(_boosters.Spend(0, "level").IsOk, Is.False);
            Assert.That(_boosters.Stock(0).Value, Is.Zero);
            Assert.That(_boosters.Stock(1).Value, Is.EqualTo(3));
        }

        [Test]
        public void Unlocks_FollowTheConfig_AndTheIntroIsRemembered()
        {
            Assert.That(_boosters.IsUnlocked(0, 3), Is.False);
            Assert.That(_boosters.IsUnlocked(0, 4), Is.True);
            Assert.That(_boosters.UnlockLevel(1), Is.EqualTo(7));
            Assert.That(_boosters.WasIntroduced(0), Is.False);
            _boosters.MarkIntroduced(0);
            Assert.That(_boosters.WasIntroduced(0), Is.True);
            Assert.That(_boosters.WasIntroduced(1), Is.False);
        }

        private sealed class Store : IKeyValueStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string Get(string key, string fallback = null) => _values.TryGetValue(key, out var value) ? value : fallback;
            public void Set(string key, string value) => _values[key] = value;
            public long GetLong(string key, long fallback = 0) => long.TryParse(Get(key), out var value) ? value : fallback;
            public void SetLong(string key, long value) => Set(key, value.ToString());
            public bool GetBool(string key, bool fallback = false) => Get(key) is { } value ? value == "1" : fallback;
            public void SetBool(string key, bool value) => Set(key, value ? "1" : "0");
        }
    }
}
