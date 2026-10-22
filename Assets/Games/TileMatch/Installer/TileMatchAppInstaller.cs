using Tileworks.Kit.App;
using Tileworks.Kit.Content.Addressables;
using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Save;
using Tileworks.Meta.Api;
using Tileworks.Meta.Installer;
using Tileworks.Tiles.Installer;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;
using TileMatch.Api;
using TileMatch.Controller;
using TileMatch.Model;
using TileMatch.Service;
using UnityEngine;

namespace TileMatch.Installer
{
    // Composition root of the App scope for Tile Match.
    [CreateAssetMenu(menuName = "Tileworks/Tile Match/App Installer")]
    public sealed class TileMatchAppInstaller : AppInstaller
    {
        public const string HomeScene = "Home";
        public const string GameScene = "Game";

        [SerializeField] private LevelPackAsset _levels;
        [SerializeField] private TextAsset _config;
        [SerializeField] private RemoteLevelsConfig _remoteLevels;

        public override void ConfigureKit(KitOptions options) => options.Content = new AddressablesContent();

        public override void Install(ContainerBuilder builder)
        {
            builder.RegisterInstance(TileMatchRules.CreateDefault());
            LevelsInstaller.Install(builder, _levels, _remoteLevels, new SerializerLevelChecker<TileMatchLevel>(new TileMatchLevelSerializer()));
            builder.Register<PlayLevelScenario>(Lifetime.Single).As<IScenarioHandler>();
            MetaInstaller.Install(builder, _config != null ? _config.text : null);
            builder.RegisterInstance(new GameSettings());

            builder.Register<TileMatchSchema>(Lifetime.Single).As<ISchemaModule>();
            builder.Register<ProgressDao>(Lifetime.Single).AsSelf().As<ICachedDao>();
            builder.Register<LevelAttemptDao>(Lifetime.Single);
            builder.Register<ProgressService>(Lifetime.Single).AsSelf().As<ILevelProgress>();
            builder.Register<ProgressLoadStep>(Lifetime.Single).As<IBootStep>();
            builder.Register<LevelResultCommitter>(Lifetime.Single);
            builder.Register<ShopService>(Lifetime.Single);
            builder.Register<StatsService>(Lifetime.Single);
            builder.Register<StarterBundleStep>(Lifetime.Single).As<IBootStep>();
            builder.Register<BoosterUnlocks>(Lifetime.Single);

            builder.RegisterFactory<IAppState>(r => new SceneState(AppStates.Home, HomeScene, r.Resolve<ISceneLoader>()), Lifetime.Single);
            builder.RegisterFactory<IAppState>(r => new SceneState(AppStates.Level, GameScene, r.Resolve<ISceneLoader>()), Lifetime.Single);
        }

        public void SetLevels(LevelPackAsset levels) => _levels = levels;
        public void SetRemoteLevels(RemoteLevelsConfig remoteLevels) => _remoteLevels = remoteLevels;
        public void SetConfig(TextAsset config) => _config = config;
    }
}
