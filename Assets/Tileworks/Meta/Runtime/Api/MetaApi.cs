using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;

namespace Tileworks.Meta.Api
{
    public static class Resources
    {
        public const string Coins = "coins";
    }

    public enum TransactionSource : byte
    {
        LevelWin,
        LevelReward,
        BoosterUse,
        Continue,
        ShopPurchase,
        Starter,
        Debug,
    }

    public readonly struct ResourceDelta
    {
        public readonly string Resource;
        public readonly long Amount;

        public ResourceDelta(string resource, long amount)
        {
            Resource = resource;
            Amount = amount;
        }

        public override string ToString() => $"{Resource}{(Amount >= 0 ? "+" : "")}{Amount}";
    }

    // Applied atomically: all deltas or none, never leaving a negative balance.
    public sealed class EconomyTransaction
    {
        public EconomyTransaction(TransactionSource source, string context, params ResourceDelta[] deltas)
        {
            Source = source;
            Context = context ?? string.Empty;
            Deltas = deltas ?? Array.Empty<ResourceDelta>();
        }

        public TransactionSource Source { get; }
        public string Context { get; }
        public IReadOnlyList<ResourceDelta> Deltas { get; }

        public static EconomyTransaction Spend(TransactionSource source, string context, string resource, long amount) =>
            new EconomyTransaction(source, context, new ResourceDelta(resource, -amount));

        public static EconomyTransaction Grant(TransactionSource source, string context, string resource, long amount) =>
            new EconomyTransaction(source, context, new ResourceDelta(resource, amount));
    }

    // Joins the caller's database transaction when one is open, so a level result commits atomically.
    public interface IEconomy
    {
        IReadOnlyObservable<long> Balance(string resource);
        bool CanAfford(EconomyTransaction transaction);
        Result Apply(EconomyTransaction transaction);

        // Applies the transaction the first time this id is granted in the save, and does nothing after that.
        Result GrantOnce(string grantId, EconomyTransaction transaction);
    }

    public interface ILives
    {
        IReadOnlyObservable<int> Count { get; }
        int Max { get; }
        bool IsInfinite { get; }
        TimeSpan UntilNext { get; }
        bool HasLife { get; }
        bool TryConsume();
        void Refill();
        void Refresh();
    }

    public interface IConfig
    {
        T Get<T>() where T : class, IValidatable, new();
    }

    public readonly struct ResourceChanged
    {
        public readonly string Resource;
        public readonly long Balance;
        public readonly long Delta;

        public ResourceChanged(string resource, long balance, long delta)
        {
            Resource = resource;
            Balance = balance;
            Delta = delta;
        }
    }
}
