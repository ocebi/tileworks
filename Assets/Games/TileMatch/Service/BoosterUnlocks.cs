using Tileworks.Kit.Save;
using Tileworks.Meta.Api;
using TileMatch.Model;

namespace TileMatch.Service
{
    // The level each booster unlocks on, and whether the player has seen its introduction card.
    public sealed class BoosterUnlocks
    {
        private readonly BoosterConfig _config;
        private readonly IKeyValueStore _store;

        public BoosterUnlocks(IConfig config, IKeyValueStore store)
        {
            _config = config.Get<BoosterConfig>();
            _store = store;
        }

        public int UnlockLevel(BoosterKind kind) => _config.UnlockLevels[(int)kind - 1];

        public bool IsUnlocked(BoosterKind kind, int levelNumber) => levelNumber >= UnlockLevel(kind);

        public bool WasIntroduced(BoosterKind kind) => _store.GetBool(Key(kind));

        public void MarkIntroduced(BoosterKind kind) => _store.SetBool(Key(kind), true);

        private static string Key(BoosterKind kind) => "tilematch.introduced." + BoosterResources.Of(kind);
    }
}
