using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworlds.Games.Editor
{
    // The level list popup every Home shares, and the pieces around it: the Levels button row and the HUD's replay tag.
    public static class LevelListScreen
    {
        public const string SkinPath = "Assets/Games/Shared/Config/LevelListSkin.asset";
        private static readonly Color GreenOutline = new Color32(14, 90, 32, 255);
        private static readonly Color Gold = new Color32(255, 196, 40, 255);
        private static readonly Color Well = new Color32(236, 224, 200, 255);
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
        private static readonly Vector2 TileSize = new Vector2(170, 176);

        // `openers` are the Home's Levels button and its level pill.
        public static LevelListView Build(params Button[] openers)
        {
            var (canvas, group, panel) = UiFactory.Popup("LevelsCanvas", 250, new Vector2(900, 1500));
            UiFactory.PopupTitle(panel, "Levels");
            var close = UiFactory.Button("Close", panel, "Red", null, Vector2.one, new Vector2(-64, -64), new Vector2(110, 110), "icon_close");
            var progress = UiFactory.Text("Progress", panel, string.Empty, 44, UiFactory.MutedInk, Top, Top, new Vector2(0, -170), new Vector2(760, 60));
            var bar = UiFactory.Rect("Bar", panel, Top, Top, new Vector2(0, -222), new Vector2(680, 20));
            UiFactory.Image(bar, null, new Color(0f, 0f, 0f, 0.15f));
            var fill = UiFactory.Rect("Fill", bar, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            UiFactory.Image(fill, null, new Color32(76, 175, 80, 255));

            var (scroll, grid) = BuildWell(panel);
            var template = Tile(canvas.transform);
            var play = UiFactory.Button("Play", panel, "Green", "Play Level 1", Bottom, new Vector2(0, 130), new Vector2(620, 140), fontSize: 56);
            var caption = UiFactory.Text("Caption", panel, string.Empty, 40, UiFactory.MutedInk, Bottom, Bottom, new Vector2(0, 250), new Vector2(780, 70));

            var view = canvas.gameObject.AddComponent<LevelListView>();
            UiFactory.AssignMotion(view);
            SceneParts.Assign(view, "_skin", Skin(template));
            SceneParts.Assign(view, "_group", group);
            SceneParts.Assign(view, "_panel", panel);
            SceneParts.AssignArray(view, "_openers", openers);
            SceneParts.Assign(view, "_close", close);
            SceneParts.Assign(view, "_progress", progress);
            SceneParts.Assign(view, "_progressFill", fill);
            SceneParts.Assign(view, "_scroll", scroll);
            SceneParts.Assign(view, "_grid", grid);
            SceneParts.Assign(view, "_template", template);
            SceneParts.Assign(view, "_play", play);
            SceneParts.Assign(view, "_playLabel", play.GetComponentInChildren<TMP_Text>());
            SceneParts.Assign(view, "_caption", caption);
            return view;
        }

        // Buttons placed in the row sit side by side, centred, so hiding one centres the other.
        public static RectTransform ButtonRow(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float spacing)
        {
            var row = UiFactory.Rect("ButtonRow", parent, anchor, anchor, position, size);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        // A small yellow tag shown under the level number while replaying; hidden until the HUD shows it.
        public static GameObject ReplayTag(Transform parent, Vector2 anchor, Vector2 position)
        {
            var size = new Vector2(225, 70);
            var tag = UiFactory.Rect("ReplayTag", parent, anchor, anchor, position, size);
            tag.localRotation = Quaternion.Euler(0f, 0f, -3f);
            UiFactory.Sliced(tag, "ui_ribbon", Slice("ui_ribbon", size.y));
            UiFactory.Lettered("Label", tag, "REPLAY", 35, UiFactory.RibbonOutline, Vector2.zero, Vector2.one, new Vector2(0, 4), Vector2.zero);
            tag.gameObject.SetActive(false);
            return tag.gameObject;
        }

        // A cream, slightly sunken well that scrolls a 4-column grid.
        private static (ScrollRect, GridLayoutGroup) BuildWell(RectTransform panel)
        {
            var well = UiFactory.Rect("Well", panel, Top, Top, new Vector2(0, -270 - 440), new Vector2(820, 880));
            UiFactory.Image(well, null, Well);
            UiFactory.Image(UiFactory.Rect("Inset", well, new Vector2(0f, 1f), Vector2.one, new Vector2(0, -9), new Vector2(0, 18)), null, new Color(0f, 0f, 0f, 0.1f));
            var viewport = UiFactory.Rect("Viewport", well, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20, -20));
            UiFactory.Image(viewport, null, Color.clear, raycast: true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = Top;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = TileSize;
            grid.spacing = new Vector2(30, 35);
            grid.padding = new RectOffset(0, 0, 30, 30);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = well.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            return (scroll, grid);
        }

        // One level tile, as the list and the Home's level path draw them.
        public static LevelTile Tile(Transform parent)
        {
            var root = UiFactory.Rect("LevelTile", parent, Centre, Centre, Vector2.zero, TileSize);
            var body = UiFactory.Stretch("Body", root);
            var ring = UiFactory.Image(UiFactory.Rect("Ring", body, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(28, 28)), KitArt.Load("ui_tile"), Gold, sliced: true);
            ring.pixelsPerUnitMultiplier = Slice("ui_tile", TileSize.y + 28);
            var background = UiFactory.Image(UiFactory.Stretch("Background", body), KitArt.Load("ui_tile"), Color.white, sliced: true, raycast: true);
            var number = UiFactory.Text("Number", body, "1", 64, UiFactory.Ink, Vector2.zero, Vector2.one, new Vector2(0, 6), Vector2.zero);
            var check = UiFactory.Rect("Check", body, Vector2.one, Vector2.one, new Vector2(-22, -22), new Vector2(58, 58));
            UiFactory.Image(check, KitArt.Load("ui_badge_green"), Color.white);
            UiFactory.Image(UiFactory.Rect("Icon", check, Centre, Centre, Vector2.zero, new Vector2(36, 36)), KitArt.Load("icon_check"), Color.white);
            var hard = UiFactory.Image(UiFactory.Rect("Hard", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22, -22), new Vector2(58, 58)),
                KitArt.Load("ui_badge_red"), Color.white);
            UiFactory.Image(UiFactory.Rect("Skull", hard.rectTransform, Centre, Centre, new Vector2(0, 1), new Vector2(40, 40)), KitArt.Load("icon_skull"), Color.white);
            var locked = UiFactory.Rect("Lock", body, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UiFactory.Image(UiFactory.Rect("Icon", locked, Centre, Centre, new Vector2(0, 16), new Vector2(64, 64)), KitArt.Load("icon_lock"), Color.white);
            var lockNumber = UiFactory.Text("Number", locked, "1", 34, UiFactory.LightOnPill, Centre, Centre, new Vector2(0, -46), new Vector2(150, 44));

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            UiFactory.Feedback(button);
            var tile = root.gameObject.AddComponent<LevelTile>();
            SceneParts.Assign(tile, "_button", button);
            SceneParts.Assign(tile, "_body", body);
            SceneParts.Assign(tile, "_background", background);
            SceneParts.Assign(tile, "_ring", ring);
            SceneParts.Assign(tile, "_number", number);
            SceneParts.Assign(tile, "_check", check.gameObject);
            SceneParts.Assign(tile, "_lock", locked.gameObject);
            SceneParts.Assign(tile, "_lockNumber", lockNumber);
            SceneParts.Assign(tile, "_hard", hard);
            SceneParts.Assign(tile, "_hardSprite", KitArt.Load("ui_badge_red"));
            SceneParts.Assign(tile, "_superHardSprite", KitArt.Load("ui_badge_purple"));
            return tile;
        }

        // The current tile's number takes the green button's outline; the template keeps the plain material.
        private static LevelListSkin Skin(LevelTile template)
        {
            var number = template.GetComponentInChildren<TMP_Text>();
            var plain = number.fontSharedMaterial;
            UiFactory.Outline(number, GreenOutline);
            var green = number.fontSharedMaterial;
            number.fontSharedMaterial = plain;
            var skin = SceneParts.LoadOrCreate<LevelListSkin>(SkinPath);
            skin.Configure(KitArt.Load("ui_tile"), Slice("ui_tile", TileSize.y), KitArt.Load("ui_button_green"), Slice("ui_button_green", TileSize.y),
                KitArt.Load("ui_pill"), Slice("ui_pill", TileSize.y), green);
            UnityEditor.EditorUtility.SetDirty(skin);
            return skin;
        }

        private static float Slice(string sprite, float height) => Mathf.Max(1f, KitArt.MinHeight(sprite) * 1.15f / height);
    }
}
