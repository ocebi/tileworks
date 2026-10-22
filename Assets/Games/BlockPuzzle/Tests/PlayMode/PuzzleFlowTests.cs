#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using BlockPuzzle.Api;
using BlockPuzzle.Model;
using BlockPuzzle.View;
using NUnit.Framework;
using Tileworks.Kit.App;
using Tileworks.Meta.Api;
using Tileworks.Tiles.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BlockPuzzle.PlayModeTests
{
    // The real app on a throwaway save; pieces are dragged with a virtual mouse like a player would.
    public sealed class PuzzleFlowTests : InputTestFixture
    {
        private const string InstallerPath = "Assets/Games/BlockPuzzle/Config/BlockPuzzleAppInstaller.asset";
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
            _saveRoot = Path.Combine(Path.GetTempPath(), "tileworks-puzzle-" + Guid.NewGuid().ToString("N"));
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
                if (scene.name == "BlockHome" || scene.name == "BlockGame") yield return SceneManager.UnloadSceneAsync(scene);
            }

            AudioListener.volume = 1f;
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator Home_DragPiecesToWin_NextLevel_Pause_BackHome()
        {
            yield return Boot();
            var coins = Service<IEconomy>().Balance(Tileworks.Meta.Api.Resources.Coins).Value;

            yield return Click("Play");
            yield return Until(() => Board()?.State != null, "level 1 never started");
            for (var move = 0; move < 40 && Board().State.Outcome == Outcome.Ongoing; move++)
            {
                var placements = Board().State.Placements;
                yield return DragBestPiece();
                Assert.That(Board().State.Placements, Is.EqualTo(placements + 1), "the drop was not placed");
                yield return Wait(0.3f);
            }

            yield return Until(() => Button("Next") != null, "the win popup never showed");
            Assert.That(Service<IEconomy>().Balance(Tileworks.Meta.Api.Resources.Coins).Value, Is.GreaterThan(coins), "no coin reward");
            Assert.That(Service<IPuzzleProgress>().CurrentIndex.Value, Is.EqualTo(1));

            yield return Click("Next");
            yield return Until(() => Board().State.Level.Id == "bp_0002", "level 2 never started");
            yield return Wait(0.3f);
            Assert.That(Board().State.Placements, Is.Zero, "the Next click also dropped a piece");

            yield return Click("Pause");
            yield return Until(() => Button("Sound") != null, "the pause popup never showed");
            yield return Click("Sound");
            Assert.That(Service<IUserSettings>().Sound.Value, Is.False);

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

        // The placement that completes the most lines, dragged from the offer to its drop point.
        private IEnumerator DragBestPiece()
        {
            var board = Board();
            var state = board.State;
            int slot = -1, anchor = -1, best = -1;
            for (var s = 0; s < BlockState.OfferSize; s++)
            {
                var piece = board.PieceAt(s);
                if (piece == null) continue;
                for (var a = 0; a < state.CellCount; a++)
                {
                    if (!Placement.Fits(state, piece.Shape, a)) continue;
                    var (rows, columns) = Placement.LinesCompletedBy(state, piece.Shape, a);
                    var lines = Bits(rows) + Bits(columns);
                    if (lines <= best) continue;
                    (slot, anchor, best) = (s, a, lines);
                }
            }

            Assert.That(slot, Is.Not.EqualTo(-1), "no piece fits");
            var from = board.Camera.WorldToScreenPoint(board.PieceAt(slot).Root.position);
            var to = board.Camera.WorldToScreenPoint(board.DropPoint(slot, anchor));
            Set(_mouse.position, (Vector2)from);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            Set(_mouse.position, Vector2.Lerp(from, to, 0.5f));
            yield return null;
            Set(_mouse.position, (Vector2)to);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
        }

        private static int Bits(int mask)
        {
            var count = 0;
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }

        private T Service<T>() => _root.Scope.Resolve<T>();

        private static PuzzleBoardView Board() => Object.FindAnyObjectByType<PuzzleBoardView>();

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

        private static bool Visible(Component c) => c.GetComponentsInParent<CanvasGroup>().All(g => g.blocksRaycasts && g.alpha > 0f);

        // Waits like a player would for the button to finish appearing, then taps it.
        private IEnumerator Click(string name)
        {
            yield return Until(() => Button(name) != null, $"{name} never became clickable");
            var button = Button(name);
            Set(_mouse.position, RectTransformUtility.WorldToScreenPoint(null, button.transform.position));
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
