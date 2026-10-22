using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Controller;
using Match3.Model;
using Match3.Service;
using Match3.View;
using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Meta.Api;
using Tileworks.Meta.Boosters;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;

namespace Match3.Installer
{
    // The event player runs in wave mode: each resolution pass plays after the one before it, so cascades read in order.
    public sealed class Match3ContextFactory : IMatch3ContextFactory
    {
        private readonly Scope _gameScope;

        public Match3ContextFactory(Scope gameScope) => _gameScope = gameScope;

        public IMatch3Context Create(LevelRef level)
        {
            var scope = _gameScope.CreateChild("Match3", builder =>
            {
                builder.RegisterFactory(r => new Match3Session(level, r.Resolve<LevelRepository>(), r.Resolve<Match3Rules>(), r.Resolve<IEventPlayer>(),
                    r.Resolve<Match3ResultCommitter>(), r.Resolve<IClock>(), r.Resolve<IEconomy>(), r.Resolve<IConfig>(), r.Resolve<BoosterService>(), r.Resolve<ILog>()), Lifetime.Single);
                builder.RegisterFactory<IEventPlayer>(r => new MovePlayer(new EventPlayer(r.Resolve<Match3Rules>().Registry, r.Resolve<PresentationClock>(),
                    r.Resolve<IReadOnlyList<IEventPresenter>>(), waveMode: true), r.Resolve<Match3PresenterKit>().Mover), Lifetime.Single);
                builder.Register<Match3PresenterKit>(Lifetime.Single);
                builder.Register<PiecesSwappedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<PieceClearedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<BoxDamagedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<FloorDamagedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<ChainBrokenPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<SpecialCreatedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<SpecialMergedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<SpecialsCombinedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<SpecialFiredPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<GemConvertedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<FlyerLaunchedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<FlyerLandedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<PieceFellPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<PieceSpawnedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<BoardShuffledPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<PieceShuffledPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<CascadePresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<MovesChangedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<GoalChangedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<LevelWonPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<LevelLostPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<HammerStruckPresenter>(Lifetime.Single).As<IEventPresenter>();
            });
            return new Match3Context(scope);
        }

        // Tells the falls where each move starts, so a later move's new pieces enter behind an earlier move's.
        private sealed class MovePlayer : IEventPlayer
        {
            private readonly IEventPlayer _player;
            private readonly FallMover _mover;

            public MovePlayer(IEventPlayer player, FallMover mover)
            {
                _player = player;
                _mover = mover;
            }

            public float IdleAt => _player.IdleAt;
            public bool IsIdle => _player.IsIdle;
            public bool IsBusy(int entity) => _player.IsBusy(entity);
            public UniTask WaitIdleAsync(CancellationToken ct) => _player.WaitIdleAsync(ct);
            public void Reset() => _player.Reset();

            public void Play(ReadOnlySpan<BoardEvent> events)
            {
                _mover.NextMove();
                _player.Play(events);
            }
        }

        private sealed class Match3Context : IMatch3Context
        {
            private readonly Scope _scope;
            private readonly Match3PresenterKit _kit;

            public Match3Context(Scope scope)
            {
                _scope = scope;
                Session = scope.Resolve<Match3Session>();
                _kit = scope.Resolve<Match3PresenterKit>();
            }

            public Match3Session Session { get; }

            public void Tick()
            {
                Session.Tick();
                _kit.Tick();
            }

            public void Dispose() => _scope.Dispose();
        }
    }
}
