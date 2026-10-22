using Tileworks.Tiles.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Tileworlds.Games.Editor.SceneParts;

namespace Tileworlds.Games.Editor
{
    // The booster bar under the board (design px × 2.77): buttons, help bubble and unlock card. When a slot has an aim line,
    // the bar also gets the pick overlay: the dim and the aim card shown while a booster is picked. Blast and Gem Swap have
    // Hammer (slot 0) and Shuffle; Tile Match passes its own four, none of which is picked.
    public static class BoosterBarBuilder
    {
        public const string ConfigPath = "Assets/Games/Shared/Config/BoosterBar.asset";

        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Color BadgeOutline = new Color32(122, 14, 8, 255);
        private static readonly Vector2 ButtonSize = new Vector2(183, 188);
        private const float ButtonGap = 288f;
        private const float ButtonHeight = 210f;
        private const float AimMargin = 32f;

        // Id names the button object; Label goes under the button, Name on the bubble and the cards.
        public readonly struct Slot
        {
            public readonly string Id, Name, Label, Icon, What, Aim;

            public Slot(string name, string icon, string what, string aim = "", string label = null, string id = null)
            {
                (Name, Icon, What, Aim) = (name, icon, what, aim);
                Label = label ?? name;
                Id = id ?? name;
            }
        }

        private static readonly Slot[] HammerAndShuffle =
        {
            new Slot("Hammer", "icon_hammer", "Smashes one piece. It doesn't use a move.", "Tap a piece to smash it. It doesn't use a move."),
            new Slot("Shuffle", "icon_shuffle", "Mixes up the pieces for new matches. It doesn't use a move."),
        };

        // Builds on a HUD canvas whose `safe` rect is clear of the notch and home indicator. The board above needs about 0.17 of
        // the screen kept free at the bottom.
        public static BoosterBarView Build(Canvas canvas, RectTransform safe) => Build(canvas, safe, HammerAndShuffle, ButtonGap, ButtonHeight);

        // `gap` is centre to centre; `height` is the buttons' centre above the safe area's bottom edge.
        public static BoosterBarView Build(Canvas canvas, RectTransform safe, Slot[] slots, float gap, float height)
        {
            var n = slots.Length;
            var bar = UiFactory.Rect("Boosters", safe, Vector2.zero, new Vector2(1f, 0f), new Vector2(0, height), new Vector2(0, ButtonSize.y));
            var view = bar.gameObject.AddComponent<BoosterBarView>();
            var buttons = new Button[n];
            var pulses = new RectTransform[n];
            var art = new GameObject[n];
            var badges = new GameObject[n];
            var counts = new TMP_Text[n];
            var putBack = new GameObject[n];
            var glows = new RectTransform[n];
            var locks = new GameObject[n];
            var lockLevels = new TMP_Text[n];
            var holds = new HoldPress[n];
            var icons = new Sprite[n];
            for (var i = 0; i < n; i++)
            {
                var x = (i - (n - 1) * 0.5f) * gap;
                icons[i] = KitArt.Load(slots[i].Icon);
                buttons[i] = UiFactory.Button(slots[i].Id, bar, "Yellow", null, Centre, new Vector2(x, 0), ButtonSize, slots[i].Icon);
                var button = (RectTransform)buttons[i].transform;
                var artRoot = UiFactory.Stretch("Art", button);
                artRoot.SetSiblingIndex(0);
                foreach (var part in new[] { "IconDrop", "Icon" })
                    if (button.Find(part) is { } child)
                        child.SetParent(artRoot, true);
                art[i] = artRoot.gameObject;
                holds[i] = buttons[i].gameObject.AddComponent<HoldPress>();

                var badge = UiFactory.Rect("Badge", button, Vector2.one, Vector2.one, new Vector2(14, 14), new Vector2(78, 78));
                UiFactory.Image(badge, KitArt.Load("ui_badge_red"), Color.white);
                counts[i] = UiFactory.Lettered("Count", badge, "3", 42, BadgeOutline, Vector2.zero, Vector2.one, new Vector2(0, 4), Vector2.zero);
                badges[i] = badge.gameObject;
                var close = UiFactory.Rect("PutBack", button, Vector2.one, Vector2.one, new Vector2(14, 14), new Vector2(78, 78));
                UiFactory.Image(close, KitArt.Load("icon_close"), Color.white);
                putBack[i] = close.gameObject;

                var locked = UiFactory.Stretch("Locked", button);
                UiFactory.Image(UiFactory.Rect("Padlock", locked, Centre, Centre, new Vector2(0, -6), new Vector2(96, 96)), KitArt.Load("icon_lock"), Color.white);
                lockLevels[i] = UiFactory.PillLabel("Unlock", locked, new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(150, 60), 34);
                locks[i] = locked.gameObject;

                pulses[i] = UiFactory.Pulse(buttons[i]);
                glows[i] = UiFactory.Rect("Glow", pulses[i], Centre, Centre, Vector2.zero, new Vector2(330, 330));
                glows[i].SetSiblingIndex(0);
                UiFactory.Image(glows[i], KitArt.Load("ui_rays"), Color.white);
                var name = UiFactory.Lettered("Name", pulses[i], slots[i].Label, 42, UiFactory.PillOutline, Centre, Centre, new Vector2(0, -136), new Vector2(260, 52));
                name.textWrappingMode = TextWrappingModes.NoWrap;
            }

            var (bubble, bubbleTitle, bubbleBody) = BuildBubble(bar);
            var (intro, introCard, frame, introTitle, introBody, introClose) = BuildIntro(canvas);

            UiFactory.AssignMotion(view);
            Assign(view, "_config", LoadOrCreate<BoosterBarConfig>(ConfigPath));
            AssignStrings(view, "_names", System.Array.ConvertAll(slots, s => s.Name));
            AssignStrings(view, "_whats", System.Array.ConvertAll(slots, s => s.What));
            AssignArray(view, "_buttons", buttons);
            AssignArray(view, "_pulses", pulses);
            AssignArray(view, "_art", art);
            AssignArray(view, "_badges", badges);
            AssignArray(view, "_counts", counts);
            AssignArray(view, "_putBack", putBack);
            AssignArray(view, "_glows", glows);
            AssignArray(view, "_locks", locks);
            AssignArray(view, "_lockLevels", lockLevels);
            AssignArray(view, "_holds", holds);
            Assign(view, "_bubble", bubble);
            Assign(view, "_bubbleTitle", bubbleTitle);
            Assign(view, "_bubbleBody", bubbleBody);
            Assign(view, "_intro", intro);
            Assign(view, "_introCard", introCard);
            Assign(view, "_introFrame", frame);
            Assign(view, "_introTitle", introTitle);
            Assign(view, "_introBody", introBody);
            Assign(view, "_introClose", introClose);
            if (System.Array.Exists(slots, s => !string.IsNullOrEmpty(s.Aim))) Assign(view, "_overlay", BuildOverlay(canvas, safe, bar, slots, icons));
            return view;
        }

        private static BoosterPickOverlay BuildOverlay(Canvas canvas, RectTransform safe, RectTransform bar, Slot[] slots, Sprite[] icons)
        {
            var overlay = bar.gameObject.AddComponent<BoosterPickOverlay>();
            var (dim, panels, cancels) = BuildDim(canvas);
            var (card, aimGroup, aimIcon, aimTitle, aimLine) = BuildAimCard(safe);
            UiFactory.AssignMotion(overlay);
            Assign(overlay, "_config", LoadOrCreate<BoosterBarConfig>(ConfigPath));
            AssignStrings(overlay, "_aimLines", System.Array.ConvertAll(slots, s => s.Aim));
            AssignArray(overlay, "_icons", icons);
            Assign(overlay, "_dim", dim);
            AssignArray(overlay, "_dimPanels", panels);
            AssignArray(overlay, "_dimButtons", cancels);
            Assign(overlay, "_aimCard", card);
            Assign(overlay, "_aimGroup", aimGroup);
            Assign(overlay, "_aimIcon", aimIcon);
            Assign(overlay, "_aimTitle", aimTitle);
            Assign(overlay, "_aimLine", aimLine);
            return overlay;
        }

        private static void AssignStrings(Object target, string field, string[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static (RectTransform Bubble, TMP_Text Title, TMP_Text Body) BuildBubble(RectTransform bar)
        {
            var bubble = UiFactory.Rect("Bubble", bar, Centre, Centre, new Vector2(0, 294), new Vector2(610, 230));
            UiFactory.NestedCanvas(bubble, false);
            UiFactory.Sliced(bubble, "ui_card");
            var title = UiFactory.Lettered("Title", bubble, string.Empty, 54, UiFactory.PillOutline, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -54), new Vector2(-40, 66));
            var body = UiFactory.Text("Body", bubble, string.Empty, 36, UiFactory.Ink, Vector2.zero, new Vector2(1f, 1f), new Vector2(0, -44), new Vector2(-60, -120));
            return (bubble, title, body);
        }

        // Four panels around the board, placed at run time, on their own sorting order.
        private static (CanvasGroup Dim, Image[] Panels, Button[] Cancels) BuildDim(Canvas canvas)
        {
            var dim = UiFactory.Stretch("BoosterDim", canvas.transform);
            UiFactory.NestedCanvas(dim, true);
            var group = dim.gameObject.AddComponent<CanvasGroup>();
            var panels = new Image[4];
            var cancels = new Button[4];
            var names = new[] { "Top", "Bottom", "Left", "Right" };
            for (var i = 0; i < 4; i++)
            {
                panels[i] = UiFactory.Image(UiFactory.Rect(names[i], dim, Centre, Centre, Vector2.zero, Vector2.zero), null, Color.black, raycast: true);
                cancels[i] = panels[i].gameObject.AddComponent<Button>();
                cancels[i].transition = Selectable.Transition.None;
                cancels[i].navigation = new Navigation { mode = Navigation.Mode.None };
                cancels[i].targetGraphic = panels[i];
            }

            return (group, panels, cancels);
        }

        // Over the HUD, on its own sorting order above the dim, as wide as the safe area allows: a tall phone is only about 980
        // units across.
        private static (RectTransform Card, CanvasGroup Group, Image Icon, TMP_Text Title, TMP_Text Line) BuildAimCard(RectTransform safe)
        {
            var card = UiFactory.Rect("AimCard", safe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -144), new Vector2(-2f * AimMargin, 266));
            UiFactory.NestedCanvas(card, false);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            UiFactory.Sliced(card, "ui_card");
            var icon = UiFactory.Image(UiFactory.Rect("Icon", card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130, 8), new Vector2(166, 166)), null, Color.white);
            var title = UiFactory.Lettered("Title", card, string.Empty, 66, UiFactory.PillOutline, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(118, 46), new Vector2(-316, 80));
            title.alignment = TextAlignmentOptions.Left;
            var line = UiFactory.Text("Line", card, string.Empty, 40, UiFactory.Ink, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(118, -34), new Vector2(-316, 110));
            line.alignment = TextAlignmentOptions.TopLeft;
            return (card, group, icon, title, line);
        }

        private static (CanvasGroup Intro, RectTransform Card, RectTransform Frame, TMP_Text Title, TMP_Text Body, Button Close) BuildIntro(Canvas canvas)
        {
            var intro = UiFactory.Stretch("BoosterIntro", canvas.transform);
            UiFactory.NestedCanvas(intro, true);
            var group = intro.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(UiFactory.Stretch("Dim", intro), null, new Color(0.04f, 0.03f, 0.1f, 0.6f), raycast: true);
            var card = UiFactory.Rect("Card", intro, Centre, Centre, new Vector2(0, 60), new Vector2(860, 860));
            UiFactory.Sliced(card, "ui_panel");
            UiFactory.PopupTitle(card, "New Booster!");
            var frame = UiFactory.Rect("Frame", card, Centre, Centre, new Vector2(0, 170), new Vector2(240, 240));
            UiFactory.Sliced(frame, "ui_button_yellow");
            var title = UiFactory.Lettered("Name", card, string.Empty, 76, UiFactory.PillOutline, Centre, Centre, new Vector2(0, -20), new Vector2(760, 100));
            var body = UiFactory.Text("What", card, string.Empty, 44, UiFactory.Ink, Centre, Centre, new Vector2(0, -140), new Vector2(720, 150));
            var close = UiFactory.Button("GotIt", card, "Green", "Got it!", new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(440, 150));
            return (group, card, frame, title, body, close);
        }
    }
}
