using Blast.View;
using Tileworlds.Games.Editor;
using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Blast.Editor.BlastUi;
using static Tileworlds.Games.Editor.SceneParts;

namespace Blast.Editor
{
    // The Blast Home, HUD and popup, laid out from the design (design px × 2.77 on the 1080-wide canvas). Anything that
    // changes while the screen is idle sits on a nested canvas, so the static frame around it never rebatches.
    public static class BlastScreens
    {
        private const int GoalSlots = 4;
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);

        // The Home, its level list (which the Levels button and the path's nodes open) and the level path.
        public static (BlastHomeView Home, LevelListView Levels, LevelPathView Path, SettingsView Settings) Home(BlastSkin skin)
        {
            var canvas = UiFactory.Canvas("HomeCanvas", 100);
            var root = UiFactory.Safe(canvas);

            // Lives and coins pills; their numbers change every second, so the bar is its own canvas.
            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -116), new Vector2(-88, 122));
            UiFactory.NestedCanvas(bar, false);
            var livesPill = UiFactory.Rect("Lives", bar, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-12, 0), new Vector2(-24, 122));
            Sliced(livesPill, "ui_pill", Color.white, 1.3f);
            Icon(livesPill, "icon_heart", new Vector2(0f, 0.5f), new Vector2(66, 0), 89, Color.white);
            var lives = Lettered("Count", livesPill, "5", 61, PillOutline, new Vector2(0f, 0.5f), new Vector2(165, 2), new Vector2(90, 100));
            var timer = Label("Timer", livesPill, "Full", 38, LightOnPill, new Vector2(1f, 0.5f), new Vector2(-110, 0), new Vector2(200, 80));
            var coinsPill = UiFactory.Rect("Coins", bar, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(12, 0), new Vector2(-24, 122));
            Sliced(coinsPill, "ui_pill", Color.white, 1.3f);
            Icon(coinsPill, "icon_coin", new Vector2(0f, 0.5f), new Vector2(66, 0), 89, Color.white);
            var coins = Lettered("Count", coinsPill, "0", 61, PillOutline, Centre, new Vector2(45, 2), new Vector2(280, 100));

            // Title with a few cubes tumbling behind it.
            var logo = UiFactory.Rect("Logo", root, Top, Top, new Vector2(0, -455), new Vector2(1000, 400));
            var cubes = new (float X, float Y, float Rotation, float Size, int Colour)[]
            {
                (-380, 115, -12, 125, 1), (-300, -100, 10, 100, 2), (330, 120, 14, 115, 3), (410, -65, -8, 95, 4), (-440, 5, 6, 85, 5), (-150, -150, -16, 90, 6),
            };
            foreach (var cube in cubes)
            {
                var rect = UiFactory.Rect("Cube", logo, Centre, Centre, new Vector2(cube.X, cube.Y), new Vector2(cube.Size, cube.Size));
                rect.localRotation = Quaternion.Euler(0f, 0f, cube.Rotation);
                UiFactory.Image(rect, skin.SpriteOf(Model.Piece.Cube, (byte)cube.Colour, 0), Color.white);
            }

            Headline("Title", logo, "Blast!", 160, Centre, Vector2.zero, new Vector2(1000, 220), -4f);

            // The level path runs between the logo and Play; the mascot peeks out behind the current node.
            var mascot = UiFactory.Mascot(root, BlastArtBuilder.LoadUi("char_cube"), Centre, Vector2.zero, 280, -8f).rectTransform;
            var path = LevelPathScreen.Build(UiFactory.Rect("LevelPath", root, new Vector2(0.03f, 1f), new Vector2(0.97f, 1f), new Vector2(0, -1030), new Vector2(0, 560)),
                mascot);

            var play = KitButton("Play", root, "ui_button_yellow", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0, 457), new Vector2(-132, 238), "Play", 105);
            ((RectTransform)play.GetComponentInChildren<TMP_Text>().transform).anchoredPosition = new Vector2(0, 30);
            var playLevel = Label("Sublabel", play.transform, "LEVEL 1", 39, YellowButtonOutline, Centre, new Vector2(0, -62), new Vector2(600, 60));
            var row = LevelListScreen.ButtonRow(root, new Vector2(0.5f, 0f), new Vector2(0, 175), new Vector2(1000, 150), 40);
            var levels = KitButton("Levels", row, "ui_button_white", Centre, Centre, Vector2.zero, new Vector2(340, 140), "Levels", 46, 1.25f);
            UiFactory.IconBeforeLabel(levels, BlastArtBuilder.LoadUi("icon_levels"), 70);
            var switchGame = KitButton("SwitchGame", row, "ui_button_white", Centre, Centre, Vector2.zero, new Vector2(340, 140), "All Games", 46, 1.25f);
            UiFactory.IconBeforeLabel(switchGame, BlastArtBuilder.LoadUi("icon_games"), 70);
            var gear = SettingsScreen.Gear(row, Centre, Vector2.zero, 140);

            UiFactory.Entrance(canvas, new Transform[] { logo, livesPill, coinsPill, mascot, levels.transform, switchGame.transform, gear.transform }, mascot);
            var view = canvas.gameObject.AddComponent<BlastHomeView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_play", play);
            Assign(view, "_playLevel", playLevel);
            Assign(view, "_hard", LevelIntroBuilder.Sticker(play.transform, Vector2.one, new Vector2(-90, 0), 0.8f));
            Assign(view, "_playPulse", UiFactory.Pulse(play));
            Assign(view, "_coins", coins);
            Assign(view, "_lives", lives);
            Assign(view, "_livesTimer", timer);
            Assign(view, "_switchGame", switchGame);
            return (view, LevelListScreen.Build(levels), path, SettingsScreen.Build(gear));
        }

        public static (BlastHudView, BoosterBarView) Hud(BlastSkin skin)
        {
            var canvas = UiFactory.Canvas("HudCanvas", 200);
            var safe = UiFactory.Safe(canvas);
            var row = UiFactory.Rect("TopRow", safe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -161), new Vector2(-66, 222));
            var movesCard = UiFactory.Rect("MovesCard", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(116, 0), new Vector2(233, 222));
            Sliced(movesCard, "ui_card", Color.white);
            Label("Label", movesCard, "MOVES", 33, Muted, Top, new Vector2(0, -42), new Vector2(220, 50));
            var goalsCard = UiFactory.Rect("GoalsCard", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            goalsCard.offsetMin = new Vector2(255, 0);
            goalsCard.offsetMax = new Vector2(-155, 0);
            Sliced(goalsCard, "ui_card", Color.white);
            var pause = KitButton("Pause", row, "ui_pill", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-66, 0), new Vector2(133, 222), borderScale: 1.3f);
            Icon(pause.transform, "icon_pause", Centre, new Vector2(0, 4), 90, Color.white);

            // Moves and goal counts change every move: their own canvas over the cards.
            var counters = UiFactory.Stretch("Counters", row);
            UiFactory.NestedCanvas(counters, false);
            var moves = Label("Moves", counters, "0", 111, Ink, new Vector2(0f, 0.5f), new Vector2(116, -22), new Vector2(233, 130));
            var chips = UiFactory.Stretch("Goals", counters);
            chips.offsetMin = new Vector2(255, 0);
            chips.offsetMax = new Vector2(-155, 0);
            var (icons, counts, checks) = GoalChips(chips, null, Color.clear, new Vector2(12, 12), new Vector2(-12, -12), 105, 50, Ink);
            var sparkle = UiBursts.GoalSparkle(counters);
            var trail = UiBursts.GoalTrail(canvas.transform);

            // Cascade callouts float over the board, on their own canvas, a little above the board's middle.
            var overlay = UiFactory.Stretch("Callouts", canvas.transform);
            UiFactory.NestedCanvas(overlay, false);
            var callout = Lettered("Callout", overlay, "Nice!", 150, TitleOutline, Centre, new Vector2(0, -60), new Vector2(1000, 220));
            UiFactory.Outline(callout, TitleOutline, 0.3f);
            var bonus = HudBanners.Bonus(overlay, new Color(0.25f, 0.05f, 0.04f, 0.85f));

            var view = canvas.gameObject.AddComponent<BlastHudView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_skin", skin);
            Assign(view, "_moves", moves);
            AssignArray(view, "_goalIcons", icons);
            AssignArray(view, "_goalCounts", counts);
            AssignArray(view, "_goalChecks", checks);
            Assign(view, "_pause", pause);
            Assign(view, "_replayTag", LevelListScreen.ReplayTag(safe, Top, new Vector2(0, -305)));
            Assign(view, "_callout", callout);
            Assign(view, "_goalSparkle", sparkle);
            Assign(view, "_goalTrail", trail);
            Assign(view, "_intro", LevelIntroBuilder.Build(icons));
            Assign(view, "_bonus", bonus);
            return (view, BoosterBarBuilder.Build(canvas, safe));
        }

        public static BlastResultView Popup(BlastSkin skin, BlastFeelConfig feel)
        {
            var canvas = UiFactory.Canvas("ResultCanvas", 300);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(UiFactory.Stretch("Dim", canvas.transform), null, Scrim, raycast: true);
            var panel = UiFactory.Rect("Panel", canvas.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0, -40), new Vector2(-132, 1300));
            Sliced(panel, "ui_panel", Color.white);

            var ribbon = UiFactory.Rect("Ribbon", panel, Top, Top, Vector2.zero, new Vector2(700, 183));
            var ribbonImage = Sliced(ribbon, "ui_ribbon", Color.white);
            var title = Lettered("Title", ribbon, "Level complete!", 72, TitleOutline, Centre, new Vector2(0, 6), new Vector2(620, 150));
            FitWidth(title, 40);
            var close = KitButton("Close", panel, "ui_button_white", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-94, -105), new Vector2(122, 122), borderScale: 1.6f);
            Icon(close.transform, "icon_close", Centre, new Vector2(0, 4), 66, Color.white);
            var body = Label("Body", panel, string.Empty, 46, MutedOnCream, Top, new Vector2(0, -200), new Vector2(900, 90));

            var win = UiFactory.WinReveal(panel, score: false);
            ((RectTransform)win.transform).anchoredPosition = new Vector2(0, -130);
            win.transform.SetSiblingIndex(ribbon.GetSiblingIndex());

            var goalsRow = UiFactory.Rect("Goals", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -370), new Vector2(-110, 190));
            var (icons, counts, checks) = GoalChips(goalsRow, "ui_chip", Color.white, Vector2.zero, Vector2.zero, 94, 55, Ink);
            var extra = UiFactory.Rect("ExtraMoves", panel, Top, Top, new Vector2(0, -610), new Vector2(540, 170));
            Sliced(extra, "ui_card", MovesPill, 1.2f);
            var extraText = Label("Moves", extra, "+5", 122, Ink, Centre, new Vector2(-120, 6), new Vector2(240, 150));
            Label("Label", extra, "extra\nmoves", 50, Ink, Centre, new Vector2(110, 0), new Vector2(240, 140));

            var settings = UiFactory.Rect("Settings", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -340), new Vector2(-110, 300));
            Button Toggle(string name, string icon, float slot)
            {
                var cell = UiFactory.Rect(name + "Cell", settings, new Vector2(slot / 4f, 0f), new Vector2((slot + 1) / 4f, 1f), Vector2.zero, Vector2.zero);
                var toggle = KitButton(name, cell, "ui_button_white", Top, Top, new Vector2(0, -95), new Vector2(172, 172), borderScale: 1.3f);
                Icon(toggle.transform, icon, Centre, new Vector2(0, 6), 92, Color.white);
                Label("Label", cell, name, 28, MutedOnCream, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(230, 90)).textWrappingMode = TextWrappingModes.Normal;
                return toggle;
            }

            var sound = Toggle("Sound", "icon_sound", 0);
            var music = Toggle("Music", "icon_music", 1);
            var haptics = Toggle("Vibration", "icon_vibration", 2);
            var motion = Toggle("Motion", "icon_motion", 3);

            var next = WideButton("Next", panel, "ui_button_yellow", 200, 60, "Next level", 89);
            var cont = WideButton("Continue", panel, "ui_button_green", 210, 60, "Keep going", 83);
            var contLabel = cont.GetComponentInChildren<TMP_Text>();
            ((RectTransform)contLabel.transform).anchoredPosition = new Vector2(-150, 8);
            ((RectTransform)contLabel.transform).sizeDelta = new Vector2(440, 150);
            FitWidth(contLabel, 40);
            var price = UiFactory.Rect("Price", cont.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-190, 6), new Vector2(250, 112));
            Sliced(price, "ui_card", Color.white, 1.6f);
            Icon(price, "icon_coin", new Vector2(0f, 0.5f), new Vector2(55, 0), 70, Color.white);
            var priceText = Label("Amount", price, "100", 61, Ink, Centre, new Vector2(35, 4), new Vector2(150, 90));
            var resume = WideButton("Resume", panel, "ui_button_green", 200, 60, "Resume", 89);
            var retry = WideButton("Retry", panel, "ui_button_white", 155, 60, "Try again", 61, 1.25f);
            var home = WideButton("Home", panel, "ui_button_white", 155, 60, "Home", 61, 1.25f);
            var levels = WideButton("Levels", panel, "ui_button_white", 155, 60, "Levels", 61, 1.25f);
            UiFactory.IconBeforeLabel(levels, BlastArtBuilder.LoadUi("icon_levels"), 80);
            var giveUp = GiveUp(panel);

            var view = canvas.gameObject.AddComponent<BlastResultView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_skin", skin);
            Assign(view, "_feel", feel);
            Assign(view, "_group", group);
            Assign(view, "_panel", panel);
            Assign(view, "_ribbon", ribbonImage);
            Assign(view, "_happyRibbon", BlastArtBuilder.LoadUi("ui_ribbon"));
            Assign(view, "_sadRibbon", BlastArtBuilder.LoadUi("ui_ribbon_pink"));
            Assign(view, "_title", title);
            Assign(view, "_body", body);
            Assign(view, "_win", win);
            Assign(view, "_goalsRow", goalsRow.gameObject);
            AssignArray(view, "_goalIcons", icons);
            AssignArray(view, "_goalCounts", counts);
            AssignArray(view, "_goalChecks", checks);
            Assign(view, "_extraMoves", extra.gameObject);
            Assign(view, "_extraMovesText", extraText);
            Assign(view, "_settingsRow", settings.gameObject);
            Assign(view, "_sound", sound);
            Assign(view, "_music", music);
            Assign(view, "_haptics", haptics);
            Assign(view, "_motion", motion);
            Assign(view, "_close", close);
            Assign(view, "_next", next);
            Assign(view, "_continue", cont);
            Assign(view, "_continuePrice", priceText);
            Assign(view, "_resume", resume);
            Assign(view, "_retry", retry);
            Assign(view, "_home", home);
            Assign(view, "_levels", levels);
            Assign(view, "_giveUp", giveUp);
            return view;
        }

        // Shrinks long text to its box instead of overflowing; the display font is wide.
        private static void FitWidth(TMP_Text label, float minSize)
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = minSize;
        }

        // "Give up −1 ♥": a text button without a frame.
        private static Button GiveUp(RectTransform panel)
        {
            var button = WideButton("GiveUp", panel, "ui_button_white", 133, 200, null, 0);
            button.targetGraphic.color = Color.clear;
            Label("Label", button.transform, "Give up", 46, MutedOnCream, Centre, new Vector2(-90, 0), new Vector2(260, 80), TextAlignmentOptions.Right);
            Icon(button.transform, "icon_heart", Centre, new Vector2(70, 0), 60, Color.white);
            Label("Cost", button.transform, "-1", 46, Warning, Centre, new Vector2(140, 0), new Vector2(90, 80), TextAlignmentOptions.Left);
            return button;
        }

        // Up to four goal chips spread across `parent` between the two offsets: an icon, a count under or beside it, and a
        // check that replaces the count when the goal is done. Chips fill four slots from the left.
        private static (Object[] Icons, Object[] Counts, Object[] Checks) GoalChips(RectTransform parent, string chipSprite, Color chipTint, Vector2 offsetMin,
            Vector2 offsetMax, float iconSize, float countSize, Color countColour, bool checks = true)
        {
            var area = UiFactory.Stretch("Chips", parent);
            area.offsetMin = offsetMin;
            area.offsetMax = offsetMax;

            var icons = new Object[GoalSlots];
            var counts = new Object[GoalSlots];
            var done = new Object[GoalSlots];
            var beside = chipSprite == "ui_chip" && chipTint == Color.white;
            for (var i = 0; i < GoalSlots; i++)
            {
                var chip = UiFactory.Rect($"Goal{i}", area, new Vector2(i / (float)GoalSlots, 0f), new Vector2((i + 1) / (float)GoalSlots, 1f), Vector2.zero,
                    new Vector2(-22, 0));
                if (chipSprite != null) Sliced(chip, chipSprite, chipTint, 1.2f);
                var iconPos = beside ? new Vector2(-iconSize * 0.45f, 0) : new Vector2(0, countSize * 0.55f);
                var icon = UiFactory.Rect("Icon", chip, Centre, Centre, iconPos, new Vector2(iconSize, iconSize));
                icons[i] = UiFactory.Image(icon, null, Color.white);
                var countPos = beside ? new Vector2(iconSize * 0.55f, 2) : new Vector2(0, -iconSize * 0.5f - 6);
                counts[i] = Label("Count", chip, "0", countSize, countColour, Centre, countPos, new Vector2(iconSize * 1.4f, countSize * 1.3f));
                if (!checks) continue;
                var check = Icon(beside ? chip : icon, "icon_check", Centre, beside ? countPos : new Vector2(iconSize * 0.4f, iconSize * 0.4f), countSize * 1.25f,
                    Color.white);
                done[i] = check.gameObject;
            }

            return (icons, counts, done);
        }
    }
}
