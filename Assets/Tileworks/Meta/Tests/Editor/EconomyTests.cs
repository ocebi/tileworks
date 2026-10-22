using System;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using Tileworks.Meta.Api;
using Tileworks.Meta.Config;
using Tileworks.Meta.Economy;

namespace Tileworks.Meta.Tests
{
    public sealed class EconomyTests
    {
        private SqliteDatabase _db;
        private FakeClock _clock;
        private EconomyService _economy;
        private MessageBus _bus;

        [SetUp]
        public void SetUp()
        {
            _db = new SqliteDatabase(DatabaseOptions.InMemory(), new NullLog());
            _db.Open();
            new SchemaMigrator(_db, new NullLog()).Migrate(new ISchemaModule[] { new EconomySchema() });
            _clock = new FakeClock(new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc));
            _bus = new MessageBus();
            _economy = new EconomyService(_db, new WalletDao(_db), new TransactionLogDao(_db), new GrantDao(_db), _bus, _clock);
            _economy.Load();
        }

        [TearDown]
        public void TearDown() => _db.Dispose();

        [Test]
        public void StarterCoins_AreSeededByTheMigration() =>
            Assert.That(_economy.Balance(Resources.Coins).Value, Is.EqualTo(EconomySchema.StarterCoins));

        [Test]
        public void Apply_MovesEveryDeltaAtomically_AndPublishes()
        {
            ResourceChanged? last = null;
            using var sub = _bus.Subscribe((in ResourceChanged e) => last = e);
            var result = _economy.Apply(new EconomyTransaction(TransactionSource.ShopPurchase, "bundle",
                new ResourceDelta(Resources.Coins, -300), new ResourceDelta("booster.undo", 3)));

            Assert.That(result.IsOk, Is.True);
            Assert.That(_economy.Balance(Resources.Coins).Value, Is.EqualTo(200));
            Assert.That(_economy.Balance("booster.undo").Value, Is.EqualTo(3));
            Assert.That(last.HasValue, Is.True);
        }

        [Test]
        public void Apply_RejectsOverspending_WithoutAnyChange()
        {
            var result = _economy.Apply(new EconomyTransaction(TransactionSource.ShopPurchase, "too much",
                new ResourceDelta(Resources.Coins, -600), new ResourceDelta("booster.undo", 3)));

            Assert.That(result.Error.Kind, Is.EqualTo(ErrorKind.InsufficientFunds));
            Assert.That(_economy.Balance(Resources.Coins).Value, Is.EqualTo(500));
            Assert.That(_economy.Balance("booster.undo").Value, Is.Zero);
        }

        [Test]
        public void Apply_InsideAnOuterTransaction_IsVisibleOnlyAfterCommit_AndRollsBackWithIt()
        {
            using (_db.Begin("outer"))
            {
                _economy.Apply(EconomyTransaction.Grant(TransactionSource.LevelWin, "tm_0001", Resources.Coins, 50));
                Assert.That(_economy.Balance(Resources.Coins).Value, Is.EqualTo(500), "observable waits for the commit");
            }

            Assert.That(_economy.Balance(Resources.Coins).Value, Is.EqualTo(500));
            Assert.That(new WalletDao(_db).Find(Resources.Coins).Balance, Is.EqualTo(500));

            using (var outer = _db.Begin("outer"))
            {
                _economy.Apply(EconomyTransaction.Grant(TransactionSource.LevelWin, "tm_0001", Resources.Coins, 50));
                outer.Commit();
            }

            Assert.That(_economy.Balance(Resources.Coins).Value, Is.EqualTo(550));
        }

        [Test]
        public void Lives_RegenerateWithTime_AndConsumptionKeepsPartialProgress()
        {
            var config = new ConfigService(new IConfigSource[] { new JsonConfigSource("{\"lives\":{\"Max\":5,\"RegenMinutes\":30}}") }, new NullLog());
            var lives = new LivesService(new LivesDao(_db), _clock, config);

            for (var i = 0; i < 5; i++) Assert.That(lives.TryConsume(), Is.True);
            Assert.That(lives.TryConsume(), Is.False);

            _clock.Advance(TimeSpan.FromMinutes(45));
            lives.Refresh();
            Assert.That(lives.Count.Value, Is.EqualTo(1));
            Assert.That(lives.UntilNext, Is.EqualTo(TimeSpan.FromMinutes(15)));

            Assert.That(lives.TryConsume(), Is.True);
            Assert.That(lives.UntilNext, Is.EqualTo(TimeSpan.FromMinutes(15)), "partial regen is kept");

            _clock.Advance(TimeSpan.FromHours(10));
            lives.Refresh();
            Assert.That(lives.Count.Value, Is.EqualTo(5));
        }

        [Test]
        public void GrantOnce_AppliesTheFirstTime_AndNeverAgain()
        {
            var grant = EconomyTransaction.Grant(TransactionSource.Starter, "starter", "booster.hammer", 3);
            Assert.That(_economy.GrantOnce("starter", grant).IsOk, Is.True);
            Assert.That(_economy.GrantOnce("starter", grant).IsOk, Is.True);
            Assert.That(_economy.Balance("booster.hammer").Value, Is.EqualTo(3));

            var reopened = new EconomyService(_db, new WalletDao(_db), new TransactionLogDao(_db), new GrantDao(_db), _bus, _clock);
            reopened.Load();
            reopened.GrantOnce("starter", grant);
            Assert.That(reopened.Balance("booster.hammer").Value, Is.EqualTo(3), "the grant is remembered in the save");
        }

        [Test]
        public void Config_InvalidSectionKeepsDefaults()
        {
            var config = new ConfigService(new IConfigSource[] { new JsonConfigSource("{\"lives\":{\"Max\":0}}") }, new NullLog());
            Assert.That(config.Get<LivesConfig>().Max, Is.EqualTo(5));
            Assert.That(config.Invalid, Does.Contain("lives"));
        }
    }
}
