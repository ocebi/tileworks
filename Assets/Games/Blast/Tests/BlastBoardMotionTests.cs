using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Blast.Controller;
using Blast.Model;
using Blast.View;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using PrimeTween;
using Tileworks.Kit.Core;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.Tests;
using Tileworks.Tiles.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Blast.Tests.BlastBoosterTests;

namespace Blast.Tests
{
    // Taps that set off several specials, played frame by frame through the game scene's board and the real presenters:
    // falls wait for what exploded, no piece jumps, and everything comes to rest on its cell. Landings the view reports go
    // back to the rules as the game presenter sends them, so a test can act while pieces are still falling.
    public sealed class BlastBoardMotionTests : IBoardLandingHandler
    {
        private const string ScenePath = "Assets/Games/Blast/Scenes/BlastGame.unity";
        private static readonly BlastRules Rules = new BlastRules();

        // Five colours on diagonals, so no two cubes group until a test places them.
        private static readonly string[] Grid = { "1234512", "3451234", "5123451", "2345123", "4512345", "1234512", "3451234" };

        private Scene _scene;
        private bool _muted;
        private BlastBoardView _board;
        private PresentationClock _clock;
        private EventPlayer _player;
        private RuleEngine<BlastState> _engine;
        private BoardFilm _film;
        private readonly List<string> _landingProblems = new List<string>();
        private readonly List<int> _landed = new List<int>();
        private readonly Dictionary<int, List<Vector3>> _paths = new Dictionary<int, List<Vector3>>();

        private static int Cell(int x, int y) => y * 7 + x;

        [SetUp]
        public void Open()
        {
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) Assert.Ignore("Close BlastGame in the Editor to run the board motion tests.");
            _muted = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = true;
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            _board = Find<BlastBoardView>();
            _clock = new PresentationClock();
            var kit = new BlastPresenterKit(_board, new Hud(), new Bar(), Rules, _clock, new Buzz());
            _board.BindLandings(this);
            var presenters = typeof(BlastPresenterKit).Assembly.GetTypes()
                .Where(t => typeof(IEventPresenter).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => (IEventPresenter)Activator.CreateInstance(t, kit))
                .ToList();
            _player = new EventPlayer(Rules.Registry, _clock, presenters, waveMode: true);
        }

        [TearDown]
        public void Close()
        {
            Tween.StopAll();
            _board.BindLandings(null);
            if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.CloseScene(_scene, true);
            EditorUtility.audioMasterMute = _muted;
        }

        [Test]
        public void RocketSetsOffARocketAndABomb()
        {
            var engine = Start(With(Grid, (1, 3, 'h'), (4, 3, 'v'), (6, 3, 'b')));
            Play(engine, Cell(1, 3));
        }

        [Test]
        public void RocketNextToABomb_Combines_AndSetsOffADiscoAndARocket()
        {
            var engine = Start(With(Grid, (3, 3, 'b'), (3, 4, 'h'), (0, 3, '*'), (3, 0, 'v')));
            Play(engine, Cell(3, 4));
        }

        [Test]
        public void DiscoNextToARocket_FiresRocketsAllOverTheBoard()
        {
            var engine = Start(With(Grid, (3, 3, '*'), (4, 3, 'h'), (1, 1, 'b')));
            Play(engine, Cell(3, 3));
        }

        [Test]
        public void SpecialsAboveAnExplosion_FallIntoPlace()
        {
            var engine = Start(With(Grid, (1, 6, 'h'), (2, 2, 'b'), (4, 1, 'v'), (5, 0, '*'), (4, 5, 'h')));
            Play(engine, Cell(1, 6));
        }

        [Test]
        public void DuckReachingTheBottom_LetsThePiecesAboveFallAgain()
        {
            var engine = Start(With(Grid, (0, 6, 'h'), (2, 5, 'd'), (5, 5, 'd'), (4, 2, 'b'), (2, 3, 'X')));
            Play(engine, Cell(0, 6));
        }

        [Test]
        public void BigGroup_MakesASpecial_ThatFalls()
        {
            var engine = Start(With(Grid, (2, 6, '5'), (3, 6, '5'), (4, 6, '5'), (2, 5, '5'), (3, 5, '5'), (4, 5, '5'), (3, 1, 'v')));
            Play(engine, Cell(3, 5));
        }

        [Test]
        public void Hammer_OnARocket_SetsItOffAndABomb()
        {
            var engine = Start(With(Grid, (2, 4, 'h'), (5, 4, 'b'), (2, 1, 'v')));
            Play(engine, new Hammer(Cell(2, 4)));
        }

        [Test]
        public void Shuffle_ThenARocket()
        {
            var engine = Start(With(Grid, (2, 4, 'h'), (5, 2, 'b'), (0, 0, 'x'), (6, 6, 'd')));
            Play(engine, new ShuffleBoard());
            var rocket = Enumerable.Range(0, engine.State.CellCount).First(c => engine.State.Pieces[c] == Piece.RocketRow);
            Play(engine, rocket);
        }

        // A refill streams down column 0 while the player taps a landed pair at the other side of the board.
        [Test]
        public void ATapOnALandedGroup_GoesThroughWhileAColumnFalls()
        {
            var engine = Start(With(Grid, (5, 6, '1'), (6, 6, '1')));
            var tapped = false;
            Play(engine, new Hammer(Cell(0, 6)), during: t =>
            {
                if (tapped || t < 0.05f) return;
                tapped = true;
                Assert.That(engine.State.Flight.Count, Is.GreaterThan(0), "column 0 is still falling");
                Assert.That(_player.IsBusy(engine.State.Ids[Cell(5, 6)]) || _player.IsBusy(engine.State.Ids[Cell(6, 6)]), Is.False);
                Submit(new Tap(Cell(5, 6)));
            });
            Assert.That(tapped, Is.True);
        }

        // Cubes falling into place fall further when the board clears under them: they keep going, land once, and rest on
        // their final cells.
        [Test]
        public void CubesInTheAir_FallFurther_WhenTheBoardClearsUnderThem()
        {
            var engine = Start(Grid);
            var s = engine.State;
            int[] flying = null;
            Play(engine, new Hammer(Cell(2, 1)), during: t =>
            {
                if (flying != null || t < 0.05f) return;
                flying = Enumerable.Range(0, s.CellCount).Where(c => s.Flight[c]).Select(c => s.Ids[c]).ToArray();
                Assert.That(flying, Is.Not.Empty);
                Submit(new Hammer(Cell(2, 5)));
            });
            foreach (var id in flying) Assert.That(_landed.Count(l => l == id), Is.EqualTo(1), $"piece {id} lands once");
        }

        // Pieces under an overhang are fed by slides: they move diagonally without jumping and land once.
        [Test]
        public void ABoxedColumn_IsFedBySlides()
        {
            var engine = Start(With(Grid, (3, 2, 'X'), (3, 5, '5'), (3, 6, '5')));
            Play(engine, new Tap(Cell(3, 6)));
            Assert.That(_landed.Distinct().Count(), Is.EqualTo(_landed.Count), "every piece lands once");
        }

        [Test]
        public void Playouts_FavouringSpecials([Values(1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL)] ulong seed)
        {
            var rows = With(Grid, (1, 1, 'h'), (5, 1, 'v'), (3, 3, 'b'), (0, 5, '*'), (2, 6, 'h'), (4, 0, 'b'), (6, 4, 'd'), (1, 4, 'X'));
            var engine = Rules.CreateEngine(Level(rows), seed);
            _board.Show(engine.State);
            var pick = new Pcg32(seed * 977);
            for (var move = 0; move < 10; move++)
            {
                var s = engine.State;
                var legal = Enumerable.Range(0, s.CellCount).Where(c => engine.Check(new Tap(c)).IsNone).ToArray();
                if (legal.Length == 0) break;
                var specials = legal.Where(c => Pieces.IsSpecial(s.Pieces[c])).ToArray();
                var pool = specials.Length > 0 && pick.NextInt(3) > 0 ? specials : legal;
                Play(engine, pool[pick.NextInt(pool.Length)], $"move {move}: ");
            }
        }

        private RuleEngine<BlastState> Start(string[] rows)
        {
            var engine = Rules.CreateEngine(Level(rows), 1);
            _board.Show(engine.State);
            return engine;
        }

        private void Play(RuleEngine<BlastState> engine, int cell, string context = "") => Play(engine, new Tap(cell), context);

        private void Play<T>(RuleEngine<BlastState> engine, T action, string context = "", Action<float> during = null) where T : struct, IGameAction
        {
            var s = engine.State;
            _engine = engine;
            _landingProblems.Clear();
            _landed.Clear();
            _paths.Clear();
            var before = Enumerable.Range(0, s.CellCount).Where(c => s.Pieces[c] != Piece.Empty).Select(c => s.Ids[c]).ToList();
            _film = new BoardFilm(_board.Get, _board.CellPosition, s.Width);
            _film.Begin(before);
            Submit(action, context);
            var start = _clock.Now;
            _film.Run(() => Settled(s), dt =>
            {
                _clock.Advance(dt);
                _board.Tick();
                during?.Invoke(_clock.Now - start);
                Trace(s);
            });

            var rest = Enumerable.Range(0, s.CellCount).Where(c => s.Pieces[c] != Piece.Empty).Select(c => (s.Ids[c], c));
            var problems = _landingProblems.Concat(_film.Problems()).Concat(_film.Overlaps()).Concat(Hitches()).Concat(_film.Misplaced(rest)).ToList();
            Assert.That(problems, Is.Empty, context + string.Join("\n", problems));
            Assert.That(s.Outcome != Outcome.Ongoing || s.Flight.Count == 0, Is.True, context + "pieces never landed");
        }

        // Where every falling piece is this frame.
        private void Trace(BlastState s)
        {
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (s.Pieces[cell] == Piece.Empty || !_board.Falls.IsMoving(s.Ids[cell])) continue;
                var piece = _board.Get(s.Ids[cell]);
                if (piece == null) continue;
                if (!_paths.TryGetValue(s.Ids[cell], out var path)) _paths[s.Ids[cell]] = path = new List<Vector3>();
                path.Add(piece.transform.localPosition);
            }
        }

        // A piece moving fast that all but stops for a frame and then moves fast again, as at a corner between two legs.
        private IEnumerable<string> Hitches()
        {
            const float fast = 4f * BoardFilm.FrameTime;
            const float still = 0.5f * BoardFilm.FrameTime;
            foreach (var pair in _paths)
            {
                var path = pair.Value;
                for (var i = 2; i + 1 < path.Count; i++)
                {
                    var before = Vector3.Distance(path[i - 1], path[i - 2]);
                    var during = Vector3.Distance(path[i], path[i - 1]);
                    var after = Vector3.Distance(path[i + 1], path[i]);
                    if (before < fast || during > still || after < fast) continue;
                    yield return $"piece {pair.Key} stalled at {path[i]} (frame {i} of its fall)";
                    break;
                }
            }
        }

        // The board is still: nothing plays, nothing falls, and the rules have heard every landing.
        private bool Settled(BlastState s) =>
            _player.IsIdle && _board.Falls.Count == 0 && _clock.Now >= _board.Falls.SquashUntil && (s.Flight.Count == 0 || s.Outcome != Outcome.Ongoing);

        // Applies a move and plays it, recording what the film must check.
        private void Submit<T>(T action, string context = "") where T : struct, IGameAction
        {
            var result = _engine.Apply(action);
            Assert.That(result.Accepted, Is.True, context + $"{action} was refused: {result.Rejected}");
            var events = _engine.Events.AsSpan().ToArray();
            var ids = Rules.Events;
            foreach (var e in events)
            {
                if (e.Type == ids.PieceFell)
                {
                    var fell = PieceFell.From(e);
                    _film.Fell(fell.Id, fell.Source, fell.Target);
                }
                else if (e.Type == ids.PieceSpawned)
                {
                    var spawned = PieceSpawned.From(e);
                    _film.Track(spawned.Id);
                    _film.Fell(spawned.Id, -1, spawned.Cell);
                }
                else if (e.Type == ids.SpecialCreated)
                {
                    _film.Track(e.Entity);
                }
                else if (e.Type == ids.PieceCleared)
                {
                    var cleared = PieceCleared.From(e);
                    _film.Cleared(cleared.Id, cleared.Cell);
                }
                else if (e.Type == ids.PieceShuffled || e.Type == ids.SpecialMerged || e.Type == ids.SpecialsCombined || e.Type == ids.SpecialFired ||
                         e.Type == ids.CubeConverted)
                {
                    _film.Free(e.Entity);
                }
            }

            _board.Falls.NextMove();
            _player.Play(events);
        }

        // As BlastTaps does; a piece reported twice without moving again, or reported at rest, is a problem.
        public void OnLanded(int id, int cell)
        {
            var s = _engine.State;
            if (s.Outcome != Outcome.Ongoing) return;
            _landed.Add(id);
            if (s.Pieces[cell] == Piece.Empty || s.Ids[cell] != id || !s.Flight[cell])
            {
                var resting = Enumerable.Range(0, s.CellCount).Any(c => s.Ids[c] == id && s.Pieces[c] != Piece.Empty && !s.Flight[c]);
                if (resting) _landingProblems.Add($"piece {id} reported landing at {cell} while at rest");
                return;
            }

            _board.SetOrigin(cell);
            Submit(new Land(cell, id));
        }

        private T Find<T>() where T : Component =>
            _scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<T>(true)).First(c => c != null);

        private sealed class Hud : IBlastHudView
        {
            public void Bind(IBlastHudHandler handler) { }
            public void SetGoals(IReadOnlyList<BlastGoal> goals) { }
            public void SetGoalLeft(int goal, int left) { }
            public void Collect(int goal, int left, Sprite sprite, Vector2 screen, float screenSize, in GoalFlight flight) { }
            public void SetMoves(int moves) { }
            public void SetInteractable(bool interactable) { }
            public void SetReplay(bool replay) { }
            public void ShowCascade(int count) { }
            public UniTask ShowIntro(int level, Tileworks.Tiles.View.LevelBadge badge, CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class Bar : IBoosterBarView
        {
            public void Bind(IBoosterBarHandler handler) { }
            public float ScreenTop => 0f;
            public void SetCount(int slot, int count) { }
            public void SetLock(int slot, int unlockLevel) { }
            public void SetInteractable(bool interactable) { }
            public UniTask IntroduceAsync(IReadOnlyList<int> slots, CancellationToken ct) => UniTask.CompletedTask;
            public void SetPicked(int slot, Rect boardScreen) { }
            public Vector2 ButtonScreenPoint(int slot) => Vector2.zero;
        }

        private sealed class Buzz : IHaptics
        {
            public bool Enabled { get; set; }
            public void Play(HapticKind kind) { }
        }
    }
}
