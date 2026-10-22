using Tileworks.Kit.App;
using Tileworks.Kit.Core;
using Tileworks.Kit.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworlds.Games.Editor
{
    // Small helpers for building uGUI hierarchies from code (scene builders only).
    public static partial class UiFactory
    {
        public const string Icons = "Assets/ThirdParty/Kenney/GameIcons";
        public const string FontsFolder = "Assets/Games/Shared/Fonts";
        public const string DisplayFontPath = FontsFolder + "/LilitaOne SDF.asset";
        public const string SymbolsFontPath = FontsFolder + "/Symbols SDF.asset";
        public const string MotionConfigPath = "Assets/Games/Shared/Config/UiMotion.asset";

        public static readonly Color Ink = new Color32(42, 14, 11, 255);
        public static readonly Color MutedInk = new Color32(74, 51, 48, 255);
        public static readonly Color PillOutline = new Color32(10, 27, 64, 255);
        public static readonly Color LightOnPill = new Color32(200, 222, 255, 255);
        public static readonly Color RibbonOutline = new Color32(154, 78, 6, 255);
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        // Kit button per colour name, with the outline its white label gets; white buttons keep dark ink labels instead.
        private static readonly (string Colour, string Sprite, Color Outline)[] Buttons =
        {
            ("Green", "ui_button_green", new Color32(14, 90, 32, 255)), ("Yellow", "ui_button_yellow", new Color32(154, 78, 6, 255)),
            ("Blue", "ui_button_blue", new Color32(15, 58, 138, 255)), ("Red", "ui_button_red", new Color32(122, 14, 8, 255)),
            ("Purple", "ui_button_purple", new Color32(62, 26, 128, 255)), ("Pink", "ui_button_pink", new Color32(142, 28, 56, 255)),
            ("White", "ui_button_white", Color.clear),
        };

        [AllowStaticState("editor-only builder setting, reset by every build")]
        public static TMP_FontAsset Font { get; set; }

        // The open scene's UI motion; buttons, views and the scene root built after BeginScene get wired to it.
        [AllowStaticState("editor-only builder setting, reset by every scene build")]
        public static UiMotion Motion { get; private set; }

        private const string ClickSound = "Assets/ThirdParty/Kenney/Audio/click_001.ogg";

        // Call right after creating a Home or Game scene.
        public static void BeginScene()
        {
            Font = DisplayFont();
            if (!AssetDatabase.IsValidFolder("Assets/Games/Shared/Config")) AssetDatabase.CreateFolder("Assets/Games/Shared", "Config");
            var config = SceneParts.LoadOrCreate<UiMotionConfig>(MotionConfigPath);
            if (config.Click == null) SceneParts.Assign(config, "_click", AssetDatabase.LoadAssetAtPath<AudioClip>(ClickSound));
            if (!config.TryGetSound(UiSound.PopupOpen, out _))
            {
                config.SetSounds(new[]
                {
                    UiClip(UiSound.PopupOpen, "maximize_003", 0.45f), UiClip(UiSound.PopupClose, "minimize_003", 0.4f),
                    UiClip(UiSound.CountTick, "chips-stack-2", 0.35f), UiClip(UiSound.StarPop, "sfx_sparkle_long", 0.7f),
                    UiClip(UiSound.Confetti, "sfx_confetti", 0.7f), UiClip(UiSound.Callout, "confirmation_003", 0.6f),
                    UiClip(UiSound.GoalArrive, "sfx_bloop", 0.5f), UiClip(UiSound.GoalDone, "sfx_sparkle", 0.6f),
                    UiClip(UiSound.MovesLow, "tick_002", 0.6f), UiClip(UiSound.Badge, "impactPunch_heavy_002", 0.6f),
                });
                EditorUtility.SetDirty(config);
            }

            Motion = new GameObject("UiMotion").AddComponent<UiMotion>();
            SceneParts.Assign(Motion, "_config", config);
        }

        private static UiSoundClip UiClip(UiSound sound, string name, float volume) =>
            new UiSoundClip { Sound = sound, Clip = SoundCues.Load(name), Volume = volume };

        public static void AssignMotion(Component view) => SceneParts.Assign(view, "_uiMotion", Motion);

        // Wraps a button in a parent that can breathe while the button itself still squashes when pressed.
        public static RectTransform Pulse(Button button)
        {
            var rect = (RectTransform)button.transform;
            var pulse = Rect(button.name + "Pulse", rect.parent, rect.anchorMin, rect.anchorMax, rect.anchoredPosition, rect.sizeDelta);
            pulse.SetSiblingIndex(rect.GetSiblingIndex());
            rect.SetParent(pulse, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            NestedCanvas(pulse, clickable: true);
            return pulse;
        }

        // Its own canvas, so frequent changes here don't rebatch the static UI around it.
        public static Canvas NestedCanvas(RectTransform rect, bool clickable)
        {
            var canvas = rect.gameObject.AddComponent<Canvas>();
            if (clickable) rect.gameObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static Canvas Canvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rect;
        }

        // A screen's content root, clear of notches and the home indicator; dims and backdrops stay on the canvas.
        public static RectTransform Safe(Canvas canvas)
        {
            var safe = Stretch("Safe", canvas.transform);
            safe.gameObject.AddComponent<SafeArea>();
            return safe;
        }

        // Only buttons and blockers need raycasts; leaving the rest off keeps EventSystem raycasts short.
        public static Image Image(RectTransform rect, Sprite sprite, Color color, bool sliced = false, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = raycast;
            image.sprite = sprite;
            image.color = color;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = !sliced && sprite != null;
            return image;
        }

        public static TMP_Text Text(string name, Transform parent, string text, float size, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 box)
        {
            var rect = Rect(name, parent, anchorMin, anchorMax, position, box);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        // A glossy kit button. Icons are kit sprites ("icon_…") or Kenney game icons, which get a white face over a drop in the
        // label's outline colour.
        public static Button Button(string name, Transform parent, string colour, string label, Vector2 anchor, Vector2 position, Vector2 size, string icon = null, float fontSize = 52)
        {
            var style = System.Array.Find(Buttons, b => b.Colour == colour);
            if (style.Sprite == null) style = Buttons[^1];
            var rect = Rect(name, parent, anchor, anchor, position, size);
            var image = Image(rect, KitArt.Load(style.Sprite), Color.white, sliced: true, raycast: true);
            image.pixelsPerUnitMultiplier = Mathf.Max(1f, KitArt.MinHeight(style.Sprite) * 1.15f / size.y);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var lettered = style.Outline.a > 0f;
            if (icon != null)
            {
                var kitIcon = icon.StartsWith("icon_");
                var sprite = kitIcon ? KitArt.Load(icon) : Sprite($"{Icons}/{icon}.png");
                if (!kitIcon) Image(Rect("IconDrop", rect, Centre, Centre, new Vector2(0, 2), size * 0.5f), sprite, lettered ? style.Outline : MutedInk);
                Image(Rect("Icon", rect, Centre, Centre, new Vector2(0, 8), size * 0.5f), sprite, kitIcon || lettered ? Color.white : Ink);
            }

            if (!string.IsNullOrEmpty(label))
            {
                var text = Text("Label", rect, label, fontSize, lettered ? Color.white : Ink, Vector2.zero, Vector2.one, new Vector2(0, 8), Vector2.zero);
                if (lettered) Outline(text, style.Outline);
            }

            Feedback(button);
            return button;
        }

        // A smaller line under a button's label, such as the level under "Play"; the label moves up to make room.
        public static TMP_Text Sublabel(Button button, string text, float fontSize, float lift)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            label.rectTransform.anchoredPosition = new Vector2(0, 8 + lift);
            var sub = Text("Sublabel", button.transform, text, fontSize, label.color, Centre, Centre, new Vector2(0, 8 - lift - fontSize * 0.9f), new Vector2(600, fontSize * 1.5f));
            sub.fontSharedMaterial = label.fontSharedMaterial;
            return sub;
        }

        // The shared press squash and click sound.
        public static void Feedback(Selectable selectable)
        {
            if (Motion == null) return;
            var feedback = selectable.gameObject.AddComponent<PressFeedback>();
            SceneParts.Assign(feedback, "_uiMotion", Motion);
            SceneParts.Assign(feedback, "_selectable", selectable);
        }

        // A kit button with a kit icon at the left of its label.
        public static Button IconLabelButton(string name, Transform parent, string colour, string icon, string label, Vector2 anchor, Vector2 position, Vector2 size,
            float iconSize, float fontSize = 52)
        {
            var button = Button(name, parent, colour, label, anchor, position, size, fontSize: fontSize);
            IconBeforeLabel(button, KitArt.Load(icon), iconSize);
            return button;
        }

        // Puts an icon at the left of the button's label and centres the pair, so it stays centred when the label changes.
        public static void IconBeforeLabel(Button button, Sprite icon, float iconSize, float spacing = 20f)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            var row = Stretch("Row", button.transform);
            row.anchoredPosition = new Vector2(0, 8);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var image = Image(Rect("Icon", row, Centre, Centre, Vector2.zero, new Vector2(iconSize, iconSize)), icon, Color.white);
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = iconSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.SetParent(row, false);
        }

        // Home top bar: a coins pill on the left, lives and the refill timer on the right. Each takes half the bar, so they never
        // meet on narrow screens.
        public static (TMP_Text Coins, TMP_Text Lives, TMP_Text Timer) CoinsAndLives(Transform parent)
        {
            var top = Rect("TopBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -116), new Vector2(-88, 122));
            NestedCanvas(top, clickable: false);
            var coinsPill = TopPill("Coins", top, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), -12f, "icon_coin");
            var coins = Lettered("Coins", coinsPill, "0", 61, PillOutline, Centre, Centre, new Vector2(45, 2), new Vector2(280, 100));
            var livesPill = TopPill("Lives", top, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), 12f, "icon_heart");
            var lives = Lettered("Lives", livesPill, "5", 61, PillOutline, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(165, 2), new Vector2(90, 100));
            var timer = Text("LivesTimer", livesPill, "Full", 38, LightOnPill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-110, 0), new Vector2(200, 80));
            return (coins, lives, timer);
        }

        private static RectTransform TopPill(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float offset, string icon)
        {
            var pill = Rect(name, parent, anchorMin, anchorMax, new Vector2(offset, 0), new Vector2(-24, 122));
            Sliced(pill, "ui_pill", 1.3f);
            Image(Rect("Icon", pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(66, 0), new Vector2(89, 89)), KitArt.Load(icon), Color.white);
            return pill;
        }

        // A kit sprite stretched over the rect; a larger border scale draws its borders smaller.
        public static Image Sliced(RectTransform rect, string sprite, float borderScale = 1f, Color? tint = null)
        {
            var image = Image(rect, KitArt.Load(sprite), tint ?? Color.white, sliced: true);
            image.pixelsPerUnitMultiplier = borderScale;
            return image;
        }

        // White display text with a dark outline in the given colour.
        public static TMP_Text Lettered(string name, Transform parent, string text, float size, Color outline, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 box)
        {
            var label = Text(name, parent, text, size, Color.white, anchorMin, anchorMax, position, box);
            Outline(label, outline);
            return label;
        }

        // A dark pill holding a lettered label, such as the level number.
        public static TMP_Text PillLabel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            var pill = Rect(name + "Pill", parent, anchor, anchor, position, size);
            Sliced(pill, "ui_pill", Mathf.Max(1f, KitArt.MinHeight("ui_pill") * 1.1f / size.y));
            return Lettered(name, pill, string.Empty, fontSize, PillOutline, Vector2.zero, Vector2.one, new Vector2(0, 2), Vector2.zero);
        }

        // A character placed over the element built just before it, like a mascot holding on to a card. `position` is its
        // centre; it pivots at its feet, so breathing grows it upwards.
        public static Image Mascot(Transform parent, Sprite sprite, Vector2 anchor, Vector2 position, float size, float tilt)
        {
            var rect = Rect("Mascot", parent, anchor, anchor, position - new Vector2(0f, size * 0.5f), new Vector2(size, size));
            rect.pivot = new Vector2(0.5f, 0f);
            rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
            return Image(rect, sprite, Color.white);
        }

        // The Home's entrance: `popIns` in order, then `breathers` loop and sparkles drift up over the whole canvas.
        public static void Entrance(Canvas canvas, Transform[] popIns, params Transform[] breathers)
        {
            var entrance = canvas.gameObject.AddComponent<HomeEntrance>();
            AssignMotion(entrance);
            SceneParts.AssignArray(entrance, "_popIns", popIns);
            SceneParts.AssignArray(entrance, "_breathers", breathers);
            SceneParts.Assign(entrance, "_sparkles", UiBursts.HomeSparkle(canvas.transform));
        }

        public static Sprite Sprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[Tileworks] Missing sprite {path}");
            return sprite;
        }
    }
}
