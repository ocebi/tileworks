using Tileworks.Kit.Audio;
using Tileworks.Kit.Core;
using Tileworks.Kit.Haptics;
using Tileworks.Kit.Save;
using Tileworks.Kit.UI;
using UnityEngine;

namespace Tileworks.Kit.App
{
    public interface IUserSettings
    {
        Observable<bool> Sound { get; }
        Observable<bool> Music { get; }
        Observable<bool> Haptics { get; }
        Observable<bool> ReduceMotion { get; }
    }

    // Player preferences, persisted in the kit settings table and loaded after the cache warms.
    public sealed class UserSettings : IUserSettings, IMotionPreference
    {
        private readonly IKeyValueStore _store;

        public UserSettings(IKeyValueStore store) => _store = store;

        public Observable<bool> Sound { get; } = new Observable<bool>(true);
        public Observable<bool> Music { get; } = new Observable<bool>(true);
        public Observable<bool> Haptics { get; } = new Observable<bool>(true);
        public Observable<bool> ReduceMotion { get; } = new Observable<bool>(false);

        bool IMotionPreference.ReduceMotion => ReduceMotion.Value;

        public void Load()
        {
            Sound.Value = _store.GetBool("settings.sound", true);
            Music.Value = _store.GetBool("settings.music", true);
            Haptics.Value = _store.GetBool("settings.haptics", true);
            ReduceMotion.Value = _store.GetBool("settings.reduceMotion", false);
            Sound.Subscribe(v => _store.SetBool("settings.sound", v), invokeNow: false);
            Music.Subscribe(v => _store.SetBool("settings.music", v), invokeNow: false);
            Haptics.Subscribe(v => _store.SetBool("settings.haptics", v), invokeNow: false);
            ReduceMotion.Subscribe(v => _store.SetBool("settings.reduceMotion", v), invokeNow: false);
        }
    }

    public sealed class UserSettingsStep : BootStep
    {
        private static readonly string[] Dependencies = { SaveSteps.Warm };
        private readonly UserSettings _settings;
        private readonly IHaptics _haptics;
        private readonly IMusic _music;

        public UserSettingsStep(UserSettings settings, IHaptics haptics, IMusic music)
        {
            _settings = settings;
            _haptics = haptics;
            _music = music;
        }

        public override string Name => "Settings";
        public override System.Collections.Generic.IReadOnlyList<string> DependsOn => Dependencies;

        public override Cysharp.Threading.Tasks.UniTask RunAsync(System.Threading.CancellationToken ct)
        {
            _settings.Load();
            _settings.Sound.Subscribe(on => AudioListener.volume = on ? 1f : 0f);
            _settings.Music.Subscribe(on => _music.Enabled = on);
            _settings.Haptics.Subscribe(on => _haptics.Enabled = on);
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        }
    }
}
