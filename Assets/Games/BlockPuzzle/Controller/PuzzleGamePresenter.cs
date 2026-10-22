using System;
using System.Threading;
using BlockPuzzle.Api;
using BlockPuzzle.Service;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using Tileworks.Kit.Core;
using Tileworks.Kit.Input;
using Tileworks.Kit.Unity;
using Tileworks.Meta.Api;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;
using Tileworks.Tiles.Model;
using UnityEngine;

namespace BlockPuzzle.Controller
{
    // Drag a piece from the offer onto the board; the rules decide, the board view only shows. Each attempt is one async
    // flow up to the player's choice on the result popup.
    public sealed class PuzzleGamePresenter : ISceneEntry, IPuzzleDragHandler, IPuzzleHudHandler, IDisposable
    {
        private const int NoSlot = -1;

        private readonly IPuzzleBoardView _board;
        private readonly IPuzzleHudView _hud;
        private readonly IPuzzleResultView _result;
        private readonly IPuzzleContextFactory _contexts;
        private readonly ILevelSequence _sequence;
        private readonly IPuzzleProgress _progress;
        private readonly IAppNavigator _states;
        private readonly IGestureSource _gestures;
        private readonly IInputGate _gate;
        private readonly ILives _lives;
        private readonly ILog _log;
        private readonly IMusic _music;
        private readonly TaskScope _tasks;
        private MusicTrack _levelTrack;
        private PuzzleGestures _input;
        private CancellationTokenSource _attempt;
        private CancellationTokenSource _pause;
        private IPuzzleContext _context;
        private int _levelIndex;
        private string _explicitLevelId;
        private bool _replay;
        private ulong _attemptNumber;
        private int _dragged = NoSlot;

        public PuzzleGamePresenter(IPuzzleBoardView board, IPuzzleHudView hud, IPuzzleResultView result, IPuzzleContextFactory contexts,
            ILevelSequence sequence, IPuzzleProgress progress, IAppNavigator states, IGestureSource gestures, IInputGate gate, ILives lives, IMusic music,
            TaskScope tasks, ILog log)
        {
            _board = board;
            _hud = hud;
            _result = result;
            _contexts = contexts;
            _sequence = sequence;
            _progress = progress;
            _states = states;
            _gestures = gestures;
            _gate = gate;
            _lives = lives;
            _log = log;
            _music = music;
            _tasks = tasks;
        }

        public UniTask EnterAsync(object args, CancellationToken ct)
        {
            _levelIndex = args is PuzzleStartArgs start ? start.LevelIndex : _progress.CurrentIndex.Value;
            _explicitLevelId = (args as PuzzleStartArgs)?.LevelId;
            _input = new PuzzleGestures(_gestures, _board, this);
            _hud.Bind(this);
            StartLevel();
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _hud.Bind(null);
            EndAttempt();
            _input?.Dispose();
            _context?.Dispose();
        }

        // A cleared level replays for fun: it needs no life and changes nothing.
        private void StartLevel()
        {
            EndAttempt();
            if (_levelIndex >= _sequence.Count) _levelIndex = _sequence.Count - 1;
            _replay = _explicitLevelId == null && _levelIndex < _progress.CurrentIndex.Value;
            if (!_replay && !_lives.HasLife)
            {
                _states.GoTo(AppStates.Home);
                return;
            }

            _context?.Dispose();
            _result.Hide();
            _dragged = NoSlot;
            var level = _explicitLevelId != null ? new LevelRef(_levelIndex, _explicitLevelId, string.Empty, 0) : _sequence.At(_levelIndex);
            _levelTrack = MusicTracks.ForLevel(_levelIndex + 1, LevelBadges.FromLabel(level.Label) != LevelBadge.None);
            _music.PlayLoop(_levelTrack);
            _context = _contexts.Create(level);
            var loaded = _context.Session.Load(StableHash.Combine((ulong)_levelIndex + 1, ++_attemptNumber));
            if (!loaded.IsOk)
            {
                _log.Error(LogTag.Level, $"Cannot load {level.LevelId}: {loaded.Error}");
                _states.GoTo(AppStates.Home);
                return;
            }

            var session = _context.Session;
            _board.Show(session.State, session.Rules.Shapes);
            _hud.SetLevel(_levelIndex + 1);
            _hud.SetReplay(_replay);
            _result.SetReplay(_replay ? Math.Min(_progress.CurrentIndex.Value, _sequence.Count - 1) + 1 : 0);
            _hud.SetGoal(session.Level.Goal, session.Level.GemCount, session.Level.ScoreTarget);
            _hud.SetInteractable(true);
            _attempt = CancellationTokenSource.CreateLinkedTokenSource(_tasks.Token);
            PlayAsync(session, _attempt.Token).Forget(_log, "Level");
        }

        private void EndAttempt()
        {
            EndPause();
            Cancel(ref _attempt);
        }

        private void EndPause() => Cancel(ref _pause);

        private static void Cancel(ref CancellationTokenSource source)
        {
            if (source == null) return;
            source.Cancel();
            source.Dispose();
            source = null;
        }

        // One attempt: the result popups until the player leaves the level or starts another.
        private async UniTask PlayAsync(PuzzleSession session, CancellationToken ct)
        {
            while (true)
            {
                var outcome = await session.WhenFinishedAsync(ct);
                EndPause();
                _hud.SetInteractable(false);
                if (outcome == Outcome.Won)
                {
                    session.Conclude();
                    _music.PlayOnce(MusicTrack.Clear);
                    Choose(await _result.ShowWin(_levelIndex + 1, _replay ? 0 : session.Ref.Reward, session.State.Score, ct));
                    return;
                }

                _music.PlayOnce(MusicTrack.Fail);
                var choice = await LoseAsync(session, ct);
                if (choice == ResultChoice.Continue)
                {
                    _music.PlayLoop(_levelTrack);
                    _result.Hide();
                    _hud.SetInteractable(true);
                    continue;
                }

                session.Conclude();
                Choose(choice);
                return;
            }
        }

        // The lose popup stays up until a choice sticks: a continue the session refuses leaves it open.
        private async UniTask<ResultChoice> LoseAsync(PuzzleSession session, CancellationToken ct)
        {
            while (true)
            {
                var choice = await _result.ShowLose(!_replay && session.CanContinue, session.ContinuePrice, session.CanAffordContinue, ct);
                if (choice != ResultChoice.Continue || session.Continue().Accepted) return choice;
            }
        }

        private async UniTask PauseAsync(CancellationToken ct)
        {
            var choice = await _result.ShowPause(ct);
            if (choice == ResultChoice.Retry) StartLevel();
            else if (choice == ResultChoice.Home) _states.GoTo(AppStates.Home);
            else _result.Hide();
        }

        // Next and Retry start another attempt here; anything else leaves for Home, Levels with the level list open.
        private void Choose(ResultChoice choice)
        {
            if (choice == ResultChoice.Next)
            {
                _explicitLevelId = null;
                _levelIndex = _replay ? _progress.CurrentIndex.Value : Math.Min(_levelIndex + 1, _sequence.Count - 1);
            }

            if (choice == ResultChoice.Next || choice == ResultChoice.Retry) StartLevel();
            else _states.GoTo(AppStates.Home, choice == ResultChoice.Levels ? new LevelListArgs() : null);
        }

        public void OnPause()
        {
            if (_context == null || _result.IsOpen || _attempt == null) return;
            EndPause();
            _pause = CancellationTokenSource.CreateLinkedTokenSource(_attempt.Token);
            PauseAsync(_pause.Token).Forget(_log, "Pause");
        }

        private bool Playing => _context != null && !_result.IsOpen && !_gate.IsBlocked && _context.Session.State.Outcome == Outcome.Ongoing;

        public void OnPress(Vector2 world)
        {
            if (!Playing) return;
            var slot = _board.SlotAt(world);
            if (slot == NoSlot) return;
            _dragged = slot;
            Preview(_board.DragTo(slot, world));
        }

        public void OnDrag(Vector2 world)
        {
            if (_dragged == NoSlot) return;
            Preview(_board.DragTo(_dragged, world));
        }

        public void OnRelease(Vector2 world)
        {
            if (_dragged == NoSlot) return;
            var slot = _dragged;
            _dragged = NoSlot;
            var anchor = _board.DragTo(slot, world);
            _board.Preview(slot, -1);
            if (!Playing || anchor < 0 || !_context.Session.Place(slot, anchor).Accepted) _board.Return(slot);
        }

        private void Preview(int anchor)
        {
            var valid = anchor >= 0 && _context.Session.Check(_dragged, anchor).IsNone;
            _board.Preview(_dragged, valid ? anchor : -1);
        }
    }
}
