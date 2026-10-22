using System;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using Tileworks.Kit.Time;

namespace Tileworks.Kit.Tests
{
    public sealed class TrustedClockTests
    {
        private sealed class FakeMonotonic : IMonotonicSource
        {
            public TimeSpan Elapsed = TimeSpan.FromHours(1);
            public bool Available = true;

            public bool TryGetSinceBoot(out TimeSpan elapsed)
            {
                elapsed = Elapsed;
                return Available;
            }
        }

        private sealed class FakeDevice : IDeviceTime
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        }

        private SqliteDatabase _db;
        private KeyValueStore _store;
        private FakeMonotonic _monotonic;
        private FakeDevice _device;

        [SetUp]
        public void SetUp()
        {
            _db = new SqliteDatabase(DatabaseOptions.InMemory(), new NullLog());
            _db.Open();
            new SchemaMigrator(_db, new NullLog()).Migrate(new ISchemaModule[] { new KitSchema() });
            var dao = new SettingsDao(_db);
            dao.Load();
            _store = new KeyValueStore(dao);
            _monotonic = new FakeMonotonic();
            _device = new FakeDevice();
        }

        [TearDown]
        public void TearDown() => _db.Dispose();

        private TrustedClock Launch()
        {
            var clock = new TrustedClock(_store, _monotonic, _device, Array.Empty<ITimeSource>(), new NullLog());
            clock.Initialize();
            return clock;
        }

        [Test]
        public void HonestClock_IsTrusted()
        {
            Launch();
            _device.UtcNow += TimeSpan.FromMinutes(30);
            _monotonic.Elapsed += TimeSpan.FromMinutes(30);

            var clock = Launch();
            Assert.That(clock.IsTrusted, Is.True);
            Assert.That(clock.UtcNow, Is.EqualTo(_device.UtcNow));
        }

        [Test]
        public void ClockSetForward_IsIgnored()
        {
            var start = _device.UtcNow;
            Launch();
            _device.UtcNow += TimeSpan.FromDays(1);
            _monotonic.Elapsed += TimeSpan.FromMinutes(10);

            var clock = Launch();
            Assert.That(clock.IsTrusted, Is.False);
            Assert.That(clock.UtcNow, Is.EqualTo(start + TimeSpan.FromMinutes(10)));
        }

        [Test]
        public void ClockSetBackward_DoesNotRewindTime()
        {
            var start = _device.UtcNow;
            Launch();
            _device.UtcNow -= TimeSpan.FromHours(5);
            _monotonic.Elapsed += TimeSpan.FromMinutes(1);

            var clock = Launch();
            Assert.That(clock.UtcNow, Is.EqualTo(start + TimeSpan.FromMinutes(1)));
        }

        [Test]
        public void AfterReboot_DeviceTimeIsAccepted()
        {
            Launch();
            _device.UtcNow += TimeSpan.FromDays(1);
            _monotonic.Elapsed = TimeSpan.FromMinutes(3);

            var clock = Launch();
            Assert.That(clock.IsTrusted, Is.True);
            Assert.That(clock.UtcNow, Is.EqualTo(_device.UtcNow));
        }

        [Test]
        public void DebugOffset_MovesTrustedTime()
        {
            var clock = Launch();
            clock.DebugOffset = TimeSpan.FromHours(2);
            Assert.That(clock.UtcNow, Is.EqualTo(_device.UtcNow + TimeSpan.FromHours(2)));
        }
    }
}
