using System.IO;
using Blast.Installer;
using Blast.View;
using Tileworlds.Games.Editor;
using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using Tileworks.Tiles.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using static Tileworlds.Games.Editor.SceneParts;
using static Tileworlds.Games.Editor.SoundCues;

namespace Blast.Editor
{
    // Rebuilds the Blast scenes and config assets from code; hand edits to the generated scenes are overwritten.
    public static class BlastSceneBuilder
    {
        private const string Game = "Assets/Games/Blast";
        private const string Config = Game + "/Config";
        private const string Scenes = Game + "/Scenes";
        private const string AppScene = Scenes + "/BlastApp.unity";
        private const string SpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        // The backdrops' bottom edge colour, so anything outside them still matches.
        private static readonly Color Background = new Color32(47, 138, 44, 255);
        private const string HomeBackdrop = "Assets/Games/Blast/Art/Design/Backgrounds/bg_blast_home.png";
        private const string GameBackdrop = "Assets/Games/Blast/Art/Design/Backgrounds/bg_blast_game.png";
        private const string BoosterFxPath = "Assets/Games/Shared/Config/BoosterFx.asset";

        [MenuItem("Tileworks/Blast/Rebuild Scenes")]
        public static void Build()
        {
            foreach (var folder in new[] { Config, Scenes }) Directory.CreateDirectory(folder);
            BlastArtBuilder.Build();
            BlastArtBuilder.BuildAtlas();
            BlastArtBuilder.ImportUi();
            var skin = LoadOrCreate<BlastSkin>($"{Config}/BlastSkin.asset");
            skin.Configure(BlastArtBuilder.ByColour("cube"), BlastArtBuilder.ByColour("disco"), Art("rocket_row"), Art("rocket_column"), Art("bomb"), Art("box_2"),
                Art("box_1"), Art("balloon"), Art("duck"), Art("cell"), BlastArtBuilder.BoardShape(), Art("fx_flash"), Art("fx_ring"), Art("fx_spark"),
                Load<Material>(SpriteMaterial), BlastArtBuilder.Colours);
            EditorUtility.SetDirty(skin);
            LoadOrCreate<BlastFeelConfig>($"{Config}/BlastFeel.asset");
            var boardFx = LoadOrCreate<BoardFxConfig>($"{Config}/BlastBoardFx.asset");
            boardFx.Configure(Load<Material>(SpriteMaterial), FxArt.Load("fx_shard"), FxArt.Load("fx_smoke"), FxArt.Load("fx_dot"), FxArt.Load("fx_star"), FxArt.Load("fx_beam"));
            EditorUtility.SetDirty(boardFx);
            var boosterFx = LoadOrCreate<BoosterFxConfig>(BoosterFxPath);
            boosterFx.Configure(Art("fx_hammer"), Art("fx_shuffle"), Art("select_ring"));
            EditorUtility.SetDirty(boosterFx);
            var sounds = LoadOrCreate<BlastSounds>($"{Config}/BlastSounds.asset");
            sounds.Configure(new[]
            {
                Clip(BlastCue.Blast, 0.7f, "sfx_pop_1", "sfx_pop_2", "sfx_pop_3"),
                Clip(BlastCue.Special, 0.6f, "powerUp2", "powerUp5", "powerUp6"),
                Clip(BlastCue.Rocket, 0.75f, "sfx_whoosh_1", "sfx_whoosh_2"),
                Clip(BlastCue.Bomb, 0.8f, "explosion_synth"),
                Clip(BlastCue.Disco, 0.7f, "impactGlass_heavy_000", "impactGlass_heavy_002", "impactGlass_heavy_003"),
                Clip(BlastCue.Box, 0.7f, "impactWood_light_000", "impactWood_light_001", "impactWood_light_002"),
                Clip(BlastCue.Balloon, 0.7f, "sfx_balloon_1", "sfx_balloon_2"),
                Clip(BlastCue.Duck, 0.7f, "sfx_squeak_1", "sfx_squeak_2"),
                Clip(BlastCue.Refuse, 0.4f, "error_004"),
                Clip(BlastCue.Win, 0.9f, "jingles_PIZZI10").WithPriority(10).WithJitter(0f),
                Clip(BlastCue.Lose, 0.8f, "jingles_HIT07").WithPriority(10).WithJitter(0f),
                Clip(BlastCue.Hammer, 0.9f, "impactPunch_heavy_002", "impactPunch_heavy_003"),
                Clip(BlastCue.Shuffle, 0.8f, "card-fan-1"),
                Clip(BlastCue.BombFuse, 0.5f, "sfx_fuse"),
                Clip(BlastCue.BigBoom, 0.95f, "explosion_synth").WithPriority(5),
                Clip(BlastCue.DiscoCharge, 0.6f, "forceField_000", "forceField_001"),
                Clip(BlastCue.DiscoZap, 0.5f, "sfx_zap_1", "sfx_zap_2").WithVoices(6).WithGap(0.03f),
                Clip(BlastCue.ComboGather, 0.7f, "sfx_riser"),
                Clip(BlastCue.ComboBoom, 0.9f, "lowFrequency_explosion_000").WithPriority(5),
                Clip(BlastCue.Land, 0.35f, "sfx_land_1", "sfx_land_2").WithVoices(2).WithGap(0.06f),
                Clip(BlastCue.BoxBreak, 0.75f, "impactPlank_medium_000", "impactPlank_medium_001"),
            });
            EditorUtility.SetDirty(sounds);
            var pack = File.Exists(BlastContentBuilder.SequencePath) ? BlastContentBuilder.UpdatePack() : null;
            if (pack == null)
            {
                BlastContentBuilder.GenerateAll();
                pack = BlastContentBuilder.UpdatePack();
            }

            var installer = LoadOrCreate<BlastAppInstaller>($"{Config}/BlastAppInstaller.asset");
            installer.SetLevels(pack);
            installer.SetRemoteLevels(Load<Tileworks.Tiles.View.RemoteLevelsConfig>("Assets/Games/Shared/Config/RemoteLevels.asset"));
            EditorUtility.SetDirty(installer);
            AssetDatabase.SaveAssets();

            BuildAppScene();
            BuildHomeScene();
            BuildGameScene();
            SetBuildScenes(Scenes, AppScene, $"{Scenes}/{BlastAppInstaller.HomeScene}.unity", $"{Scenes}/{BlastAppInstaller.GameScene}.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[Tileworks] Blast scenes rebuilt");
        }

        private static void BuildAppScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("AppRoot").AddComponent<AppRoot>();
            Assign(root, "_installer", Load<BlastAppInstaller>($"{Config}/BlastAppInstaller.asset"));
            Assign(root, "_games", RegisterGame("Blast", AppScene));
            UiFactory.Font = UiFactory.DisplayFont();
            Assign(root, "_loading", UiFactory.LoadingScreen("Blast!", Background, UiFactory.BackdropSprite(HomeBackdrop), BlastUi.TitleOutline));
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
            var (view, levels, path, settings) = BlastScreens.Home(Load<BlastSkin>($"{Config}/BlastSkin.asset"));

            var rootGo = new GameObject("SceneRoot");
            var installer = rootGo.AddComponent<BlastHomeSceneInstaller>();
            Assign(installer, "_home", view);
            Assign(installer, "_levels", levels);
            Assign(installer, "_levelPath", path);
            Assign(installer, "_settings", settings);
            AddSceneRoot(rootGo, AppScene, installer);
            EditorSceneManager.SaveScene(scene, $"{Scenes}/{BlastAppInstaller.HomeScene}.unity");
        }

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UiFactory.BeginScene();
            var camera = Camera("BoardCamera", Background);
            UiFactory.Backdrop(camera, UiFactory.BackdropSprite(GameBackdrop));

            var sfx = new GameObject("Sfx").AddComponent<BlastSfx>();
            Assign(sfx, "_sounds", Load<BlastSounds>($"{Config}/BlastSounds.asset"));

            var board = new GameObject("Board").AddComponent<BlastBoardView>();
            Assign(board, "_camera", camera);
            Assign(board, "_skin", Load<BlastSkin>($"{Config}/BlastSkin.asset"));
            Assign(board, "_sfx", sfx);
            Assign(board, "_feel", Load<BlastFeelConfig>($"{Config}/BlastFeel.asset"));
            Assign(board, "_boosterFx", Load<BoosterFxConfig>(BoosterFxPath));
            var effects = new GameObject("Effects").AddComponent<BoardFx>();
            effects.transform.SetParent(board.transform, false);
            Assign(effects, "_config", Load<BoardFxConfig>($"{Config}/BlastBoardFx.asset"));
            Assign(effects, "_board", board.transform);
            Assign(board, "_effects", effects);
            Assign(board.gameObject.AddComponent<BlastIdle>(), "_board", board);

            var skin = Load<BlastSkin>($"{Config}/BlastSkin.asset");
            var (hud, boosters) = BlastScreens.Hud(skin);
            var fit = board.gameObject.AddComponent<BoardCameraFit>();
            Assign(fit, "_camera", camera);
            Assign(fit, "_boosters", boosters);
            Assign(board, "_fit", fit);
            var result = BlastScreens.Popup(skin, Load<BlastFeelConfig>($"{Config}/BlastFeel.asset"));

            var rootGo = new GameObject("SceneRoot");
            var installer = rootGo.AddComponent<BlastGameSceneInstaller>();
            Assign(installer, "_board", board);
            Assign(installer, "_hud", hud);
            Assign(installer, "_result", result);
            Assign(installer, "_boosters", boosters);
            AddSceneRoot(rootGo, AppScene, installer);
            EditorSceneManager.SaveScene(scene, $"{Scenes}/{BlastAppInstaller.GameScene}.unity");
        }

        private static Sprite Art(string name) => BlastArtBuilder.Load(name);
    }
}
