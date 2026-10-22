using Tileworks.Kit.App;
using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Save;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;

namespace Tileworks.Tiles.Installer
{
    // A game's levels: the built-in pack, overlaid by published updates when remote levels are on (docs/REMOTE-LEVELS.md).
    // Registers ILevelSequence, the LevelRepository and the MemoryLevelSource that scenarios fill, and the update boot step.
    public static class LevelsInstaller
    {
        public static void Install(ContainerBuilder builder, LevelPackAsset pack, RemoteLevelsConfig remoteLevels, ILevelChecker checker)
        {
            var overrides = new MemoryLevelSource();
            builder.RegisterInstance(overrides);
            var remote = remoteLevels != null && remoteLevels.Enabled ? remoteLevels : null;
            builder.RegisterFactory(r => new LiveLevels(pack, pack.LoadSequence(), pack.PublishedRevision, remote != null ? Cache(r, checker.Game) : null,
                    AppStates.Home, r.Resolve<ILog>()),
                Lifetime.Single).AsSelf().As<ILevelSequence>().As<IAppStateListener>();
            builder.RegisterFactory(r =>
            {
                var levels = r.Resolve<LiveLevels>();
                var repository = new LevelRepository(new CompositeLevelSource(overrides, levels));
                levels.Changed += repository.Clear;
                return repository;
            }, Lifetime.Single);
            if (remote == null) return;

            builder.RegisterFactory(r => new LevelUpdateStep(
                new LevelUpdater(remote.OptionsFor(checker.Game), r.Resolve<IHttpClient>(), r.Resolve<LiveLevels>(), Cache(r, checker.Game), checker, r.Resolve<ILog>()),
                r.Resolve<IClock>(), r.Resolve<ILog>(), remote.BootWaitMs, remote.RefreshAfter), Lifetime.Single).As<IBootStep>();
        }

        private static LevelCache Cache(IResolver resolver, string game) =>
            new LevelCache(LevelCache.FolderFor(resolver.Resolve<ProfileStore>().ProfileDirectory, game));
    }
}
