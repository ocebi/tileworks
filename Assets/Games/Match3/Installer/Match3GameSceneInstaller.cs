using Match3.Controller;
using Match3.View;
using Tileworks.Kit.App;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Match3.Installer
{
    public sealed class Match3GameSceneInstaller : SceneInstaller
    {
        [SerializeField, ReadOnlyInInspector] private Match3BoardView _board;
        [SerializeField, ReadOnlyInInspector] private Match3HudView _hud;
        [SerializeField, ReadOnlyInInspector] private Match3ResultView _result;
        [SerializeField, ReadOnlyInInspector] private Match3BoosterBoard _boosterBoard;
        [SerializeField, ReadOnlyInInspector] private BoosterBarView _boosters;

        public override void Install(ContainerBuilder builder)
        {
            builder.RegisterInstance(_board).AsSelf().As<IMatch3BoardView>();
            builder.RegisterInstance<IMatch3HudView>(_hud);
            builder.RegisterInstance<IMatch3ResultView>(_result);
            builder.RegisterInstance(_boosterBoard).AsSelf().As<IHammerBoard>();
            builder.RegisterInstance<IBoosterBarView>(_boosters);
            builder.RegisterInstance<ISettingsPanel>(_result);
            builder.Register<PresentationClock>(Lifetime.Single);
            builder.Register<Match3GamePresenter>(Lifetime.Single).As<ISceneEntry>();
            builder.Register<PreferencesController>(Lifetime.Single);
            builder.RegisterFactory<IMatch3ContextFactory>(r => new Match3ContextFactory((Scope)r), Lifetime.Single);
        }
    }
}
