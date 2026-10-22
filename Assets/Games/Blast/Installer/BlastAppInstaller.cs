using Blast.Api;
using Blast.Controller;
using Blast.Model;
using Blast.Service;
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

namespace Blast.Installer
{
    // Composition root of the App scope for Blast.
    [CreateAssetMenu(menuName = "Tileworks/Blast/App Installer")]
    public sealed class BlastAppInstaller : AppInstaller
    {
        public const string HomeScene = "BlastHome";
        public const string GameScene = "BlastGame";

        [SerializeField] private LevelPackAsset _levels;
        [SerializeField] private TextAsset _config;
        [SerializeField] private RemoteLevelsConfig _remoteLevels;

        public override void ConfigureKit(KitOptions options)
        {
            options.SaveFolder = "save-blast";
            options.Content = new AddressablesContent();
        }

        public override void Install(ContainerBuilder builder)
        {
            builder.RegisterInstance(new BlastRules());
            LevelsInstaller.Install(builder, _levels, _remoteLevels, new SerializerLevelChecker<BlastLevel>(new BlastLevelSerializer()));
            builder.Register<PlayLevelScenario>(Lifetime.Single).As<IScenarioHandler>();
            MetaInstaller.Install(builder, _config != null ? _config.text : null);
            BoosterInstaller.Install(builder);

            builder.Register<BlastSchema>(Lifetime.Single).As<ISchemaModule>();
            builder.Register<BlastProgressDao>(Lifetime.Single).AsSelf().As<ICachedDao>();
            builder.Register<BlastAttemptDao>(Lifetime.Single);
            builder.Register<BlastProgressService>(Lifetime.Single).AsSelf().As<IBlastProgress>();
            builder.Register<BlastProgressLoadStep>(Lifetime.Single).As<IBootStep>();
            builder.Register<BlastResultCommitter>(Lifetime.Single);

            builder.RegisterFactory<IAppState>(r => new SceneState(AppStates.Home, HomeScene, r.Resolve<ISceneLoader>()), Lifetime.Single);
            builder.RegisterFactory<IAppState>(r => new SceneState(AppStates.Level, GameScene, r.Resolve<ISceneLoader>()), Lifetime.Single);
        }

        public void SetLevels(LevelPackAsset levels) => _levels = levels;
        public void SetRemoteLevels(RemoteLevelsConfig remoteLevels) => _remoteLevels = remoteLevels;
        public void SetConfig(TextAsset config) => _config = config;
    }
}
