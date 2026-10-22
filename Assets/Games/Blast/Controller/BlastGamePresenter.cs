using System;
using System.Collections.Generic;
using System.Threading;
using Blast.Api;
using Blast.Model;
using Blast.Service;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using Tileworks.Kit.Core;
using Tileworks.Kit.Input;
using Tileworks.Kit.Unity;
using Tileworks.Meta.Api;
using Tileworks.Meta.Boosters;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;
using Tileworks.Tiles.Model;
using UnityEngine;

namespace Blast.Controller
{
    // Taps go to the rules; the board view only shows what the events say happened and reports each landing back. A picked
    // hammer takes the board's touches until it strikes or is put back. Each attempt is one async flow from the level card to the player's choice
    // on the result popup.
    public sealed class BlastGamePresenter : ISceneEntry, ITickable, IBoardTapHandler, IHammerHandler, IBlastHudHandler, IBoosterBarHandler,
        IDisposable
    {
        private readonly IBlastBoardView _board;
        private readonly IBlastHudView _hud;
        private readonly IBlastResultView _result;
        private readonly IBlastContextFactory _contexts;
        private readonly ILevelSequence _sequence;
        private readonly IBlastProgress _progress;
        private readonly IAppNavigator _states;
        private readonly IGestureSource _gestures;
        private readonly IInputGate _gate;
        private readonly ILives _lives;
        private readonly IBoosterBarView _boosterBar;
        private readonly BoosterService _boosters;
        private readonly HammerAim _aim;
        private readonly BlastTaps _taps;
        private readonly ILog _log;
        private readonly IMusic _music;
        private readonly TaskScope _tasks;
        private readonly IClock _clock;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
        private readonly List<int> _fresh = new List<int>();
        private MusicTrack _levelTrack;
        private int[] _hint = new int[0];
        private DateTime _idleSince;
        private bool _hinting;
        private IBlastContext _context;
        private BoardInput _input;
        private CancellationTokenSource _attempt;
        private CancellationTokenSource _pause;
        private UniTaskCompletionSource _skip;
        private int _levelIndex;
        private string _explicitLevelId;
        private bool _replay;
        private ulong _attemptNumber;
        private bool _barOpen;

        public BlastGamePresenter(IBlastBoardView board, IBlastHudView hud, IBlastResultView result, IBlastContextFactory contexts,
            ILevelSequence sequence, IBlastProgress progress, IAppNavigator states, IGestureSource gestures, IInputGate gate, ILives lives, IClock clock,
            IBoosterBarView boosterBar, BoosterService boosters, IMusic music, TaskScope tasks, ILog log)
        {
            _boosterBar = boosterBar;
            _boosters = boosters;
            _aim = new HammerAim(gestures, board, this);
            _taps = new BlastTaps(board, clock);
            _clock = clock;
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
            _levelIndex = args is BlastStartArgs start ? start.LevelIndex : _progress.CurrentIndex.Value;
            _explicitLevelId = (args as BlastStartArgs)?.LevelId;
            _input = new BoardInput(_gestures, _board, this);
            _hud.Bind(this);
            _boosterBar.Bind(this);
            _board.BindLandings(_taps);
            for (var slot = 0; slot < _boosters.Count; slot++)
            {
                var s = slot;
                _subscriptions.Add(_boosters.Stock(slot).Subscribe(count => _boosterBar.SetCount(s, (int)count)));
            }

            StartLevel();
            return UniTask.CompletedTask;
        }

        // A hint appears once the player has been idle for a while with the board settled; the wait starts when it settles.
        public void Tick()
        {
            _board.Tick();
            if (_context == null) return;
            _context.Session.Tick();
            _taps.Retry();
            var open = Playing && _context.Session.IsSettled;
            if (open != _barOpen) _boosterBar.SetInteractable(_barOpen = open);
            if (!open) _idleSince = _clock.UtcNow;
            if (_hinting || !open || _aim.Armed) return;
            if ((_clock.UtcNow - _idleSince).TotalSeconds < _board.HintDelay) return;
            var state = _context.Session.State;
            if (_hint.Length < state.CellCount) _hint = new int[state.CellCount];
            var count = BlastHints.Best(state, _hint);
            if (count == 0) return;
            _board.ShowHint(_hint, count);
            _hinting = true;
        }

        public void Dispose()
        {
            _hud.Bind(null);
            _boosterBar.Bind(null);
            _board.BindLandings(null);
            EndAttempt();
            _aim.Dispose();
            _input?.Dispose();
            _subscriptions.Dispose();
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

            Disarm();
            _context?.Dispose();
            _result.Hide();
            var level = _explicitLevelId != null ? new LevelRef(_levelIndex, _explicitLevelId, string.Empty, 0) : _sequence.At(_levelIndex);
            var badge = LevelBadges.FromLabel(level.Label);
            _levelTrack = MusicTracks.ForLevel(_levelIndex + 1, badge != LevelBadge.None);
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
            _taps.Begin(session);
            _board.Show(session.State);
            ResetHint();
            _hud.SetGoals(session.Level.Goals);
            _hud.SetReplay(_replay);
            _result.SetReplay(_replay ? Math.Min(_progress.CurrentIndex.Value, _sequence.Count - 1) + 1 : 0);
            _hud.SetMoves(session.State.MovesLeft);
            _hud.SetInteractable(true);
            ShowBoosters();
            _board.HidePieces();
            _attempt = CancellationTokenSource.CreateLinkedTokenSource(_tasks.Token);
            PlayAsync(session, badge, _attempt.Token).Forget(_log, "Level");
        }

        private void EndAttempt()
        {
            EndPause();
            Cancel(ref _attempt);
            _skip = null;
        }

        private void EndPause() => Cancel(ref _pause);

        private static void Cancel(ref CancellationTokenSource source)
        {
            if (source == null) return;
            source.Cancel();
            source.Dispose();
            source = null;
        }

        // One attempt: the level card, the drop-in and any booster cards, then the result popups until the player leaves
        // the level or starts another.
        private async UniTask PlayAsync(BlastSession session, LevelBadge badge, CancellationToken ct)
        {
            using (_gate.Block("level card"))
            {
                await _hud.ShowIntro(_levelIndex + 1, badge, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(_board.DropIn()), cancellationToken: ct);
                await IntroduceBoostersAsync(ct);
            }

            ResetHint();
            while (true)
            {
                var outcome = await session.WhenFinishedAsync(ct);
                EndPause();
                Disarm();
                _hud.SetInteractable(false);
                if (outcome == Outcome.Won)
                {
                    session.Conclude();
                    await BonusAsync(session, ct);
                    _music.PlayOnce(MusicTrack.Clear);
                    Choose(await _result.ShowWin(_levelIndex + 1, _replay ? 0 : session.Ref.Reward, ct));
                    return;
                }

                _music.PlayOnce(MusicTrack.Fail);
                var choice = await LoseAsync(session, ct);
                if (choice == ResultChoice.Continue)
                {
                    _music.PlayLoop(_levelTrack);
                    ResetHint();
                    _result.Hide();
                    _hud.SetInteractable(true);
                    continue;
                }

                session.Conclude();
                Choose(choice);
                return;
            }
        }

        // Leftover moves become rockets; a tap on the board skips the show.
        private async UniTask BonusAsync(BlastSession session, CancellationToken ct)
        {
            var left = session.State.MovesLeft;
            var seconds = left > 0 ? _board.PlayBonus(left, _hud.ShowBonus()) : 0f;
            if (seconds > 0f)
            {
                _skip = new UniTaskCompletionSource();
                await UniTask.WhenAny(_skip.Task, UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct));
                _skip = null;
            }

            _board.SkipBonus();
        }

        // The lose popup stays up until a choice sticks: a continue the session refuses leaves it open.
        private async UniTask<ResultChoice> LoseAsync(BlastSession session, CancellationToken ct)
        {
            while (true)
            {
                var choice = await _result.ShowLose(session.Level.Goals, session.State.GoalsLeft, !_replay && session.CanContinue, session.ExtraMoves,
                    session.ContinuePrice, session.CanAffordContinue, ct);
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

        // Boosters unlock with progress, so a replay of an early level keeps the ones the player has, and a level played from
        // the level editor has them all.
        private int BoosterLevel => _explicitLevelId != null ? int.MaxValue : (_replay ? _progress.CurrentIndex.Value : _levelIndex) + 1;

        private void ShowBoosters()
        {
            for (var slot = 0; slot < _boosters.Count; slot++)
                _boosterBar.SetLock(slot, _boosters.IsUnlocked(slot, BoosterLevel) ? 0 : _boosters.UnlockLevel(slot));
        }

        // After the level card and the drop-in, so a booster's card points at its button beside a board already in place.
        private async UniTask IntroduceBoostersAsync(CancellationToken ct)
        {
            _fresh.Clear();
            for (var slot = 0; slot < _boosters.Count; slot++)
            {
                if (!_boosters.IsUnlocked(slot, BoosterLevel) || _boosters.WasIntroduced(slot)) continue;
                _boosters.MarkIntroduced(slot);
                _fresh.Add(slot);
            }

            if (_fresh.Count > 0) await _boosterBar.IntroduceAsync(_fresh, ct);
        }

        public void OnPause()
        {
            if (_context == null || _result.IsOpen || _attempt == null) return;
            EndPause();
            _pause = CancellationTokenSource.CreateLinkedTokenSource(_attempt.Token);
            PauseAsync(_pause.Token).Forget(_log, "Pause");
        }

        public void OnClicked(int slot)
        {
            if (!Playing || !_context.Session.IsSettled || !_boosters.IsUnlocked(slot, BoosterLevel)) return;
            if (slot == BoardBoosters.Hammer)
            {
                if (_aim.Armed) Disarm();
                else Arm();
                return;
            }

            Disarm();
            ResetHint();
            _board.SetOrigin(-1);
            var result = _context.Session.Shuffle();
            if (!result.Accepted) _log.Info(LogTag.Level, "Shuffle not applicable: {0}", result.Rejected);
        }

        public void OnCancel() => Disarm();

        private void Arm()
        {
            ResetHint();
            _aim.Arm(true);
            _boosterBar.SetPicked(BoardBoosters.Hammer, _board.ScreenRect);
        }

        private void Disarm()
        {
            if (!_aim.Armed) return;
            _aim.Arm(false);
            _boosterBar.SetPicked(-1, default);
        }

        // The hammer's presenter lifts the dim once it has struck.
        public bool CanHit(int cell) => _context != null && _context.Session.CanHammer(cell);

        public void OnStruck(int cell)
        {
            if (!Playing || !_context.Session.IsSettled) return;
            _aim.Arm(false);
            ResetHint();
            _board.SetOrigin(cell);
            if (_context.Session.Hammer(cell).Accepted) return;
            _boosterBar.SetPicked(-1, default);
            _board.Refuse(cell);
        }

        private bool Playing => _context != null && !_result.IsOpen && !_gate.IsBlocked && _context.Session.State.Outcome == Outcome.Ongoing;

        // During the bonus any tap skips it. Otherwise a tap acts on landed pieces while others still fall (BlastTaps).
        public void OnBoardTap(Vector2 world)
        {
            if (_skip != null)
            {
                _skip.TrySetResult();
                return;
            }

            if (!Playing || _aim.Armed || _aim.Consumed) return;
            var cell = _board.CellAt(world);
            if (cell < 0) return;
            ResetHint();
            _board.SetOrigin(cell);
            _taps.Tap(cell);
        }

        private void ResetHint()
        {
            if (_hinting) _board.HideHint();
            _hinting = false;
            _idleSince = _clock.UtcNow;
        }
    }
}
