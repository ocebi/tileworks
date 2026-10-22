using System.IO;
using Match3.Installer;
using Match3.View;
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

namespace Match3.Editor
{
    // Rebuilds the Match3 scenes and config assets from code; hand edits to the generated scenes are overwritten.
    public static class Match3SceneBuilder
    {
        private const string Game = "Assets/Games/Match3";
        private const string Config = Game + "/Config";
        private const string Scenes = Game + "/Scenes";
        private const string AppScene = Scenes + "/Match3App.unity";
        private const string SpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        // The backdrops' bottom edge colour, so anything outside them still matches.
        private static readonly Color Background = new Color32(240, 194, 122, 255);
        private const string HomeBackdrop = "Assets/Games/Match3/Art/Design/Backgrounds/bg_match3_home.png";
        private const string GameBackdrop = "Assets/Games/Match3/Art/Design/Backgrounds/bg_match3_game.png";
        private const string BoosterFxPath = "Assets/Games/Shared/Config/BoosterFx.asset";
        // The booster art is Blast's, as the effects are.
        private const string BlastArt = "Assets/Games/Blast/Art/Design";

        [MenuItem("Tileworks/Match3/Rebuild Scenes")]
        public static void Build()
        {
            foreach (var folder in new[] { Config, Scenes }) Directory.CreateDirectory(folder);
            Match3ArtBuilder.Build();
            var skin = LoadOrCreate<Match3Skin>($"{Config}/Match3Skin.asset");
            skin.Configure(Match3ArtBuilder.ByColour(), Arts("rocket_row", "rocket_column", "bomb", "disco", "flyer"),
                Arts("box_2", "box_1", "floor_2", "floor_1", "chain", "balloon", "duck"), Arts("cell_light", "cell_dark", "board_rim", "board_fill", "select_ring", "hint_glow"),
                Arts("fx_flash", "fx_ring", "fx_spark"), Load<Material>(SpriteMaterial), Match3ArtBuilder.Colours);
            EditorUtility.SetDirty(skin);
            LoadOrCreate<Match3FeelConfig>($"{Config}/Match3Feel.asset");
            var boardFx = LoadOrCreate<BoardFxConfig>($"{Config}/Match3BoardFx.asset");
            boardFx.Configure(Load<Material>(SpriteMaterial), FxArt.Load("fx_shard"), FxArt.Load("fx_smoke"), FxArt.Load("fx_dot"), FxArt.Load("fx_star"), FxArt.Load("fx_beam"));
            EditorUtility.SetDirty(boardFx);
            var boosterFx = LoadOrCreate<BoosterFxConfig>(BoosterFxPath);
            boosterFx.Configure(Load<Sprite>($"{BlastArt}/fx_hammer.png"), Load<Sprite>($"{BlastArt}/fx_shuffle.png"), Load<Sprite>($"{BlastArt}/select_ring.png"));
            EditorUtility.SetDirty(boosterFx);
            var sounds = LoadOrCreate<Match3Sounds>($"{Config}/Match3Sounds.asset");
            sounds.Configure(new[]
            {
                Clip(Match3Cue.Match, 0.6f, "glass_003", "glass_005", "glass_006"),
                Clip(Match3Cue.Swap, 0.5f, "switch_002"),
                Clip(Match3Cue.Special, 0.6f, "powerUp2", "powerUp5", "powerUp6"),
                Clip(Match3Cue.Rocket, 0.75f, "sfx_whoosh_1", "sfx_whoosh_2"),
                Clip(Match3Cue.Bomb, 0.8f, "explosion_synth"),
                Clip(Match3Cue.Disco, 0.7f, "impactGlass_heavy_000", "impactGlass_heavy_002", "impactGlass_heavy_003"),
                Clip(Match3Cue.Flyer, 0.7f, "sfx_whoosh_short"),
                Clip(Match3Cue.Box, 0.7f, "impactWood_light_000", "impactWood_light_001", "impactWood_light_002"),
                Clip(Match3Cue.Floor, 0.6f, "impactPlate_light_000", "impactPlate_light_002", "impactPlate_light_003"),
                Clip(Match3Cue.Chain, 0.7f, "impactMetal_light_001", "impactMetal_light_002", "impactMetal_light_004"),
                Clip(Match3Cue.Balloon, 0.7f, "sfx_balloon_1", "sfx_balloon_2"),
                Clip(Match3Cue.Duck, 0.7f, "sfx_squeak_1", "sfx_squeak_2"),
                Clip(Match3Cue.Refuse, 0.4f, "error_004"),
                Clip(Match3Cue.Shuffle, 0.8f, "card-fan-1"),
                Clip(Match3Cue.Win, 0.9f, "jingles_PIZZI10").WithPriority(10).WithJitter(0f),
                Clip(Match3Cue.Lose, 0.8f, "jingles_HIT07").WithPriority(10).WithJitter(0f),
                Clip(Match3Cue.Hammer, 0.9f, "impactPunch_heavy_002", "impactPunch_heavy_003"),
                Clip(Match3Cue.BombFuse, 0.5f, "sfx_fuse"),
                Clip(Match3Cue.BigBoom, 0.95f, "explosion_synth").WithPriority(5),
                Clip(Match3Cue.DiscoCharge, 0.6f, "forceField_000", "forceField_001"),
                Clip(Match3Cue.DiscoZap, 0.5f, "sfx_zap_1", "sfx_zap_2").WithVoices(6).WithGap(0.03f),
                Clip(Match3Cue.ComboGather, 0.7f, "sfx_riser"),
                Clip(Match3Cue.ComboBoom, 0.9f, "lowFrequency_explosion_000").WithPriority(5),
                Clip(Match3Cue.Land, 0.35f, "sfx_land_1", "sfx_land_2").WithVoices(2).WithGap(0.06f),
                Clip(Match3Cue.BoxBreak, 0.75f, "impactPlank_medium_000", "impactPlank_medium_001"),
                Clip(Match3Cue.ChainBreak, 0.75f, "impactMetal_medium_000", "impactMetal_medium_003"),
            });
            EditorUtility.SetDirty(sounds);
            var pack = File.Exists(Match3ContentBuilder.SequencePath) ? Match3ContentBuilder.UpdatePack() : null;
            if (pack == null)
            {
                Match3ContentBuilder.GenerateAll();
                pack = Match3ContentBuilder.UpdatePack();
            }

            var installer = LoadOrCreate<Match3AppInstaller>($"{Config}/Match3AppInstaller.asset");
            installer.SetLevels(pack);
            installer.SetRemoteLevels(Load<Tileworks.Tiles.View.RemoteLevelsConfig>("Assets/Games/Shared/Config/RemoteLevels.asset"));
            EditorUtility.SetDirty(installer);
            AssetDatabase.SaveAssets();

            BuildAppScene();
            BuildHomeScene();
            BuildGameScene();
            SetBuildScenes(Scenes, AppScene, $"{Scenes}/{Match3AppInstaller.HomeScene}.unity", $"{Scenes}/{Match3AppInstaller.GameScene}.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[Tileworks] Match3 scenes rebuilt");
        }

        private static void BuildAppScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("AppRoot").AddComponent<AppRoot>();
            Assign(root, "_installer", Load<Match3AppInstaller>($"{Config}/Match3AppInstaller.asset"));
            Assign(root, "_games", RegisterGame(Match3Screens.Title, AppScene));
            UiFactory.Font = UiFactory.DisplayFont();
            Assign(root, "_loading", UiFactory.LoadingScreen(Match3Screens.Title, Background, UiFactory.BackdropSprite(HomeBackdrop), Match3Ui.TitleOutline));
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
            var (view, levels, path, settings) = Match3Screens.Home(Load<Match3Skin>($"{Config}/Match3Skin.asset"));

            var rootGo = new GameObject("SceneRoot");
            var installer = rootGo.AddComponent<Match3HomeSceneInstaller>();
            Assign(installer, "_home", view);
            Assign(installer, "_levels", levels);
            Assign(installer, "_levelPath", path);
            Assign(installer, "_settings", settings);
            AddSceneRoot(rootGo, AppScene, installer);
            EditorSceneManager.SaveScene(scene, $"{Scenes}/{Match3AppInstaller.HomeScene}.unity");
        }

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UiFactory.BeginScene();
            var camera = Camera("BoardCamera", Background);
            UiFactory.Backdrop(camera, UiFactory.BackdropSprite(GameBackdrop));

            var sfx = new GameObject("Sfx").AddComponent<Match3Sfx>();
            Assign(sfx, "_sounds", Load<Match3Sounds>($"{Config}/Match3Sounds.asset"));

            var board = new GameObject("Board").AddComponent<Match3BoardView>();
            Assign(board, "_camera", camera);
            Assign(board, "_skin", Load<Match3Skin>($"{Config}/Match3Skin.asset"));
            Assign(board, "_sfx", sfx);
            Assign(board, "_feel", Load<Match3FeelConfig>($"{Config}/Match3Feel.asset"));

            // In the scene rather than added at runtime, so player builds don't strip SpriteMask.
            var mask = new GameObject("Mask").AddComponent<SpriteMask>();
            mask.transform.SetParent(board.transform, false);
            Assign(board, "_mask", mask);
            var boosterBoard = board.gameObject.AddComponent<Match3BoosterBoard>();
            Assign(boosterBoard, "_board", board);
            Assign(boosterBoard, "_fx", Load<BoosterFxConfig>(BoosterFxPath));

            var skin = Load<Match3Skin>($"{Config}/Match3Skin.asset");
            var (hud, boosters) = Match3Screens.Hud(skin, Load<Match3FeelConfig>($"{Config}/Match3Feel.asset"));
            var fit = board.gameObject.AddComponent<BoardCameraFit>();
            Assign(fit, "_camera", camera);
            Assign(fit, "_boosters", boosters);
            Assign(board, "_fit", fit);
            var effects = new GameObject("Effects").AddComponent<BoardFx>();
            effects.transform.SetParent(board.transform, false);
            Assign(effects, "_config", Load<BoardFxConfig>($"{Config}/Match3BoardFx.asset"));
            Assign(effects, "_board", board.transform);
            Assign(board, "_effects", effects);
            Assign(board.gameObject.AddComponent<Match3Idle>(), "_board", board);
            var result = Match3Screens.Popup(skin, Load<Match3FeelConfig>($"{Config}/Match3Feel.asset"));

            var rootGo = new GameObject("SceneRoot");
            var installer = rootGo.AddComponent<Match3GameSceneInstaller>();
            Assign(installer, "_board", board);
            Assign(installer, "_hud", hud);
            Assign(installer, "_result", result);
            Assign(installer, "_boosterBoard", boosterBoard);
            Assign(installer, "_boosters", boosters);
            AddSceneRoot(rootGo, AppScene, installer);
            EditorSceneManager.SaveScene(scene, $"{Scenes}/{Match3AppInstaller.GameScene}.unity");
        }

        private static Sprite[] Arts(params string[] names) => System.Array.ConvertAll(names, Match3ArtBuilder.Load);
    }
}
