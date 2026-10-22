#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Match3.Api;
using Match3.Model;
using Match3.View;
using NUnit.Framework;
using Tileworks.Kit.App;
using Tileworks.Meta.Api;
using Tileworks.Tiles.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Match3.PlayModeTests
{
    // The real app on a throwaway save; gems are swiped with a virtual mouse like a player would.
    public sealed class Match3FlowTests : InputTestFixture
    {
        private const string InstallerPath = "Assets/Games/Match3/Config/Match3AppInstaller.asset";
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
            _saveRoot = Path.Combine(Path.GetTempPath(), "tileworks-match3-" + Guid.NewGuid().ToString("N"));
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
                if (scene.name == "Match3Home" || scene.name == "Match3Game") yield return SceneManager.UnloadSceneAsync(scene);
            }

            AudioListener.volume = 1f;
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator Home_SwipeGemsToWin_NextLevel_Pause_BackHome()
        {
            yield return Boot();
            var coins = Service<IEconomy>().Balance(Tileworks.Meta.Api.Resources.Coins).Value;

            yield return Click("Play");
            yield return Until(() => Board()?.State != null, "level 1 never started");
            for (var move = 0; move < 40 && Board().State.Outcome == Outcome.Ongoing; move++)
            {
                yield return Until(() => Board().State.Flight.Count == 0 || Board().State.Outcome != Outcome.Ongoing, "gems never landed");
                if (Board().State.Outcome != Outcome.Ongoing) break;
                var used = Board().State.MovesUsed;
                yield return SwipeBestSwap();
                Assert.That(Board().State.MovesUsed, Is.EqualTo(used + 1), "the swipe was not played");
                yield return Wait(0.3f);
            }

            yield return Until(() => Button("Next") != null, "the win popup never showed");
            Assert.That(Service<IEconomy>().Balance(Tileworks.Meta.Api.Resources.Coins).Value, Is.GreaterThan(coins), "no coin reward");
            Assert.That(Service<IMatch3Progress>().CurrentIndex.Value, Is.EqualTo(1));

            yield return Click("Next");
            yield return Until(() => Board().State.Level.Id == "m3_0002", "level 2 never started");
            yield return Wait(0.3f);
            Assert.That(Board().State.MovesUsed, Is.Zero, "the Next click also played a move");

            yield return Click("Pause");
            yield return Until(() => Button("Sound") != null, "the pause popup never showed");
            yield return Click("Sound");
            Assert.That(Service<IUserSettings>().Sound.Value, Is.False);

            yield return Click("Home");
            yield return Until(() => Button("Play") != null, "Home never came back");
        }

        // The 15 Oct bug: swipes made while a cascade falls left phantom clears and stacked gems. Swipes now go through
        // only on gems that have landed, and the board always ends up matching the rules.
        [UnityTest]
        public IEnumerator SwipesDuringFalls_ActOnLandedGemsOnly_AndTheBoardMatchesTheRules()
        {
            yield return Boot();
            yield return Click("Play");
            yield return Until(() => Board()?.State != null && Board().State.Flight.Count == 0, "level 1 never started");
            var midFall = 0;
            for (var move = 0; move < 25 && Board().State.Outcome == Outcome.Ongoing; move++)
            {
                var state = Board().State;
                if (state.Flight.Count > 0) midFall++;
                yield return SwipeBestSwap(1, false);
                yield return Wait(0.06f);
            }

            yield return Until(() => Board().State.Outcome != Outcome.Ongoing || Board().State.Flight.Count == 0, "pieces never landed");
            yield return Wait(1.5f);
            var board = Board();
            var s = board.State;
            var pieces = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (s.Pieces[cell] == Piece.Empty) continue;
                pieces++;
                var renderer = board.Get(s.Ids[cell]);
                Assert.That(renderer, Is.Not.Null, $"no gem drawn for cell {cell}");
                Assert.That(Vector3.Distance(renderer.transform.localPosition, board.CellPosition(cell)), Is.LessThan(0.02f), $"cell {cell}'s gem rests off its cell");
            }

            Assert.That(board.PieceCount, Is.EqualTo(pieces), "a gem the rules removed is still drawn");
            Assert.That(midFall, Is.GreaterThan(0), "no swipe was made while gems fell");
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

        // The swap a greedy player would make among the landed gems, dragged from one gem towards its neighbour. A swipe
        // is ignored while a special plays, so it waits between tries.
        // `mustHaveMove` false: with no swap among the landed gems, nothing is swiped.
        private IEnumerator SwipeBestSwap(int attempts = 20, bool mustHaveMove = true)
        {
            var board = Board();
            var state = board.State;
            var domain = new Match3SearchDomain(new Match3Rules(), state.Level);
            var moves = new Swap[state.CellCount * 2];
            var count = domain.GetMoves(state, moves);
            if (count == 0 && !mustHaveMove) yield break;
            Assert.That(count, Is.GreaterThan(0), "no move");
            var best = moves.Take(count).OrderByDescending(m => domain.Score(state, m)).First();
            var used = state.MovesUsed;
            for (var attempt = 0; attempt < attempts && board.State.MovesUsed == used; attempt++)
            {
                Vector2 from = board.Camera.WorldToScreenPoint(board.CellCentre(best.From));
                Vector2 to = board.Camera.WorldToScreenPoint(board.CellCentre(best.To));
                RequireOnScreen(from);
                Set(_mouse.position, from);
                yield return null;
                Press(_mouse.leftButton);
                yield return null;
                for (var step = 1; step <= 4; step++)
                {
                    Set(_mouse.position, Vector2.Lerp(from, to, step / 4f));
                    yield return null;
                }

                Release(_mouse.leftButton);
                yield return null;
                if (board.State.MovesUsed == used) yield return Wait(0.2f);
            }
        }

        // In the Editor, Screen is another window's size while the Game view or Simulator isn't drawing, and a press off it
        // reaches nothing.
        private static void RequireOnScreen(Vector2 point)
        {
            if (!new Rect(0f, 0f, Screen.width, Screen.height).Contains(point))
                Assert.Inconclusive($"board point {point} is off the {Screen.width}x{Screen.height} screen: the Editor isn't drawing the Game view or Simulator; bring Unity to the front and rerun");
        }

        private T Service<T>() => _root.Scope.Resolve<T>();

        private static Match3BoardView Board() => Object.FindAnyObjectByType<Match3BoardView>();

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
        // Fails with what was hit when the tap lands on something else or the button doesn't take it.
        private IEnumerator Click(string name)
        {
            yield return Until(() => Button(name) != null, $"{name} never became clickable");
            var button = Button(name);
            var screen = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);

            // An overlay that closes by itself, like the level-start card, is waited out.
            var end = Time.realtimeSinceStartup + Timeout;
            while (TopHit(screen) is { } over && !over.transform.IsChildOf(button.transform) && Time.realtimeSinceStartup < end) yield return null;
            var hit = TopHit(screen);
            Assert.That(hit != null && hit.transform.IsChildOf(button.transform), $"{name} at {screen} is under {(hit != null ? hit.name : "nothing")}");
            var clicked = false;
            UnityAction mark = () => clicked = true;
            button.onClick.AddListener(mark);
            Set(_mouse.position, screen);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
            button.onClick.RemoveListener(mark);
            Assert.That(clicked, $"{name} was pressed at {screen} but took no click (under the pointer now: {TopHit(screen)?.name ?? "nothing"})");
        }

        private static GameObject TopHit(Vector2 screen)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, results);
            return results.Count > 0 ? results[0].gameObject : null;
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
