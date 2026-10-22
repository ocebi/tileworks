using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Tiles.Levels;
using TileMatch.Api;

namespace TileMatch.Controller
{
    // Editor "Play" from the level editor: the payload is a level file that is played without being shipped.
    public sealed class PlayLevelScenario : IScenarioHandler
    {
        private readonly MemoryLevelSource _overrides;
        private readonly IAppNavigator _navigator;
        private readonly ILevelProgress _progress;

        public PlayLevelScenario(MemoryLevelSource overrides, IAppNavigator navigator, ILevelProgress progress)
        {
            _overrides = overrides;
            _navigator = navigator;
            _progress = progress;
        }

        public string Kind => "play-level";

        public UniTask RunAsync(string payload, CancellationToken ct)
        {
            var envelope = LevelEnvelope.Parse(payload);
            _overrides.Set(envelope.Id, payload);
            _navigator.GoTo(AppStates.Level, new LevelStartArgs(_progress.CurrentIndex.Value, envelope.Id));
            return UniTask.CompletedTask;
        }
    }
}
