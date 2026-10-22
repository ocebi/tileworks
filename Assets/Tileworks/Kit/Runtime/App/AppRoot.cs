using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Audio;
using Tileworks.Kit.Content;
using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Input;
using Tileworks.Kit.Save;
using Tileworks.Kit.Unity;
using UnityEngine;

namespace Tileworks.Kit.App
{
    public interface IAppRestarter
    {
        UniTask RestartAsync();
    }

    public interface IProgressReset
    {
        // Wipes this game's save and boots again; the player's settings survive.
        UniTask ResetAsync();
    }

    // The composition root of the persistent App scene.
    [DisallowMultipleComponent]
    public sealed class AppRoot : MonoBehaviour, IAppRestarter, IProgressReset, ICarryOver, IMusicHost
    {
        [SerializeField, ReadOnlyInInspector] private AppInstaller _installer;
        [SerializeField, ReadOnlyInInspector] private GameCatalog _games;
        [SerializeField, ReadOnlyInInspector] private LoadingScreen _loading;
        [SerializeField, ReadOnlyInInspector] private MusicPlayer _music;
        [SerializeField] private string _initialState = AppStates.Boot;
        [SerializeField] private LogLevel _logLevel = LogLevel.Info;
        [SerializeField, Min(30), Tooltip("Mobile players run at 30 fps unless asked; tweens and falls need 60.")] private int _targetFrameRate = 60;

        private Scope _scope;
        private ILog _log;
        private KitOptions _options = new KitOptions();
        private bool _adoptedMusic;
        private (bool Sound, bool Music, bool Haptics, bool ReduceMotion)? _keptSettings;

        public Scope Scope => _scope;

        // For tests that build the app in code; call before the first frame.
        public void Configure(AppInstaller installer, KitOptions options)
        {
            _installer = installer;
            _options = Guard.NotNull(options, nameof(options));
        }

        private void Start()
        {
            // Browsers pace frames themselves; a target rate there makes Unity time frames with timers instead.
            Application.targetFrameRate = Application.platform == RuntimePlatform.WebGLPlayer ? -1 : _targetFrameRate;
            _log = new Log(_logLevel);
            _log.AddSink(new UnityLogSink());
            if (ScenarioFile.TryPeek(out var scenario) && !string.IsNullOrEmpty(scenario.profile))
            {
                _options.PersistentRoot = ScenarioFile.ProfileRoot(scenario.profile);
                if (scenario.fresh) WipeProfile(_options.PersistentRoot);
                _log.Info(LogTag.App, "Scenario profile {0}", _options.PersistentRoot);
            }

            if (_adoptedMusic) _log.Info(LogTag.App, "Adopted the music player carried over from the previous app");
            StartAppAsync(destroyCancellationToken).Forget(_log, "App start");
        }

        // On a game switch the loading screen and the music outlive this scene; the next app's root adopts the music.
        public void CarryOver()
        {
            if (_loading != null) _loading.CarryOver();
            if (_music != null) _music.CarryOver();
        }

        // Called when this scene has loaded, before Start: the carried player replaces this scene's own, so its loop
        // plays on through the load.
        public void Adopt(MusicPlayer player)
        {
            if (player == _music) return;
            if (_music != null) Destroy(_music.gameObject == gameObject ? _music : (UnityEngine.Object)_music.gameObject);
            _music = player;
            player.transform.SetParent(transform, false);
            _adoptedMusic = true;
        }

        public async UniTask RestartAsync()
        {
            _log.Info(LogTag.App, "Restarting");
            DisposeScope();
            await UniTask.Yield();
            await StartAppAsync(destroyCancellationToken);
        }

        public async UniTask ResetAsync()
        {
            _log.Info(LogTag.App, "Resetting progress");
            var settings = _scope.Resolve<UserSettings>();
            _keptSettings = (settings.Sound.Value, settings.Music.Value, settings.Haptics.Value, settings.ReduceMotion.Value);
            var profile = _scope.Resolve<ProfileStore>();
            _scope.Resolve<IDatabase>().Wipe();
            DisposeScope();
            profile.DeleteDatabaseFiles();
            await UniTask.Yield();
            await StartAppAsync(destroyCancellationToken);
        }

        private async UniTask StartAppAsync(CancellationToken ct)
        {
            var started = DateTime.UtcNow;
            var loop = new UpdateLoop(_log);
            if (!TryGetComponent(out LoopDriver driver)) driver = gameObject.AddComponent<LoopDriver>();
            driver.Bind(loop);
            var pointer = GetComponentInChildren<PointerSurface>();
            if (pointer == null) pointer = PointerSurface.Create(transform);

            if (_music == null) _music = gameObject.AddComponent<MusicPlayer>();
            _options.Music = _music;
            if (_installer != null) _installer.ConfigureKit(_options);
            var content = _options.Content ??= new BuildSettingsContent();
            var scenes = new SceneLoader(_log, content);
            var builder = new ContainerBuilder();
            AppComposition.RegisterCore(builder, _log, loop, scenes, this, pointer, new GameSwitcher(_games, gameObject.scene.path, this, content, _loading, _log));
            builder.RegisterInstance<IProgressReset>(this);
            if (_music.Config != null)
            {
                builder.RegisterInstance(_music.Config);
                builder.Register<ContentMusicClips>(Lifetime.Single).As<IMusicClips>();
            }
            if (_loading != null) builder.RegisterInstance<ILoadingOverlay>(_loading);
            KitInstaller.Install(builder, _options, _log);
            if (_installer != null) _installer.Install(builder);

            _scope = builder.Build("App");
            _scope.ErrorSink = e => _log.Exception(LogTag.Di, e);
            foreach (var warning in _scope.Warnings) _log.Warn(LogTag.Di, warning);
            scenes.AttachRoot(_scope);
            _music.Bind(_scope.TryResolve<IMusicClips>(out var clips) ? clips : null);

            await _scope.StartAsync(ct);
            if (_keptSettings.HasValue) RestoreSettings();
            _log.Info(LogTag.App, "App scope ready in {0} ms ({1} instances)", (int)(DateTime.UtcNow - started).TotalMilliseconds, _scope.InstanceCount);
            await _scope.Resolve<IAppStateMachine>().GoToAsync(_initialState, null, ct);
        }

        private void RestoreSettings()
        {
            var kept = _keptSettings.Value;
            _keptSettings = null;
            var settings = _scope.Resolve<UserSettings>();
            settings.Sound.Value = kept.Sound;
            settings.Music.Value = kept.Music;
            settings.Haptics.Value = kept.Haptics;
            settings.ReduceMotion.Value = kept.ReduceMotion;
        }

        private void DisposeScope()
        {
            _scope?.Dispose();
            _scope = null;
        }

        private void OnDestroy() => DisposeScope();

        // A fresh scenario profile starts with no game's saves; the first app of the session wipes them all, once.
        private static void WipeProfile(string root)
        {
            if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
            ScenarioFile.ClearFresh();
        }
    }
}
