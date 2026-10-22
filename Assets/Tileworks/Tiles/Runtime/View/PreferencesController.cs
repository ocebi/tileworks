using System;
using Tileworks.Kit.App;
using Tileworks.Kit.Core;
using Tileworks.Kit.Unity;

namespace Tileworks.Tiles.View
{
    // Settings toggles in a pause popup; Reduce Motion also shortens the board animations. Reset Progress wipes this
    // game's save and boots again.
    public sealed class PreferencesController : IInitializable, ISettingsHandler, IDisposable
    {
        private readonly IUserSettings _settings;
        private readonly ISettingsPanel _panel;
        private readonly PresentationClock _clock;
        private readonly IProgressReset _reset;
        private readonly ILog _log;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

        public PreferencesController(IUserSettings settings, ISettingsPanel panel, PresentationClock clock, IProgressReset reset, ILog log)
        {
            _settings = Guard.NotNull(settings, nameof(settings));
            _panel = Guard.NotNull(panel, nameof(panel));
            _clock = Guard.NotNull(clock, nameof(clock));
            _reset = Guard.NotNull(reset, nameof(reset));
            _log = Guard.NotNull(log, nameof(log));
        }

        public void Initialize()
        {
            _panel.Bind(this);
            _subscriptions.Add(_settings.Sound.Subscribe(_ => Refresh()));
            _subscriptions.Add(_settings.Music.Subscribe(_ => Refresh()));
            _subscriptions.Add(_settings.Haptics.Subscribe(_ => Refresh()));
            _subscriptions.Add(_settings.ReduceMotion.Subscribe(on =>
            {
                _clock.ReduceMotion = on;
                Refresh();
            }));
        }

        public void Dispose()
        {
            _panel.Bind(null);
            _subscriptions.Dispose();
        }

        public void OnToggle(SettingToggle toggle)
        {
            var setting = toggle switch
            {
                SettingToggle.Sound => _settings.Sound,
                SettingToggle.Haptics => _settings.Haptics,
                SettingToggle.Music => _settings.Music,
                _ => _settings.ReduceMotion,
            };
            setting.Value = !setting.Value;
        }

        public void OnResetProgress() => _reset.ResetAsync().Forget(_log, "Reset progress");

        private void Refresh()
        {
            _panel.SetSettings(_settings.Sound.Value, _settings.Haptics.Value, _settings.ReduceMotion.Value);
            _panel.SetMusic(_settings.Music.Value);
        }
    }
}
