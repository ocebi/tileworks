using Match3.Controller;
using Match3.View;
using Tileworks.Kit.App;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Match3.Installer
{
    public sealed class Match3HomeSceneInstaller : SceneInstaller
    {
        [SerializeField, ReadOnlyInInspector] private Match3HomeView _home;
        [SerializeField, ReadOnlyInInspector] private LevelListView _levels;
        [SerializeField, ReadOnlyInInspector] private LevelPathView _levelPath;
        [SerializeField, ReadOnlyInInspector] private SettingsView _settings;

        public override void Install(ContainerBuilder builder)
        {
            builder.RegisterInstance<IMatch3HomeView>(_home);
            builder.RegisterInstance<IGameSwitchView>(_home);
            builder.RegisterInstance<ILevelListView>(_levels);
            builder.Register<Match3HomePresenter>(Lifetime.Single).As<ISceneEntry>().As<ILevelLauncher>();
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
