using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using Tileworks.Meta.Api;
using Tileworks.Meta.Config;

namespace Tileworks.Meta.Boosters
{
    [ConfigSection("boosters")]
    public sealed class BoosterConfig : IValidatable
    {
        public string[] Ids = { "hammer", "shuffle" };
        public int[] UnlockLevels = { 4, 7 };
        public int StarterStock = 3;

        public void Validate(ValidationReport report)
        {
            report.Require(Ids != null && Ids.Length > 0, "ids must not be empty");
            report.Require(UnlockLevels != null && Ids != null && UnlockLevels.Length == Ids.Length, "unlockLevels needs one level per booster");
            report.Require(StarterStock >= 0, "starterStock must not be negative");
        }
    }

    // Boosters by slot: the stock lives in the economy, the unlock level in config, the seen-its-card flag in the store.
    public sealed class BoosterService
    {
        public const string StarterGrant = "boosters.starter";

        private readonly BoosterConfig _config;
        private readonly IEconomy _economy;
        private readonly IKeyValueStore _store;

        public BoosterService(IConfig config, IEconomy economy, IKeyValueStore store)
        {
            _config = Guard.NotNull(config, nameof(config)).Get<BoosterConfig>();
            _economy = Guard.NotNull(economy, nameof(economy));
            _store = Guard.NotNull(store, nameof(store));
        }

        public int Count => _config.Ids.Length;

        public string ResourceOf(int slot) => "booster." + _config.Ids[slot];

        public IReadOnlyObservable<long> Stock(int slot) => _economy.Balance(ResourceOf(slot));

        public int UnlockLevel(int slot) => _config.UnlockLevels[slot];

        public bool IsUnlocked(int slot, int levelNumber) => levelNumber >= UnlockLevel(slot);

        public bool WasIntroduced(int slot) => _store.GetBool(IntroKey(slot));

        public void MarkIntroduced(int slot) => _store.SetBool(IntroKey(slot), true);

        public Result Spend(int slot, string levelId) =>
            _economy.Apply(EconomyTransaction.Spend(TransactionSource.BoosterUse, levelId, ResourceOf(slot), 1));

        public Result GrantStarter()
        {
            var deltas = new List<ResourceDelta>();
            for (var slot = 0; slot < Count; slot++) deltas.Add(new ResourceDelta(ResourceOf(slot), _config.StarterStock));
            return _economy.GrantOnce(StarterGrant, new EconomyTransaction(TransactionSource.Starter, StarterGrant, deltas.ToArray()));
        }

        private string IntroKey(int slot) => "boosters.introduced." + _config.Ids[slot];
    }

    // The starter stock, once per save.
    public sealed class BoosterStarterStep : BootStep
    {
        private static readonly string[] Dependencies = { "Meta" };

        private readonly BoosterService _boosters;

        public BoosterStarterStep(BoosterService boosters) => _boosters = boosters;

        public override string Name => "Boosters";
        public override IReadOnlyList<string> DependsOn => Dependencies;

        public override UniTask RunAsync(CancellationToken ct)
        {
            _boosters.GrantStarter();
            return UniTask.CompletedTask;
        }
    }
}
