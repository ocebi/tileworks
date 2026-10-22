using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;

namespace Tileworlds.Games.Editor
{
    // The yes/no popup the pause popups ask "Reset progress?" with. It sits above the result popup.
    public static class ConfirmScreen
    {
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);

        public static ConfirmPopup Build()
        {
            var (canvas, group, panel) = UiFactory.Popup("ConfirmCanvas", 350, new Vector2(820, 640));
            var title = UiFactory.PopupTitle(panel, "Reset progress?");
            var body = UiFactory.Text("Body", panel, string.Empty, 44, UiFactory.MutedInk, Centre, Centre, new Vector2(0, 0), new Vector2(680, 240));
            var cancel = UiFactory.Button("Cancel", panel, "White", "Cancel", Bottom, new Vector2(-180, 110), new Vector2(320, 130), fontSize: 46);
            var confirm = UiFactory.Button("Confirm", panel, "Red", "Reset", Bottom, new Vector2(180, 110), new Vector2(320, 130), fontSize: 46);

            var view = canvas.gameObject.AddComponent<ConfirmPopup>();
            UiFactory.AssignMotion(view);
            SceneParts.Assign(view, "_group", group);
            SceneParts.Assign(view, "_panel", panel);
            SceneParts.Assign(view, "_title", title);
            SceneParts.Assign(view, "_body", body);
            SceneParts.Assign(view, "_confirm", confirm);
            SceneParts.Assign(view, "_confirmLabel", confirm.GetComponentInChildren<TMP_Text>());
            SceneParts.Assign(view, "_cancel", cancel);
            return view;
        }
    }
}
