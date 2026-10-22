using System;
using System.IO;
using System.Linq;
using Hub.Installer;
using Hub.View;
using Tileworlds.Games.Editor;
using Tileworks.Kit.App;
using Tileworks.Kit.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static Tileworlds.Games.Editor.SceneParts;

namespace Hub.Editor
{
    // Rebuilds the hub scenes from code and puts them first in the build, so a build with every game opens on the hub.
    public static class HubSceneBuilder
    {
        private const string Root = "Assets/Games/Hub";
        private const string Art = Root + "/Art";
        private const string Config = Root + "/Config";
        private const string Scenes = Root + "/Scenes";
        private const string AppScene = Scenes + "/HubApp.unity";
        private const string HubScene = Scenes + "/" + HubAppInstaller.HubScene + ".unity";
        private const string InstallerPath = Config + "/HubAppInstaller.asset";
        private const string FeelPath = Config + "/HubFeel.asset";
        private const string CatalogPath = "Assets/Games/Shared/GameCatalog.asset";
        private const string Wordmark = "Tileworlds";
        private static readonly Color Background = new Color32(20, 24, 56, 255);
        // The boot screen continues the iOS launch screen (ProjectSetup), in Block Puzzle's purple.
        private static readonly Color LaunchPurple = new Color32(0x36, 0x28, 0x7A, 255);
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 CardSize = new Vector2(280, 301);
        private const float CardSpacing = 300f;
        private const float RowWidth = 1000f;

        // Each game as its Home shows it, matched to the catalog by title.
        private static readonly Look[] Looks =
        {
            new Look("Tile Match", "Tile Match", UiFactory.PillOutline, "hub_card_tilematch", "Assets/Games/TileMatch/Art/Backgrounds/bg_tm_home.png",
                "Assets/Games/TileMatch/Art/UI/char_bear.png", -6f),
            new Look("Block Puzzle", "Block Puzzle", new Color32(42, 20, 96, 255), "hub_card_blockpuzzle",
                "Assets/Games/BlockPuzzle/Art/Backgrounds/bg_bp_home.png", "Assets/Games/BlockPuzzle/Art/UI/char_block.png", 6f),
            new Look("Blast", "Blast!", new Color32(90, 20, 15, 255), "hub_card_blast", "Assets/Games/Blast/Art/Design/Backgrounds/bg_blast_home.png",
                "Assets/Games/Blast/Art/Design/UI/char_cube.png", -4f),
            new Look("Gem Swap", "Gem Swap", new Color32(13, 71, 102, 255), "hub_card_match3", "Assets/Games/Match3/Art/Design/Backgrounds/bg_match3_home.png",
                "Assets/Games/Match3/Art/Design/UI/char_star.png", -4f),
        };

        private readonly struct Look
        {
            public readonly string Game;
            public readonly string Title;
            public readonly Color Outline;
            public readonly string Card;
            public readonly string Backdrop;
            public readonly string Mascot;
            public readonly float Tilt;

            public Look(string game, string title, Color outline, string card, string backdrop, string mascot, float tilt)
            {
                Game = game;
                Title = title;
                Outline = outline;
                Card = card;
                Backdrop = backdrop;
                Mascot = mascot;
                Tilt = tilt;
            }
        }

        [MenuItem("Tileworks/Hub/Rebuild Scenes")]
        public static void Build()
        {
            foreach (var folder in new[] { Config, Scenes }) Directory.CreateDirectory(folder);
            LoadOrCreate<HubAppInstaller>(InstallerPath);
            LoadOrCreate<HubFeel>(FeelPath);
            var catalog = LoadOrCreate<GameCatalog>(CatalogPath);
            catalog.SetHub(AppScene);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            // A new scene unloads assets nothing references, so each scene loads its own after creating it.
            BuildAppScene();
            BuildHubScene();
            SetBuildScenes(Scenes);
            AssetDatabase.SaveAssets();
            Debug.Log("[Tileworks] Hub scenes rebuilt");
        }

        private static void BuildAppScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("AppRoot").AddComponent<AppRoot>();
            Assign(root, "_installer", Load<HubAppInstaller>(InstallerPath));
            Assign(root, "_games", Load<GameCatalog>(CatalogPath));
            UiFactory.Font = UiFactory.DisplayFont();
            Assign(root, "_loading", UiFactory.LoadingScreen(Wordmark, LaunchPurple));
            MusicSetup.Add(root);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            new GameObject("AudioListener", typeof(AudioListener));
            EditorSceneManager.SaveScene(scene, AppScene);
        }

        private static void BuildHubScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = Load<GameCatalog>(CatalogPath);
            UiFactory.BeginScene();
            Camera("HubCamera", Background);
            var hubCanvas = UiFactory.Canvas("HubCanvas", 0);
            var canvas = hubCanvas.transform;

            var backdrop = UiFactory.Stretch("Backdrop", canvas);
            var back = Cover("Back", backdrop);
            var front = Cover("Front", backdrop);
            front.enabled = false;
            UiFactory.Image(UiFactory.Rect("Shade", canvas, Vector2.zero, new Vector2(1f, 0.46f), Vector2.zero, Vector2.zero), HubSprite("hub_shade"),
                new Color(0f, 0f, 0f, 0.55f));
            var safe = UiFactory.Safe(hubCanvas);
            var wordmark = BuildWordmark(safe);

            var games = catalog.Games;
            var titles = new TMP_Text[games.Count];
            var mascots = new RectTransform[games.Count];
            for (var i = 0; i < games.Count; i++)
            {
                var look = LookOf(games[i]);
                titles[i] = UiFactory.HomeTitle(safe, look.Title ?? games[i].Title, look.Title != null ? look.Outline : UiFactory.PillOutline);
                mascots[i] = BuildMascot(safe, look);
            }

            var controls = UiFactory.Stretch("Controls", safe);
            var group = controls.gameObject.AddComponent<CanvasGroup>();
            var slots = new (RectTransform Slot, Button Card, Image Glow, RectTransform Badge, RectTransform Name)[games.Count];

            // A row too wide for the screen shrinks around the cards' line.
            var cards = UiFactory.Stretch("Cards", controls);
            cards.pivot = new Vector2(0.5f, 0.275f);
            cards.localScale = Vector3.one * Mathf.Min(1f, RowWidth / (games.Count * CardSpacing));
            for (var i = 0; i < games.Count; i++)
                slots[i] = BuildCard(cards, games[i], LookOf(games[i]), (i - (games.Count - 1) * 0.5f) * CardSpacing);
            var play = UiFactory.Button("Play", controls, "Green", "Play", new Vector2(0.5f, 0.09f), Vector2.zero, new Vector2(640, 190), fontSize: 84);
            var pulse = UiFactory.Pulse(play);

            var view = new GameObject("HubView").AddComponent<HubView>();
            var so = new SerializedObject(view);
            var list = so.FindProperty("_games");
            list.arraySize = games.Count;
            for (var i = 0; i < games.Count; i++)
            {
                var game = list.GetArrayElementAtIndex(i);
                var look = LookOf(games[i]);
                game.FindPropertyRelative("BootScene").stringValue = games[i].BootScene;
                game.FindPropertyRelative("Backdrop").objectReferenceValue = look.Backdrop != null ? UiFactory.BackdropSprite(look.Backdrop) : null;
                game.FindPropertyRelative("Title").objectReferenceValue = titles[i].gameObject;
                game.FindPropertyRelative("Mascot").objectReferenceValue = mascots[i];
                game.FindPropertyRelative("Slot").objectReferenceValue = slots[i].Slot;
                game.FindPropertyRelative("Card").objectReferenceValue = slots[i].Card;
                game.FindPropertyRelative("Glow").objectReferenceValue = slots[i].Glow;
                game.FindPropertyRelative("Badge").objectReferenceValue = slots[i].Badge;
                game.FindPropertyRelative("Name").objectReferenceValue = slots[i].Name;
            }

            so.FindProperty("_feel").objectReferenceValue = Load<HubFeel>(FeelPath);
            so.FindProperty("_back").objectReferenceValue = back;
            so.FindProperty("_front").objectReferenceValue = front;
            so.FindProperty("_wordmark").objectReferenceValue = wordmark;
            so.FindProperty("_controls").objectReferenceValue = group;
            so.FindProperty("_play").objectReferenceValue = play;
            so.FindProperty("_playPulse").objectReferenceValue = pulse;
            so.FindProperty("_uiMotion").objectReferenceValue = UiFactory.Motion;
            so.ApplyModifiedPropertiesWithoutUndo();

            var rootGo = new GameObject("SceneRoot");
            var installer = rootGo.AddComponent<HubSceneInstaller>();
            Assign(installer, "_view", view);
            AddSceneRoot(rootGo, AppScene, installer);
            EditorSceneManager.SaveScene(scene, HubScene);
        }

        // Fills the screen, cropping instead of stretching; the view sets the sprite and its aspect.
        private static Image Cover(string name, Transform parent)
        {
            var rect = UiFactory.Stretch(name, parent);
            var image = UiFactory.Image(rect, null, Color.white);
            rect.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            return image;
        }

        private static RectTransform BuildWordmark(Transform canvas)
        {
            var wordmark = UiFactory.Rect("Wordmark", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(600f, 130f));
            UiFactory.Image(UiFactory.Rect("Mark", wordmark, Centre, Centre, new Vector2(-205f, 0f), new Vector2(105f, 105f)), HubSprite("hub_mark"), Color.white);
            UiFactory.Lettered("Name", wordmark, Wordmark, 86, UiFactory.PillOutline, Centre, Centre, new Vector2(65f, 0f), new Vector2(430f, 120f));
            return wordmark;
        }

        // The parent pivots at its feet so breathing grows the mascot upwards; the tilted picture inside pops in.
        private static RectTransform BuildMascot(Transform canvas, Look look)
        {
            var size = new Vector2(440f, 440f);
            var parent = UiFactory.Rect("Mascot", canvas, Centre, Centre, new Vector2(0f, -size.y * 0.5f), size);
            parent.pivot = new Vector2(0.5f, 0f);
            var sprite = look.Mascot != null ? ArtImport.Sprite(look.Mascot, 100) : null;
            UiFactory.Mascot(parent, sprite, Centre, Vector2.zero, size.x, look.Tilt);
            return parent;
        }

        private static (RectTransform, Button, Image, RectTransform, RectTransform) BuildCard(Transform controls, GameEntry game, Look look, float x)
        {
            var anchor = new Vector2(0.5f, 0.275f);
            var slot = UiFactory.Rect("Slot " + game.Title, controls, anchor, anchor, new Vector2(x, 0f), CardSize);
            var glow = UiFactory.Image(UiFactory.Rect("Glow", slot, Centre, Centre, Vector2.zero, new Vector2(350f, 371f)), HubSprite("hub_card_glow"), Color.white);

            var cardRect = UiFactory.Rect("Card", slot, Centre, Centre, Vector2.zero, CardSize);
            var image = UiFactory.Image(cardRect, look.Card != null ? HubSprite(look.Card) : null, Color.white, raycast: true);
            var card = cardRect.gameObject.AddComponent<Button>();
            card.targetGraphic = image;
            var feedback = cardRect.gameObject.AddComponent<PressFeedback>();
            Assign(feedback, "_uiMotion", UiFactory.Motion);
            Assign(feedback, "_selectable", card);

            var badge = UiFactory.Rect("Badge", slot, Centre, Centre, new Vector2(120f, 130f), new Vector2(90f, 90f));
            UiFactory.Image(badge, KitArt.Load("ui_badge_green"), Color.white);
            UiFactory.Image(UiFactory.Rect("Check", badge, Centre, Centre, Vector2.zero, new Vector2(54f, 54f)), KitArt.Load("icon_check"), Color.white);
            var name = UiFactory.Lettered("Name", slot, game.Title, 44, UiFactory.Ink, Centre, Centre, new Vector2(0f, -190f), new Vector2(360f, 60f));
            return (slot, card, glow, badge, name.rectTransform);
        }

        private static Look LookOf(GameEntry game) => Array.Find(Looks, l => l.Game == game.Title);

        private static Sprite HubSprite(string name) => ArtImport.Sprite($"{Art}/{name}.png", 100);
    }
}
