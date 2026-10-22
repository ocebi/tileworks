using Blast.Controller;
using Blast.View;
using Tileworks.Kit.App;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Blast.Installer
{
    public sealed class BlastGameSceneInstaller : SceneInstaller
    {
        [SerializeField, ReadOnlyInInspector] private BlastBoardView _board;
        [SerializeField, ReadOnlyInInspector] private BlastHudView _hud;
        [SerializeField, ReadOnlyInInspector] private BlastResultView _result;
        [SerializeField, ReadOnlyInInspector] private BoosterBarView _boosters;

        public override void Install(ContainerBuilder builder)
        {
            builder.RegisterInstance(_board).AsSelf().As<IBlastBoardView>();
            builder.RegisterInstance<IBlastHudView>(_hud);
            builder.RegisterInstance<IBlastResultView>(_result);
            builder.RegisterInstance<IBoosterBarView>(_boosters);
            builder.RegisterInstance<ISettingsPanel>(_result);
            builder.Register<PresentationClock>(Lifetime.Single);
            builder.Register<BlastGamePresenter>(Lifetime.Single).As<ISceneEntry>();
            builder.Register<PreferencesController>(Lifetime.Single);
            builder.RegisterFactory<IBlastContextFactory>(r => new BlastContextFactory((Scope)r), Lifetime.Single);
        }
    }
}
