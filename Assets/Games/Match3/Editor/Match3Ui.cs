using System.Linq;
using Tileworlds.Games.Editor;
using Tileworks.Kit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Editor
{
    // Building blocks for the match-3 screens, drawn with the shared UI kit. Sizes are canvas units (design px × 2.77).
    public static class Match3Ui
    {
        public static readonly Color Ink = new Color32(42, 14, 11, 255);
        public static readonly Color Cream = new Color32(255, 248, 236, 255);
        public static readonly Color Muted = new Color32(107, 58, 34, 255);
        public static readonly Color MutedOnCream = new Color32(74, 51, 48, 255);
        public static readonly Color LightOnPill = new Color32(200, 222, 255, 255);
        public static readonly Color Scrim = new Color(8 / 255f, 22 / 255f, 40 / 255f, 0.6f);
        public static readonly Color MovesPill = new Color32(255, 224, 138, 255);
        public static readonly Color Warning = new Color32(179, 38, 30, 255);
        public static readonly Color TitleOutline = new Color32(13, 71, 102, 255);
        public static readonly Color RibbonOutline = new Color32(154, 78, 6, 255);
        public static readonly Color PillOutline = new Color32(10, 27, 64, 255);

        // Outline for white labels on each coloured button; white buttons keep dark ink text.
        private static readonly (string Sprite, Color Outline)[] ButtonOutlines =
        {
            ("ui_button_yellow", new Color32(154, 78, 6, 255)), ("ui_button_green", new Color32(14, 90, 32, 255)),
            ("ui_button_pink", new Color32(142, 28, 56, 255)),
        };

        public static Color YellowButtonOutline => ButtonOutlines[0].Outline;

        public static Image Sliced(RectTransform rect, string sprite, Color tint, float borderScale = 1f, bool raycast = false)
        {
            var image = UiFactory.Image(rect, KitArt.Load(sprite), tint, sliced: true, raycast: raycast);
            image.pixelsPerUnitMultiplier = borderScale;
            return image;
        }

        public static Image Icon(Transform parent, string sprite, Vector2 anchor, Vector2 position, float size, Color tint)
        {
            var rect = UiFactory.Rect(sprite, parent, anchor, anchor, position, new Vector2(size, size));
            return UiFactory.Image(rect, KitArt.Load(sprite), tint);
        }

        public static TMP_Text Label(string name, Transform parent, string text, float size, Color colour, Vector2 anchor, Vector2 position, Vector2 box,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = UiFactory.Text(name, parent, text, size, colour, anchor, anchor, position, box);
            label.font = UiFactory.DisplayFont();
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        // White display lettering with a dark outline, for text on red, on dark pills and on coloured buttons and ribbons.
        public static TMP_Text Lettered(string name, Transform parent, string text, float size, Color outline, Vector2 anchor, Vector2 position, Vector2 box,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = Label(name, parent, text, size, Color.white, anchor, position, box, alignment);
            UiFactory.Outline(label, outline);
            return label;
        }

        // A kit button: a sliced sprite as the target graphic, an optional label, and the shared press feedback.
        public static Button KitButton(string name, Transform parent, string sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size,
            string label = null, float fontSize = 0f, float borderScale = 1f)
        {
            var rect = UiFactory.Rect(name, parent, anchorMin, anchorMax, position, size);
            var image = Sliced(rect, sprite, Color.white, borderScale, raycast: true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (!string.IsNullOrEmpty(label))
            {
                var box = size.x > 0 ? size : new Vector2(900, size.y);
                var outline = ButtonOutlines.FirstOrDefault(b => b.Sprite == sprite);
                if (outline.Sprite != null) Lettered("Label", rect, label, fontSize, outline.Outline, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), box);
                else Label("Label", rect, label, fontSize, Ink, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), box);
            }

            if (UiFactory.Motion != null)
            {
                var feedback = rect.gameObject.AddComponent<PressFeedback>();
                SceneParts.Assign(feedback, "_uiMotion", UiFactory.Motion);
                SceneParts.Assign(feedback, "_selectable", button);
            }

            return button;
        }

        // A button that spans its parent's width with `margin` on each side.
        public static Button WideButton(string name, Transform parent, string sprite, float height, float margin, string label, float fontSize, float borderScale = 1f)
        {
            return KitButton(name, parent, sprite, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-2f * margin, height), label, fontSize,
                borderScale);
        }

        public static TMP_Text Headline(string name, Transform parent, string text, float size, Vector2 anchor, Vector2 position, Vector2 box, float rotation)
        {
            var label = Lettered(name, parent, text, size, TitleOutline, anchor, position, box);
            label.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            return label;
        }
    }
}
