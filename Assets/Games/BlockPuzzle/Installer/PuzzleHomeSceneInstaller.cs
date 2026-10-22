using BlockPuzzle.Controller;
using BlockPuzzle.View;
using Tileworks.Kit.App;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using UnityEngine;

namespace BlockPuzzle.Installer
{
    public sealed class PuzzleHomeSceneInstaller : SceneInstaller
    {
        [SerializeField, ReadOnlyInInspector] private PuzzleHomeView _home;
        [SerializeField, ReadOnlyInInspector] private LevelListView _levels;
        [SerializeField, ReadOnlyInInspector] private LevelPathView _levelPath;
        [SerializeField, ReadOnlyInInspector] private SettingsView _settings;

        public override void Install(ContainerBuilder builder)
        {
            builder.RegisterInstance<IPuzzleHomeView>(_home);
            builder.RegisterInstance<IGameSwitchView>(_home);
            builder.RegisterInstance<ILevelListView>(_levels);
            builder.Register<PuzzleHomePresenter>(Lifetime.Single).As<ISceneEntry>().As<ILevelLauncher>();
            builder.Register<LevelListPresenter>(Lifetime.Single).As<ISceneEntry>().As<ILevelListOpener>();
            builder.Register<GameSwitchPresenter>(Lifetime.Single);
            if (_levelPath != null) RegisterPath(builder, _levelPath);
            if (_settings != null) RegisterSettings(builder, _settings);
        }

        // Homes built before the path keep working without it.
        private static void RegisterPath(ContainerBuilder builder, ILevelPathView path)
        {
            builder.RegisterInstance(path);
            builder.Register<LevelPathPresenter>(Lifetime.Single).As<ISceneEntry>();
        }

        private static void RegisterSettings(ContainerBuilder builder, ISettingsPanel settings)
        {
            builder.RegisterInstance(settings);
            builder.Register<PresentationClock>(Lifetime.Single);
            builder.Register<PreferencesController>(Lifetime.Single);
        }
    }
}
