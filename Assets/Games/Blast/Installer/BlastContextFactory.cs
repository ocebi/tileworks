using System;
using System.Collections.Generic;
using System.Threading;
using Blast.Controller;
using Blast.Model;
using Blast.Service;
using Blast.View;
using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Meta.Api;
using Tileworks.Meta.Boosters;
using Cysharp.Threading.Tasks;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;

namespace Blast.Installer
{
    // The event player runs in wave mode: each resolution pass plays after the one before it, so cascades read in order.
    public sealed class BlastContextFactory : IBlastContextFactory
    {
        private readonly Scope _gameScope;

        public BlastContextFactory(Scope gameScope) => _gameScope = gameScope;

        public IBlastContext Create(LevelRef level)
        {
            var scope = _gameScope.CreateChild("Blast", builder =>
            {
                builder.RegisterFactory(r => new BlastSession(level, r.Resolve<LevelRepository>(), r.Resolve<BlastRules>(), r.Resolve<IEventPlayer>(),
                    r.Resolve<BlastResultCommitter>(), r.Resolve<IClock>(), r.Resolve<IEconomy>(), r.Resolve<IConfig>(), r.Resolve<BoosterService>(), r.Resolve<ILog>()), Lifetime.Single);
                builder.RegisterFactory<IEventPlayer>(r => new MovePlayer(new EventPlayer(r.Resolve<BlastRules>().Registry, r.Resolve<PresentationClock>(),
                    r.Resolve<IReadOnlyList<IEventPresenter>>(), waveMode: true), r.Resolve<BlastBoardView>()), Lifetime.Single);
                builder.Register<BlastPresenterKit>(Lifetime.Single);
                builder.Register<PieceClearedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<BoxDamagedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<SpecialCreatedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<SpecialMergedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<SpecialsCombinedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<SpecialFiredPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<CubeConvertedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<PieceFellPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<PieceSpawnedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<CubeRecolouredPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<MovesChangedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<GoalChangedPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<LevelWonPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<LevelLostPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<HammerStruckPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<BoardShuffledPresenter>(Lifetime.Single).As<IEventPresenter>();
                builder.Register<PieceShuffledPresenter>(Lifetime.Single).As<IEventPresenter>();
            });
            return new BlastContext(scope);
        }

        // Starts a new move on the board's falls before its events play, so its new pieces enter behind earlier moves'.
        private sealed class MovePlayer : IEventPlayer
        {
            private readonly IEventPlayer _player;
            private readonly BlastBoardView _board;

            public MovePlayer(IEventPlayer player, BlastBoardView board)
            {
                _player = player;
                _board = board;
            }

            public float IdleAt => _player.IdleAt;
            public bool IsIdle => _player.IsIdle;
            public bool IsBusy(int entity) => _player.IsBusy(entity);
            public UniTask WaitIdleAsync(CancellationToken ct) => _player.WaitIdleAsync(ct);
            public void Reset() => _player.Reset();

            public void Play(ReadOnlySpan<BoardEvent> events)
            {
                _board.Falls?.NextMove();
                _player.Play(events);
            }
        }

        private sealed class BlastContext : IBlastContext
        {
            private readonly Scope _scope;

            public BlastContext(Scope scope)
            {
                _scope = scope;
                Session = scope.Resolve<BlastSession>();
            }

            public BlastSession Session { get; }

            public void Dispose() => _scope.Dispose();
        }
    }
}
