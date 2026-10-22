using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;

namespace Tileworks.Meta.Economy
{
    public sealed class MetaLoadStep : BootStep
    {
        public const string StepName = "Meta";
        private static readonly string[] Dependencies = { SaveSteps.Warm };

        private readonly EconomyService _economy;
        private readonly LivesService _lives;

        public MetaLoadStep(EconomyService economy, LivesService lives)
        {
            _economy = economy;
            _lives = lives;
        }

        public override string Name => StepName;
        public override IReadOnlyList<string> DependsOn => Dependencies;

        public override UniTask RunAsync(CancellationToken ct)
        {
            _economy.Load();
            _lives.Refresh();
            return UniTask.CompletedTask;
        }
    }
}
