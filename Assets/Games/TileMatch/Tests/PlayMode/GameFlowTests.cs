#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tileworks.Kit.App;
using Tileworks.Meta.Api;
using TileMatch.Api;
using TileMatch.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TileMatch.PlayModeTests
{
    // The real app on a throwaway save, driven by a virtual mouse through the EventSystem like a player.
    public sealed class GameFlowTests : InputTestFixture
    {
        private const string InstallerPath = "Assets/Games/TileMatch/Config/TileMatchAppInstaller.asset";
        private const string ReplayFolder = "Assets/Games/TileMatch/Tests/Replays";
        private const float Timeout = 20f;

        private Mouse _mouse;
        private string _saveRoot;
        private GameObject _events;
        private GameObject _app;
        private AppRoot _root;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _saveRoot = Path.Combine(Path.GetTempPath(), "tileworks-flow-" + Guid.NewGuid().ToString("N"));
            _events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule), typeof(AudioListener));
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (_app != null) Object.Destroy(_app);
            Object.Destroy(_events);
            yield return null;
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == "Home" || scene.name == "Game") yield return SceneManager.UnloadSceneAsync(scene);
            }

            AudioListener.volume = 1f;
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator Home_PlayWin_NextLevel_PauseSettings_BackHome()
        {
            yield return Boot();
            var coins = Service<IEconomy>().Balance(Tileworks.Meta.Api.Resources.Coins).Value;

            yield return Click("Play");
            yield return Until(() => Board()?.State?.TileCount == 9, "level 1 never started");
            foreach (var tile in ReplayTiles("tm_0001"))
            {
                yield return Click(Board().Camera.WorldToScreenPoint(Board().ViewOf(tile).transform.position));
                yield return Wait(0.2f);
            }

            yield return Until(() => Button("Next") != null, "the win popup never showed");
            Assert.That(Service<IEconomy>().Balance(Tileworks.Meta.Api.Resources.Coins).Value, Is.GreaterThan(coins), "no coin reward");
            Assert.That(Service<ILevelProgress>().CurrentIndex.Value, Is.EqualTo(1));

            yield return Click("Next");
            yield return Until(() => Board()?.State?.TileCount == 18, "level 2 never started");
            yield return Wait(0.3f);
            Assert.That(Board().State.ActionsTaken, Is.Zero, "the Next click also tapped the new board");

            yield return Click("Pause");
            yield return Until(() => Button("Sound") != null, "the pause popup never showed");
            yield return Click("Sound");
            Assert.That(Service<IUserSettings>().Sound.Value, Is.False);
            Assert.That(AudioListener.volume, Is.Zero);

            yield return Click("Home");
            yield return Until(() => Button("Play") != null, "Home never came back");
        }

        private IEnumerator Boot()
        {
            _app = new GameObject("AppRoot");
            _app.SetActive(false);
            _root = _app.AddComponent<AppRoot>();
            _root.Configure(AssetDatabase.LoadAssetAtPath<AppInstaller>(InstallerPath), new KitOptions { PersistentRoot = _saveRoot });
            _app.SetActive(true);
            yield return Until(() => Button("Play") != null, "Home never showed");
        }

        private T Service<T>() => _root.Scope.Resolve<T>();

        private static BoardView Board() => Object.FindAnyObjectByType<BoardView>();

        // Only buttons a player could press right now: active, and not behind a hidden or blocking canvas group.
        private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.name == name && b.IsActive() && b.IsInteractable() && Visible(b) && Settled(b));

        // Popups and their buttons scale in, and a player can only hit a button once it is near full size.
        private static bool Settled(Component c)
        {
            for (var t = c.transform; t != null && t.GetComponent<Canvas>() == null; t = t.parent)
            {
                if (t.localScale.x < 0.95f) return false;
            }

            return true;
        }

        private static bool Visible(Component c)
        {
            foreach (var group in c.GetComponentsInParent<CanvasGroup>())
            {
                if (!group.blocksRaycasts || group.alpha <= 0f) return false;
            }

            return true;
        }

        private static IEnumerable<int> ReplayTiles(string levelId)
        {
            var replay = JObject.Parse(File.ReadAllText(Path.Combine(ReplayFolder, levelId + ".json")));
            return replay["actions"].Select(a => (int)a[1]).ToList();
        }

        // Waits like a player would for the button to finish appearing, then taps it.
        private IEnumerator Click(string name)
        {
            yield return Until(() => Button(name) != null, $"{name} never became clickable");
            var button = Button(name);
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, button.transform.position));
        }

        private IEnumerator Click(Vector2 screen)
        {
            Set(_mouse.position, screen);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private static IEnumerator Until(Func<bool> condition, string failure)
        {
            var end = Time.realtimeSinceStartup + Timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail(failure);
                yield return null;
            }
        }
    }
}
#endif
