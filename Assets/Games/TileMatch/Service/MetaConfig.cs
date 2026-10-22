using System;
using Tileworks.Kit.Core;
using Tileworks.Meta.Config;
using TileMatch.Model;

namespace TileMatch.Service
{
    public static class BoosterResources
    {
        public static string Of(BoosterKind kind) => kind switch
        {
            BoosterKind.Undo => "booster.undo",
            BoosterKind.Magnet => "booster.magnet",
            BoosterKind.Shuffle => "booster.shuffle",
            _ => "booster.extraSlot",
        };
    }

    [ConfigSection("continue")]
    public sealed class ContinueConfig : IValidatable
    {
        public int[] CoinPrices = { 100, 200, 400 };
        public int ExtraSlots = 1;
        public int ReturnCount = 3;
        public int MaxContinues = 2;

        public int PriceFor(int used) => CoinPrices[Math.Min(used, CoinPrices.Length - 1)];

        public void Validate(ValidationReport report)
        {
            report.Require(CoinPrices != null && CoinPrices.Length > 0, "coinPrices must not be empty");
            report.Require(ExtraSlots >= 0 && ReturnCount >= 0, "extraSlots and returnCount must not be negative");
        }
    }

    [ConfigSection("starter")]
    public sealed class StarterConfig : IValidatable
    {
        public int BoostersEach = 3;

        public void Validate(ValidationReport report) => report.Require(BoostersEach >= 0, "boostersEach must not be negative");
    }

    [ConfigSection("boosters")]
    public sealed class BoosterConfig : IValidatable
    {
        // The level each booster opens on, in BoosterKind order: undo, magnet, shuffle, extra slot.
        public int[] UnlockLevels = { 3, 5, 7, 9 };

        public void Validate(ValidationReport report) => report.Require(UnlockLevels != null && UnlockLevels.Length == 4, "unlockLevels needs one level per booster");
    }

    [Serializable]
    public sealed class ShopItem
    {
        public string Id;
        public string Title;
        public int Cost;
        public string Resource;
        public int Amount;
    }

    [ConfigSection("shop")]
    public sealed class ShopConfig : IValidatable
    {
        public const string LivesRefill = "lives.refill";

        public ShopItem[] Items =
        {
            new ShopItem { Id = "undo3", Title = "3 Undo", Cost = 150, Resource = "booster.undo", Amount = 3 },
            new ShopItem { Id = "magnet3", Title = "3 Magnet", Cost = 250, Resource = "booster.magnet", Amount = 3 },
            new ShopItem { Id = "shuffle3", Title = "3 Shuffle", Cost = 150, Resource = "booster.shuffle", Amount = 3 },
            new ShopItem { Id = "slot3", Title = "3 Extra Slot", Cost = 200, Resource = "booster.extraSlot", Amount = 3 },
            new ShopItem { Id = "lives", Title = "Refill Lives", Cost = 300, Resource = LivesRefill, Amount = 1 },
        };

        public void Validate(ValidationReport report)
        {
            report.Require(Items != null && Items.Length > 0, "shop needs items");
            if (Items == null) return;
            foreach (var item in Items) report.Require(item.Cost > 0 && item.Amount > 0 && !string.IsNullOrEmpty(item.Resource), $"item {item.Id} is incomplete");
        }
    }
}
