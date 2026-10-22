using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Tileworks.Tiles.Levels;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Tileworks.Tiles.Editor
{
    // The Levels window's Publish button: exports one game, runs Tools/publish_levels.py in the background and reports the
    // result (docs/REMOTE-LEVELS.md).
    public sealed class LevelPublisher
    {
        private const string Script = "Tools/publish_levels.py";
        private const double TimeoutSeconds = 180;
        private static readonly Regex ResultLine = new Regex(@"^RESULT (\S+) (published|unchanged|committed) (\d+)$", RegexOptions.Multiline);

        private readonly StringBuilder _output = new StringBuilder();
        private Process _process;
        private LevelDesigner _designer;
        private double _deadline;

        public bool Running => _process != null;
        public string LastResult { get; private set; } = string.Empty;
        public event Action Finished;

        // "Published r3", with whether the levels changed since; or "Not published".
        public static string Describe(LevelDesigner designer)
        {
            var path = LevelContent.PublishedPath(designer.Folder);
            if (!File.Exists(path)) return "Not published";
            try
            {
                var record = JObject.Parse(File.ReadAllText(path));
                var changed = !Matches(record["levels"] as JArray, designer.Folder);
                return $"Published r{(int?)record["revision"] ?? 0}" + (changed ? ", changed since" : ", up to date");
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return "Published (unreadable published.json)";
            }
        }

        public void Publish(LevelDesigner designer, string extraArguments = "")
        {
            if (Running) return;
            var problems = new List<string>();
            if (RemoteLevelsExport.ExportGame(designer, RemoteLevelsExport.OutputFolder, problems) == null)
            {
                LastResult = $"Not published: {problems.Count} level problem(s), listed in the Console";
                Debug.LogError($"[Tileworks] {designer.Title} levels not published:\n  " + string.Join("\n  ", problems));
                Finished?.Invoke();
                return;
            }

            var start = new ProcessStartInfo("python3", $"-u {Script} --game {designer.Game} {extraArguments}")
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            // A push that needs a password fails instead of waiting for a prompt nobody sees.
            start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            start.EnvironmentVariables["PATH"] = ShellPath(start.EnvironmentVariables["PATH"]);
            _output.Clear();
            _designer = designer;
            _process = new Process { StartInfo = start };
            _process.OutputDataReceived += (_, e) => Append(e.Data);
            _process.ErrorDataReceived += (_, e) => Append(e.Data);
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            LastResult = "Publishing…";
            EditorApplication.update += Poll;
        }

        // Unity started from the Dock gets the system's minimal PATH, without the user's tools (git-lfs, which the Pages
        // repo's pre-push hook needs, Homebrew).
        private static string ShellPath(string path)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            var parts = new List<string>((path ?? string.Empty).Split(':'));
            foreach (var extra in new[] { "/usr/local/bin", "/opt/homebrew/bin", Path.Combine(home, ".local/bin") })
                if (Directory.Exists(extra) && !parts.Contains(extra)) parts.Insert(0, extra);
            return string.Join(":", parts);
        }

        private void Append(string line)
        {
            if (line == null) return;
            lock (_output) _output.AppendLine(line);
        }

        private void Poll()
        {
            if (!_process.HasExited)
            {
                if (EditorApplication.timeSinceStartup < _deadline) return;
                _process.Kill();
            }

            _process.WaitForExit();
            EditorApplication.update -= Poll;
            var exitCode = _process.HasExited ? _process.ExitCode : -1;
            _process.Dispose();
            _process = null;
            string output;
            lock (_output) output = _output.ToString().Trim();

            // A RESULT line means the levels are out; only the CDN warm-up after it can still run over the time limit.
            var match = ResultLine.Match(output);
            if (match.Success)
            {
                AssetDatabase.Refresh();
                LevelContent.RefreshPack(_designer.Folder);
                var revision = match.Groups[3].Value;
                LastResult = match.Groups[2].Value == "published" ? $"Published r{revision}" :
                    match.Groups[2].Value == "unchanged" ? $"Nothing changed since r{revision}" : $"Committed r{revision}, not pushed";
                Debug.Log($"[Tileworks] {_designer.Title}: {LastResult}\n{output}");
            }
            else
            {
                LastResult = exitCode == 0 ? "Publish finished without a result; see the Console" : "Publish failed; see the Console";
                Debug.LogError($"[Tileworks] {_designer.Title} publish failed (exit {exitCode}):\n{output}");
            }

            Finished?.Invoke();
        }

        private static bool Matches(JArray published, string folder)
        {
            var sequence = LevelContent.Sequence(folder);
            if (published == null || published.Count != sequence.Count) return false;
            for (var i = 0; i < sequence.Count; i++)
            {
                var entry = published[i];
                var level = sequence[i];
                var path = LevelContent.PathOf(folder, level.LevelId);
                if ((string)entry["id"] != level.LevelId || ((int?)entry["reward"] ?? 0) != level.Reward) return false;
                if (((string)entry["label"] ?? string.Empty) != (level.Label ?? string.Empty) || !File.Exists(path)) return false;
                if ((string)entry["hash"] != LevelEnvelope.Parse(File.ReadAllText(path)).ContentHash) return false;
            }

            return true;
        }
    }
}
