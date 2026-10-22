using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.App;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Editor;
using Tileworks.Tiles.Levels;
using TileMatch.Generation;
using TileMatch.Model;
using TileMatch.View;
using UnityEditor;
using UnityEngine;

namespace TileMatch.Editor
{
    // Paints tile match layers with the game's tiles: place and re-kind tiles on the chosen layer, freeze them, hide them.
    // Layers below show dimmed, so stacks read like the game. Links and spawners are kept and drawn.
    public sealed class TileLevelDesigner : LevelDesigner
    {
        private const string SkinPath = "Assets/Games/TileMatch/Config/TileSkin.asset";
        private const int MaxKinds = 30;
        private const int Playouts = 200;

        private static readonly string[] Tools = { "Tile", "Erase", "Ice", "Hidden" };
        private static readonly Color Above = new Color(1f, 1f, 1f, 0.3f);

        private readonly TileLevelDoc _doc = new TileLevelDoc();
        private readonly BoardPainter _painter = new BoardPainter();
        private readonly HashSet<TileLevelDoc.Tile> _stroke = new HashSet<TileLevelDoc.Tile>();
        private TileSkin _skin;
        private int _tool;
        private int _kind;
        private int _layer;
        private int _kinds = 6;
        private bool _showAbove = true;
        private bool _mirror;

        public override string Game => TileMatchLevelSerializer.GameId;
        public override string Title => "Tile Match";
        public override string Folder => ContentBuilder.Root;
        public override string Prefix => "tm";
        public override bool CanPlay => true;

        private TileSkin Skin => _skin != null ? _skin : _skin = AssetDatabase.LoadAssetAtPath<TileSkin>(SkinPath);

        [MenuItem("Assets/Create/Tileworks/Tile Match Level", priority = 82)]
        public static void CreateLevel() => LevelContent.Create(new TileLevelDesigner());

        public override void Load(JObject payload)
        {
            _doc.Load(payload);
            if (_doc.Tiles.Count > 0) _layer = Mathf.Min(_layer, _doc.Tiles.Max(t => t.Layer));
        }

        public override JObject Save() => _doc.Save();

        // Half cells as pixels, higher layers lighter.
        public override Texture2D Thumbnail(JObject payload)
        {
            var doc = new TileLevelDoc();
            doc.Load(payload);
            var pixels = new Color32[TileLevelDoc.Span * TileLevelDoc.Span];
            foreach (var tile in doc.Tiles.OrderBy(t => t.Layer))
            {
                var shade = (byte)Mathf.Min(255, 110 + tile.Layer * 22);
                for (var y = Mathf.Max(0, tile.Y); y < Mathf.Min(TileLevelDoc.Span, tile.Y + 2); y++)
                for (var x = Mathf.Max(0, tile.X); x < Mathf.Min(TileLevelDoc.Span, tile.X + 2); x++)
                    pixels[y * TileLevelDoc.Span + x] = new Color32(shade, shade, (byte)Mathf.Min(255, shade + 25), 255);
            }

            return LevelGui.Pixels(TileLevelDoc.Span, TileLevelDoc.Span, pixels);
        }

        public override JObject NewPayload()
        {
            var doc = new TileLevelDoc();
            for (var i = 0; i < 9; i++) doc.Tiles.Add(new TileLevelDoc.Tile { X = 6 + i % 3 * 2, Y = 6 + i / 3 * 2, Kind = i / 3 });
            return doc.Save();
        }

        public override void OnGUI(LevelEdit edit)
        {
            DrawSettings(edit);
            EditorGUILayout.Space(6f);
            _tool = GUILayout.Toolbar(_tool, Tools);
            if (_tool == 0) _kind = LevelGui.Palette(edit.Width, _kind, KindBrushes(), (i, rect) => DrawFace(rect, i, Color.white), 36f);
            using (new EditorGUILayout.HorizontalScope())
            {
                _layer = EditorGUILayout.IntSlider("Layer", _layer, 0, TileLevelDoc.MaxLayer);
                _showAbove = GUILayout.Toggle(_showAbove, "Show layers above", GUILayout.Width(130f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _mirror = GUILayout.Toggle(_mirror, "Mirror left and right", GUILayout.Width(150f));
                GUILayout.Label(ToolHelp(), EditorStyles.miniLabel);
            }

            DrawCanvas(edit);
            GUILayout.Label(Summary(), EditorStyles.miniLabel);
            DrawDealing(edit);
        }

        public override void Validate(LevelEnvelope envelope, List<string> problems)
        {
            var serializer = new TileMatchLevelSerializer();
            TileMatchLevel level;
            try
            {
                level = serializer.Read(envelope);
            }
            catch (Exception e)
            {
                problems.Add(e.Message);
                return;
            }

            var report = new ValidationReport();
            serializer.Validate(level, report);
            foreach (var issue in report.Issues) problems.Add(issue.Message);
            foreach (var pair in _doc.KindCounts().Where(p => p.Value % 3 != 0).OrderBy(p => p.Key))
                problems.Add($"Kind {pair.Key + 1} has {pair.Value} tiles; every kind needs a multiple of 3.");
        }

        public override LevelMeasure? Measure(LevelEnvelope envelope)
        {
            var level = new TileMatchLevelSerializer().Read(envelope);
            var metrics = DifficultyAnalyzer.Measure(TileMatchRules.CreateDefault(), level,
                new AnalyzerSettings { Playouts = Playouts, Seed = StableHash.Hash64(envelope.Id) });
            var band = metrics.Solvable ? DifficultyBands.Label(new DifficultyBands().Classify(metrics)) : "superHard";
            return new LevelMeasure(metrics.PCasual, 0f, metrics.Solvable ? band : "unsolvable", "pCasual");
        }

        public override void Play(LevelEnvelope envelope) =>
            EditorScenario.Play(new ScenarioRequest { kind = "play-level", payload = envelope.ToJson(), profile = "level-test", fresh = true });

        private void DrawSettings(LevelEdit edit)
        {
            var tray = EditorGUILayout.IntSlider(new GUIContent("Tray", "Slots in the tray; a full tray loses"), _doc.Tray, 3, 9);
            var time = EditorGUILayout.IntField(new GUIContent("Time limit (s)", "0 plays without a timer"), _doc.TimeLimit);
            if (tray == _doc.Tray && time == _doc.TimeLimit) return;
            edit.Change("Level settings");
            (_doc.Tray, _doc.TimeLimit) = (tray, Mathf.Max(0, time));
        }

        private GUIContent[] KindBrushes()
        {
            var count = Mathf.Clamp(Skin != null ? Skin.IconCount : MaxKinds, 1, MaxKinds);
            var brushes = new GUIContent[count];
            for (var i = 0; i < count; i++) brushes[i] = new GUIContent(string.Empty, $"Kind {i + 1}");
            return brushes;
        }

        private string ToolHelp() => _tool switch
        {
            0 => "Click empty space to place a tile on this layer, or a tile to change its kind.",
            1 => "Click a tile on this layer to remove it.",
            2 => "Click a tile to add a layer of ice (none, 1, 2).",
            _ => "Click a tile to hide or show its face until it's uncovered.",
        };

        private void DrawCanvas(LevelEdit edit)
        {
            var area = LevelGui.Board(edit.Width, TileLevelDoc.Span, TileLevelDoc.Span, 18f, out var half);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(area, new Color(0f, 0f, 0f, 0.12f));
                for (var x = 0; x <= TileLevelDoc.Span; x += 2) EditorGUI.DrawRect(new Rect(area.x + x * half, area.y, 1f, area.height), new Color(1f, 1f, 1f, 0.05f));
                for (var y = 0; y <= TileLevelDoc.Span; y += 2) EditorGUI.DrawRect(new Rect(area.x, area.y + y * half, area.width, 1f), new Color(1f, 1f, 1f, 0.05f));
                foreach (var tile in _doc.Tiles.OrderBy(t => t.Layer))
                {
                    if (tile.Layer > _layer && !_showAbove) continue;
                    var tint = tile.Layer < _layer ? Skin != null ? Skin.CoveredTint : Color.gray : tile.Layer > _layer ? Above : Color.white;
                    DrawTile(new Rect(area.x + tile.X * half, area.y + tile.Y * half, 2f * half, 2f * half), tile, tint);
                }
            }

            if (Event.current.type == EventType.MouseDown) _stroke.Clear();
            if (!_painter.Paint(area, half, out var hit, out var erase)) return;
            Apply(edit, hit.x, hit.y, erase);
            if (_mirror) Apply(edit, TileLevelDoc.Span - 1 - hit.x, hit.y, erase);
        }

        private void Apply(LevelEdit edit, int x, int y, bool erase)
        {
            var tile = _doc.At(_layer, x, y);
            if (erase || _tool == 1)
            {
                if (tile == null) return;
                edit.Change("Remove tile");
                _doc.Remove(tile);
                return;
            }

            if (tile == null)
            {
                if (_tool != 0) return;
                var tx = Mathf.Clamp(x - 1, 0, TileLevelDoc.Span - 2);
                var ty = Mathf.Clamp(y - 1, 0, TileLevelDoc.Span - 2);
                if (_doc.Overlaps(_layer, tx, ty)) return;
                edit.Change("Place tile");
                _doc.Tiles.Add(new TileLevelDoc.Tile { X = tx, Y = ty, Layer = _layer, Kind = _kind });
                return;
            }

            if (!_stroke.Add(tile)) return;
            switch (_tool)
            {
                case 0 when tile.Kind != _kind:
                    edit.Change("Change kind");
                    tile.Kind = _kind;
                    break;
                case 2:
                    edit.Change("Ice");
                    tile.Ice = (tile.Ice + 1) % 3;
                    break;
                case 3:
                    edit.Change("Hidden");
                    tile.Hidden = !tile.Hidden;
                    break;
            }
        }

        private void DrawTile(Rect rect, TileLevelDoc.Tile tile, Color tint)
        {
            var skin = Skin;
            if (skin == null) return;
            if (tile.Hidden) LevelGui.Sprite(rect, skin.HiddenBack, tint);
            else DrawFace(rect, tile.Kind, tint);
            if (tile.Ice > 0) LevelGui.Sprite(rect, skin.IceOf(tile.Ice), tint);
            if (tile.Link != null || _doc.Tiles.Any(t => t.Link == tile))
                LevelGui.Sprite(new Rect(rect.xMax - rect.width * 0.4f, rect.y, rect.width * 0.4f, rect.height * 0.4f), skin.Link, tint);
            if (tile.Queue != null) GUI.Label(rect, $"+{tile.Queue.Count}", LevelGui.Centred);
        }

        private void DrawFace(Rect rect, int kind, Color tint)
        {
            var skin = Skin;
            if (skin == null) return;
            LevelGui.Sprite(rect, skin.Face, tint);
            var inset = rect.width * (1f - skin.IconScale) * 0.5f;
            LevelGui.Sprite(new Rect(rect.x + inset, rect.y + inset, rect.width - 2f * inset, rect.height - 2f * inset), skin.IconOf(kind), tint);
        }

        private string Summary()
        {
            var counts = _doc.KindCounts();
            var layers = _doc.Tiles.Count == 0 ? 0 : _doc.Tiles.Max(t => t.Layer) + 1;
            var onLayer = _doc.Tiles.Count(t => t.Layer == _layer);
            return $"{_doc.Tiles.Count} tiles on {layers} layers ({onLayer} on this one) · {counts.Count} kinds";
        }

        // Re-deals every kind in triples with the generator, aiming at the chosen difficulty, and keeps only solvable deals.
        private void DrawDealing(LevelEdit edit)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _kinds = EditorGUILayout.IntSlider(new GUIContent("Kinds to deal", "How many different kinds Deal kinds uses"), _kinds, 2, MaxKinds);
                if (!GUILayout.Button(new GUIContent("Deal kinds", "Assigns kinds in triples so the level is solvable at its difficulty"), GUILayout.Width(90f)))
                    return;
            }

            var envelope = new LevelEnvelope { Id = "designer", Game = Game, Payload = _doc.Save() };
            GenerationResult result;
            try
            {
                EditorUtility.DisplayProgressBar("Dealing kinds", "Searching for a solvable deal…", 0.5f);
                var layout = new TileMatchLevelSerializer().Read(envelope);
                var generator = new LevelGenerator(TileMatchRules.CreateDefault(), new DifficultyBands(), new AnalyzerSettings { Playouts = 150 });
                result = generator.Generate(new GenerationRequest
                {
                    Id = "designer", Layout = layout, TargetBand = DifficultyBands.Parse(edit.Difficulty ?? "normal"),
                    Seed = (ulong)Environment.TickCount, MaxAttempts = 30, Assigner = new AssignerSettings { KindCount = _kinds },
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Tileworks] Deal kinds: {e.Message}");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (result == null)
            {
                Debug.LogWarning("[Tileworks] Deal kinds found no solvable deal; use fewer tiles per layer or more kinds.");
                return;
            }

            edit.Change("Deal kinds");
            var kinds = result.Level.Kinds;
            for (var i = 0; i < _doc.Tiles.Count && i < kinds.Length; i++) _doc.Tiles[i].Kind = kinds[i];
            var next = result.Level.BoardTileCount;
            foreach (var tile in _doc.Tiles.Where(t => t.Queue != null))
                for (var q = 0; q < tile.Queue.Count && next < kinds.Length; q++)
                    tile.Queue[q] = kinds[next++];
        }
    }
}
