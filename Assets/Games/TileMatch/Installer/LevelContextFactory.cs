using Tileworks.Kit.Injection;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using TileMatch.Controller;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.View;

namespace TileMatch.Installer
{
    // One child scope per attempt: retry or exit disposes everything the attempt created.
    public sealed class LevelContextFactory : ILevelContextFactory
    {
        private readonly Scope _gameScope;

        public LevelContextFactory(Scope gameScope) => _gameScope = gameScope;

        public ILevelContext Create(LevelRef level)
        {
            var scope = _gameScope.CreateChild("Level", builder =>
            {
                builder.RegisterInstance(new LevelRefHolder(level));
                builder.RegisterFactory(r => new LevelSession(level, r.Resolve<LevelRepository>(), r.Resolve<TileMatchRules>(),
                    r.Resolve<IEventPlayer>(), r.Resolve<LevelResultCommitter>(), r.Resolve<Tileworks.Kit.Core.IClock>(),
                    r.Resolve<Tileworks.Meta.Api.IEconomy>(), r.Resolve<Tileworks.Meta.Api.IConfig>(), r.Resolve<Tileworks.Kit.Core.ILog>()), Lifetime.Single);
                builder.RegisterFactory<IEventPlayer>(r => new EventPlayer(r.Resolve<TileMatchRules>().Registry, r.Resolve<PresentationClock>(),
                    r.Resolve<System.Collections.Generic.IReadOnlyList<IEventPresenter>>()), Lifetime.Single);
                builder.Register<PresenterKit>(Lifetime.Single);
                RegisterPresenters(builder);
            });
            return new LevelContext(scope);
        }

        private static void RegisterPresenters(ContainerBuilder b)
        {
            b.Register<TileLeftBoardPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<TileMovedToTrayPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<TraySlotChangedPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<TripleClearedPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<TrayCapacityChangedPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<TileUncoveredPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<TileCoveredPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<TileRevealedPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<TileKindChangedPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<ModifierChangedPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<TileSpawnedPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<TileReturnedToBoardPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<LevelWonPresenter>(Lifetime.Single).As<IEventPresenter>();
            b.Register<LevelLostPresenter>(Lifetime.Single).As<IEventPresenter>();
        }

        private sealed class LevelRefHolder
        {
            public LevelRefHolder(LevelRef level) => Level = level;
            public LevelRef Level { get; }
        }

        private sealed class LevelContext : ILevelContext
        {
            private readonly Scope _scope;

            public LevelContext(Scope scope)
            {
                _scope = scope;
                Session = scope.Resolve<LevelSession>();
                Player = scope.Resolve<IEventPlayer>();
            }

            public LevelSession Session { get; }
            public IEventPlayer Player { get; }

            public void Dispose() => _scope.Dispose();
        }
    }
}
