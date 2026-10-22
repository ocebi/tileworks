using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Tileworlds.Games.Editor.SceneParts;

namespace Tileworlds.Games.Editor
{
    // The level-start card over the board: the level on the popup ribbon, a Hard or Super Hard sticker, and up to four goals in
    // a centred row. A tap anywhere closes it early.
    public static class LevelIntroBuilder
    {
        public const string ConfigPath = "Assets/Games/Shared/Config/LevelIntro.asset";
        private const int SortingOrder = 250;
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);

        // `targets` are the HUD's goal icons, where the goals fly to.
        public static LevelIntroView Build(Object[] targets)
        {
            var canvas = UiFactory.Canvas("IntroCanvas", SortingOrder);
            var root = UiFactory.Stretch("Intro", canvas.transform);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var dim = UiFactory.Image(UiFactory.Stretch("Dim", root), null, new Color(0.04f, 0.03f, 0.1f, 0.55f), raycast: true);
            var dismiss = dim.gameObject.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.navigation = new Navigation { mode = Navigation.Mode.None };

            var card = UiFactory.Rect("Card", root, Centre, Centre, new Vector2(0, 60), new Vector2(860, 600));
            UiFactory.Sliced(card, "ui_panel");
            var title = UiFactory.PopupTitle(card, "Level 1");
            UiFactory.Text("Collect", card, "Collect these:", 48, UiFactory.Ink, Top, Top, new Vector2(0, -190), new Vector2(700, 70));

            var row = UiFactory.Rect("Goals", card, Top, Top, new Vector2(0, -370), new Vector2(780, 230));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 36;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var icons = new Image[targets.Length];
            var counts = new TMP_Text[targets.Length];
            for (var i = 0; i < targets.Length; i++)
            {
                var slot = UiFactory.Rect($"Goal{i}", row, Centre, Centre, Vector2.zero, new Vector2(170, 230));
                icons[i] = UiFactory.Image(UiFactory.Rect("Icon", slot, Centre, Centre, new Vector2(0, 30), new Vector2(150, 150)), null, Color.white);
                icons[i].preserveAspect = true;
                counts[i] = UiFactory.Lettered("Count", slot, "0", 66, UiFactory.PillOutline, Centre, Centre, new Vector2(0, -80), new Vector2(170, 80));
            }

            // A sticker on the card's top-right corner.
            var badge = Sticker(card, Vector2.one, new Vector2(-120, -150), 1f);

            var fliers = UiFactory.Stretch("Fliers", canvas.transform);
            var view = canvas.gameObject.AddComponent<LevelIntroView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_config", LoadOrCreate<LevelIntroConfig>(ConfigPath));
            Assign(view, "_group", group);
            Assign(view, "_card", card);
            Assign(view, "_dismiss", dismiss);
            Assign(view, "_title", title);
            Assign(view, "_badge", badge);
            AssignArray(view, "_icons", icons);
            AssignArray(view, "_counts", counts);
            Assign(view, "_fliers", fliers);
            return view;
        }

        // A tilted ribbon with a skull for Hard and Super Hard levels, hidden until shown; `scale` sizes it for its host.
        public static LevelBadgeSticker Sticker(Transform parent, Vector2 anchor, Vector2 position, float scale)
        {
            // The holder carries the tilt and size, so the sticker itself pops and swings around its own rest.
            var holder = UiFactory.Rect("HardBadge", parent, anchor, anchor, position, new Vector2(430, 124));
            holder.localScale = Vector3.one * scale;
            holder.localRotation = Quaternion.Euler(0f, 0f, -8f);
            var root = UiFactory.Stretch("Sticker", holder);
            var ribbon = UiFactory.Sliced(root, "ui_ribbon_red", 0.7f);
            UiFactory.Image(UiFactory.Rect("Skull", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62, 6), new Vector2(104, 104)),
                KitArt.Load("icon_skull"), Color.white);
            var label = UiFactory.Lettered("Label", root, "Hard Level", 56, UiFactory.PillOutline, Vector2.zero, Vector2.one, new Vector2(48, 6), new Vector2(-130, 0));
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 34;
            label.fontSizeMax = 56;
            var sticker = root.gameObject.AddComponent<LevelBadgeSticker>();
            UiFactory.AssignMotion(sticker);
            Assign(sticker, "_config", LoadOrCreate<LevelIntroConfig>(ConfigPath));
            Assign(sticker, "_ribbon", ribbon);
            Assign(sticker, "_label", label);
            Assign(sticker, "_hardRibbon", KitArt.Load("ui_ribbon_red"));
            Assign(sticker, "_superHardRibbon", KitArt.Load("ui_ribbon_purple"));
            root.gameObject.SetActive(false);
            return sticker;
        }
    }
}
