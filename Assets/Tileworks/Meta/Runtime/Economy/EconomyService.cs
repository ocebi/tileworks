using System.Collections.Generic;
using System.Text;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using Tileworks.Meta.Api;

namespace Tileworks.Meta.Economy
{
    public sealed class EconomyService : IEconomy
    {
        private readonly IDatabase _db;
        private readonly WalletDao _wallet;
        private readonly TransactionLogDao _log;
        private readonly GrantDao _grants;
        private readonly IMessageBus _bus;
        private readonly IClock _clock;
        private readonly Dictionary<string, Observable<long>> _balances = new Dictionary<string, Observable<long>>();

        public EconomyService(IDatabase db, WalletDao wallet, TransactionLogDao log, GrantDao grants, IMessageBus bus, IClock clock)
        {
            _grants = Guard.NotNull(grants, nameof(grants));
            _db = Guard.NotNull(db, nameof(db));
            _wallet = Guard.NotNull(wallet, nameof(wallet));
            _log = Guard.NotNull(log, nameof(log));
            _bus = Guard.NotNull(bus, nameof(bus));
            _clock = Guard.NotNull(clock, nameof(clock));
        }

        public void Load()
        {
            foreach (var row in _wallet.All()) Observe(row.ResourceId).SetSilently(row.Balance);
        }

        public IReadOnlyObservable<long> Balance(string resource) => Observe(resource);

        public bool CanAfford(EconomyTransaction transaction)
        {
            foreach (var (resource, delta) in Totals(transaction))
            {
                if (delta < 0 && Stored(resource) + delta < 0) return false;
            }

            return true;
        }

        public Result Apply(EconomyTransaction transaction)
        {
            if (!CanAfford(transaction)) return new Error(ErrorKind.InsufficientFunds, "economy", Describe(transaction));

            using var tx = _db.Begin("Economy:" + transaction.Source);
            var applied = new List<(string Resource, long Balance, long Delta)>();
            foreach (var (resource, delta) in Totals(transaction))
            {
                var balance = Stored(resource) + delta;
                _wallet.Upsert(new WalletEntity { ResourceId = resource, Balance = balance });
                applied.Add((resource, balance, delta));
            }

            _log.Insert(new TransactionEntity
            {
                Source = transaction.Source.ToString(), Context = transaction.Context, Deltas = Describe(transaction), CreatedAt = _clock.UtcNow.ToString("O"),
            });
            _log.Trim();
            tx.OnCommitted(() =>
            {
                foreach (var (resource, balance, delta) in applied)
                {
                    Observe(resource).Value = balance;
                    _bus.Publish(new ResourceChanged(resource, balance, delta));
                }
            });
            tx.Commit();
            return Result.Ok();
        }

        public Result GrantOnce(string grantId, EconomyTransaction transaction)
        {
            using var tx = _db.Begin("Grant:" + grantId);
            if (_grants.Find(grantId) != null) return Result.Ok();
            _grants.Insert(new GrantEntity { Id = grantId, CreatedAt = _clock.UtcNow.ToString("O") });
            var applied = Apply(transaction);
            if (!applied.IsOk) return applied;
            tx.Commit();
            return Result.Ok();
        }

        private long Stored(string resource) => _wallet.Find(resource)?.Balance ?? 0;

        private Observable<long> Observe(string resource)
        {
            if (!_balances.TryGetValue(resource, out var observable)) _balances[resource] = observable = new Observable<long>();
            return observable;
        }

        private static IEnumerable<(string, long)> Totals(EconomyTransaction transaction)
        {
            var totals = new Dictionary<string, long>();
            var order = new List<string>();
            foreach (var delta in transaction.Deltas)
            {
                if (!totals.ContainsKey(delta.Resource)) order.Add(delta.Resource);
                totals.TryGetValue(delta.Resource, out var sum);
                totals[delta.Resource] = sum + delta.Amount;
            }

            foreach (var resource in order) yield return (resource, totals[resource]);
        }

        private static string Describe(EconomyTransaction transaction)
        {
            var text = new StringBuilder();
            foreach (var delta in transaction.Deltas) text.Append(text.Length == 0 ? "" : ",").Append(delta);
            return text.ToString();
        }
    }
}
