using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Tileworks.Kit.App
{
    // A developer launch state (§11.13): seeds what it needs, then navigates.
    public interface IScenarioHandler
    {
        string Kind { get; }
        UniTask RunAsync(string payload, CancellationToken ct);
    }

    [Serializable]
    public sealed class ScenarioRequest
    {
        public string kind;
        public string payload;

        // Non-empty: the app runs on a scenario profile instead of the player's save.
        public string profile;
        public bool fresh;
    }

    // Editor tools drop a request into Library/. The launch state runs once, in the first app with a handler for it; the
    // profile applies to every app of that Play session, so a game switch never reaches the player's own saves. Leaving
    // Play deletes the file (Kit Editor).
    public static class ScenarioFile
    {
        public static string Path => System.IO.Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? ".", "Library", "TileworksScenario.json");

        public static void Write(ScenarioRequest request) => File.WriteAllText(Path, JsonUtility.ToJson(request));

        public static void Delete()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }

        // The launch state, once: the file keeps the profile and loses the state.
        public static bool TryTake(out ScenarioRequest request)
        {
            if (!TryPeek(out request) || string.IsNullOrEmpty(request.kind))
            {
                request = null;
                return false;
            }

            Write(new ScenarioRequest { profile = request.profile, fresh = request.fresh });
            return true;
        }

        // Once the profile has been wiped: the apps that follow in the session keep what the first ones saved.
        public static void ClearFresh()
        {
            if (TryPeek(out var request) && request.fresh)
                Write(new ScenarioRequest { kind = request.kind, payload = request.payload, profile = request.profile });
        }

        public static bool TryPeek(out ScenarioRequest request)
        {
            request = null;
#if UNITY_EDITOR
            if (!File.Exists(Path)) return false;
            request = JsonUtility.FromJson<ScenarioRequest>(File.ReadAllText(Path));
            return request != null && (!string.IsNullOrEmpty(request.kind) || !string.IsNullOrEmpty(request.profile));
#else
            return false;
#endif
        }

        public static string ProfileRoot(string profile) =>
            System.IO.Path.Combine(Application.persistentDataPath, "Scenarios", profile);
    }
}
