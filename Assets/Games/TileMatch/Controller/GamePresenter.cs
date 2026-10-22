using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Kit.Audio;
using Tileworks.Kit.Core;
using Tileworks.Kit.Input;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using Tileworks.Meta.Api;
using TileMatch.Api;
using TileMatch.Model;
using TileMatch.Service;
using UnityEngine;

namespace TileMatch.Controller
{
    public sealed class GameSettings
    {
        public float HalfUnit = 0.5f;
        public Vector2 LayerOffset = new Vector2(0.04f, 0.06f);
        public float PickTolerance = 0.06f;
    }

    // Runs attempts in the Game scene: builds a level context, routes input and HUD, and plays each attempt as one async
    // flow from the booster cards to the player's choice on the result popup.
    public sealed class GamePresenter : ISceneEntry, IBoardTapHandler, ISlotOccupancy, IHudHandler, IBoosterBarHandler, IDisposable
    {
        private readonly IBoardView _board;
        private readonly IHudView _hud;
        private readonly IBoosterBarView _boosterBar;
        private readonly IResultView _result;
        private readonly ILevelContextFactory _contexts;
        private readonly ILevelSequence _sequence;
        private readonly ILevelProgress _progress;
        private readonly IAppNavigator _states;
        private readonly IGestureSource _gestures;
        private readonly IInputGate _gate;
        private readonly GameSettings _settings;
        private readonly ILives _lives;
        private readonly BoosterUnlocks _boosters;
        private readonly ILog _log;
        private readonly IMusic _music;
        private readonly TaskScope _tasks;
        private readonly CompositeDisposable _levelSubscriptions = new CompositeDisposable();
        private readonly List<int> _fresh = new List<int>();
        private MusicTrack _levelTrack;
        private ILevelContext _context;
        private LayeredBoardMapper _mapper;
        private BoardInput _input;
        private CancellationTokenSource _attempt;
        private int _levelIndex;
        private string _explicitLevelId;
        private ulong _attemptNumber;
        private bool _replay;

        public GamePresenter(IBoardView board, IHudView hud, IBoosterBarView boosterBar, IResultView result, ILevelContextFactory contexts, ILevelSequence sequence,
            ILevelProgress progress, IAppNavigator states, IGestureSource gestures, IInputGate gate, GameSettings settings, ILives lives, BoosterUnlocks boosters,
            IMusic music, TaskScope tasks, ILog log)
        {
            _board = board;
            _hud = hud;
            _boosterBar = boosterBar;
            _result = result;
            _contexts = contexts;
            _sequence = sequence;
            _progress = progress;
            _states = states;
            _gestures = gestures;
            _gate = gate;
            _settings = settings;
            _lives = lives;
            _boosters = boosters;
            _music = music;
            _tasks = tasks;
            _log = log;
        }

        public UniTask EnterAsync(object args, CancellationToken ct)
        {
            _levelIndex = args is LevelStartArgs start ? start.LevelIndex : _progress.CurrentIndex.Value;
            _explicitLevelId = (args as LevelStartArgs)?.LevelId;
            _input = new BoardInput(_gestures, _board, this);
            _hud.Bind(this);
            _boosterBar.Bind(this);
            StartLevel();
            return UniTask.CompletedTask;
        }

        public bool IsOccupied(int slot) => _context != null && _context.Session.State.Occupant[slot] >= 0;

        public void OnBoardTap(Vector2 world)
        {
            if (_context == null || _mapper == null || _result.IsOpen) return;
            var slot = _mapper.Pick(world, this, _settings.PickTolerance);
            if (slot < 0) return;
            var tile = _context.Session.State.Occupant[slot];
            var result = _context.Session.Select(tile);
            if (result.Rejected == RejectReason.NotFree || result.Rejected == TileMatchRejects.Frozen ||
                result.Rejected == TileMatchRejects.PartnerBlocked || result.Rejected == RejectReason.NoRoom)
                _board.Nope(tile);
        }

        public void OnClicked(int slot)
        {
            var kind = KindOf(slot);
            if (_context == null || _gate.IsBlocked || !_boosters.IsUnlocked(kind, BoosterLevel)) return;
            var result = _context.Session.UseBooster(kind);
            if (!result.Accepted) _log.Info(LogTag.Level, "Booster {0} not applicable: {1}", kind, result.Rejected);
        }

        public void OnPause()
        {
            if (_context == null || _result.IsOpen) return;
            PauseAsync(_attempt.Token).Forget(_log, "Pause");
        }

        public void Dispose()
        {
            _hud.Bind(null);
            _boosterBar.Bind(null);
            EndAttempt();
            _levelSubscriptions.Dispose();
            _input?.Dispose();
            _context?.Dispose();
        }

        // A cleared level replays for fun: it needs no life and changes nothing.
        private void StartLevel()
        {
            EndAttempt();
            _replay = _explicitLevelId == null && _levelIndex < _progress.CurrentIndex.Value;
            if (!_replay && !_lives.HasLife)
            {
                _states.GoTo(AppStates.Home);
                return;
            }

            _levelSubscriptions.Clear();
            _context?.Dispose();
            _result.Hide();
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

            _mapper = new LayeredBoardMapper(_context.Session.Level.Layout, _settings.HalfUnit, _settings.LayerOffset);
            _board.Show(_context.Session.State, _mapper, _context.Session.Rules.Modifiers);
            _hud.SetLevel(_levelIndex + 1);
            _hud.SetReplay(_replay);
            _result.SetReplay(_replay ? _progress.CurrentIndex.Value + 1 : 0);
            SetInteractable(true);
            for (var kind = BoosterKind.Undo; kind <= BoosterKind.ExtraSlot; kind++)
            {
                var k = kind;
                _levelSubscriptions.Add(_context.Session.BoosterCount(kind).Subscribe(count => _boosterBar.SetCount(SlotOf(k), (int)count)));
            }

            _attempt = CancellationTokenSource.CreateLinkedTokenSource(_tasks.Token);
            PlayAsync(_context.Session, _attempt.Token).Forget(_log, "Level");
        }

        private void EndAttempt()
        {
            if (_attempt == null) return;
            _attempt.Cancel();
            _attempt.Dispose();
            _attempt = null;
        }

        // One attempt: the booster cards, then the result popups until the player leaves the level or starts another.
        private async UniTask PlayAsync(LevelSession session, CancellationToken ct)
        {
            await IntroduceBoostersAsync(ct);
            while (true)
            {
                var outcome = await session.WhenFinishedAsync(ct);
                SetInteractable(false);
                if (outcome == Outcome.Won)
                {
                    session.Conclude();
                    _music.PlayOnce(MusicTrack.Clear);
                    Choose(await _result.ShowWin(_levelIndex + 1, _replay ? 0 : session.Ref.Reward, ct));
                    return;
                }

                _music.PlayOnce(MusicTrack.Fail);
                var choice = await LoseAsync(session, ct);
                if (choice == ResultChoice.Continue)
                {
                    _music.PlayLoop(_levelTrack);
                    _result.Hide();
                    SetInteractable(true);
                    continue;
                }

                session.Conclude();
                Choose(choice);
                return;
            }
        }

        // The lose popup stays up until a choice sticks: a continue the session refuses leaves it open.
        private async UniTask<ResultChoice> LoseAsync(LevelSession session, CancellationToken ct)
        {
            var canContinue = !_replay && session.ContinuesLeft > 0 && session.State.LoseReason != LoseReason.TimeUp;
            while (true)
            {
                var choice = await _result.ShowLose(session.State.LoseReason, canContinue, session.ContinuePrice, session.CanAffordContinue, ct);
                if (choice != ResultChoice.Continue || session.Continue().Accepted) return choice;
            }
        }

        private async UniTask PauseAsync(CancellationToken ct)
        {
            switch (await _result.ShowPause(ct))
            {
                case ResultChoice.Retry:
                    StartLevel();
                    break;
                case ResultChoice.Home:
                    _states.GoTo(AppStates.Home);
                    break;
                default:
                    _result.Hide();
                    break;
            }
        }

        private void Choose(ResultChoice choice)
        {
            switch (choice)
            {
                case ResultChoice.Next:
                    Next();
                    break;
                case ResultChoice.Retry:
                    StartLevel();
                    break;
                case ResultChoice.Levels:
                    _states.GoTo(AppStates.Home, new LevelListArgs());
                    break;
                default:
                    _states.GoTo(AppStates.Home);
                    break;
            }
        }

        private void Next()
        {
            if (_explicitLevelId != null || _replay)
            {
                _explicitLevelId = null;
                _levelIndex = _progress.CurrentIndex.Value;
            }
            else _levelIndex++;

            StartLevel();
        }

        // Locked boosters show the level they open on. One that is open but was never introduced (it opens on this level, or
        // opened before the player had seen it) gets its card once; the board waits until the cards are closed.
        private async UniTask IntroduceBoostersAsync(CancellationToken ct)
        {
            var number = BoosterLevel;
            _fresh.Clear();
            for (var kind = BoosterKind.Undo; kind <= BoosterKind.ExtraSlot; kind++)
            {
                var unlocked = _boosters.IsUnlocked(kind, number);
                _boosterBar.SetLock(SlotOf(kind), unlocked ? 0 : _boosters.UnlockLevel(kind));
                if (!unlocked || _boosters.WasIntroduced(kind)) continue;
                _boosters.MarkIntroduced(kind);
                _fresh.Add(SlotOf(kind));
            }

            if (_fresh.Count == 0) return;
            using (_gate.Block("booster intro")) await _boosterBar.IntroduceAsync(_fresh, ct);
        }

        // Tile Match never picks a booster to aim it, so there is nothing to put back.
        public void OnCancel() { }

        private void SetInteractable(bool interactable)
        {
            _hud.SetInteractable(interactable);
            _boosterBar.SetInteractable(interactable);
        }

        private static int SlotOf(BoosterKind kind) => (int)kind - 1;

        private static BoosterKind KindOf(int slot) => (BoosterKind)(slot + 1);

        // Boosters unlock with progress, so a replay of an early level keeps the ones the player has.
        private int BoosterLevel => (_replay ? _progress.CurrentIndex.Value : _levelIndex) + 1;
    }
}
