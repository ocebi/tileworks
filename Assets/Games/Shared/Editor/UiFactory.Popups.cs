using Tileworks.Kit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworlds.Games.Editor
{
    public static partial class UiFactory
    {
        public static (Canvas Canvas, CanvasGroup Group, RectTransform Panel) Popup(string name, int order, Vector2 size)
        {
            var canvas = Canvas(name, order);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            var dim = Stretch("Dim", canvas.transform);
            Image(dim, null, new Color(0.04f, 0.03f, 0.1f, 0.6f), raycast: true);
            var panel = Rect("Panel", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Sliced(panel, "ui_panel");
            return (canvas, group, panel);
        }

        // A ribbon across the top edge of a popup panel, with the title lettered on it; long titles shrink to fit.
        public static TMP_Text PopupTitle(RectTransform panel, string title)
        {
            var ribbon = Rect("Ribbon", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(700, 180));
            Sliced(ribbon, "ui_ribbon");
            var text = Lettered("Title", ribbon, title, 72, RibbonOutline, Vector2.zero, Vector2.one, new Vector2(0, 8), new Vector2(-80, 0));
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 40;
            text.fontSizeMax = 72;
            return text;
        }

        // Win, lose and pause in one panel; the view type declares the fields assigned here. The panel stacks whatever is shown
        // under the ribbon and fits its height to it.
        public static T ResultPopup<T>(bool score = false) where T : Component
        {
            var (canvas, group, panel) = Popup("ResultCanvas", 300, new Vector2(860, 1080));
            var title = PopupTitle(panel, "Level Complete!");
            var ribbon = title.transform.parent;
            ribbon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 140, 80);
            layout.spacing = 34;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var top = new Vector2(0.5f, 1f);
            var win = WinReveal(panel, score);
            var body = Text("Body", panel, "", 48, MutedInk, top, top, Vector2.zero, new Vector2(760, 150));
            var settings = Rect("Settings", panel, top, top, Vector2.zero, new Vector2(860, 140));
            Button Toggle(string name, float x) =>
                Button(name, settings, "White", name, new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(200, 130), fontSize: 30);
            var sound = Toggle("Sound", -330);
            var music = Toggle("Music", -110);
            var haptics = Toggle("Vibration", 110);
            var motion = Toggle("Motion", 330);
            Button Row(string name, string colour, string label) => Button(name, panel, colour, label, top, Vector2.zero, new Vector2(600, 130));
            var cont = Row("Continue", "Green", "Continue");
            var next = Row("Next", "Green", "Next Level");
            var resume = Row("Resume", "Blue", "Resume");
            var retry = Row("Retry", "Yellow", "Retry");
            var home = Row("Home", "White", "Home");
            var levels = Row("Levels", "White", "Levels");
            IconBeforeLabel(levels, KitArt.Load("icon_levels"), 56);
            ribbon.SetAsLastSibling();

            var view = canvas.gameObject.AddComponent<T>();
            AssignMotion(view);
            SceneParts.Assign(view, "_group", group);
            SceneParts.Assign(view, "_panel", panel);
            SceneParts.Assign(view, "_title", title);
            SceneParts.Assign(view, "_body", body);
            SceneParts.Assign(view, "_win", win);
            SceneParts.Assign(view, "_next", next);
            SceneParts.Assign(view, "_retry", retry);
            SceneParts.Assign(view, "_home", home);
            SceneParts.Assign(view, "_levels", levels);
            SceneParts.Assign(view, "_continue", cont);
            SceneParts.Assign(view, "_resume", resume);
            SceneParts.Assign(view, "_continueLabel", cont.GetComponentInChildren<TMP_Text>());
            SceneParts.Assign(view, "_settingsRow", settings.gameObject);
            SceneParts.Assign(view, "_sound", sound);
            SceneParts.Assign(view, "_music", music);
            SceneParts.Assign(view, "_haptics", haptics);
            SceneParts.Assign(view, "_motion", motion);
            return view;
        }

        // The top of a win popup: the level number on a star in a turning glow with sparkles, then an optional score row and the
        // coin reward, each a pill with an icon. Its height fits what it holds, so it stacks in a layout group.
        public static WinReveal WinReveal(Transform parent, bool score)
        {
            const float hero = 380f, row = 116f, gap = 26f;
            var top = new Vector2(0.5f, 1f);
            var root = Rect("Win", parent, top, top, Vector2.zero, new Vector2(760, hero + (score ? row + gap : 0f) + row));
            root.pivot = top;
            var heroRect = Rect("Hero", root, top, top, new Vector2(0, -hero * 0.5f), new Vector2(hero, hero));
            NestedCanvas(heroRect, clickable: false);
            var glow = Image(Rect("Glow", heroRect, Centre, Centre, Vector2.zero, new Vector2(740, 740)), KitArt.Load("ui_rays"), Color.white);
            var spots = new[] { new Vector3(-215, 100, 92), new Vector3(200, 140, 70), new Vector3(228, -40, 96), new Vector3(-238, -90, 64), new Vector3(-112, 190, 56),
                new Vector3(128, -178, 62) };
            var sparkles = new Object[spots.Length];
            for (var i = 0; i < spots.Length; i++)
                sparkles[i] = Image(Rect($"Sparkle{i}", heroRect, Centre, Centre, spots[i], Vector2.one * spots[i].z), KitArt.Load("ui_sparkle"), Color.white).transform;
            var star = Rect("Star", heroRect, Centre, Centre, new Vector2(0, 4), new Vector2(345, 345));
            Image(star, KitArt.Load("ui_star"), Color.white);
            var level = Lettered("Level", star, "1", 118, RibbonOutline, Centre, Centre, new Vector2(0, -13), new Vector2(194, 140));
            level.textWrappingMode = TextWrappingModes.NoWrap;
            level.enableAutoSizing = true;
            level.fontSizeMin = 60;
            level.fontSizeMax = 118;

            (RectTransform Rect, TMP_Text Count, RectTransform Icon) Pill(string name, float y, string icon)
            {
                var rect = Rect(name, root, top, top, new Vector2(0, y), new Vector2(440, row));
                Sliced(rect, "ui_pill", Mathf.Max(1f, KitArt.MinHeight("ui_pill") * 1.1f / row));
                var iconRect = Rect("Icon", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24, 4), new Vector2(136, 136));
                Image(iconRect, KitArt.Load(icon), Color.white);
                return (rect, Lettered("Count", rect, "0", 68, PillOutline, Centre, Centre, new Vector2(34, 2), new Vector2(320, 100)), iconRect);
            }

            var reveal = root.gameObject.AddComponent<WinReveal>();
            var y = -hero - row * 0.5f;
            if (score)
            {
                var scoreRow = Pill("Score", y, "icon_trophy");
                SceneParts.Assign(reveal, "_scoreRow", scoreRow.Rect);
                SceneParts.Assign(reveal, "_score", scoreRow.Count);
                y -= row + gap;
            }

            var coins = Pill("Coins", y, "icon_coin");
            AssignMotion(reveal);
            SceneParts.Assign(reveal, "_glow", glow);
            SceneParts.Assign(reveal, "_hero", heroRect);
            SceneParts.Assign(reveal, "_star", star);
            SceneParts.Assign(reveal, "_level", level);
            SceneParts.AssignArray(reveal, "_sparkles", sparkles);
            SceneParts.Assign(reveal, "_coinRow", coins.Rect);
            SceneParts.Assign(reveal, "_coins", coins.Count);
            SceneParts.Assign(reveal, "_coinIcon", coins.Icon);
            SceneParts.Assign(reveal, "_starBurst", UiBursts.StarBurst(root));
            SceneParts.Assign(reveal, "_coinBurst", UiBursts.Coins(root));
            // Confetti spans the whole screen, above the popup.
            SceneParts.Assign(reveal, "_confetti", UiBursts.Confetti(parent.GetComponentInParent<Canvas>().rootCanvas.transform));
            return reveal;
        }
    }
}
