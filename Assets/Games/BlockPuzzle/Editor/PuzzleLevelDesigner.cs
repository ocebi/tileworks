using System;
using System.Collections.Generic;
using BlockPuzzle.Generation;
using BlockPuzzle.Model;
using BlockPuzzle.View;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.App;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Editor;
using Tileworks.Tiles.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BlockPuzzle.Editor
{
    // Paints a block puzzle start board (blocks, gem blocks) with the game's sprites and sets the goal and the shape pool.
    public sealed class PuzzleLevelDesigner : LevelDesigner
    {
        private const string SkinPath = "Assets/Games/BlockPuzzle/Config/PuzzleSkin.asset";
        private const string AppScene = "Assets/Games/BlockPuzzle/Scenes/BlockApp.unity";
        private const int Colours = 7;
        private const int Playouts = 60;
        private const float Card = 76f;

        private static readonly string[] Goals = { "score", "gems" };
        private static readonly string[] GoalLabels = { "Reach a score", "Collect every gem" };
        private static readonly GUIContent[] Brushes = BuildBrushes();

        private readonly PuzzleLevelDoc _doc = new PuzzleLevelDoc();
        private readonly BoardPainter _painter = new BoardPainter();
        private readonly ShapeCatalog _shapes = new BlockRules().Shapes;
        private PuzzleSkin _skin;
        private int _brush = 1;
        private bool _mirror;

        public override string Game => BlockLevelSerializer.GameId;
        public override string Title => "Block Puzzle";
        public override string Folder => PuzzleContentBuilder.Root;
        public override string Prefix => "bp";
        public override bool CanPlay => true;

        private PuzzleSkin Skin => _skin != null ? _skin : _skin = AssetDatabase.LoadAssetAtPath<PuzzleSkin>(SkinPath);

        [MenuItem("Assets/Create/Tileworks/Block Puzzle Level", priority = 81)]
        public static void CreateLevel() => LevelContent.Create(new PuzzleLevelDesigner());

        public override void Load(JObject payload) => _doc.Load(payload);

        public override JObject Save() => _doc.Save();

        public override Texture2D Thumbnail(JObject payload)
        {
            var doc = new PuzzleLevelDoc();
            doc.Load(payload);
            var pixels = new Color32[doc.Cells.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var code = doc.Cells[i];
                if (code == '.' || Skin == null) pixels[i] = new Color32(55, 60, 80, 255);
                else if (code >= 'a') pixels[i] = Color.Lerp(Skin.ColourOf((byte)(code - 'a' + 1)), Color.white, 0.55f);
                else pixels[i] = Skin.ColourOf((byte)(code - 'A' + 1));
            }

            return LevelGui.Pixels(doc.Width, doc.Height, pixels);
        }

        public override JObject NewPayload()
        {
            var doc = new PuzzleLevelDoc();
            foreach (var name in new[] { "dot", "i2", "i2v", "i3", "i3v", "o2" }) doc.SetWeight(name, 4);
            return doc.Save();
        }

        public override void OnGUI(LevelEdit edit)
        {
            DrawSettings(edit);
            EditorGUILayout.Space(6f);
            _brush = LevelGui.Palette(edit.Width, _brush, Brushes, DrawBrush);
            using (new EditorGUILayout.HorizontalScope())
            {
                _mirror = GUILayout.Toggle(_mirror, "Mirror left and right", GUILayout.Width(150f));
                GUILayout.Label($"Painting: {Brushes[_brush].tooltip.Split(':')[0]}. Click or drag on the board; right-click empties a cell.", EditorStyles.miniLabel);
            }

            DrawBoard(edit);
            var gems = _doc.Count(c => c >= 'a' && c <= 'g');
            GUILayout.Label($"{_doc.Count(c => c != '.')} filled cells · {gems} gems", EditorStyles.miniLabel);
            if (GUILayout.Button(new GUIContent("Clear board", "Empties every cell"), GUILayout.Width(100f))) Clear(edit);
            EditorGUILayout.Space(6f);
            DrawPool(edit);
        }

        public override void Validate(LevelEnvelope envelope, List<string> problems)
        {
            var serializer = new BlockLevelSerializer(_shapes);
            BlockLevel level;
            try
            {
                level = serializer.Read(envelope);
            }
            catch (Exception e)
            {
                problems.Add(e is NullReferenceException ? "The shape pool names a shape that doesn't exist." : e.Message);
                return;
            }

            var report = new ValidationReport();
            serializer.Validate(level, report);
            foreach (var issue in report.Issues) problems.Add(issue.Message);
            if (level.Goal == BlockGoal.Gems && _doc.Count(c => c >= 'a' && c <= 'g') == 0) problems.Add("The goal is gems, but the board has none.");
        }

        public override LevelMeasure? Measure(LevelEnvelope envelope)
        {
            var rules = new BlockRules();
            var level = new BlockLevelSerializer(rules.Shapes).Read(envelope);
            var stats = new PuzzleGenerator(rules, Playouts).Measure(level, StableHash.Hash64(envelope.Id));
            return new LevelMeasure(stats.WinRate, stats.AverageMoves, PuzzleGenerator.Classify(stats.WinRate).ToString().ToLowerInvariant());
        }

        public override void Play(LevelEnvelope envelope)
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ScenarioFile.Write(new ScenarioRequest { kind = "play-level", payload = envelope.ToJson(), profile = "level-test", fresh = true });
            EditorSceneManager.OpenScene(AppScene);
            EditorApplication.isPlaying = true;
        }

        private static GUIContent[] BuildBrushes()
        {
            var brushes = new GUIContent[1 + 2 * Colours];
            brushes[0] = new GUIContent(string.Empty, "Empty cell");
            for (var i = 0; i < Colours; i++)
            {
                brushes[1 + i] = new GUIContent(string.Empty, $"Block, colour {i + 1}");
                brushes[1 + Colours + i] = new GUIContent(string.Empty, $"Gem block, colour {i + 1}: clearing its line collects the gem");
            }

            return brushes;
        }

        private static char CodeOf(int brush) => brush == 0 ? '.' : brush <= Colours ? (char)('A' + brush - 1) : (char)('a' + brush - 1 - Colours);

        private void DrawSettings(LevelEdit edit)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Board");
                var width = EditorGUILayout.IntSlider(_doc.Width, 4, 12);
                GUILayout.Label("×", GUILayout.Width(12f));
                var height = EditorGUILayout.IntSlider(_doc.Height, 4, 12);
                if (width != _doc.Width || height != _doc.Height)
                {
                    edit.Change("Resize board");
                    _doc.Resize(width, height);
                }
            }

            var goal = EditorGUILayout.Popup("Goal", Math.Max(0, Array.IndexOf(Goals, _doc.Goal)), GoalLabels);
            var score = _doc.Score;
            if (goal == 0) score = EditorGUILayout.IntSlider("Score target", _doc.Score, 10, 5000);
            if (Goals[goal] == _doc.Goal && score == _doc.Score) return;
            edit.Change("Goal");
            (_doc.Goal, _doc.Score) = (Goals[goal], score);
        }

        private void DrawBoard(LevelEdit edit)
        {
            var board = LevelGui.Board(edit.Width, _doc.Width, _doc.Height, 44f, out var cell);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(board.x - 4f, board.y - 4f, board.width + 8f, board.height + 8f), Skin != null ? Skin.FrameTint : Color.black);
                for (var y = 0; y < _doc.Height; y++)
                for (var x = 0; x < _doc.Width; x++)
                    DrawCell(new Rect(board.x + x * cell, board.y + y * cell, cell, cell), _doc[x, y]);
            }

            if (!_painter.Paint(board, cell, out var hit, out var erase) || hit.x >= _doc.Width || hit.y >= _doc.Height) return;
            var code = erase ? '.' : CodeOf(_brush);
            Set(edit, hit.x, hit.y, code);
            if (_mirror) Set(edit, _doc.Width - 1 - hit.x, hit.y, code);
        }

        private void DrawCell(Rect rect, char code)
        {
            var skin = Skin;
            if (skin == null) return;
            LevelGui.Sprite(rect, skin.Cell, skin.CellTint);
            if (code == '.') return;
            var gem = code >= 'a';
            var colour = (byte)(gem ? code - 'a' + 1 : code - 'A' + 1);
            LevelGui.Sprite(rect, skin.Block, skin.ColourOf(colour));
            if (gem) LevelGui.Sprite(new Rect(rect.x + rect.width * 0.2f, rect.y + rect.height * 0.2f, rect.width * 0.6f, rect.height * 0.6f), skin.Gem, Color.white);
        }

        private void DrawBrush(int brush, Rect rect)
        {
            if (Skin == null) return;
            if (brush == 0) LevelGui.Sprite(rect, Skin.Cell, Skin.CellTint);
            else DrawCell(rect, CodeOf(brush));
        }

        // Each shape is a card with its picture and its weight in the pool; weight 0 leaves it out.
        private void DrawPool(LevelEdit edit)
        {
            EditorGUILayout.LabelField(new GUIContent("Shape pool", "Higher weight: the shape is offered more often. 0 leaves it out."), EditorStyles.boldLabel);
            var perRow = Mathf.Max(1, (int)((edit.Width - 40f) / (Card + 6f)));
            var shapes = _shapes.All;
            for (var start = 0; start < shapes.Count; start += perRow)
            {
                var row = GUILayoutUtility.GetRect(edit.Width - 40f, Card + 24f);
                for (var i = start; i < Mathf.Min(start + perRow, shapes.Count); i++)
                {
                    var shape = shapes[i];
                    var rect = new Rect(row.x + (i - start) * (Card + 6f), row.y, Card, Card + 20f);
                    var weight = _doc.WeightOf(shape.Name);
                    GUI.Box(rect, GUIContent.none);
                    DrawShape(new Rect(rect.x + 6f, rect.y + 6f, Card - 12f, Card - 30f), shape, weight > 0);
                    GUI.Label(new Rect(rect.x, rect.y + Card - 22f, Card, 16f), shape.Name, EditorStyles.centeredGreyMiniLabel);
                    var changed = EditorGUI.IntField(new Rect(rect.x + 14f, rect.y + Card - 4f, Card - 28f, 18f), weight);
                    if (changed == weight) continue;
                    edit.Change("Shape weight");
                    _doc.SetWeight(shape.Name, Mathf.Clamp(changed, 0, 99));
                }
            }
        }

        private void DrawShape(Rect area, Shape shape, bool inPool)
        {
            if (Skin == null) return;
            var size = Mathf.Floor(Mathf.Min(area.width / Mathf.Max(1, shape.Width), area.height / Mathf.Max(1, shape.Height), 14f));
            var origin = new Vector2(area.x + (area.width - shape.Width * size) * 0.5f, area.y + (area.height - shape.Height * size) * 0.5f);
            var tint = inPool ? Skin.ColourOf(2) : new Color(0.5f, 0.5f, 0.5f, 0.35f);
            for (var i = 0; i < shape.Count; i++) LevelGui.Sprite(new Rect(origin.x + shape.Dx(i) * size, origin.y + shape.Dy(i) * size, size, size), Skin.Block, tint);
        }

        private void Set(LevelEdit edit, int x, int y, char code)
        {
            if (_doc[x, y] == code) return;
            edit.Change("Paint cells");
            _doc[x, y] = code;
        }

        private void Clear(LevelEdit edit)
        {
            edit.Change("Clear board");
            for (var i = 0; i < _doc.Cells.Length; i++) _doc.Cells[i] = '.';
        }
    }
}
