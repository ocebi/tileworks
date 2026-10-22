using BlockPuzzle.Api;
using BlockPuzzle.Controller;
using BlockPuzzle.Model;
using BlockPuzzle.Service;
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

namespace BlockPuzzle.Installer
{
    // Composition root of the App scope for Block Puzzle.
    [CreateAssetMenu(menuName = "Tileworks/Block Puzzle/App Installer")]
    public sealed class BlockPuzzleAppInstaller : AppInstaller
    {
        public const string HomeScene = "BlockHome";
        public const string GameScene = "BlockGame";

        [SerializeField] private LevelPackAsset _levels;
        [SerializeField] private TextAsset _config;
        [SerializeField] private RemoteLevelsConfig _remoteLevels;

        public override void ConfigureKit(KitOptions options)
        {
            options.SaveFolder = "save-blockpuzzle";
            options.Content = new AddressablesContent();
        }

        public override void Install(ContainerBuilder builder)
        {
            var rules = new BlockRules();
            builder.RegisterInstance(rules);
            LevelsInstaller.Install(builder, _levels, _remoteLevels, new SerializerLevelChecker<BlockLevel>(new BlockLevelSerializer(rules.Shapes)));
            builder.Register<PlayLevelScenario>(Lifetime.Single).As<IScenarioHandler>();
            MetaInstaller.Install(builder, _config != null ? _config.text : null);

            builder.Register<PuzzleSchema>(Lifetime.Single).As<ISchemaModule>();
            builder.Register<PuzzleProgressDao>(Lifetime.Single).AsSelf().As<ICachedDao>();
            builder.Register<PuzzleAttemptDao>(Lifetime.Single);
            builder.Register<PuzzleProgressService>(Lifetime.Single).AsSelf().As<IPuzzleProgress>();
            builder.Register<PuzzleProgressLoadStep>(Lifetime.Single).As<IBootStep>();
            builder.Register<PuzzleResultCommitter>(Lifetime.Single);

            builder.RegisterFactory<IAppState>(r => new SceneState(AppStates.Home, HomeScene, r.Resolve<ISceneLoader>()), Lifetime.Single);
            builder.RegisterFactory<IAppState>(r => new SceneState(AppStates.Level, GameScene, r.Resolve<ISceneLoader>()), Lifetime.Single);
        }

        public void SetLevels(LevelPackAsset levels) => _levels = levels;
        public void SetRemoteLevels(RemoteLevelsConfig remoteLevels) => _remoteLevels = remoteLevels;
        public void SetConfig(TextAsset config) => _config = config;
    }
}
