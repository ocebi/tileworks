using Match3.Model;
using Match3.View;
using Tileworlds.Games.Editor;
using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Match3.Editor.Match3Ui;
using static Tileworlds.Games.Editor.SceneParts;

namespace Match3.Editor
{
    // The match-3 Home, HUD and popup, laid out from the design (design px × 2.77 on the 1080-wide canvas). Anything that
    // changes while the screen is idle sits on a nested canvas, so the static frame around it never rebatches.
    public static class Match3Screens
    {
        public const string Title = "Gem Swap";

        private const int GoalSlots = 4;

        // Home, bottom up: the band the level path runs in above Play, and the line the logo starts above.
        private const float PathBottom = 680;
        private const float PathHeight = 560;
        private const float LogoBottom = PathBottom + PathHeight + 20;
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly Color Band = new Color(13 / 255f, 71 / 255f, 102 / 255f, 0.85f);

        // The Home, its level list (which the Levels button and the path's nodes open) and the level path.
        public static (Match3HomeView Home, LevelListView Levels, LevelPathView Path, SettingsView Settings) Home(Match3Skin skin)
        {
            var canvas = UiFactory.Canvas("HomeCanvas", 100);
            var root = UiFactory.Safe(canvas);

            // Lives and coins pills; their numbers change every second, so the bar is its own canvas.
            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -129), new Vector2(-100, 136));
            UiFactory.NestedCanvas(bar, false);
            var livesPill = UiFactory.Rect("Lives", bar, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-28, 0), new Vector2(-56, 136));
            Sliced(livesPill, "ui_pill", Color.white, 1.3f);
            Icon(livesPill, "icon_heart", new Vector2(0f, 0.5f), new Vector2(72, 0), 100, Color.white);
            var lives = Lettered("Count", livesPill, "5", 66, PillOutline, new Vector2(0f, 0.5f), new Vector2(180, 2), new Vector2(90, 100));
            var timer = Label("Timer", livesPill, "Full", 42, LightOnPill, new Vector2(1f, 0.5f), new Vector2(-105, 0), new Vector2(200, 80));
            var coinsPill = UiFactory.Rect("Coins", bar, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(28, 0), new Vector2(-56, 136));
            Sliced(coinsPill, "ui_pill", Color.white, 1.3f);
            Icon(coinsPill, "icon_coin", new Vector2(0f, 0.5f), new Vector2(72, 0), 100, Color.white);
            var coins = Lettered("Count", coinsPill, "0", 66, PillOutline, Centre, new Vector2(55, 2), new Vector2(280, 100));

            // Title with a few gems floating around it, centred in the space between the top bar and the star, so short
            // screens shrink the gap instead of overlapping.
            var logo = UiFactory.Stretch("Logo", root);
            logo.offsetMin = new Vector2(0, LogoBottom);
            logo.offsetMax = new Vector2(0, -210);
            var gems = new (float X, float Y, float Rotation, float Size, int Colour)[]
            {
                (-418, 0.82f, -12, 122, 1), (355, 0.84f, 10, 127, 3), (-399, 0.17f, 8, 94, 5),
            };
            foreach (var gem in gems)
            {
                var anchor = new Vector2(0.5f, gem.Y);
                var rect = UiFactory.Rect("Gem", logo, anchor, anchor, new Vector2(gem.X, 0), new Vector2(gem.Size, gem.Size));
                rect.localRotation = Quaternion.Euler(0f, 0f, gem.Rotation);
                UiFactory.Image(rect, skin.SpriteOf(Piece.Gem, (byte)gem.Colour, 0), Color.white);
            }

            Headline("Title", logo, Title, 183, Centre, Vector2.zero, new Vector2(1000, 230), -4f);

            // The level path runs between the logo and Play; the mascot peeks out behind the current node.
            var mascot = UiFactory.Mascot(root, Match3ArtBuilder.LoadUi("hero_gems"), Centre, Vector2.zero, 280, 0f).rectTransform;
            var path = LevelPathScreen.Build(UiFactory.Rect("LevelPath", root, new Vector2(0.03f, 0f), new Vector2(0.97f, 0f),
                new Vector2(0, PathBottom + PathHeight * 0.5f), new Vector2(0, PathHeight)), mascot);

            var play = KitButton("Play", root, "ui_button_yellow", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 470), new Vector2(886, 249), "Play", 116);
            ((RectTransform)play.GetComponentInChildren<TMP_Text>().transform).anchoredPosition = new Vector2(0, 30);
            var playLevel = Label("Sublabel", play.transform, "LEVEL 1", 39, YellowButtonOutline, Centre, new Vector2(0, -62), new Vector2(600, 60));
            var row = LevelListScreen.ButtonRow(root, new Vector2(0.5f, 0f), new Vector2(0, 190), new Vector2(1000, 155), 44);
            var levels = KitButton("Levels", row, "ui_button_white", Centre, Centre, Vector2.zero, new Vector2(375, 155), "Levels", 52, 1.25f);
            UiFactory.IconBeforeLabel(levels, KitArt.Load("icon_levels"), 78);
            var switchGame = KitButton("SwitchGame", row, "ui_button_white", Centre, Centre, Vector2.zero, new Vector2(375, 155), "All Games", 52, 1.25f);
            UiFactory.IconBeforeLabel(switchGame, KitArt.Load("icon_games"), 78);
            var gear = SettingsScreen.Gear(row, Centre, Vector2.zero, 155);

            UiFactory.Entrance(canvas, new Transform[] { logo, livesPill, coinsPill, mascot, levels.transform, switchGame.transform, gear.transform }, mascot);
            var view = canvas.gameObject.AddComponent<Match3HomeView>();
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

        public static (Match3HudView, BoosterBarView) Hud(Match3Skin skin, Match3FeelConfig feel)
        {
            var canvas = UiFactory.Canvas("HudCanvas", 200);
            var safe = UiFactory.Safe(canvas);
            var row = UiFactory.Rect("TopRow", safe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -166), new Vector2(0, 233));
            var movesCard = UiFactory.Rect("MovesCard", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(135, 0), new Vector2(205, 233));
            Sliced(movesCard, "ui_tile", Color.white);
            Label("Label", movesCard, "MOVES", 33, Muted, Top, new Vector2(0, -50), new Vector2(200, 50));
            var goalsCard = UiFactory.Rect("GoalsCard", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            goalsCard.offsetMin = new Vector2(260, 0);
            goalsCard.offsetMax = new Vector2(-194, 0);
            Sliced(goalsCard, "ui_tile", Color.white);
            var pause = KitButton("Pause", row, "ui_button_blue", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-102, 11), new Vector2(139, 155), borderScale: 1.3f);
            Icon(pause.transform, "icon_pause", Centre, new Vector2(0, 6), 66, Color.white);

            // Moves and goal counts change every move: their own canvas over the cards.
            var counters = UiFactory.Stretch("Counters", row);
            UiFactory.NestedCanvas(counters, false);
            var moves = Label("Moves", counters, "0", 100, Ink, new Vector2(0f, 0.5f), new Vector2(135, -22), new Vector2(205, 120));
            var chips = UiFactory.Stretch("Goals", counters);
            chips.offsetMin = new Vector2(260, 0);
            chips.offsetMax = new Vector2(-194, 0);
            var (icons, counts, checks) = GoalChips(chips, null, Color.clear, new Vector2(16, 24), new Vector2(-16, -16), 105, 47, Ink);
            var sparkle = UiBursts.GoalSparkle(counters);
            var trail = UiBursts.GoalTrail(canvas.transform);

            // Cascade callouts and the No moves ribbon float over the board, on their own canvas.
            var overlay = UiFactory.Stretch("Callouts", canvas.transform);
            UiFactory.NestedCanvas(overlay, false);
            // Callouts sit a little above the board's middle, which is below the canvas centre because the HUD takes the top.
            var callout = Lettered("Callout", overlay, "Nice!", 150, TitleOutline, Centre, new Vector2(0, -60), new Vector2(1000, 220));
            UiFactory.Outline(callout, TitleOutline, 0.3f);
            var noMoves = UiFactory.Rect("NoMoves", overlay, Centre, Centre, new Vector2(0, 205), new Vector2(1080, 266));
            UiFactory.Image(UiFactory.Stretch("Band", noMoves), null, Band);
            var ribbon = UiFactory.Rect("Ribbon", noMoves, Centre, Centre, new Vector2(0, 0), new Vector2(748, 200));
            ribbon.localRotation = Quaternion.Euler(0f, 0f, -3f);
            Sliced(ribbon, "ui_ribbon", Color.white);
            Lettered("Label", ribbon, "No moves!", 94, RibbonOutline, Centre, new Vector2(0, 8), new Vector2(680, 150));
            var shuffling = UiFactory.Rect("Shuffling", noMoves, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, -80), new Vector2(600, 90));
            Icon(shuffling, "icon_shuffle", Centre, new Vector2(-170, 0), 78, Color.white);
            Lettered("Label", shuffling, "Shuffling…", 55, TitleOutline, Centre, new Vector2(40, 4), new Vector2(380, 90));
            var bonus = HudBanners.Bonus(overlay, Band);

            var view = canvas.gameObject.AddComponent<Match3HudView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_skin", skin);
            Assign(view, "_feel", feel);
            Assign(view, "_moves", moves);
            AssignArray(view, "_goalIcons", icons);
            AssignArray(view, "_goalCounts", counts);
            AssignArray(view, "_goalChecks", checks);
            Assign(view, "_pause", pause);
            Assign(view, "_replayTag", LevelListScreen.ReplayTag(safe, Top, new Vector2(0, -330)));
            Assign(view, "_callout", callout);
            Assign(view, "_noMoves", noMoves);
            Assign(view, "_goalSparkle", sparkle);
            Assign(view, "_goalTrail", trail);
            Assign(view, "_intro", LevelIntroBuilder.Build(icons));
            Assign(view, "_bonus", bonus);
            return (view, BoosterBarBuilder.Build(canvas, safe));
        }

        public static Match3ResultView Popup(Match3Skin skin, Match3FeelConfig feel)
        {
            var canvas = UiFactory.Canvas("ResultCanvas", 300);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(UiFactory.Stretch("Dim", canvas.transform), null, Scrim, raycast: true);
            var panel = UiFactory.Rect("Panel", canvas.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0, -40), new Vector2(-144, 1300));
            Sliced(panel, "ui_panel", Color.white);

            var ribbon = UiFactory.Rect("Ribbon", panel, Top, Top, Vector2.zero, new Vector2(760, 177));
            var ribbonImage = Sliced(ribbon, "ui_ribbon", Color.white);
            var title = Lettered("Title", ribbon, "Level Complete!", 75, RibbonOutline, Centre, new Vector2(0, 6), new Vector2(680, 150));
            FitWidth(title, 40);
            var close = KitButton("Close", panel, "ui_button_pink", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-94, -105), new Vector2(133, 139), borderScale: 1.6f);
            Icon(close.transform, "icon_close", Centre, new Vector2(0, 6), 61, Color.white);
            var body = Label("Body", panel, string.Empty, 50, MutedOnCream, Top, new Vector2(0, -200), new Vector2(900, 90));

            var win = UiFactory.WinReveal(panel, score: false);
            ((RectTransform)win.transform).anchoredPosition = new Vector2(0, -130);
            win.transform.SetSiblingIndex(ribbon.GetSiblingIndex());

            var goalsRow = UiFactory.Rect("Goals", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -370), new Vector2(-150, 216));
            var (icons, counts, checks) = GoalChips(goalsRow, "ui_tile", Color.white, Vector2.zero, Vector2.zero, 105, 47, Ink, centred: true);
            var extra = UiFactory.Rect("ExtraMoves", panel, Top, Top, new Vector2(0, -620), new Vector2(471, 144));
            extra.localRotation = Quaternion.Euler(0f, 0f, -3f);
            Sliced(extra, "ui_pill", Color.white, 1.3f);
            var extraText = Lettered("Moves", extra, "+5", 83, PillOutline, Centre, new Vector2(-70, 6), new Vector2(160, 120));
            Lettered("Label", extra, "EXTRA\nMOVES", 42, PillOutline, Centre, new Vector2(80, 0), new Vector2(200, 120));

            var settings = UiFactory.Rect("Settings", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -340), new Vector2(-110, 300));
            Button Toggle(string name, string icon, float slot)
            {
                var cell = UiFactory.Rect(name + "Cell", settings, new Vector2(slot / 4f, 0f), new Vector2((slot + 1) / 4f, 1f), Vector2.zero, Vector2.zero);
                var toggle = KitButton(name, cell, "ui_button_white", Top, Top, new Vector2(0, -95), new Vector2(172, 172), borderScale: 1.3f);
                Icon(toggle.transform, icon, Centre, new Vector2(0, 6), 92, Color.white);
                Label("Label", cell, name, 36, MutedOnCream, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(230, 90)).textWrappingMode = TextWrappingModes.Normal;
                return toggle;
            }

            var sound = Toggle("Sound", "icon_sound", 0);
            var music = Toggle("Music", "icon_music", 1);
            var haptics = Toggle("Vibration", "icon_vibration", 2);
            var motion = Toggle("Motion", "icon_motion", 3);

            var next = WideButton("Next", panel, "ui_button_yellow", 188, 85, "Next level", 75);
            var cont = WideButton("Continue", panel, "ui_button_green", 200, 70, "Keep going", 75);
            var contLabel = cont.GetComponentInChildren<TMP_Text>();
            ((RectTransform)contLabel.transform).anchoredPosition = new Vector2(-150, 8);
            ((RectTransform)contLabel.transform).sizeDelta = new Vector2(440, 150);
            FitWidth(contLabel, 40);
            var price = UiFactory.Rect("Price", cont.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-170, 6), new Vector2(230, 105));
            Sliced(price, "ui_card", Color.white, 1.6f);
            Icon(price, "icon_coin", new Vector2(0f, 0.5f), new Vector2(50, 0), 83, Color.white);
            var priceText = Label("Amount", price, "100", 55, Ink, Centre, new Vector2(35, 4), new Vector2(150, 90));
            var resume = WideButton("Resume", panel, "ui_button_green", 194, 80, "Resume", 78);
            var retry = WideButton("Retry", panel, "ui_button_white", 150, 120, "Try again", 55, 1.25f);
            var home = WideButton("Home", panel, "ui_button_white", 150, 120, "Home", 55, 1.25f);
            UiFactory.IconBeforeLabel(home, KitArt.Load("icon_home"), 72);
            var levels = WideButton("Levels", panel, "ui_button_white", 150, 120, "Levels", 55, 1.25f);
            UiFactory.IconBeforeLabel(levels, KitArt.Load("icon_levels"), 72);
            var giveUp = GiveUp(panel);

            var view = canvas.gameObject.AddComponent<Match3ResultView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_skin", skin);
            Assign(view, "_feel", feel);
            Assign(view, "_group", group);
            Assign(view, "_panel", panel);
            Assign(view, "_ribbon", ribbonImage);
            Assign(view, "_happyRibbon", KitArt.Load("ui_ribbon"));
            Assign(view, "_sadRibbon", KitArt.Load("ui_ribbon"));
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
            Label("Label", button.transform, "Give up", 47, Muted, Centre, new Vector2(-90, 0), new Vector2(260, 80), TextAlignmentOptions.Right);
            Icon(button.transform, "icon_heart", Centre, new Vector2(70, 0), 61, Color.white);
            Label("Cost", button.transform, "-1", 47, Warning, Centre, new Vector2(140, 0), new Vector2(90, 80), TextAlignmentOptions.Left);
            return button;
        }

        // Up to four goal chips across `parent` between the two offsets: an icon, a count under it, and a check that replaces
        // the count when the goal is done. Spread chips fill four slots from the left; centred ones close up around the middle.
        private static (Object[] Icons, Object[] Counts, Object[] Checks) GoalChips(RectTransform parent, string chipSprite, Color chipTint, Vector2 offsetMin,
            Vector2 offsetMax, float iconSize, float countSize, Color countColour, bool checks = true, bool centred = false)
        {
            var area = UiFactory.Stretch("Chips", parent);
            area.offsetMin = offsetMin;
            area.offsetMax = offsetMax;
            if (centred)
            {
                var layout = area.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 28;
                layout.childControlWidth = layout.childControlHeight = false;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            }

            var icons = new Object[GoalSlots];
            var counts = new Object[GoalSlots];
            var done = new Object[GoalSlots];
            for (var i = 0; i < GoalSlots; i++)
            {
                var chip = centred
                    ? UiFactory.Rect($"Goal{i}", area, Centre, Centre, Vector2.zero, new Vector2(183, 216))
                    : UiFactory.Rect($"Goal{i}", area, new Vector2(i / (float)GoalSlots, 0f), new Vector2((i + 1) / (float)GoalSlots, 1f), Vector2.zero,
                        new Vector2(-22, 0));
                if (chipSprite != null) Sliced(chip, chipSprite, chipTint, 1.2f);
                var icon = UiFactory.Rect("Icon", chip, Centre, Centre, new Vector2(0, countSize * 0.55f), new Vector2(iconSize, iconSize));
                icons[i] = UiFactory.Image(icon, null, Color.white);
                var countPos = new Vector2(0, -iconSize * 0.5f - 6);
                counts[i] = Label("Count", chip, "0", countSize, countColour, Centre, countPos, new Vector2(iconSize * 1.4f, countSize * 1.3f));
                if (!checks) continue;
                done[i] = Icon(icon, "icon_check", Centre, new Vector2(iconSize * 0.4f, iconSize * 0.4f), countSize * 1.25f, Color.white).gameObject;
            }

            return (icons, counts, done);
        }
    }
}
