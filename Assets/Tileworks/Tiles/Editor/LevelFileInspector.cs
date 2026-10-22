using System;
using System.Collections.Generic;
using System.IO;
using Tileworks.Tiles.Levels;
using UnityEditor;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // A level file (a JSON envelope whose game has a designer) opens as a board to paint; any other text asset keeps Unity's
    // own inspector. Edits go to a draft with undo and reach the file only on Save. A draft left unsaved by Play mode or a
    // recompile waits in SessionState and comes back when the level is selected again.
    [CustomEditor(typeof(TextAsset))]
    public sealed class LevelFileInspector : UnityEditor.Editor
    {
        private readonly List<string> _problems = new List<string>();
        private readonly SequencePanel _sequence = new SequencePanel();
        private UnityEditor.Editor _fallback;
        private LevelDesigner _designer;
        private LevelDraft _draft;
        private LevelEdit _edit;
        private LevelEnvelope _envelope;
        private LevelMeasure? _measure;
        private string _path;
        private string _saved;
        private string _loaded;

        // The width to lay the board out in; 0 uses the Inspector's.
        public float Width { get; set; }

        private bool Dirty => _draft != null && _draft.Json != _saved;
        private string DraftKey => "Tileworks.LevelDraft:" + _path;

        private void OnEnable()
        {
            _path = AssetDatabase.GetAssetPath(target);
            if (targets.Length == 1 && _path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && TryOpen()) return;
            var inspector = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.TextAssetInspector");
            if (inspector != null) _fallback = CreateEditor(targets, inspector);
        }

        private void OnDisable()
        {
            if (_fallback != null) DestroyImmediate(_fallback);
            if (_draft == null) return;
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (Dirty && File.Exists(_path))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) SessionState.SetString(DraftKey, _draft.Json);
                else if (EditorUtility.DisplayDialog("Unsaved level", $"Save the changes to {Path.GetFileName(_path)}?", "Save", "Discard")) Write();
                else SessionState.EraseString(DraftKey);
            }

            Undo.ClearUndo(_draft);
            DestroyImmediate(_draft);
        }

        public override void OnInspectorGUI()
        {
            if (_designer == null)
            {
                if (_fallback != null) _fallback.OnInspectorGUI();
                return;
            }

            DrawToolbar();
            DrawMeta();
            if (_problems.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", _problems), MessageType.Warning);
            EditorGUILayout.Space(4f);
            _edit.Reset();
            _edit.Difficulty = _envelope.Difficulty;
            _edit.Width = Width > 0f ? Width : EditorGUIUtility.currentViewWidth;
            _designer.OnGUI(_edit);
            if (!_edit.Changed) return;
            Commit();
            Repaint();
        }

        private bool TryOpen()
        {
            var text = File.ReadAllText(_path);
            if (!text.Contains("\"payload\"")) return false;
            LevelEnvelope envelope;
            try
            {
                envelope = LevelEnvelope.Parse(text);
            }
            catch (Exception)
            {
                return false;
            }

            _designer = LevelDesigners.Create(envelope.Game);
            if (_designer == null) return false;
            _draft = CreateInstance<LevelDraft>();
            _draft.hideFlags = HideFlags.DontSave;
            _edit = new LevelEdit(_draft);
            _saved = text;
            var stashed = SessionState.GetString(DraftKey, null);
            SessionState.EraseString(DraftKey);
            Load(string.IsNullOrEmpty(stashed) ? text : stashed);
            Undo.undoRedoPerformed += OnUndoRedo;
            return true;
        }

        private void Load(string json)
        {
            _envelope = LevelEnvelope.Parse(json);
            _draft.Json = json;
            _loaded = json;
            _measure = null;
            _designer.Load(_envelope.Payload);
            Check();
        }

        private void Commit()
        {
            _envelope.Payload = _designer.Save();
            _draft.Json = _envelope.ToJson();
            _loaded = _draft.Json;
            _measure = null;
            Check();
        }

        private void Check()
        {
            _problems.Clear();
            try
            {
                _designer.Validate(_envelope, _problems);
            }
            catch (Exception e)
            {
                _problems.Add(e.Message);
            }
        }

        private void OnUndoRedo()
        {
            if (_draft == null || _draft.Json == _loaded) return;
            Load(_draft.Json);
            Repaint();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(_envelope.Id + (Dirty ? "  •  unsaved" : string.Empty), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!Dirty))
                {
                    if (GUILayout.Button("Save", GUILayout.Width(60f))) Write();
                    if (GUILayout.Button("Revert", GUILayout.Width(60f)))
                    {
                        _edit.Change("Revert level");
                        Load(_saved);
                    }
                }

                using (new EditorGUI.DisabledScope(_problems.Count > 0))
                {
                    if (GUILayout.Button(new GUIContent("Measure", "Bot playouts: win rate and the difficulty it suggests"), GUILayout.Width(70f))) Measure();
                    if (_designer.CanPlay && GUILayout.Button(new GUIContent("Play", "Saves, then plays this level on the \"level-test\" profile"),
                            GUILayout.Width(50f)))
                    {
                        if (Dirty) Write();
                        _designer.Play(_envelope);
                    }
                }
            }
        }

        private void DrawMeta()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var difficulties = _designer.Difficulties;
                var index = Math.Max(0, Array.IndexOf(difficulties, _envelope.Difficulty));
                var chosen = EditorGUILayout.Popup("Difficulty", index, difficulties);
                if (chosen != index)
                {
                    _edit.Change("Difficulty");
                    _envelope.Difficulty = difficulties[chosen];
                    Commit();
                }

                GUILayout.Label(Metrics(), EditorStyles.miniLabel);
            }

            _sequence.Draw(_path, _envelope);
            if (_measure is { } m)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var moves = m.AverageMoves > 0f ? $", in {m.AverageMoves:0.#} moves on average" : string.Empty;
                    EditorGUILayout.HelpBox($"Bots win {m.WinRate:P0} of runs{moves}: {m.Difficulty}.", MessageType.Info);
                    if (GUILayout.Button("Use", GUILayout.Width(50f), GUILayout.Height(38f)))
                    {
                        _edit.Change("Measured difficulty");
                        _envelope.Difficulty = m.Difficulty;
                        _envelope.Metrics[m.Metric] = m.WinRate;
                        Commit();
                        _measure = m;
                    }
                }
            }
        }

        private string Metrics()
        {
            if (_envelope.Metrics.TryGetValue("winRate", out var winRate)) return $"measured win rate {winRate:P0}";
            if (_envelope.Metrics.TryGetValue("pCasual", out var casual)) return $"measured casual win rate {casual:P0}";
            return "not measured";
        }

        private void Measure()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Measuring", "Bot playouts…", 0.5f);
                _measure = _designer.Measure(_envelope);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // Saved levels carry the edited tag, so regenerating the content keeps them.
        private void Write()
        {
            if (!_envelope.Tags.Contains(LevelEnvelope.EditedTag)) _envelope.Tags.Add(LevelEnvelope.EditedTag);
            var json = _envelope.ToJson();
            File.WriteAllText(_path, json);
            _draft.Json = _loaded = _saved = json;
            AssetDatabase.ImportAsset(_path);
        }
    }
}
