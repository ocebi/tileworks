namespace Tileworks.Tiles.View
{
    public enum SettingToggle
    {
        Sound,
        Haptics,
        ReduceMotion,
        Music,
    }

    public interface ISettingsHandler
    {
        void OnToggle(SettingToggle toggle);

        // Only after the player confirmed in the panel.
        void OnResetProgress();
    }

    public interface ISettingsPanel
    {
        // Null unbinds.
        void Bind(ISettingsHandler handler);
        void SetSettings(bool sound, bool haptics, bool reduceMotion);

        // Panels without a music toggle keep the default.
        void SetMusic(bool music) { }
    }
}
