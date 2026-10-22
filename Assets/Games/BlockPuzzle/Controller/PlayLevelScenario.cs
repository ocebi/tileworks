using System.Threading;
using BlockPuzzle.Api;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Tiles.Levels;

namespace BlockPuzzle.Controller
{
    // The level editor's Play: the payload is a level file that is played without being shipped.
    public sealed class PlayLevelScenario : IScenarioHandler
    {
        private readonly MemoryLevelSource _overrides;
        private readonly IAppNavigator _navigator;
        private readonly IPuzzleProgress _progress;

        public PlayLevelScenario(MemoryLevelSource overrides, IAppNavigator navigator, IPuzzleProgress progress)
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
            _navigator.GoTo(AppStates.Level, new PuzzleStartArgs(_progress.CurrentIndex.Value, envelope.Id));
            return UniTask.CompletedTask;
        }
    }
}
