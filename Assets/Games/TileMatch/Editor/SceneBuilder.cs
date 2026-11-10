using System.IO;
using System.Linq;
using Tileworlds.Games.Editor;
using static Tileworlds.Games.Editor.SceneParts;
using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using Tileworks.Tiles.View;
using TileMatch.Installer;
using TileMatch.Service;
using TileMatch.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TileMatch.Editor
{
    // Rebuilds the placeholder scenes, prefab and config assets from code. Re-run after changing the layout here;
    // hand edits to the generated scenes are overwritten.
    public static partial class SceneBuilder
    {
        private const string Game = "Assets/Games/TileMatch";
        private const string Config = Game + "/Config";
        private const string Prefabs = Game + "/Prefabs";
        private const string Scenes = Game + "/Scenes";
        private const string Animals = "Assets/ThirdParty/Kenney/AnimalPackRemastered";
        // The backdrops' bottom edge colour, so anything outside them still matches.
        private static readonly Color Background = new Color32(62, 158, 53, 255);
        private static readonly Color BadgeOutline = new Color32(122, 14, 8, 255);
        private const string HomeBackdrop = Game + "/Art/Backgrounds/bg_tm_home.png";
        private const string GameBackdrop = Game + "/Art/Backgrounds/bg_tm_game.png";
        private const string HeroPath = Game + "/Art/UI/hero_tiles.png";

        [MenuItem("Tileworks/Tile Match/Rebuild Scenes and Prefabs")]
        public static void Build()
        {
            foreach (var folder in new[] { Config, Prefabs, Scenes }) Directory.CreateDirectory(folder);
            KitArt.Import();
            TileArtBuilder.Build();
            UiFactory.Font = UiFactory.DisplayFont();

            BuildSkin();
            LoadOrCreate<GameFeelConfig>($"{Config}/GameFeel.asset");
            var sounds = LoadOrCreate<TileSounds>($"{Config}/TileSounds.asset");
            sounds.Configure(new[]
            {
                Clip(Sfx.Tap, "click_002", 0.6f), Clip(Sfx.Land, "drop_002", 0.7f), Clip(Sfx.Triple, "confirmation_002", 0.9f),
                Clip(Sfx.Nope, "error_004", 0.6f), Clip(Sfx.Crack, "glass_001", 0.8f), Clip(Sfx.Spawn, "pluck_001", 0.7f),
                Clip(Sfx.Booster, "maximize_006", 0.8f), Clip(Sfx.Win, "jingles_PIZZI10", 0.9f), Clip(Sfx.Lose, "jingles_HIT07", 0.8f),
                Clip(Sfx.Button, "click_001", 0.6f),
            });
            EditorUtility.SetDirty(sounds);
            var pack = ContentBuilder.UpdatePack();
            var installer = LoadOrCreate<TileMatchAppInstaller>($"{Config}/TileMatchAppInstaller.asset");
            installer.SetLevels(pack);
            installer.SetConfig(Load<TextAsset>($"{Config}/config.json"));
            installer.SetRemoteLevels(Load<Tileworks.Tiles.View.RemoteLevelsConfig>("Assets/Games/Shared/Config/RemoteLevels.asset"));
            EditorUtility.SetDirty(installer);
            BuildTilePrefab();
            AssetDatabase.SaveAssets();

            // NewScene(Single) unloads loaded assets, so each scene reloads what it references by path.
            BuildAppScene();
            BuildHomeScene();
            BuildGameScene();
            SetBuildScenes(Scenes, $"{Scenes}/App.unity", $"{Scenes}/Home.unity", $"{Scenes}/Game.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[Tileworks] Scenes and prefabs rebuilt");
        }

        private static TileSkin BuildSkin()
        {
            var skin = LoadOrCreate<TileSkin>($"{Config}/TileSkin.asset");
            var icons = Directory.GetFiles(Animals, "*.png").OrderBy(Path.GetFileName).Select(path =>
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 154 || importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 154;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }

                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }).ToArray();
            TileArtBuilder.BuildAtlas(icons);
            var basePath = TileArtBuilder.Folder;
            skin.Configure(icons, Load<Sprite>($"{basePath}/tile_face.png"), Load<Sprite>($"{basePath}/tile_shadow.png"),
                Load<Sprite>($"{basePath}/tile_hidden.png"),
                new[] { Load<Sprite>($"{basePath}/tile_ice_1.png"), Load<Sprite>($"{basePath}/tile_ice_2.png") },
                Load<Sprite>($"{basePath}/tile_link.png"));
            EditorUtility.SetDirty(skin);
            return skin;
        }

        private static TileView BuildTilePrefab()
        {
            var root = new GameObject("TileView");
            var material = Load<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            SpriteRenderer Part(string name, Vector3 position)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = position;
                var renderer = go.AddComponent<SpriteRenderer>();
                if (material != null) renderer.sharedMaterial = material;
                return renderer;
            }

            var shadow = Part("Shadow", new Vector3(0.04f, -0.07f, 0f));
            var face = Part("Face", Vector3.zero);
            var icon = Part("Icon", new Vector3(0f, 0.02f, 0f));
            var overlay = Part("Overlay", Vector3.zero);
            overlay.enabled = false;
            var view = root.AddComponent<TileView>();
            Assign(view, "_shadow", shadow);
            Assign(view, "_face", face);
            Assign(view, "_icon", icon);
            Assign(view, "_overlay", overlay);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/TileView.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<TileView>();
        }

        private static void BuildAppScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var app = new GameObject("AppRoot");
            var root = app.AddComponent<AppRoot>();
            Assign(root, "_installer", Load<TileMatchAppInstaller>($"{Config}/TileMatchAppInstaller.asset"));
            Assign(root, "_games", RegisterGame("Tile Match", $"{Scenes}/App.unity"));
            UiFactory.Font = UiFactory.DisplayFont();
            Assign(root, "_loading", UiFactory.LoadingScreen("Tile Match", Background, UiFactory.BackdropSprite(HomeBackdrop)));
            MusicSetup.Add(root);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            new GameObject("AudioListener", typeof(AudioListener));
            EditorSceneManager.SaveScene(scene, $"{Scenes}/App.unity");
        }

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UiFactory.BeginScene();
            var skin = Load<TileSkin>($"{Config}/TileSkin.asset");
            var feel = Load<GameFeelConfig>($"{Config}/GameFeel.asset");
            var tilePrefab = Load<GameObject>($"{Prefabs}/TileView.prefab").GetComponent<TileView>();
            var camera = Camera("BoardCamera", Background);
            UiFactory.Backdrop(camera, UiFactory.BackdropSprite(GameBackdrop));

            var board = new GameObject("Board");
            var tiles = new GameObject("Tiles");
            tiles.transform.SetParent(board.transform, false);
            var pool = new GameObject("Pool");
            pool.transform.SetParent(board.transform, false);
            pool.SetActive(false);

            var trayGo = new GameObject("Tray");
            trayGo.transform.SetParent(board.transform, false);
            var trayBackground = new GameObject("Background").AddComponent<SpriteRenderer>();
            trayBackground.transform.SetParent(trayGo.transform, false);
            trayBackground.sprite = Load<Sprite>($"{TileArtBuilder.Folder}/tray.png");
            trayBackground.drawMode = SpriteDrawMode.Sliced;
            trayBackground.sortingOrder = -10;
            var material = Load<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (material != null) trayBackground.sharedMaterial = material;
            var tray = trayGo.AddComponent<TrayView>();
            Assign(tray, "_background", trayBackground);
            Assign(tray, "_feel", feel);

            var sfx = new GameObject("Sfx").AddComponent<SfxPlayer>();
            Assign(sfx, "_sounds", Load<TileSounds>($"{Config}/TileSounds.asset"));

            var boardView = board.AddComponent<BoardView>();
            Assign(boardView, "_camera", camera);
            Assign(boardView, "_tilesRoot", tiles.transform);
            Assign(boardView, "_poolRoot", pool.transform);
            Assign(boardView, "_tilePrefab", tilePrefab);
            Assign(boardView, "_tray", tray);
            Assign(boardView, "_skin", skin);
            Assign(boardView, "_feel", feel);
            Assign(boardView, "_sfx", sfx);

            var (hud, boosters) = BuildHud();
            var result = UiFactory.ResultPopup<ResultView>();

            var rootGo = new GameObject("SceneRoot");
            var installer = rootGo.AddComponent<GameSceneInstaller>();
            Assign(installer, "_board", boardView);
            Assign(installer, "_hud", hud);
            Assign(installer, "_boosters", boosters);
            Assign(installer, "_result", result);
            SceneParts.AddSceneRoot(rootGo, $"{Scenes}/App.unity", installer);
            EditorSceneManager.SaveScene(scene, $"{Scenes}/Game.unity");
        }

        private static (HudView, BoosterBarView) BuildHud()
        {
            var canvas = UiFactory.Canvas("HudCanvas", 200);
            var safe = UiFactory.Safe(canvas);
            var top = UiFactory.Rect("Top", safe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -90), new Vector2(0, 180));
            var level = UiFactory.PillLabel("Level", top, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 122), 60);
            var pause = UiFactory.Button("Pause", top, "Blue", null, new Vector2(1f, 0.5f), new Vector2(-110, 0), new Vector2(140, 140), "icon_pause");

            var view = canvas.gameObject.AddComponent<HudView>();
            Assign(view, "_level", level);
            Assign(view, "_replayTag", LevelListScreen.ReplayTag(top, new Vector2(0.5f, 0.5f), new Vector2(0, -92)));
            Assign(view, "_pause", pause);
            return (view, BuildBoosters(canvas, safe));
        }

        private static SoundCue Clip(Sfx cue, string file, float volume) => SoundCues.Clip(cue, volume, file);
    }
}
