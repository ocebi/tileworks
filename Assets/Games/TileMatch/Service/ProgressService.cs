using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Kit.Save;
using TileMatch.Api;

namespace TileMatch.Service
{
    public sealed class ProgressService : ILevelProgress
    {
        private const int RowId = 1;

        private readonly ProgressDao _dao;
        private readonly IClock _clock;
        private readonly Observable<int> _current = new Observable<int>();

        public ProgressService(ProgressDao dao, IClock clock)
        {
            _dao = Guard.NotNull(dao, nameof(dao));
            _clock = Guard.NotNull(clock, nameof(clock));
        }

        public IReadOnlyObservable<int> CurrentIndex => _current;

        public void Load() => _current.Value = _dao.Find(RowId)?.LevelIndex ?? 0;

        // Called inside the level-result transaction; the observable updates only after the commit.
        public void RecordWin(IDatabase db, int levelIndex)
        {
            var next = Math.Max(_current.Value, levelIndex + 1);
            var starterGranted = _dao.Find(RowId)?.StarterGranted ?? false;
            _dao.Upsert(new ProgressEntity { Id = RowId, LevelIndex = next, UpdatedAt = _clock.UtcNow.ToString("O"), StarterGranted = starterGranted });
            var transaction = db.Current;
            if (transaction == null) _current.Value = next;
            else transaction.OnCommitted(() => _current.Value = next);
        }
    }

    public sealed class ProgressLoadStep : BootStep
    {
        private static readonly string[] Dependencies = { SaveSteps.Warm };
        private readonly ProgressService _progress;

        public ProgressLoadStep(ProgressService progress) => _progress = progress;

        public override string Name => "Progress";
        public override IReadOnlyList<string> DependsOn => Dependencies;

        public override UniTask RunAsync(CancellationToken ct)
        {
            _progress.Load();
            return UniTask.CompletedTask;
        }
    }
}
