using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworlds.Games.Editor
{
    // The Home's settings popup (toggles and Reset progress) and the gear button that opens it.
    public static class SettingsScreen
    {
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);

        // A round gear button under the top bar's right pill.
        public static Button Gear(Transform parent, Vector2 anchor, Vector2 position, float size = 120f) =>
            UiFactory.Button("Settings", parent, "Blue", null, anchor, position, new Vector2(size, size), "icon_settings");

        public static SettingsView Build(params Button[] openers)
        {
            var (canvas, group, panel) = UiFactory.Popup("SettingsCanvas", 320, new Vector2(860, 900));
            UiFactory.PopupTitle(panel, "Settings");
            var close = UiFactory.Button("Close", panel, "Red", null, Vector2.one, new Vector2(-64, -64), new Vector2(110, 110), "icon_close");
            var row = UiFactory.Rect("Toggles", panel, Top, Top, new Vector2(0, -330), new Vector2(820, 300));
            Button Toggle(string name, string icon, float slot)
            {
                var cell = UiFactory.Rect(name + "Cell", row, new Vector2(slot / 4f, 0f), new Vector2((slot + 1) / 4f, 1f), Vector2.zero, Vector2.zero);
                var toggle = UiFactory.Button(name, cell, "White", null, Top, new Vector2(0, -95), new Vector2(172, 172), icon);
                var label = UiFactory.Text("Label", cell, name, 30, UiFactory.MutedInk, Bottom, Bottom, new Vector2(0, 40), new Vector2(200, 90));
                label.textWrappingMode = TextWrappingModes.Normal;
                return toggle;
            }

            var sound = Toggle("Sound", "icon_sound", 0);
            var music = Toggle("Music", "icon_music", 1);
            var haptics = Toggle("Vibration", "icon_vibration", 2);
            var motion = Toggle("Motion", "icon_motion", 3);
            UiFactory.Text("ResetHint", panel, "Starts this game over: levels, coins and boosters.", 34, UiFactory.MutedInk, Bottom, Bottom, new Vector2(0, 290),
                new Vector2(800, 60));
            var reset = UiFactory.Button("Reset", panel, "Red", "Reset progress", Bottom, new Vector2(0, 180), new Vector2(520, 130), fontSize: 46);
            var confirm = ConfirmScreen.Build();

            var view = canvas.gameObject.AddComponent<SettingsView>();
            UiFactory.AssignMotion(view);
            SceneParts.Assign(view, "_group", group);
            SceneParts.Assign(view, "_panel", panel);
            SceneParts.AssignArray(view, "_openers", openers);
            SceneParts.Assign(view, "_close", close);
            SceneParts.Assign(view, "_sound", sound);
            SceneParts.Assign(view, "_music", music);
            SceneParts.Assign(view, "_haptics", haptics);
            SceneParts.Assign(view, "_motion", motion);
            SceneParts.Assign(view, "_reset", reset);
            SceneParts.Assign(view, "_confirm", confirm);
            return view;
        }
    }
}
