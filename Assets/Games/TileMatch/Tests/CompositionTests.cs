using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Tileworks.Kit.App;
using Tileworks.Kit.Content;
using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Input;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.Levels;
using Tileworks.Meta.Api;
using Tileworks.Tiles.View;
using TileMatch.Controller;
using TileMatch.Installer;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.View;
using UnityEditor;
using UnityEngine;

namespace TileMatch.Tests
{
    // Builds every scope of the game with the real installers and fakes for engine-bound views (§7.4),
    // so a missing registration or a cycle fails here instead of on a device.
    public sealed class CompositionTests
    {
        private const string PackPath = "Assets/Games/TileMatch/Content/LevelPack.asset";

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private Scope _app;

        [SetUp]
        public void SetUp()
        {
            var log = new NullLog();
            var builder = new ContainerBuilder();
            AppComposition.RegisterCore(builder, log, new UpdateLoop(log), new FakeSceneLoader(), new FakeRestarter(), new FakePointerSurface(),
                new GameSwitcher(null, null, null, new BuildSettingsContent(), null, log));
            builder.RegisterInstance<IProgressReset>(new FakeReset());
            KitInstaller.Install(builder, new KitOptions { InMemoryDatabase = true, PersistentRoot = Path.GetTempPath() }, log);
            var installer = Track(ScriptableObject.CreateInstance<TileMatchAppInstaller>());
            installer.SetLevels(AssetDatabase.LoadAssetAtPath<LevelPackAsset>(PackPath));
            installer.SetConfig(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Games/TileMatch/Config/config.json"));
            installer.Install(builder);
            _app = builder.Build("App");
        }

        [TearDown]
        public void TearDown()
        {
            _app?.Dispose();
            foreach (var obj in _created) UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();
        }

        [Test]
        public void AppScope_Boots()
        {
            var report = _app.Resolve<BootPipeline>().RunAsync(null, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(report.CriticalFailure, Is.False, report.ToString());
            Assert.That(_app.Resolve<IReadOnlyList<IAppState>>().Select(s => s.Id), Is.EquivalentTo(new[] { AppStates.Boot, AppStates.Home, AppStates.Level }));
            Assert.That(_app.Resolve<IAppStateMachine>(), Is.Not.Null);
        }

        [Test]
        public void Boot_GrantsStarterBundleOnce_AndSeedsCoins()
        {
            _app.Resolve<BootPipeline>().RunAsync(null, CancellationToken.None).GetAwaiter().GetResult();
            var economy = _app.Resolve<IEconomy>();
            Assert.That(economy.Balance("booster.undo").Value, Is.EqualTo(3));
            Assert.That(economy.Balance(Tileworks.Meta.Api.Resources.Coins).Value, Is.EqualTo(500));
            Assert.That(_app.Resolve<ILives>().Count.Value, Is.EqualTo(5));
        }

        [Test]
        public void HomeScope_Builds()
        {
            using var home = _app.CreateChild("Home", b => HomeSceneInstaller.Install(b, new FakeHome(), new FakeShop(), new FakeStats(), new FakeLevels()));
            Assert.That(home.Resolve<IReadOnlyList<ISceneEntry>>().Select(e => e.GetType()), Is.EquivalentTo(new[] { typeof(HomePresenter), typeof(LevelListPresenter) }));
        }

        [Test]
        public void GameAndLevelScopes_Build_AndALevelLoads()
        {
            _app.Resolve<BootPipeline>().RunAsync(null, CancellationToken.None).GetAwaiter().GetResult();
            var board = Track(new GameObject("Board")).AddComponent<BoardView>();
            using var game = _app.CreateChild("Game", b => GameSceneInstaller.Install(b, board, new FakeHud(), new FakeBar(), new FakeResult()));
            Assert.That(game.Resolve<IReadOnlyList<ISceneEntry>>().Single(), Is.InstanceOf<GamePresenter>());

            using var level = game.Resolve<ILevelContextFactory>().Create(_app.Resolve<ILevelSequence>().At(0));
            Assert.That(level.Session.Load(1).IsOk, Is.True);
            Assert.That(level.Session.State.TileCount, Is.GreaterThan(0));
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            _created.Add(obj);
            return obj;
        }

        private sealed class FakeSceneLoader : ISceneLoader
        {
            public UniTask<SceneHandle> LoadAsync(string sceneName, CancellationToken ct, Scope parent = null) => throw new NotSupportedException();
            public UniTask UnloadAsync(SceneHandle handle) => UniTask.CompletedTask;
        }

        private sealed class FakePointerSurface : IPointerSurface
        {
            public event Action<int, Vector2> Pressed { add { } remove { } }
            public event Action<int, Vector2> DragStarted { add { } remove { } }
            public event Action<int, Vector2> Dragged { add { } remove { } }
            public event Action<int, Vector2> Released { add { } remove { } }
        }

        private sealed class FakeRestarter : IAppRestarter
        {
            public UniTask RestartAsync() => UniTask.CompletedTask;
        }

        private sealed class FakeReset : IProgressReset
        {
            public UniTask ResetAsync() => UniTask.CompletedTask;
        }

        private sealed class FakeHome : IHomeView
        {
            public void Bind(IHomeHandler handler) { }
            public void SetLevel(int number) { }
            public void SetCoins(long coins) { }
            public void SetLives(int lives, in LivesTimer timer) { }
            public void SetPlayable(bool playable) { }
            public void Bind(IGameSwitchHandler handler) { }
            public void ShowSwitchGame(string title) { }
        }

        private sealed class FakeShop : IShopView
        {
            public void Bind(IShopHandler handler) { }
            public bool IsOpen => false;
            public void Show(IReadOnlyList<ShopEntry> entries) { }
            public void Hide() { }
        }

        private sealed class FakeStats : IStatsView
        {
            public void Show(string title, string body) { }
            public void Hide() { }
        }

        private sealed class FakeLevels : ILevelListView
        {
            public event Action OpenRequested { add { } remove { } }
            public event Action<int> TileClicked { add { } remove { } }
            public event Action PlayClicked { add { } remove { } }
            public bool IsOpen => false;
            public void Open(int count, int current, int selected, bool playable) { }
            public void Select(int index, bool playable) { }
            public void RefuseLocked(int index) { }
            public void Close() { }
        }

        private sealed class FakeHud : IHudView
        {
            public void Bind(IHudHandler handler) { }
            public void SetLevel(int number) { }
            public void SetInteractable(bool interactable) { }
            public void SetReplay(bool replay) { }
        }

        private sealed class FakeBar : IBoosterBarView
        {
            public void Bind(IBoosterBarHandler handler) { }
            public void SetCount(int slot, int count) { }
            public void SetLock(int slot, int unlockLevel) { }
            public void SetInteractable(bool interactable) { }
            public UniTask IntroduceAsync(IReadOnlyList<int> slots, CancellationToken ct) => UniTask.CompletedTask;
            public void SetPicked(int slot, Rect boardScreen) { }
            public Vector2 ButtonScreenPoint(int slot) => Vector2.zero;
            public float ScreenTop => 0f;
        }

        private sealed class FakeResult : IResultView
        {
            public void Bind(ISettingsHandler handler) { }
            public bool IsOpen => false;
            public void SetReplay(int currentLevel) { }
            public UniTask<ResultChoice> ShowWin(int levelNumber, int coins, CancellationToken ct) => UniTask.FromResult(ResultChoice.Home);
            public UniTask<ResultChoice> ShowLose(LoseReason reason, bool canContinue, int continuePrice, bool canAfford, CancellationToken ct) => UniTask.FromResult(ResultChoice.Home);
            public UniTask<ResultChoice> ShowPause(CancellationToken ct) => UniTask.FromResult(ResultChoice.Resume);
            public void SetSettings(bool sound, bool haptics, bool reduceMotion) { }
            public void Hide() { }
        }
    }
}
