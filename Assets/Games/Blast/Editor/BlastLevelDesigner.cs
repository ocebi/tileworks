using System;
using System.Linq;
using System.Collections.Generic;
using Blast.Generation;
using Blast.Model;
using Blast.View;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.App;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Editor;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blast.Editor
{
    // Paints a blast board with the game's own sprites. Brushes are the payload's cell codes, so what you paint is what ships.
    public sealed class BlastLevelDesigner : LevelDesigner
    {
        private const string SkinPath = "Assets/Games/Blast/Config/BlastSkin.asset";
        private const string AppScene = "Assets/Games/Blast/Scenes/BlastApp.unity";
        private const int MaxGoals = 4;
        private const int Playouts = 60;

        private static readonly string[] GoalKinds = { "colour", "box", "balloon", "duck" };
        private static readonly string[] GoalLabels = { "Cubes", "Boxes", "Balloons", "Ducks" };
        private static readonly string[] ColourNames = { "Red", "Blue", "Green", "Yellow", "Purple", "Teal" };
        private static readonly char[] Codes = { '.', '1', '2', '3', '4', '5', '6', 'h', 'v', 'b', '*', 'x', 'X', 'o', 'd', '_', '#' };

        private static readonly GUIContent[] Brushes =
        {
            new GUIContent(string.Empty, "Random cube: its colour comes from the seed"), new GUIContent(string.Empty, "Red cube"),
            new GUIContent(string.Empty, "Blue cube"), new GUIContent(string.Empty, "Green cube"), new GUIContent(string.Empty, "Yellow cube"),
            new GUIContent(string.Empty, "Purple cube"), new GUIContent(string.Empty, "Teal cube"), new GUIContent(string.Empty, "Row rocket"),
            new GUIContent(string.Empty, "Column rocket"), new GUIContent(string.Empty, "Bomb"), new GUIContent(string.Empty, "Disco ball"),
            new GUIContent(string.Empty, "Box, one layer"), new GUIContent(string.Empty, "Box, two layers"),
            new GUIContent(string.Empty, "Balloon: pops when a group next to it blasts"),
            new GUIContent(string.Empty, "Duck: collected when it reaches the bottom"), new GUIContent("Pen", "Pen: an empty cell sealed behind boxes; it fills once its wall breaks"),
            new GUIContent("Hole", "Hole: not part of the board"),
        };

        private readonly BlastLevelDoc _doc = new BlastLevelDoc();
        private readonly BoardPainter _painter = new BoardPainter();
        private BlastSkin _skin;
        private int _brush = 1;
        private bool _mirror;

        public override string Game => BlastLevelSerializer.GameId;
        public override string Title => "Blast";
        public override string Folder => BlastContentBuilder.Root;
        public override string Prefix => "bl";
        public override bool CanPlay => true;

        private BlastSkin Skin => _skin != null ? _skin : _skin = AssetDatabase.LoadAssetAtPath<BlastSkin>(SkinPath);

        [MenuItem("Assets/Create/Tileworks/Blast Level", priority = 80)]
        public static void CreateLevel() => LevelContent.Create(new BlastLevelDesigner());

        public override void Load(JObject payload) => _doc.Load(payload);

        public override JObject Save() => _doc.Save();

        public override Texture2D Thumbnail(JObject payload)
        {
            var doc = new BlastLevelDoc();
            doc.Load(payload);
            var pixels = new Color32[doc.Cells.Length];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = ThumbnailColour(doc.Cells[i]);
            return LevelGui.Pixels(doc.Width, doc.Height, pixels);
        }

        public override JObject NewPayload()
        {
            var doc = new BlastLevelDoc();
            doc.Goals.Add(new BlastLevelDoc.Goal());
            return doc.Save();
        }

        public override void OnGUI(LevelEdit edit)
        {
            DrawSettings(edit);
            DrawGoals(edit);
            EditorGUILayout.Space(6f);
            _brush = LevelGui.Palette(edit.Width, _brush, Brushes, (i, rect) => DrawPiece(rect, Codes[i]));
            using (new EditorGUILayout.HorizontalScope())
            {
                _mirror = GUILayout.Toggle(_mirror, "Mirror left and right", GUILayout.Width(150f));
                GUILayout.Label($"Painting: {BrushName(_brush)}. Click or drag on the board; right-click sets a random cube.", EditorStyles.miniLabel);
            }

            DrawBoard(edit);
            GUILayout.Label(Summary(), EditorStyles.miniLabel);
            DrawTools(edit);
        }

        public override void Validate(LevelEnvelope envelope, List<string> problems)
        {
            var serializer = new BlastLevelSerializer();
            BlastLevel level;
            try
            {
                level = serializer.Read(envelope);
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException)
            {
                problems.Add(e.Message);
                return;
            }

            var report = new ValidationReport();
            serializer.Validate(level, report);
            foreach (var issue in report.Issues) problems.Add(issue.Message);
            if (level.Goals.Length > MaxGoals) problems.Add($"The HUD shows {MaxGoals} goals; this level has {level.Goals.Length}.");
            CheckPens(level.Playable, level.Pieces.Select(p => p == Piece.Box).ToArray(), cell => level.Pieces[cell] == Piece.Empty, _ => false, level.Width,
                level.Height, "boxes", problems);
            for (var x = 0; x < level.Width; x++)
                if (level.Pieces[(level.Height - 1) * level.Width + x] == Piece.Duck)
                    problems.Add($"The duck in column {x + 1} starts on the bottom row, so it's collected before the first move.");
        }

        // Holes alone never cut a cell off; cells the boxes cut off are pens and start empty with no goal on them; every other empty
        // cell would fill on the first move.
        private static void CheckPens(bool[] playable, bool[] statics, Func<int, bool> empty, Func<int, bool> goal, int width, int height, string walls,
            List<string> problems)
        {
            var bare = BoardReach.Unreachable(playable, default, width, height);
            if (bare > 0) problems.Add($"{bare} cells can't be refilled: holes cut them off from every fall and slide.");
            var pens = BoardReach.Sealed(playable, statics, width, height);
            int fed = 0, full = 0;
            for (var cell = 0; cell < playable.Length; cell++)
            {
                if (!playable[cell] || statics[cell]) continue;
                if (pens[cell] && (!empty(cell) || goal(cell))) full++;
                else if (!pens[cell] && empty(cell)) fed++;
            }

            if (fed > 0) problems.Add($"{fed} empty cells would fill on the first move: a pen needs {walls} around it so nothing falls or slides in.");
            if (full > 0) problems.Add($"{full} cells sealed behind {walls} hold a piece or a goal: a pen starts empty.");
        }

        public override LevelMeasure? Measure(LevelEnvelope envelope)
        {
            var level = new BlastLevelSerializer().Read(envelope);
            var stats = new BlastGenerator(new BlastRules(), Playouts).Measure(level, StableHash.Hash64(envelope.Id));
            return new LevelMeasure(stats.WinRate, stats.AverageMoves, BlastGenerator.Classify(stats.WinRate).ToString().ToLowerInvariant());
        }

        public override void Play(LevelEnvelope envelope)
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ScenarioFile.Write(new ScenarioRequest { kind = "play-level", payload = envelope.ToJson(), profile = "level-test", fresh = true });
            EditorSceneManager.OpenScene(AppScene);
            EditorApplication.isPlaying = true;
        }

        private void DrawSettings(LevelEdit edit)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Board");
                var width = EditorGUILayout.IntSlider(_doc.Width, BlastLevel.MinSize, BlastLevel.MaxSize);
                GUILayout.Label("×", GUILayout.Width(12f));
                var height = EditorGUILayout.IntSlider(_doc.Height, BlastLevel.MinSize, BlastLevel.MaxSize);
                if (width != _doc.Width || height != _doc.Height)
                {
                    edit.Change("Resize board");
                    _doc.Resize(width, height);
                }
            }

            var moves = EditorGUILayout.IntSlider("Moves", _doc.Moves, 1, 99);
            var colours = EditorGUILayout.IntSlider(new GUIContent("Colours", "How many cube colours random cubes and refills use"), _doc.Colours, 2,
                BlastLevel.MaxColours);
            if (moves == _doc.Moves && colours == _doc.Colours) return;
            edit.Change("Level settings");
            (_doc.Moves, _doc.Colours) = (moves, colours);
        }

        private void DrawGoals(LevelEdit edit)
        {
            EditorGUILayout.LabelField("Goals", EditorStyles.boldLabel);
            for (var i = 0; i < _doc.Goals.Count; i++)
            {
                var goal = _doc.Goals[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    var kind = EditorGUILayout.Popup(Math.Max(0, Array.IndexOf(GoalKinds, goal.Kind)), GoalLabels, GUILayout.Width(80f));
                    var colour = goal.Colour;
                    if (kind == 0) colour = EditorGUILayout.Popup(goal.Colour - 1, ColourNames, GUILayout.Width(70f)) + 1;
                    var count = EditorGUILayout.IntField(goal.Count, GUILayout.Width(50f));
                    GUILayout.Label(Available(kind), EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    var remove = GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60f));
                    if (remove)
                    {
                        edit.Change("Remove goal");
                        _doc.Goals.RemoveAt(i);
                        return;
                    }

                    if (GoalKinds[kind] == goal.Kind && colour == goal.Colour && count == goal.Count) continue;
                    edit.Change("Edit goal");
                    (goal.Kind, goal.Colour, goal.Count) = (GoalKinds[kind], colour, Math.Max(1, count));
                }
            }

            using (new EditorGUI.DisabledScope(_doc.Goals.Count >= MaxGoals))
            {
                if (!GUILayout.Button("Add goal", GUILayout.Width(90f))) return;
                edit.Change("Add goal");
                _doc.Goals.Add(new BlastLevelDoc.Goal());
            }
        }

        private static string BrushName(int brush) => Brushes[brush].tooltip.Split(':')[0];

        private string Available(int kind) => kind switch
        {
            1 => $"of {_doc.Count('x') + _doc.Count('X')} on the board",
            2 => $"of {_doc.Count('o')} on the board",
            3 => $"of {_doc.Count('d')} on the board",
            _ => string.Empty,
        };

        private void DrawBoard(LevelEdit edit)
        {
            var board = LevelGui.Board(edit.Width, _doc.Width, _doc.Height, 48f, out var cell);
            if (Event.current.type == EventType.Repaint)
            {
                var skin = Skin;
                for (var y = 0; y < _doc.Height; y++)
                for (var x = 0; x < _doc.Width; x++)
                {
                    var rect = new Rect(board.x + x * cell, board.y + y * cell, cell, cell);
                    var code = _doc[x, y];
                    if (code == '#')
                    {
                        EditorGUI.DrawRect(Inset(rect, cell * 0.3f), new Color(0f, 0f, 0f, 0.15f));
                        continue;
                    }

                    EditorGUI.DrawRect(Inset(rect, -1f), skin != null ? skin.BoardFill : Color.gray);
                    if (skin != null) LevelGui.Sprite(rect, skin.Cell, Color.white);
                    DrawPiece(Inset(rect, cell * 0.06f), code);
                }
            }

            if (!_painter.Paint(board, cell, out var hit, out var erase) || hit.x >= _doc.Width || hit.y >= _doc.Height) return;
            var paint = erase ? '.' : Codes[_brush];
            Set(edit, hit.x, hit.y, paint);
            if (_mirror) Set(edit, _doc.Width - 1 - hit.x, hit.y, paint);
        }

        private void DrawTools(LevelEdit edit)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Random colours", "Every fixed-colour cube becomes a random cube"))) Replace(edit, c => c >= '1' && c <= '6', '.');
                if (GUILayout.Button(new GUIContent("Fill holes", "Every hole becomes a random cube"))) Replace(edit, c => c == '#', '.');
                if (GUILayout.Button(new GUIContent("Clear board", "Every cell becomes a random cube"))) Replace(edit, c => c != '.', '.');
            }
        }

        private void Replace(LevelEdit edit, Func<char, bool> match, char code)
        {
            edit.Change("Replace cells");
            for (var i = 0; i < _doc.Cells.Length; i++)
                if (match(_doc.Cells[i]))
                    _doc.Cells[i] = code;
        }

        private void Set(LevelEdit edit, int x, int y, char code)
        {
            if (_doc[x, y] == code) return;
            edit.Change("Paint cells");
            _doc[x, y] = code;
        }

        private string Summary()
        {
            var playable = _doc.Cells.Length - _doc.Count('#');
            var pens = _doc.Count('_') > 0 ? $" · {_doc.Count('_')} pen cells" : string.Empty;
            var specials = _doc.Count('h') + _doc.Count('v') + _doc.Count('b') + _doc.Count('*');
            return $"{playable} cells · {_doc.Count('x') + _doc.Count('X')} boxes · {_doc.Count('o')} balloons · {_doc.Count('d')} ducks · {specials} specials{pens}";
        }

        // A random cube is a faint cube with a question mark, so painted colours, specials and blockers stand out.
        private Color32 ThumbnailColour(char code) => code switch
        {
            '#' => new Color32(0, 0, 0, 0),
            '_' => new Color32(60, 60, 72, 255),
            '.' => new Color32(150, 150, 150, 255),
            >= '1' and <= '6' => Skin != null ? (Color32)Skin.ColourOf((byte)(code - '0')) : new Color32(255, 255, 255, 255),
            'x' or 'X' => new Color32(165, 112, 62, 255),
            'o' => new Color32(240, 120, 170, 255),
            'd' => new Color32(250, 210, 60, 255),
            _ => new Color32(255, 255, 255, 255),
        };

        private void DrawPiece(Rect rect, char code)
        {
            var skin = Skin;
            if (skin == null || code == '#' || code == '_') return;
            if (code == '.')
            {
                LevelGui.Sprite(rect, skin.SpriteOf(Piece.Cube, 1, 0), new Color(0.3f, 0.3f, 0.35f, 0.45f));
                GUI.Label(rect, "?", LevelGui.Centred);
                return;
            }

            var (piece, colour, hp) = code switch
            {
                'h' => (Piece.RocketRow, (byte)0, 0),
                'v' => (Piece.RocketColumn, (byte)0, 0),
                'b' => (Piece.Bomb, (byte)0, 0),
                '*' => (Piece.Disco, (byte)1, 0),
                'x' => (Piece.Box, (byte)0, 1),
                'X' => (Piece.Box, (byte)0, 2),
                'o' => (Piece.Balloon, (byte)0, 0),
                'd' => (Piece.Duck, (byte)0, 0),
                _ => (Piece.Cube, (byte)(code - '0'), 0),
            };
            LevelGui.Sprite(rect, skin.SpriteOf(piece, colour, hp), Color.white);
        }

        private static Rect Inset(Rect rect, float by) => new Rect(rect.x + by, rect.y + by, rect.width - 2f * by, rect.height - 2f * by);
    }
}
