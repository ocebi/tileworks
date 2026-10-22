using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using Tileworks.Meta.Api;
using Tileworks.Meta.Config;
using TileMatch.Model;

namespace TileMatch.Service
{
    public sealed class ShopService
    {
        private readonly IEconomy _economy;
        private readonly ILives _lives;
        private readonly IDatabase _db;

        public ShopService(IEconomy economy, ILives lives, IDatabase db, IConfig config)
        {
            _economy = economy;
            _lives = lives;
            _db = db;
            Items = config.Get<ShopConfig>().Items;
        }

        public IReadOnlyList<ShopItem> Items { get; }

        public bool CanBuy(ShopItem item) =>
            _economy.CanAfford(EconomyTransaction.Spend(TransactionSource.ShopPurchase, item.Id, Resources.Coins, item.Cost)) &&
            (item.Resource != ShopConfig.LivesRefill || _lives.Count.Value < _lives.Max);

        public Result Buy(ShopItem item)
        {
            if (!CanBuy(item)) return new Error(ErrorKind.InsufficientFunds, "shop", item.Id);
            using var tx = _db.Begin("Shop:" + item.Id);
            var deltas = item.Resource == ShopConfig.LivesRefill
                ? new[] { new ResourceDelta(Resources.Coins, -item.Cost) }
                : new[] { new ResourceDelta(Resources.Coins, -item.Cost), new ResourceDelta(item.Resource, item.Amount) };
            var result = _economy.Apply(new EconomyTransaction(TransactionSource.ShopPurchase, item.Id, deltas));
            if (!result.IsOk) return result;
            if (item.Resource == ShopConfig.LivesRefill) _lives.Refill();
            tx.Commit();
            return Result.Ok();
        }
    }

    // Grants the starter boosters once per profile, in the same transaction that records the grant.
    public sealed class StarterBundleStep : BootStep
    {
        private static readonly string[] Dependencies = { "Meta", "Progress" };

        private readonly IDatabase _db;
        private readonly ProgressDao _progress;
        private readonly IEconomy _economy;
        private readonly StarterConfig _config;

        public StarterBundleStep(IDatabase db, ProgressDao progress, IEconomy economy, IConfig config)
        {
            _db = db;
            _progress = progress;
            _economy = economy;
            _config = config.Get<StarterConfig>();
        }

        public override string Name => "Starter";
        public override IReadOnlyList<string> DependsOn => Dependencies;

        public override UniTask RunAsync(CancellationToken ct)
        {
            var row = _progress.Find(1) ?? new ProgressEntity { Id = 1, UpdatedAt = string.Empty };
            if (row.StarterGranted) return UniTask.CompletedTask;

            using var tx = _db.Begin("StarterBundle");
            var deltas = new List<ResourceDelta>();
            for (var kind = BoosterKind.Undo; kind <= BoosterKind.ExtraSlot; kind++)
                deltas.Add(new ResourceDelta(BoosterResources.Of(kind), _config.BoostersEach));
            _economy.Apply(new EconomyTransaction(TransactionSource.Starter, "starter", deltas.ToArray()));
            _progress.Upsert(new ProgressEntity { Id = 1, LevelIndex = row.LevelIndex, UpdatedAt = row.UpdatedAt, StarterGranted = true });
            tx.Commit();
            return UniTask.CompletedTask;
        }
    }
}
