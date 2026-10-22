using Tileworks.Kit.App;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using TileMatch.Controller;
using TileMatch.View;
using UnityEngine;

namespace TileMatch.Installer
{
    public sealed class HomeSceneInstaller : SceneInstaller
    {
        [SerializeField, ReadOnlyInInspector] private HomeView _home;
        [SerializeField, ReadOnlyInInspector] private ShopView _shop;
        [SerializeField, ReadOnlyInInspector] private StatsView _stats;
        [SerializeField, ReadOnlyInInspector] private LevelListView _levels;
        [SerializeField, ReadOnlyInInspector] private LevelPathView _levelPath;
        [SerializeField, ReadOnlyInInspector] private SettingsView _settings;

        public override void Install(ContainerBuilder builder) => Install(builder, _home, _shop, _stats, _levels, _levelPath, _settings);

        public static void Install(ContainerBuilder builder, IHomeView home, IShopView shop, IStatsView stats, ILevelListView levels, ILevelPathView path = null,
            ISettingsPanel settings = null)
        {
            builder.RegisterInstance(home);
            builder.RegisterInstance<IGameSwitchView>(home);
            builder.RegisterInstance(shop);
            builder.RegisterInstance(stats);
            builder.RegisterInstance(levels);
            builder.Register<HomePresenter>(Lifetime.Single).As<ISceneEntry>().As<ILevelLauncher>();
            builder.Register<LevelListPresenter>(Lifetime.Single).As<ISceneEntry>().As<ILevelListOpener>();
            builder.Register<GameSwitchPresenter>(Lifetime.Single);
            if (path != null) RegisterPath(builder, path);
            if (settings != null) RegisterSettings(builder, settings);
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
