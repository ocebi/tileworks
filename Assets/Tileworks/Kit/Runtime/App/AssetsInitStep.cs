using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Content;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.App
{
    // Brings the content system up before any scene or asset is asked for (ARCHITECTURE §7.8 row 6).
    public sealed class AssetsInitStep : BootStep
    {
        public const string StepName = "Assets";

        private readonly IContent _content;

        public AssetsInitStep(IContent content) => _content = Guard.NotNull(content, nameof(content));

        public override string Name => StepName;

        // The web fetches the content catalog here.
        public override int TimeoutMs => 30000;

        public override UniTask RunAsync(CancellationToken ct) => _content.InitializeAsync(ct);
    }
}
