using System;
using System.Linq;
using System.Collections.Generic;
using Match3.Generation;
using Match3.Model;
using Match3.View;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.App;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Editor;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Match3.Editor
{
    // Paints a match-3 board with the game's own sprites. Brushes are the payload's cell codes, so what you paint is what ships;
    // the floor and chain brushes paint their own layers under and over the pieces.
    public sealed class Match3LevelDesigner : LevelDesigner
    {
        private const string SkinPath = "Assets/Games/Match3/Config/Match3Skin.asset";
        private const string AppScene = "Assets/Games/Match3/Scenes/Match3App.unity";
        private const int MaxGoals = 4;
        private const int Playouts = 60;

        private static readonly string[] GoalKinds = { "colour", "box", "balloon", "duck", "floor", "chain" };
        private static readonly string[] GoalLabels = { "Gems", "Boxes", "Balloons", "Ducks", "Floor", "Chains" };
        private static readonly string[] ColourNames = { "Red", "Blue", "Green", "Yellow", "Purple", "Teal" };
        private static readonly char[] Codes = { '.', '1', '2', '3', '4', '5', '6', 'h', 'v', 'b', '*', 'f', 'x', 'X', 'o', 'd', '_', '#' };

        // Brushes past the piece codes paint a layer: floor with one or two layers, no floor, a chain, no chain.
        private static readonly char[] Layers = { '1', '2', '.', 'c', '-' };

        private static readonly GUIContent[] Brushes =
        {
            new GUIContent(string.Empty, "Random gem: its colour comes from the seed"), new GUIContent(string.Empty, "Red gem"),
            new GUIContent(string.Empty, "Blue gem"), new GUIContent(string.Empty, "Green gem"), new GUIContent(string.Empty, "Yellow gem"),
            new GUIContent(string.Empty, "Purple gem"), new GUIContent(string.Empty, "Teal gem"), new GUIContent(string.Empty, "Row rocket"),
            new GUIContent(string.Empty, "Column rocket"), new GUIContent(string.Empty, "Bomb"), new GUIContent(string.Empty, "Disco"),
            new GUIContent(string.Empty, "Flyer"), new GUIContent(string.Empty, "Box, one layer: stays put"), new GUIContent(string.Empty, "Box, two layers: stays put"),
            new GUIContent(string.Empty, "Balloon: pops when a match happens next to it"),
            new GUIContent(string.Empty, "Duck: collected when it reaches the bottom"), new GUIContent("Pen", "Pen: an empty cell sealed behind boxes or chains; it fills once its wall breaks"),
            new GUIContent("Hole", "Hole: not part of the board"),
            new GUIContent(string.Empty, "Floor, one layer: under the piece"), new GUIContent(string.Empty, "Floor, two layers: under the piece"),
            new GUIContent("No floor", "No floor: clears the floor layer"), new GUIContent(string.Empty, "Chain: locks a gem in place"),
            new GUIContent("No chain", "No chain: frees the gem"),
        };

        private readonly Match3LevelDoc _doc = new Match3LevelDoc();
        private readonly BoardPainter _painter = new BoardPainter();
        private Match3Skin _skin;
        private int _brush = 1;
        private bool _mirror;

        public override string Game => Match3LevelSerializer.GameId;
        public override string Title => "Match 3";
        public override string Folder => Match3ContentBuilder.Root;
        public override string Prefix => "m3";
        public override bool CanPlay => true;

        private Match3Skin Skin => _skin != null ? _skin : _skin = AssetDatabase.LoadAssetAtPath<Match3Skin>(SkinPath);

        [MenuItem("Assets/Create/Tileworks/Match3 Level", priority = 80)]
        public static void CreateLevel() => LevelContent.Create(new Match3LevelDesigner());

        public override void Load(JObject payload) => _doc.Load(payload);

        public override JObject Save() => _doc.Save();

        public override Texture2D Thumbnail(JObject payload)
        {
            var doc = new Match3LevelDoc();
            doc.Load(payload);
            var pixels = new Color32[doc.Cells.Length];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = ThumbnailColour(doc.Cells[i]);
            return LevelGui.Pixels(doc.Width, doc.Height, pixels);
        }

        public override JObject NewPayload()
        {
            var doc = new Match3LevelDoc();
            doc.Goals.Add(new Match3LevelDoc.Goal());
            return doc.Save();
        }

        public override void OnGUI(LevelEdit edit)
        {
            DrawSettings(edit);
            DrawGoals(edit);
            EditorGUILayout.Space(6f);
            _brush = LevelGui.Palette(edit.Width, _brush, Brushes, DrawBrush);
            using (new EditorGUILayout.HorizontalScope())
            {
                _mirror = GUILayout.Toggle(_mirror, "Mirror left and right", GUILayout.Width(150f));
                GUILayout.Label($"Painting: {BrushName(_brush)}. Click or drag on the board; right-click sets a random gem.", EditorStyles.miniLabel);
            }

            DrawBoard(edit);
            GUILayout.Label(Summary(), EditorStyles.miniLabel);
            DrawTools(edit);
        }

        public override void Validate(LevelEnvelope envelope, List<string> problems)
        {
            var serializer = new Match3LevelSerializer();
            Match3Level level;
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
            CheckPens(level.Playable, level.Pieces.Select((p, c) => p == Piece.Box || level.Chains[c]).ToArray(), cell => level.Pieces[cell] == Piece.Empty,
                cell => level.Floors[cell] > 0, level.Width, level.Height, "boxes or chains", problems);
            for (var x = 0; x < level.Width; x++)
                if (level.Pieces[(level.Height - 1) * level.Width + x] == Piece.Duck)
                    problems.Add($"The duck in column {x + 1} starts on the bottom row, so it's collected before the first move.");
            if (!Matches.HasMove(new Match3Rules().CreateState(level, 1))) problems.Add("The starting board has no move, even after a shuffle.");
        }

        // Holes alone never cut a cell off; cells the boxes or chains cut off are pens and start empty with no goal on them; every other empty
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
            var level = new Match3LevelSerializer().Read(envelope);
            var stats = new Match3Generator(new Match3Rules(), Playouts).Measure(level, StableHash.Hash64(envelope.Id));
            return new LevelMeasure(stats.WinRate, stats.AverageMoves, Match3Generator.Classify(stats.WinRate).ToString().ToLowerInvariant());
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
                var width = EditorGUILayout.IntSlider(_doc.Width, Match3Level.MinSize, Match3Level.MaxWidth);
                GUILayout.Label("×", GUILayout.Width(12f));
                var height = EditorGUILayout.IntSlider(_doc.Height, Match3Level.MinSize, Match3Level.MaxHeight);
                if (width != _doc.Width || height != _doc.Height)
                {
                    edit.Change("Resize board");
                    _doc.Resize(width, height);
                }
            }

            var moves = EditorGUILayout.IntSlider("Moves", _doc.Moves, 1, 99);
            var colours = EditorGUILayout.IntSlider(new GUIContent("Colours", "How many gem colours random gems and refills use"), _doc.Colours,
                Match3Level.MinColours, Match3Level.MaxColours);
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
                _doc.Goals.Add(new Match3LevelDoc.Goal());
            }
        }

        private static string BrushName(int brush) => Brushes[brush].tooltip.Split(':')[0];

        private string Available(int kind) => kind switch
        {
            1 => $"of {_doc.Count('x') + _doc.Count('X')} on the board",
            2 => $"of {_doc.Count('o')} on the board",
            3 => $"of {_doc.Count('d')} on the board",
            4 => $"of {_doc.FloorCells()} floor cells",
            5 => $"of {_doc.ChainCount()} chains",
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

                    if (skin == null) EditorGUI.DrawRect(Inset(rect, -1f), Color.gray);
                    else LevelGui.Sprite(rect, skin.Cell(x, y), Color.white);
                    var floor = _doc.Floor[y * _doc.Width + x];
                    if (skin != null && (floor == '1' || floor == '2')) LevelGui.Sprite(rect, skin.Floor(floor - '0'), Color.white);
                    DrawPiece(Inset(rect, cell * 0.06f), code);
                    if (skin != null && _doc.Chains[y * _doc.Width + x] == 'c') LevelGui.Sprite(rect, skin.Chain, Color.white);
                }
            }

            if (!_painter.Paint(board, cell, out var hit, out var erase) || hit.x >= _doc.Width || hit.y >= _doc.Height) return;
            Paint(edit, hit.x, hit.y, erase);
            if (_mirror) Paint(edit, _doc.Width - 1 - hit.x, hit.y, erase);
        }

        // A chain only sits on a gem and a floor only on the board, so painting a piece or a hole drops what no longer fits.
        private void Paint(LevelEdit edit, int x, int y, bool erase)
        {
            var i = y * _doc.Width + x;
            if (!erase && _brush >= Codes.Length)
            {
                var layer = Layers[_brush - Codes.Length];
                if (_doc[x, y] == '#') return;
                if (layer == 'c' || layer == '-')
                {
                    if (layer == 'c' && !IsGem(_doc[x, y])) return;
                    SetLayer(edit, _doc.Chains, i, layer == 'c' ? 'c' : '.');
                }
                else
                {
                    SetLayer(edit, _doc.Floor, i, layer);
                }

                return;
            }

            var code = erase ? '.' : Codes[_brush];
            if (_doc[x, y] == code) return;
            edit.Change("Paint cells");
            _doc[x, y] = code;
            if (!IsGem(code)) _doc.Chains[i] = '.';
            if (code == '#' || code == '_') _doc.Floor[i] = '.';
        }

        private static void SetLayer(LevelEdit edit, char[] layer, int i, char code)
        {
            if (layer[i] == code) return;
            edit.Change("Paint layer");
            layer[i] = code;
        }

        private static bool IsGem(char code) => code == '.' || (code >= '1' && code <= '6');

        private void DrawTools(LevelEdit edit)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Random colours", "Every fixed-colour gem becomes a random gem"))) Replace(edit, c => c >= '1' && c <= '6', '.');
                if (GUILayout.Button(new GUIContent("Fill holes", "Every hole becomes a random gem"))) Replace(edit, c => c == '#', '.');
                if (GUILayout.Button(new GUIContent("Clear board", "Every cell becomes a random gem with no floor or chain"))) ClearBoard(edit);
            }
        }

        private void Replace(LevelEdit edit, Func<char, bool> match, char code)
        {
            edit.Change("Replace cells");
            for (var i = 0; i < _doc.Cells.Length; i++)
                if (match(_doc.Cells[i]))
                    _doc.Cells[i] = code;
        }

        private void ClearBoard(LevelEdit edit)
        {
            edit.Change("Clear board");
            for (var i = 0; i < _doc.Cells.Length; i++) (_doc.Cells[i], _doc.Floor[i], _doc.Chains[i]) = ('.', '.', '.');
        }

        private string Summary()
        {
            var playable = _doc.Cells.Length - _doc.Count('#');
            var pens = _doc.Count('_') > 0 ? $" · {_doc.Count('_')} pen cells" : string.Empty;
            var specials = _doc.Count('h') + _doc.Count('v') + _doc.Count('b') + _doc.Count('*') + _doc.Count('f');
            return $"{playable} cells · {_doc.Count('x') + _doc.Count('X')} boxes · {_doc.FloorCells()} floor · {_doc.ChainCount()} chains · " +
                $"{_doc.Count('o')} balloons · {_doc.Count('d')} ducks · {specials} specials{pens}";
        }

        // A random gem is a faint gem with a question mark, so painted colours, specials and blockers stand out.
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

        private void DrawBrush(int brush, Rect rect)
        {
            if (brush < Codes.Length)
            {
                DrawPiece(rect, Codes[brush]);
                return;
            }

            var skin = Skin;
            var layer = Layers[brush - Codes.Length];
            if (skin == null || layer == '.' || layer == '-') return;
            LevelGui.Sprite(rect, layer == 'c' ? skin.Chain : skin.Floor(layer - '0'), Color.white);
        }

        private void DrawPiece(Rect rect, char code)
        {
            var skin = Skin;
            if (skin == null || code == '#' || code == '_') return;
            if (code == '.')
            {
                LevelGui.Sprite(rect, skin.SpriteOf(Piece.Gem, 1, 0), new Color(0.3f, 0.3f, 0.35f, 0.45f));
                GUI.Label(rect, "?", LevelGui.Centred);
                return;
            }

            var (piece, colour, hp) = code switch
            {
                'h' => (Piece.RocketRow, (byte)0, 0),
                'v' => (Piece.RocketColumn, (byte)0, 0),
                'b' => (Piece.Bomb, (byte)0, 0),
                '*' => (Piece.Disco, (byte)0, 0),
                'f' => (Piece.Flyer, (byte)0, 0),
                'x' => (Piece.Box, (byte)0, 1),
                'X' => (Piece.Box, (byte)0, 2),
                'o' => (Piece.Balloon, (byte)0, 0),
                'd' => (Piece.Duck, (byte)0, 0),
                _ => (Piece.Gem, (byte)(code - '0'), 0),
            };
            LevelGui.Sprite(rect, skin.SpriteOf(piece, colour, hp), Color.white);
        }

        private static Rect Inset(Rect rect, float by) => new Rect(rect.x + by, rect.y + by, rect.width - 2f * by, rect.height - 2f * by);
    }
}
