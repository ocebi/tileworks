using Match3.Api;
using Match3.Controller;
using Match3.Model;
using Match3.Service;
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
using UnityEngine;

namespace Match3.Installer
{
    // Composition root of the App scope for Match3.
    [CreateAssetMenu(menuName = "Tileworks/Match3/App Installer")]
    public sealed class Match3AppInstaller : AppInstaller
    {
        public const string HomeScene = "Match3Home";
        public const string GameScene = "Match3Game";

        [SerializeField] private LevelPackAsset _levels;
        [SerializeField] private TextAsset _config;
        [SerializeField] private RemoteLevelsConfig _remoteLevels;

        public override void ConfigureKit(KitOptions options)
        {
            options.SaveFolder = "save-match3";
            options.Content = new AddressablesContent();
        }

        public override void Install(ContainerBuilder builder)
        {
            builder.RegisterInstance(new Match3Rules());
            LevelsInstaller.Install(builder, _levels, _remoteLevels, new SerializerLevelChecker<Match3Level>(new Match3LevelSerializer()));
            builder.Register<PlayLevelScenario>(Lifetime.Single).As<IScenarioHandler>();
            MetaInstaller.Install(builder, _config != null ? _config.text : null);
            BoosterInstaller.Install(builder);

            builder.Register<Match3Schema>(Lifetime.Single).As<ISchemaModule>();
            builder.Register<Match3ProgressDao>(Lifetime.Single).AsSelf().As<ICachedDao>();
            builder.Register<Match3AttemptDao>(Lifetime.Single);
            builder.Register<Match3ProgressService>(Lifetime.Single).AsSelf().As<IMatch3Progress>();
            builder.Register<Match3ProgressLoadStep>(Lifetime.Single).As<IBootStep>();
            builder.Register<Match3ResultCommitter>(Lifetime.Single);

            builder.RegisterFactory<IAppState>(r => new SceneState(AppStates.Home, HomeScene, r.Resolve<ISceneLoader>()), Lifetime.Single);
            builder.RegisterFactory<IAppState>(r => new SceneState(AppStates.Level, GameScene, r.Resolve<ISceneLoader>()), Lifetime.Single);
        }

        public void SetLevels(LevelPackAsset levels) => _levels = levels;
        public void SetRemoteLevels(RemoteLevelsConfig remoteLevels) => _remoteLevels = remoteLevels;
        public void SetConfig(TextAsset config) => _config = config;
    }
}
