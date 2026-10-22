using BlockPuzzle.Controller;
using BlockPuzzle.View;
using Tileworks.Kit.App;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using UnityEngine;

namespace BlockPuzzle.Installer
{
    public sealed class PuzzleGameSceneInstaller : SceneInstaller
    {
        [SerializeField, ReadOnlyInInspector] private PuzzleBoardView _board;
        [SerializeField, ReadOnlyInInspector] private PuzzleHudView _hud;
        [SerializeField, ReadOnlyInInspector] private PuzzleResultView _result;

        public override void Install(ContainerBuilder builder)
        {
            builder.RegisterInstance(_board).AsSelf().As<IPuzzleBoardView>();
            builder.RegisterInstance<IPuzzleHudView>(_hud);
            builder.RegisterInstance<IPuzzleResultView>(_result);
            builder.RegisterInstance<ISettingsPanel>(_result);
            builder.Register<PresentationClock>(Lifetime.Single);
            builder.Register<PuzzleGamePresenter>(Lifetime.Single).As<ISceneEntry>();
            builder.Register<PreferencesController>(Lifetime.Single);
            builder.RegisterFactory<IPuzzleContextFactory>(r => new PuzzleContextFactory((Scope)r), Lifetime.Single);
        }
    }
}
