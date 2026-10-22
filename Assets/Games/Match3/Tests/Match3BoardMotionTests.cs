using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Match3.Controller;
using Match3.Model;
using Match3.View;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using PrimeTween;
using Tileworks.Kit.Core;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.Tests;
using Tileworks.Tiles.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Match3.Tests.Match3RulesTests;

namespace Match3.Tests
{
    // Moves that set off several specials, played frame by frame through the game scene's board and the real presenters,
    // with every landing reported by the view going back to the rules as the game does: falls wait for what exploded, no
    // piece jumps, every piece in flight lands once, and everything comes to rest on its cell.
    public sealed class Match3BoardMotionTests : IPieceLandingHandler
    {
        private const string ScenePath = "Assets/Games/Match3/Scenes/Match3Game.unity";
        private static readonly Match3Rules Rules = new Match3Rules();

        // Five colours on diagonals, so nothing lines up until a test places something.
        private static readonly string[] Grid = { "1234512", "3451234", "5123451", "2345123", "4512345", "1234512", "3451234" };

        private Scene _scene;
        private bool _muted;
        private Match3BoardView _board;
        private PresentationClock _clock;
        private EventPlayer _player;
        private Match3PresenterKit _kit;
        private RuleEngine<Match3State> _engine;
        private BoardFilm _film;
        private readonly List<string> _landingProblems = new List<string>();
        private readonly List<int> _landed = new List<int>();
        private bool _settledBeforeLast;
        private float _lastLandingAt;
        private float _settledAt;

        private static int Cell(int x, int y) => y * 7 + x;

        [SetUp]
        public void Open()
        {
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) Assert.Ignore("Close Match3Game in the Editor to run the board motion tests.");
            _muted = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = true;
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            _board = Find<Match3BoardView>();
            _clock = new PresentationClock();
            _kit = new Match3PresenterKit(_board, Find<Match3BoosterBoard>(), new Hud(), new Bar(), Rules, _clock, new Buzz());
            _board.BindLandings(this);
            var presenters = typeof(Match3PresenterKit).Assembly.GetTypes()
                .Where(t => typeof(IEventPresenter).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => (IEventPresenter)Activator.CreateInstance(t, _kit))
                .ToList();
            _player = new EventPlayer(Rules.Registry, _clock, presenters, waveMode: true);
            PrimeTweenConfig.SetTweensCapacity(_board.Feel.TweenCapacity);
        }

        [TearDown]
        public void Close()
        {
            Tween.StopAll();
            if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.CloseScene(_scene, true);
            EditorUtility.audioMasterMute = _muted;
        }

        [Test]
        public void RocketSetsOffARocketAndABomb()
        {
            var engine = Start(With(Grid, (1, 3, 'h'), (4, 3, 'v'), (6, 3, 'b')));
            Play(engine, new Swap(Cell(1, 3), Cell(0, 3)));
        }

        [Test]
        public void BombAndRocketCombo_SetsOffADiscoAndARocket()
        {
            var engine = Start(With(Grid, (3, 3, 'b'), (3, 4, 'h'), (0, 3, '*'), (3, 0, 'v')));
            Play(engine, new Swap(Cell(3, 4), Cell(3, 3)));
        }

        [Test]
        public void DiscoAndRocketCombo_FiresRocketsAllOverTheBoard()
        {
            var engine = Start(With(Grid, (3, 3, '*'), (4, 3, 'h'), (1, 1, 'b')));
            Play(engine, new Swap(Cell(4, 3), Cell(3, 3)));
        }

        [Test]
        public void SpecialsAboveAnExplosion_FallIntoPlace()
        {
            var engine = Start(With(Grid, (1, 6, 'h'), (2, 2, 'b'), (4, 1, 'v'), (5, 0, '*'), (4, 5, 'h')));
            Play(engine, new Swap(Cell(1, 6), Cell(0, 6)));
        }

        [Test]
        public void BombsAmongBlockersAndHoles_SlidePiecesInAroundThem()
        {
            var rows = With(Grid, (3, 3, 'b'), (2, 4, 'x'), (4, 4, 'X'), (3, 6, '#'), (0, 5, 'd'), (5, 2, 'v'), (2, 1, 'b'));
            var chains = new[] { ".......", ".......", ".c.....", ".......", ".......", ".......", "......." };
            var engine = Rules.CreateEngine(Level(rows, chains: chains), 3);
            Show(engine);
            Play(engine, new Swap(Cell(3, 3), Cell(3, 2)));
        }

        [Test]
        public void FlyersOverSandAndDucks()
        {
            var rows = With(Grid, (3, 3, 'f'), (4, 3, 'f'), (1, 2, 'd'), (5, 5, 'x'), (2, 0, 'h'));
            var floor = new[] { ".......", ".......", "..11...", "..22...", ".......", "...1...", "......." };
            var engine = Rules.CreateEngine(Level(rows, floor: floor), 2);
            Show(engine);
            Play(engine, new Swap(Cell(4, 3), Cell(3, 3)));
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
            var s = engine.State;
            var rocket = Enumerable.Range(0, s.CellCount).First(c => s.Pieces[c] == Piece.RocketRow);
            var swap = new[] { rocket + 1, rocket - 1, rocket + 7, rocket - 7 }.Select(n => new Swap(rocket, n)).First(m => engine.Check(m).IsNone);
            Play(engine, swap);
        }

        // A refill streams down column 0 while the player makes a match at the other side of the board.
        [Test]
        public void ASwapBetweenLandedGems_GoesThroughWhileAColumnFalls()
        {
            var engine = Start(With(Grid, (5, 6, '1'), (6, 6, '1'), (4, 5, '1')));
            var swap = new Swap(Cell(4, 5), Cell(4, 6));
            var swapped = false;
            Play(engine, new Hammer(Cell(0, 6)), during: t =>
            {
                if (swapped || t < 0.05f) return;
                swapped = true;
                Assert.That(engine.State.Flight.Count, Is.GreaterThan(0), "column 0 is still falling");
                Assert.That(_player.IsBusy(engine.State.Ids[Cell(4, 5)]) || _player.IsBusy(engine.State.Ids[Cell(4, 6)]), Is.False);
                Submit(swap);
            });
            Assert.That(swapped, Is.True);
        }

        // Gems falling into place fall further when the board clears under them: they keep going, land once, and rest on
        // their final cells.
        [Test]
        public void GemsInTheAir_FallFurther_WhenTheBoardClearsUnderThem()
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

        // The cell under a box is fed by slides only. Clearing under it while the column below is still falling makes a
        // gem slide into the pocket: it waits for the gems falling through the pocket to pass, so none goes through it.
        [Test]
        public void ASlideIntoAPocket_WaitsForTheGemsFallingThroughIt()
        {
            var engine = Start(With(Grid, (3, 2, 'X')));
            var hit = false;
            Play(engine, new Hammer(Cell(3, 6)), during: t =>
            {
                if (hit || t < 0.06f) return;
                hit = true;
                Assert.That(engine.State.Flight.Count, Is.GreaterThan(0), "the column is still falling");
                Submit(new Hammer(Cell(3, 6)));
            });
            Assert.That(hit, Is.True);
        }

        // A rocket under a box empties the column below it, which only slides can refill: the gems queue at the pocket's
        // two openings and slide in one after another, each a cell behind the one before.
        [Test]
        public void APocketUnderABox_FillsBySlidesInTurn()
        {
            Play(Start(With(Grid, (3, 2, 'X'), (3, 5, 'v'))), new Activate(Cell(3, 5)));
        }

        // A pocket level refills a column in more than one batch within a move (a slide reopens its top): each batch
        // comes down behind the gems already falling there.
        [Test]
        public void APocketLevel_RefillsEachColumnInOrder([Values(1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL)] ulong seed)
        {
            var level = new Match3LevelSerializer().Read(LevelEnvelope.Parse(File.ReadAllText("Assets/Games/Match3/Content/Levels/m3_0023.json")));
            var engine = Rules.CreateEngine(level, seed);
            Show(engine);
            var domain = new Match3SearchDomain(Rules, level);
            var moves = new Swap[level.CellCount * 2];
            for (var move = 0; move < 6 && engine.State.Outcome == Outcome.Ongoing; move++)
            {
                var count = domain.GetMoves(engine.State, moves);
                if (count == 0) break;
                var s = engine.State;
                Play(engine, moves.Take(count).OrderByDescending(m => domain.Score(s, m)).First(), $"move {move}: ");
            }
        }

        [Test]
        public void Playouts_FavouringSpecials([Values(1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL)] ulong seed)
        {
            var rows = With(Grid, (1, 1, 'h'), (5, 1, 'v'), (3, 3, 'b'), (0, 5, '*'), (6, 5, 'f'), (2, 6, 'h'), (4, 0, 'b'));
            var engine = Rules.CreateEngine(Level(rows), seed);
            Show(engine);
            var pick = new Pcg32(seed * 977);
            for (var move = 0; move < 10; move++)
            {
                var s = engine.State;
                var legal = Enumerable.Range(0, s.CellCount).SelectMany(a => new[] { new Swap(a, a + 1), new Swap(a, a + 7) })
                    .Where(m => engine.Check(m).IsNone)
                    .ToArray();
                if (legal.Length == 0) break;
                var specials = legal.Where(m => Pieces.IsSpecial(s.Pieces[m.From]) || Pieces.IsSpecial(s.Pieces[m.To])).ToArray();
                var pool = specials.Length > 0 && pick.NextInt(3) > 0 ? specials : legal;
                Play(engine, pool[pick.NextInt(pool.Length)], $"move {move}: ");
            }
        }

        private RuleEngine<Match3State> Start(string[] rows)
        {
            var engine = Rules.CreateEngine(Level(rows), 1);
            Show(engine);
            return engine;
        }

        private void Show(RuleEngine<Match3State> engine) => _board.Show(engine.State);

        private void Play<T>(RuleEngine<Match3State> engine, T action, string context = "", Action<float> during = null) where T : struct, IGameAction
        {
            var s = engine.State;
            _engine = engine;
            _landingProblems.Clear();
            _landed.Clear();
            var before = Enumerable.Range(0, s.CellCount).Where(c => s.Pieces[c] != Piece.Empty).Select(c => s.Ids[c]).ToList();
            _film = new BoardFilm(_board.Get, _board.CellPosition, s.Width);
            _film.Begin(before);
            Submit(action, context);
            var start = _clock.Now;
            _film.Run(() => Settled(s), dt =>
            {
                _clock.Advance(dt);
                _kit.Tick();
                during?.Invoke(_clock.Now - start);
                if (_settledAt < 0f && IsSettled(s)) _settledAt = _clock.Now;
            });

            var rest = Enumerable.Range(0, s.CellCount).Where(c => s.Pieces[c] != Piece.Empty).Select(c => (s.Ids[c], c));
            var problems = _landingProblems.Concat(_film.Problems()).Concat(_film.Overlaps()).Concat(_film.Misplaced(rest)).ToList();
            Assert.That(problems, Is.Empty, context + string.Join("\n", problems));
            Assert.That(s.Outcome != Outcome.Ongoing || s.Flight.Count == 0, Is.True, context + "pieces never landed");
            if (_landed.Count == 0 || s.Outcome != Outcome.Ongoing) return;
            Assert.That(_settledBeforeLast, Is.False, context + "the board counted as settled before its last landing");
            var lag = _settledAt - _lastLandingAt;
            Assert.That(lag, Is.InRange(0f, 2f * BoardFilm.FrameTime + 1e-4f), context + $"the board settled {lag * 1000f:0} ms after its last landing");
        }

        // The session's rule: the presentation is idle and every piece has landed.
        private bool IsSettled(Match3State s) => _player.IsIdle && s.Flight.Count == 0;

        // The board is still: nothing plays, nothing falls, and the rules have heard every landing.
        private bool Settled(Match3State s) =>
            _player.IsIdle && _kit.Mover.Count == 0 && _clock.Now >= _kit.SquashUntil && (s.Flight.Count == 0 || s.Outcome != Outcome.Ongoing);

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
                else if (e.Type == ids.PiecesSwapped)
                {
                    _film.Free(e.Entity);
                    _film.Free(PiecesSwapped.From(e).OtherId);
                }
                else if (e.Type == ids.PieceShuffled || e.Type == ids.SpecialMerged || e.Type == ids.SpecialsCombined || e.Type == ids.SpecialFired ||
                         e.Type == ids.FlyerLaunched || e.Type == ids.GemConverted)
                {
                    _film.Free(e.Entity);
                }
            }

            _kit.Mover.NextMove();
            _player.Play(events);
        }

        // As the game presenter does; a piece reported twice without moving again, or never in flight, is a problem.
        public void OnLanded(int id)
        {
            var s = _engine.State;
            if (s.Outcome != Outcome.Ongoing) return;
            _landed.Add(id);
            var cell = -1;
            for (var c = 0; c < s.CellCount; c++)
                if (s.Ids[c] == id && s.Pieces[c] != Piece.Empty && s.Flight[c]) cell = c;
            if (cell < 0)
            {
                var resting = Enumerable.Range(0, s.CellCount).Any(c => s.Ids[c] == id && s.Pieces[c] != Piece.Empty);
                if (resting) _landingProblems.Add($"piece {id} reported landing while at rest");
                return;
            }

            _board.SetOrigin(cell);
            _settledBeforeLast = IsSettled(s);
            Submit(new Land(cell, id));
            _lastLandingAt = _clock.Now;
            _settledAt = IsSettled(s) ? _clock.Now : -1f;
        }

        private T Find<T>() where T : Component =>
            _scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<T>(true)).First(c => c != null);

        private sealed class Hud : IMatch3HudView
        {
            public void Bind(IMatch3HudHandler handler) { }
            public void SetGoals(IReadOnlyList<Match3Goal> goals) { }
            public void SetGoalLeft(int goal, int left) { }
            public void Collect(int goal, int left, Sprite sprite, Vector2 screen, float screenSize, in GoalFlight flight) { }
            public void SetMoves(int moves) { }
            public void SetInteractable(bool interactable) { }
            public void SetReplay(bool replay) { }
            public void ShowCascade(int count) { }
            public UniTask ShowIntro(int level, Tileworks.Tiles.View.LevelBadge badge, CancellationToken ct) => UniTask.CompletedTask;
            public void ShowNoMoves() { }
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
