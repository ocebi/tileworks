using System.Collections.Generic;
using BlockPuzzle.Controller;
using BlockPuzzle.Model;
using BlockPuzzle.Service;
using BlockPuzzle.View;
using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Meta.Api;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;

namespace BlockPuzzle.Installer
{
    public sealed class PuzzleContextFactory : IPuzzleContextFactory
    {
        private readonly Scope _gameScope;

        public PuzzleContextFactory(Scope gameScope) => _gameScope = gameScope;

        public IPuzzleContext Create(LevelRef level)
        {
            var scope = _gameScope.CreateChild("Puzzle", builder =>
            {
                builder.RegisterFactory(r => new PuzzleSession(level, r.Resolve<LevelRepository>(), r.Resolve<BlockRules>(), r.Resolve<IEventPlayer>(),
                    r.Resolve<PuzzleResultCommitter>(), r.Resolve<IClock>(), r.Resolve<IEconomy>(), r.Resolve<IConfig>(), r.Resolve<ILog>()), Lifetime.Single);
                builder.RegisterFactory<IEventPlayer>(r => new EventPlayer(r.Resolve<BlockRules>().Registry, r.Resolve<PresentationClock>(),
                    r.Resolve<IReadOnlyList<IEventPresenter>>()), Lifetime.Single);
                builder.Register<PuzzlePresenterKit>(Lifetime.Single);
                builder.Register<PiecePlacedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<CellFilledPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<CellClearedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<LinesClearedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<ScoreChangedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<OfferDealtPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<LevelWonPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<LevelLostPresenter>(Lifetime.Single).As<IEventPresenter>();
            });
            return new PuzzleContext(scope);
        }

        private sealed class PuzzleContext : IPuzzleContext
        {
            private readonly Scope _scope;

            public PuzzleContext(Scope scope)
            {
                _scope = scope;
                Session = scope.Resolve<PuzzleSession>();
            }

            public PuzzleSession Session { get; }

            public void Dispose() => _scope.Dispose();
        }
    }
}
