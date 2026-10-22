using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tileworks.Tiles.Levels;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // A game's levels in play order: drag to reorder, add, copy or take levels out, and edit the selected one alongside.
    // Clicks only queue their change; it runs at the start of the next layout pass, so a pass never sees the list change.
    public sealed class LevelSequenceWindow : EditorWindow
    {
        private const float ListWidth = 350f;
        private const float RowHeight = 46f;
        private const float CurveHeight = 70f;

        [SerializeField] private string _game;
        [SerializeField] private string _selected;

        private List<LevelDesigner> _designers;
        private LevelCatalog _catalog;
        private ReorderableList _list;
        private UnityEditor.Editor _inspector;
        private Vector2 _listScroll;
        private Vector2 _editScroll;
        private int _hovered = -1;
        private readonly LevelPublisher _publisher = new LevelPublisher();
        private string _publishState;

        [MenuItem("Tileworks/Levels", priority = 0)]
        public static void OpenWindow() => Open(null, null);

        public static void Open(string game, string id)
        {
            var window = GetWindow<LevelSequenceWindow>("Levels");
            window.minSize = new Vector2(800f, 500f);
            if (game != null) window.ShowGame(game, id);
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            _designers = LevelDesigners.All();
            EditorApplication.projectChanged += OnProjectChanged;
            _publisher.Finished += OnPublished;
            ShowGame(_game ?? _designers.FirstOrDefault()?.Game, _selected);
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            _publisher.Finished -= OnPublished;
            CloseInspector();
            _catalog?.Dispose();
        }

        private void OnProjectChanged()
        {
            _catalog?.Reload();
            _publishState = null;
            Repaint();
        }

        private void OnGUI()
        {
            if (_catalog == null)
            {
                EditorGUILayout.HelpBox("No game has a level designer.", MessageType.Info);
                return;
            }

            DrawToolbar();
            var curve = GUILayoutUtility.GetRect(position.width, CurveHeight);
            var was = _hovered;
            var clicked = DifficultyCurve.Draw(new Rect(curve.x + 6f, curve.y + 4f, curve.width - 12f, curve.height - 8f), _catalog, _catalog.IndexOf(_selected), out _hovered);
            if (clicked >= 0) Defer(() => Select(_catalog.Sequence[clicked].LevelId));
            if (Event.current.type == EventType.MouseMove && was != _hovered) Repaint();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.Width(ListWidth)))
                {
                    _listScroll = scroll.scrollPosition;
                    _list.DoLayoutList();
                    DrawLeftOut();
                }

                using (new EditorGUILayout.VerticalScope()) DrawEditor();
            }
        }

        private void ShowGame(string game, string id)
        {
            var designer = _designers.FirstOrDefault(d => d.Game == game);
            if (designer == null) return;
            _publishState = null;
            if (_catalog == null || _catalog.Designer.Game != game)
            {
                CloseInspector();
                _catalog?.Dispose();
                _catalog = new LevelCatalog(designer);
                _list = BuildList();
            }
            else
            {
                _catalog.Reload();
            }

            _game = game;
            Select(id);
        }

        private void Select(string id)
        {
            var entry = _catalog[id];
            if (entry?.Id != _selected) CloseInspector();
            _selected = entry?.Id;
            _list.index = _catalog.IndexOf(_selected);
            GUI.FocusControl(null);
            Repaint();
        }

        private ReorderableList BuildList() => new ReorderableList(_catalog.Sequence, typeof(LevelRef), true, true, false, false)
        {
            elementHeight = RowHeight,
            drawHeaderCallback = rect => GUI.Label(rect, $"Play order: {_catalog.Sequence.Count} levels. Drag to reorder."),
            drawElementCallback = (rect, index, active, focused) => DrawRow(rect, index + 1, _catalog.Sequence[index].LevelId, _catalog.Sequence[index].Reward),
            onSelectCallback = list => Defer(() => Select(_catalog.Sequence[list.index].LevelId)),
            onReorderCallback = _ => Defer(_catalog.Save),
        };

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var current = _designers.FindIndex(d => d.Game == _game);
                var chosen = EditorGUILayout.Popup(current, _designers.Select(d => d.Title).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(130f));
                if (chosen != current) Defer(() => ShowGame(_designers[chosen].Game, null));
                GUILayout.Label($"{_catalog.Sequence.Count} in play order, {_catalog.LeftOut.Count} left out", EditorStyles.miniLabel);
                _publishState ??= LevelPublisher.Describe(_catalog.Designer);
                GUILayout.Label(_publisher.Running ? "Publishing…" : _publishState, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("New level", "An empty level, placed after the selected one"), EditorStyles.toolbarButton)) Defer(() => AddLevel(false));
                using (new EditorGUI.DisabledScope(_catalog[_selected] == null))
                {
                    if (GUILayout.Button(new GUIContent("Duplicate", "A copy of the selected level, placed after it"), EditorStyles.toolbarButton)) Defer(() => AddLevel(true));
                }

                using (new EditorGUI.DisabledScope(_catalog.IndexOf(_selected) < 0))
                {
                    if (GUILayout.Button(new GUIContent("Take out", "Removes the level from the play order. The file stays, under Left out."), EditorStyles.toolbarButton))
                        Defer(TakeOut);
                }

                using (new EditorGUI.DisabledScope(_publisher.Running || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(new GUIContent("Publish", "Checks this game's saved levels and publishes them for installed games to download"),
                            EditorStyles.toolbarButton))
                        Defer(Publish);
                }
            }
        }

        private void Publish()
        {
            var designer = _catalog.Designer;
            var message = $"Publish {designer.Title}'s saved levels ({_catalog.Sequence.Count} in play order) to the live site?\n\n" +
                          "Players get them on their next start or return to Home. Unsaved edits aren't included.";
            if (EditorUtility.DisplayDialog("Publish levels", message, "Publish", "Cancel")) _publisher.Publish(designer);
        }

        private void OnPublished()
        {
            _publishState = null;
            ShowNotification(new GUIContent(_publisher.LastResult));
            Repaint();
        }

        private void DrawRow(Rect rect, int number, string id, int? reward)
        {
            var entry = _catalog[id];
            var thumb = new Rect(rect.x, rect.y + 3f, RowHeight - 6f, RowHeight - 6f);
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(thumb, new Color(0f, 0f, 0f, 0.25f));
            if (entry == null)
            {
                GUI.Label(new Rect(thumb.xMax + 8f, rect.y + 4f, 220f, 18f), $"{id}: file missing", EditorStyles.boldLabel);
                return;
            }

            var texture = _catalog.ThumbnailOf(entry);
            if (texture != null) GUI.DrawTexture(thumb, texture, ScaleMode.ScaleToFit);
            var x = thumb.xMax + 8f;
            if (number > 0) GUI.Label(new Rect(x, rect.y + 4f, 34f, 18f), number.ToString(), EditorStyles.boldLabel);
            var name = new GUIContent(id + (entry.Edited ? "  (hand-edited)" : string.Empty), entry.Edited ? "Regenerating levels keeps this one" : "Made by the generator");
            GUI.Label(new Rect(number > 0 ? x + 34f : x, rect.y + 4f, 200f, 18f), name);
            var chip = new Rect(x, rect.y + 25f, 66f, 15f);
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(chip, DifficultyCurve.Colour(entry.Difficulty));
            GUI.Label(chip, entry.Difficulty, EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(chip.xMax + 6f, rect.y + 24f, 110f, 18f), entry.WinRate is float rate ? $"bots win {rate:P0}" : "not measured", EditorStyles.miniLabel);
            if (reward.HasValue) GUI.Label(new Rect(rect.xMax - 70f, rect.y + 24f, 70f, 18f), $"{reward} coins", EditorStyles.miniLabel);
        }

        private void DrawLeftOut()
        {
            if (_catalog.LeftOut.Count == 0) return;
            EditorGUILayout.Space(8f);
            GUILayout.Label(new GUIContent($"Left out ({_catalog.LeftOut.Count})", "Level files that aren't in the play order"), EditorStyles.boldLabel);
            foreach (var id in _catalog.LeftOut.ToArray())
            {
                var rect = GUILayoutUtility.GetRect(ListWidth - 24f, RowHeight);
                if (id == _selected && Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.35f));
                DrawRow(new Rect(rect.x + 4f, rect.y, rect.width - 130f, rect.height), 0, id, null);
                if (GUI.Button(new Rect(rect.xMax - 122f, rect.y + 12f, 60f, 20f), new GUIContent("Put in", "Into the play order, after the selected level"))) Defer(() => Insert(id));
                else if (GUI.Button(new Rect(rect.xMax - 58f, rect.y + 12f, 56f, 20f), "Delete")) Defer(() => Delete(id));
                else if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    Defer(() => Select(id));
                    Event.current.Use();
                }
            }
        }

        private void DrawEditor()
        {
            var entry = _catalog[_selected];
            if (entry == null)
            {
                EditorGUILayout.HelpBox("Pick a level on the left or in the curve above, or press New level.", MessageType.Info);
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(entry.Path);
            using (new EditorGUILayout.HorizontalScope())
            {
                var index = _catalog.IndexOf(entry.Id);
                GUILayout.Label(index >= 0 ? $"Level {index + 1} of {_catalog.Sequence.Count}" : "Not in the play order", EditorStyles.boldLabel, GUILayout.Width(150f));
                if (index >= 0)
                {
                    var level = _catalog.Sequence[index];
                    GUILayout.Label(new GUIContent("Reward", "Coins for winning this level"), GUILayout.Width(48f));
                    var reward = EditorGUILayout.DelayedIntField(level.Reward, GUILayout.Width(50f));
                    if (reward != level.Reward)
                        Defer(() =>
                        {
                            _catalog.Sequence[index] = new LevelRef(index, level.LevelId, level.Label, Mathf.Max(0, reward));
                            _catalog.Save();
                        });
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Show file", "Highlights the level file in the Project window"), GUILayout.Width(80f))) EditorGUIUtility.PingObject(asset);
            }

            if (asset == null) return;
            if (_inspector == null || _inspector.target != asset)
            {
                CloseInspector();
                _inspector = UnityEditor.Editor.CreateEditor(asset, typeof(LevelFileInspector));
            }

            if (_inspector is LevelFileInspector inspector) inspector.Width = position.width - ListWidth - 30f;
            using (var scroll = new EditorGUILayout.ScrollViewScope(_editScroll))
            {
                _editScroll = scroll.scrollPosition;
                _inspector.OnInspectorGUI();
            }
        }

        // Runs after this GUI pass, outside OnGUI: a change mid-pass breaks the layout, and Unity cancels any dialog opened
        // inside one (publish, delete, save before switching levels).
        private void Defer(System.Action change)
        {
            void Run()
            {
                EditorApplication.update -= Run;
                if (this == null) return;
                change();
                Repaint();
            }

            EditorApplication.update += Run;
        }

        // Closing the open level asks to save it when it has changes.
        private void CloseInspector()
        {
            if (_inspector != null) DestroyImmediate(_inspector);
            _inspector = null;
        }

        private void AddLevel(bool copySelected)
        {
            CloseInspector();
            var source = copySelected && _catalog[_selected] != null ? LevelEnvelope.Parse(File.ReadAllText(_catalog[_selected].Path)) : null;
            var id = LevelContent.CreateFile(_catalog.Designer, source);
            _catalog.Reload();
            Insert(id);
        }

        // After the selected level when it's in the play order, otherwise at the end; the reward copies its neighbour's.
        private void Insert(string id)
        {
            var after = _catalog.IndexOf(_selected);
            var at = after >= 0 ? after + 1 : _catalog.Sequence.Count;
            var reward = _catalog.Sequence.Count == 0 ? 0 : _catalog.Sequence[Mathf.Clamp(at - 1, 0, _catalog.Sequence.Count - 1)].Reward;
            _catalog.Sequence.Insert(at, new LevelRef(at, id, string.Empty, reward));
            _catalog.Save();
            Select(id);
        }

        private void TakeOut()
        {
            _catalog.Sequence.RemoveAt(_catalog.IndexOf(_selected));
            _catalog.Save();
            Select(_selected);
        }

        private void Delete(string id)
        {
            if (!EditorUtility.DisplayDialog("Delete level", $"Delete the file {id}.json? This can't be undone here.", "Delete", "Cancel")) return;
            if (id == _selected)
            {
                CloseInspector();
                _selected = null;
            }

            AssetDatabase.DeleteAsset(_catalog[id].Path);
            LevelContent.RefreshPack(_catalog.Designer.Folder);
            _catalog.Reload();
            Select(_selected);
        }
    }
}
