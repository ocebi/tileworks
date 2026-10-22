using Tileworks.Kit.App;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using TileMatch.Controller;
using TileMatch.View;
using UnityEngine;

namespace TileMatch.Installer
{
    public sealed class GameSceneInstaller : SceneInstaller
    {
        [SerializeField, ReadOnlyInInspector] private BoardView _board;
        [SerializeField, ReadOnlyInInspector] private HudView _hud;
        [SerializeField, ReadOnlyInInspector] private BoosterBarView _boosters;
        [SerializeField, ReadOnlyInInspector] private ResultView _result;

        public override void Install(ContainerBuilder builder) => Install(builder, _board, _hud, _boosters, _result);

        public static void Install(ContainerBuilder builder, BoardView board, IHudView hud, IBoosterBarView boosters, IResultView result)
        {
            builder.RegisterInstance(board).As<IBoardView>();
            builder.RegisterInstance(hud);
            builder.RegisterInstance(boosters);
            builder.RegisterInstance(result);
            builder.RegisterInstance<ISettingsPanel>(result);
            builder.Register<PresentationClock>(Lifetime.Single);
            builder.Register<GamePresenter>(Lifetime.Single).As<ISceneEntry>();
            builder.Register<PreferencesController>(Lifetime.Single);
            builder.RegisterFactory<ILevelContextFactory>(r => new LevelContextFactory((Scope)r), Lifetime.Single);
        }
    }
}
