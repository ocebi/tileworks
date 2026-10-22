using System.IO;
using System.Linq;
using Tileworks.Kit.Core;
using TileMatch.Generation;
using TileMatch.Model;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TileMatch.Editor
{
    // Paints kind-less layouts on the half-unit grid, validates and generates them, and plays them in the Editor.
    public sealed class LevelEditorWindow : EditorWindow
    {
        private const float Cell = 22f;
        private static readonly string[] Tools = { "Tile", "Frozen 1", "Frozen 2", "Hidden", "Link", "Spawner", "Erase" };
        private static readonly char[] ToolMarks = { 'x', 'f', 'F', 'h', 'L', 's', ' ' };

        private LayoutDocument _doc = new LayoutDocument();
        private string _path;
        private int _layer;
        private int _tool;
        private bool _mirror = true;
        private char _nextLink = '1';
        private string _status = "New layout";
        private DifficultyMetrics _metrics;
        private readonly System.Collections.Generic.List<string> _history = new System.Collections.Generic.List<string>();
        private Label _statusLabel;

        [MenuItem("Tileworks/Tile Match/Layout Editor")]
        public static void Open() => GetWindow<LevelEditorWindow>("Layout Editor").minSize = new Vector2(720, 520);

        private void CreateGUI()
        {
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(NewLayout) { text = "New" });
            toolbar.Add(new ToolbarButton(OpenLayout) { text = "Open…" });
            toolbar.Add(new ToolbarButton(SaveLayout) { text = "Save" });
            toolbar.Add(new ToolbarButton(UndoEdit) { text = "Undo" });
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(Validate) { text = "Validate" });
            toolbar.Add(new ToolbarButton(PlayInEditor) { text = "Play" });
            rootVisualElement.Add(toolbar);

            var body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            body.Add(new IMGUIContainer(DrawSidebar) { style = { width = 220, paddingLeft = 6, paddingTop = 6 } });
            body.Add(new IMGUIContainer(DrawCanvas) { style = { flexGrow = 1 } });
            rootVisualElement.Add(body);
            _statusLabel = new Label(_status) { style = { paddingLeft = 6, paddingBottom = 4 } };
            rootVisualElement.Add(_statusLabel);
        }

        private void DrawSidebar()
        {
            EditorGUILayout.LabelField(_path == null ? "Unsaved layout" : Path.GetFileName(_path), EditorStyles.boldLabel);
            _layer = EditorGUILayout.IntSlider("Layer", _layer, 0, 7);
            _tool = GUILayout.SelectionGrid(_tool, Tools, 2);
            _mirror = EditorGUILayout.Toggle("Mirror X", _mirror);
            EditorGUILayout.Space();
            _doc.Tray = EditorGUILayout.IntSlider("Tray", _doc.Tray, 3, 9);
            _doc.Kinds = EditorGUILayout.IntSlider("Kinds", _doc.Kinds, 2, 30);
            _doc.Band = (DifficultyBand)EditorGUILayout.EnumPopup("Band", _doc.Band);
            _doc.Margin = EditorGUILayout.IntSlider("Margin", _doc.Margin, 0, 3);
            _doc.SpawnerQueue = EditorGUILayout.IntSlider("Spawner queue", _doc.SpawnerQueue, 1, 9);
            EditorGUILayout.Space();
            var countStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = _doc.IsValidCount ? Color.green : new Color(1f, 0.5f, 0.3f) } };
            EditorGUILayout.LabelField($"Tiles: {_doc.TotalTiles} ({(_doc.IsValidCount ? "ok" : "needs a multiple of 3")})", countStyle);
            if (_metrics != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Metrics", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Solvable: {_metrics.Solvable}");
                EditorGUILayout.LabelField($"pGreedy: {_metrics.PGreedy:0.00}   pRandom: {_metrics.PRandom:0.00}");
                EditorGUILayout.LabelField($"Min peak: {_metrics.MinPeak}   Dead ends: {_metrics.DeadEndRatio:0.00}");
                EditorGUILayout.LabelField($"Band: {DifficultyBands.Label(new DifficultyBands().Classify(_metrics))}");
            }
        }

        private void DrawCanvas()
        {
            var area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(area, new Color(0.16f, 0.22f, 0.32f));
            var origin = new Vector2(area.x + 30, area.y + 30);

            for (var x = 0; x <= 24; x += 2)
                EditorGUI.DrawRect(new Rect(origin.x + x * Cell, origin.y, 1, 24 * Cell), new Color(1, 1, 1, 0.06f));
            for (var y = 0; y <= 24; y += 2)
                EditorGUI.DrawRect(new Rect(origin.x, origin.y + y * Cell, 24 * Cell, 1), new Color(1, 1, 1, 0.06f));

            foreach (var cell in _doc.Cells.OrderBy(c => c.Layer))
            {
                var rect = new Rect(origin.x + cell.X * Cell + 1, origin.y + cell.Y * Cell + 1, Cell * 2 - 2, Cell * 2 - 2);
                var current = cell.Layer == _layer;
                var shade = 0.45f + 0.1f * cell.Layer;
                var color = current ? ColourOf(cell.Mark) : new Color(shade, shade, shade, cell.Layer < _layer ? 0.35f : 0.15f);
                EditorGUI.DrawRect(rect, color);
                if (current && cell.Mark != 'x') GUI.Label(rect, cell.Mark.ToString(), EditorStyles.whiteBoldLabel);
            }

            HandleMouse(origin);
        }

        private void HandleMouse(Vector2 origin)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
            var local = (e.mousePosition - origin) / Cell;
            var x = Mathf.RoundToInt(local.x - 1f);
            var y = Mathf.RoundToInt(local.y - 1f);
            if (x < 0 || y < 0 || x > 22 || y > 22) return;

            if (e.type == EventType.MouseDown) PushHistory();
            Apply(x, y);
            if (_mirror)
            {
                var maxX = _doc.Cells.Count == 0 ? x : Mathf.Max(_doc.Cells.Max(c => c.X), x);
                var minX = _doc.Cells.Count == 0 ? x : Mathf.Min(_doc.Cells.Min(c => c.X), x);
                var mirrored = maxX + minX - x;
                if (mirrored != x && Tools[_tool] != "Link") Apply(mirrored, y);
            }

            _metrics = null;
            e.Use();
            Repaint();
        }

        private void Apply(int x, int y)
        {
            var mark = ToolMarks[_tool];
            if (mark == ' ')
            {
                _doc.Remove(x, y, _layer);
                return;
            }

            if (mark == 'L')
            {
                _doc.Set(x, y, _layer, _nextLink);
                var pending = _doc.Cells.Count(c => c.Mark == _nextLink);
                if (pending >= 2) _nextLink = _nextLink == '9' ? '1' : (char)(_nextLink + 1);
                return;
            }

            _doc.Set(x, y, _layer, mark);
        }

        private static Color ColourOf(char mark) => mark switch
        {
            'f' => new Color(0.55f, 0.8f, 1f),
            'F' => new Color(0.35f, 0.65f, 1f),
            'h' => new Color(0.45f, 0.55f, 0.9f),
            's' => new Color(0.95f, 0.75f, 0.3f),
            _ when char.IsDigit(mark) => new Color(0.8f, 0.6f, 0.9f),
            _ => new Color(1f, 0.97f, 0.9f),
        };

        private void PushHistory()
        {
            _history.Add(_doc.ToText());
            if (_history.Count > 50) _history.RemoveAt(0);
        }

        private void UndoEdit()
        {
            if (_history.Count == 0) return;
            _doc = LayoutDocument.Parse(_history[_history.Count - 1]);
            _history.RemoveAt(_history.Count - 1);
            _metrics = null;
            Repaint();
        }

        private void NewLayout()
        {
            _doc = new LayoutDocument();
            _path = null;
            _metrics = null;
            SetStatus("New layout");
        }

        private void OpenLayout()
        {
            var path = EditorUtility.OpenFilePanel("Open layout", ContentBuilder.LayoutsFolder, "txt");
            if (string.IsNullOrEmpty(path)) return;
            _doc = LayoutDocument.Parse(File.ReadAllText(path));
            _path = path;
            _metrics = null;
            SetStatus($"Opened {Path.GetFileName(path)}");
        }

        private void SaveLayout()
        {
            var path = _path ?? EditorUtility.SaveFilePanel("Save layout", ContentBuilder.LayoutsFolder, "L999", "txt");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, _doc.ToText());
            _path = path;
            AssetDatabase.Refresh();
            SetStatus($"Saved {Path.GetFileName(path)}");
        }

        private void Validate()
        {
            if (!_doc.IsValidCount)
            {
                SetStatus("Tile count must be a multiple of 3");
                return;
            }

            var result = Generate();
            if (result == null) return;
            _metrics = result.Metrics;
            SetStatus($"Generated in {result.Attempts} attempt(s): {DifficultyBands.Label(result.Band)}{(result.HitTarget ? "" : " (missed target)")}");
        }

        private void PlayInEditor()
        {
            var result = Generate();
            if (result == null) return;
            var serializer = new TileMatchLevelSerializer();
            var envelope = new Tileworks.Tiles.Levels.LevelEnvelope
            {
                Id = "editor_play", Game = serializer.Game, Payload = serializer.Write(result.Level),
            };
            EditorScenario.PlayLevel(envelope.ToJson());
        }

        private GenerationResult Generate()
        {
            try
            {
                var file = _doc.ToLayoutFile(_path == null ? "editor" : Path.GetFileNameWithoutExtension(_path));
                var generator = new LevelGenerator(TileMatchRules.CreateDefault(), new DifficultyBands(), new AnalyzerSettings { Playouts = 150 });
                var result = generator.Generate(new GenerationRequest
                {
                    Id = "editor", Layout = file.Layout, TargetBand = file.Band, Seed = StableHash.Hash64(file.Name), MaxAttempts = 30,
                    Assigner = ContentBuilder.AssignerFor(file),
                });
                if (result == null) SetStatus("No solvable kind assignment found; loosen the layout or lower the band");
                return result;
            }
            catch (System.Exception e)
            {
                SetStatus(e.Message);
                return null;
            }
        }

        private void SetStatus(string status)
        {
            _status = status;
            if (_statusLabel != null) _statusLabel.text = status;
            Repaint();
        }
    }
}
