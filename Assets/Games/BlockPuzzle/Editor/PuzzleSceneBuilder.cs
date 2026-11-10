using System.IO;
using BlockPuzzle.Installer;
using BlockPuzzle.View;
using Tileworlds.Games.Editor;
using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using static Tileworlds.Games.Editor.SceneParts;

namespace BlockPuzzle.Editor
{
    // Rebuilds the Block Puzzle scenes and config assets from code; hand edits to the generated scenes are overwritten.
    public static class PuzzleSceneBuilder
    {
        private const string Game = "Assets/Games/BlockPuzzle";
        private const string Config = Game + "/Config";
        private const string Scenes = Game + "/Scenes";
        private const string AppScene = Scenes + "/BlockApp.unity";
        private const string SpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        // The backdrops' bottom edge colour, so anything outside them still matches.
        private static readonly Color Background = new Color32(30, 20, 80, 255);
        private static readonly Color TitleOutline = new Color32(42, 20, 96, 255);
        private const string HomeBackdrop = PuzzleArtBuilder.Folder + "/Backgrounds/bg_bp_home.png";
        private const string GameBackdrop = PuzzleArtBuilder.Folder + "/Backgrounds/bg_bp_game.png";
        private const string HeroPath = PuzzleArtBuilder.Folder + "/UI/hero_blocks.png";

        [MenuItem("Tileworks/Block Puzzle/Rebuild Scenes")]
        public static void Build()
        {
            foreach (var folder in new[] { Config, Scenes }) Directory.CreateDirectory(folder);
            KitArt.Import();
            PuzzleArtBuilder.Build();
            UiFactory.Font = UiFactory.DisplayFont();
            var skin = LoadOrCreate<PuzzleSkin>($"{Config}/PuzzleSkin.asset");
            skin.Configure(Load<Sprite>($"{PuzzleArtBuilder.Folder}/block.png"), Load<Sprite>($"{PuzzleArtBuilder.Folder}/cell.png"),
                Load<Sprite>($"{PuzzleArtBuilder.Folder}/gem.png"), Load<Sprite>($"{PuzzleArtBuilder.Folder}/frame.png"), Load<Material>(SpriteMaterial),
                PuzzleArtBuilder.Colours);
            EditorUtility.SetDirty(skin);
            LoadOrCreate<PuzzleFeelConfig>($"{Config}/PuzzleFeel.asset");
            var sounds = LoadOrCreate<PuzzleSounds>($"{Config}/PuzzleSounds.asset");
            sounds.Configure(new[]
            {
                Clip(PuzzleCue.Pick, "click_002", 0.5f), Clip(PuzzleCue.Place, "drop_002", 0.8f), Clip(PuzzleCue.Clear, "confirmation_002", 0.9f),
                Clip(PuzzleCue.Gem, "glass_001", 0.8f), Clip(PuzzleCue.Deal, "pluck_001", 0.6f), Clip(PuzzleCue.Nope, "error_004", 0.5f),
                Clip(PuzzleCue.Win, "jingles_PIZZI10", 0.9f), Clip(PuzzleCue.Lose, "jingles_HIT07", 0.8f),
            });
            EditorUtility.SetDirty(sounds);
            var pack = File.Exists(PuzzleContentBuilder.SequencePath) ? PuzzleContentBuilder.UpdatePack() : null;
            if (pack == null)
            {
                PuzzleContentBuilder.GenerateAll();
                pack = PuzzleContentBuilder.UpdatePack();
            }

            var installer = LoadOrCreate<BlockPuzzleAppInstaller>($"{Config}/BlockPuzzleAppInstaller.asset");
            installer.SetLevels(pack);
            installer.SetRemoteLevels(Load<Tileworks.Tiles.View.RemoteLevelsConfig>("Assets/Games/Shared/Config/RemoteLevels.asset"));
            EditorUtility.SetDirty(installer);
            AssetDatabase.SaveAssets();

            BuildAppScene();
            BuildHomeScene();
            BuildGameScene();
            SetBuildScenes(Scenes, AppScene, $"{Scenes}/{BlockPuzzleAppInstaller.HomeScene}.unity", $"{Scenes}/{BlockPuzzleAppInstaller.GameScene}.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[Tileworks] Block Puzzle scenes rebuilt");
        }

        private static void BuildAppScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("AppRoot").AddComponent<AppRoot>();
            Assign(root, "_installer", Load<BlockPuzzleAppInstaller>($"{Config}/BlockPuzzleAppInstaller.asset"));
            Assign(root, "_games", RegisterGame("Block Puzzle", AppScene));
            UiFactory.Font = UiFactory.DisplayFont();
            Assign(root, "_loading", UiFactory.LoadingScreen("Block Puzzle", Background, UiFactory.BackdropSprite(HomeBackdrop), TitleOutline));
            MusicSetup.Add(root);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            new GameObject("AudioListener", typeof(AudioListener));
            EditorSceneManager.SaveScene(scene, AppScene);
        }

        private static void BuildHomeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UiFactory.BeginScene();
            UiFactory.Backdrop(Camera("HomeCamera", Background), UiFactory.BackdropSprite(HomeBackdrop));
            var canvas = UiFactory.Canvas("HomeCanvas", 100);
            var root = UiFactory.Safe(canvas);
            var (coins, lives, timer) = UiFactory.CoinsAndLives(root);
            var title = UiFactory.HomeTitle(root, "Block Puzzle", TitleOutline);
            var centre = new Vector2(0.5f, 0.5f);
            // The level path runs between the title and Play; the mascot peeks out behind the current node.
            var mascot = UiFactory.Mascot(root, ArtImport.Sprite(HeroPath, 100), centre, Vector2.zero, 280, 0f);
            var path = LevelPathScreen.Build(UiFactory.Rect("LevelPath", root, new Vector2(0.03f, 0.40f), new Vector2(0.97f, 0.62f), Vector2.zero, Vector2.zero),
                mascot.rectTransform);
            var play = UiFactory.Button("Play", root, "Green", "Play", new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(560, 190), fontSize: 76);
            var level = UiFactory.Sublabel(play, "Level 1", 36, 20);
            var row = LevelListScreen.ButtonRow(root, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(1000, 130), 40);
            var levels = UiFactory.IconLabelButton("Levels", row, "White", "icon_levels", "Levels", centre, Vector2.zero, new Vector2(360, 120), 64, 46);
            var switchGame = UiFactory.IconLabelButton("SwitchGame", row, "White", "icon_games", "All Games", centre, Vector2.zero, new Vector2(360, 120), 64, 46);
            var gear = SettingsScreen.Gear(row, centre, Vector2.zero);

            UiFactory.Entrance(canvas, new Transform[]
            {
                title.transform, coins.transform.parent, lives.transform.parent, mascot.transform, levels.transform, switchGame.transform, gear.transform,
            }, mascot.transform);
            var view = canvas.gameObject.AddComponent<PuzzleHomeView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_level", level);
            Assign(view, "_play", play);
            Assign(view, "_playPulse", UiFactory.Pulse(play));
            Assign(view, "_coins", coins);
            Assign(view, "_lives", lives);
            Assign(view, "_livesTimer", timer);
            Assign(view, "_switchGame", switchGame);

            var rootGo = new GameObject("SceneRoot");
            var installer = rootGo.AddComponent<PuzzleHomeSceneInstaller>();
            Assign(installer, "_home", view);
            Assign(installer, "_levels", LevelListScreen.Build(levels));
            Assign(installer, "_levelPath", path);
            Assign(installer, "_settings", SettingsScreen.Build(gear));
            AddSceneRoot(rootGo, AppScene, installer);
            EditorSceneManager.SaveScene(scene, $"{Scenes}/{BlockPuzzleAppInstaller.HomeScene}.unity");
        }

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UiFactory.BeginScene();
            var camera = Camera("BoardCamera", Background);
            UiFactory.Backdrop(camera, UiFactory.BackdropSprite(GameBackdrop));

            var sfx = new GameObject("Sfx").AddComponent<PuzzleSfx>();
            Assign(sfx, "_sounds", Load<PuzzleSounds>($"{Config}/PuzzleSounds.asset"));

            var callout = new GameObject("Callout").AddComponent<TextMeshPro>();
            if (UiFactory.Font != null) callout.font = UiFactory.Font;
            callout.fontSize = 9;
            callout.alignment = TextAlignmentOptions.Center;
            callout.rectTransform.sizeDelta = new Vector2(10f, 2f);
            callout.sortingOrder = 200;
            callout.color = new Color32(255, 230, 120, 255);
            callout.outlineWidth = 0.2f;

            var board = new GameObject("Board").AddComponent<PuzzleBoardView>();
            Assign(board, "_camera", camera);
            Assign(board, "_skin", Load<PuzzleSkin>($"{Config}/PuzzleSkin.asset"));
            Assign(board, "_sfx", sfx);
            Assign(board, "_feel", Load<PuzzleFeelConfig>($"{Config}/PuzzleFeel.asset"));
            Assign(board, "_callout", callout);

            var hud = BuildHud();
            var result = UiFactory.ResultPopup<PuzzleResultView>(score: true);

            var rootGo = new GameObject("SceneRoot");
            var installer = rootGo.AddComponent<PuzzleGameSceneInstaller>();
            Assign(installer, "_board", board);
            Assign(installer, "_hud", hud);
            Assign(installer, "_result", result);
            AddSceneRoot(rootGo, AppScene, installer);
            EditorSceneManager.SaveScene(scene, $"{Scenes}/{BlockPuzzleAppInstaller.GameScene}.unity");
        }

        private static PuzzleHudView BuildHud()
        {
            var canvas = UiFactory.Canvas("HudCanvas", 200);
            var safe = UiFactory.Safe(canvas);
            var top = UiFactory.Rect("Top", safe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -150), new Vector2(0, 300));
            var level = UiFactory.PillLabel("Level", top, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(400, 110), 54);
            var counters = UiFactory.Stretch("Counters", top);
            UiFactory.NestedCanvas(counters, clickable: false);
            var goal = UiFactory.Lettered("Goal", counters, string.Empty, 50, TitleOutline, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -175),
                new Vector2(700, 80));
            var bar = UiFactory.Rect("Progress", counters, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -245), new Vector2(640, 48));
            UiFactory.Sliced(bar, "ui_pill", 3f);
            var fill = UiFactory.Rect("Fill", bar, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(-12, -14));
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(6, 0);
            UiFactory.Sliced(fill, "ui_chip", 4f, new Color32(120, 220, 90, 255));
            var pause = UiFactory.Button("Pause", top, "Blue", null, new Vector2(1f, 1f), new Vector2(-110, -110), new Vector2(140, 140), "icon_pause");

            var view = canvas.gameObject.AddComponent<PuzzleHudView>();
            UiFactory.AssignMotion(view);
            Assign(view, "_level", level);
            Assign(view, "_replayTag", LevelListScreen.ReplayTag(top, new Vector2(0.5f, 1f), new Vector2(-320, -80)));
            Assign(view, "_goal", goal);
            Assign(view, "_progress", fill);
            Assign(view, "_pause", pause);
            return view;
        }

        private static SoundCue Clip(PuzzleCue cue, string file, float volume) => SoundCues.Clip(cue, volume, file);
    }
}
