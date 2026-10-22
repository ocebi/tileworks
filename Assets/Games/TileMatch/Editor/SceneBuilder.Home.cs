using Tileworlds.Games.Editor;
using static Tileworlds.Games.Editor.SceneParts;
using TileMatch.Installer;
using TileMatch.View;
using Tileworks.Kit.App;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.Editor
{
    // Home scene: top bar (coins, lives), play, shop and stats popups.
    public static partial class SceneBuilder
    {
        private static void BuildHomeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UiFactory.BeginScene();
            UiFactory.Backdrop(Camera("HomeCamera", Background), UiFactory.BackdropSprite(HomeBackdrop));
            var canvas = UiFactory.Canvas("HomeCanvas", 100);
            var root = UiFactory.Safe(canvas);
            var (coins, lives, timer) = UiFactory.CoinsAndLives(root);

            var title = UiFactory.HomeTitle(root, "Tile Match");
            // The level path runs between the title and Play; the mascot peeks out behind the current node.
            var mascot = UiFactory.Mascot(root, ArtImport.Sprite(BearPath, 100), Centre, Vector2.zero, 280, -6f);
            var path = LevelPathScreen.Build(UiFactory.Rect("LevelPath", root, new Vector2(0.03f, 0.40f), new Vector2(0.97f, 0.62f), Vector2.zero, Vector2.zero),
                mascot.rectTransform);
            var play = UiFactory.Button("Play", root, "Green", "Play", new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(560, 190), fontSize: 76);
            var level = UiFactory.Sublabel(play, "Level 1", 36, 20);
            var shop = UiFactory.Button("Shop", root, "Yellow", "Shop", new Vector2(0.5f, 0.1f), new Vector2(-220, 0), new Vector2(380, 150));
            var row = LevelListScreen.ButtonRow(root, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(1000, 130), 40);
            var levels = UiFactory.IconLabelButton("Levels", row, "White", "icon_levels", "Levels", Centre, Vector2.zero, new Vector2(360, 120), 64, 46);
            var switchGame = UiFactory.IconLabelButton("SwitchGame", row, "White", "icon_games", "All Games", Centre, Vector2.zero, new Vector2(360, 120), 64, 46);
            var gear = SettingsScreen.Gear(row, Centre, Vector2.zero);
            var stats = UiFactory.Button("Stats", root, "Blue", "Stats", new Vector2(0.5f, 0.1f), new Vector2(220, 0), new Vector2(380, 150));

            UiFactory.Entrance(canvas, new Transform[]
            {
                title.transform, coins.transform.parent, lives.transform.parent, mascot.transform, levels.transform, switchGame.transform, gear.transform,
                shop.transform, stats.transform,
            }, mascot.transform);
            var view = canvas.gameObject.AddComponent<HomeView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_level", level);
            Assign(view, "_play", play);
            Assign(view, "_playPulse", UiFactory.Pulse(play));
            Assign(view, "_coins", coins);
            Assign(view, "_lives", lives);
            Assign(view, "_livesTimer", timer);
            Assign(view, "_shop", shop);
            Assign(view, "_stats", stats);
            Assign(view, "_switchGame", switchGame);

            var shopView = BuildShop();
            var statsView = BuildStats();
            var levelList = LevelListScreen.Build(levels);

            var rootGo = new GameObject("SceneRoot");
            var installer = rootGo.AddComponent<HomeSceneInstaller>();
            Assign(installer, "_home", view);
            Assign(installer, "_shop", shopView);
            Assign(installer, "_stats", statsView);
            Assign(installer, "_levels", levelList);
            Assign(installer, "_levelPath", path);
            Assign(installer, "_settings", SettingsScreen.Build(gear));
            SceneParts.AddSceneRoot(rootGo, $"{Scenes}/App.unity", installer);
            EditorSceneManager.SaveScene(scene, $"{Scenes}/Home.unity");
        }

        private static ShopView BuildShop()
        {
            var (canvas, group, panel) = UiFactory.Popup("ShopCanvas", 300, new Vector2(880, 1240));
            UiFactory.PopupTitle(panel, "Shop");
            var items = new Button[6];
            var labels = new TMP_Text[6];
            for (var i = 0; i < items.Length; i++)
            {
                items[i] = UiFactory.Button("Item" + i, panel, i == 4 ? "Red" : "Yellow", "item", new Vector2(0.5f, 1f), new Vector2(0, -250 - i * 150), new Vector2(720, 130), fontSize: 46);
                labels[i] = items[i].GetComponentInChildren<TMP_Text>();
            }

            var close = UiFactory.Button("Close", panel, "White", "Close", new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(420, 130));
            var view = canvas.gameObject.AddComponent<ShopView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_group", group);
            Assign(view, "_panel", panel);
            AssignArray(view, "_items", items);
            AssignArray(view, "_labels", labels);
            Assign(view, "_close", close);
            return view;
        }

        private static StatsView BuildStats()
        {
            var (canvas, group, panel) = UiFactory.Popup("StatsCanvas", 300, new Vector2(880, 1240));
            var title = UiFactory.PopupTitle(panel, "Stats");
            var body = UiFactory.Text("Body", panel, string.Empty, 40, UiFactory.MutedInk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(760, 860));
            body.alignment = TextAlignmentOptions.TopLeft;
            var close = UiFactory.Button("Close", panel, "White", "Close", new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(420, 130));
            var view = canvas.gameObject.AddComponent<StatsView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_group", group);
            Assign(view, "_panel", panel);
            Assign(view, "_title", title);
            Assign(view, "_body", body);
            Assign(view, "_close", close);
            return view;
        }
    }
}
