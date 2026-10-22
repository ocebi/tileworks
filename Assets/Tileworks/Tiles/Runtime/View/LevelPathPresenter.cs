using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.View
{
    // A reached node opens the level list on it (Play starts the current level); a locked node refuses.
    public sealed class LevelPathPresenter : ISceneEntry, ILevelPathHandler, IDisposable
    {
        public const int Behind = 2;
        public const int Ahead = 2;

        private readonly ILevelPathView _view;
        private readonly ILevelLauncher _launcher;
        private readonly ILevelListOpener _list;

        public LevelPathPresenter(ILevelPathView view, ILevelLauncher launcher, ILevelListOpener list)
        {
            _view = Guard.NotNull(view, nameof(view));
            _launcher = Guard.NotNull(launcher, nameof(launcher));
            _list = Guard.NotNull(list, nameof(list));
        }

        public UniTask EnterAsync(object args, CancellationToken ct)
        {
            _view.Bind(this);
            var count = _launcher.LevelCount;
            if (count <= 0) return UniTask.CompletedTask;
            var current = Math.Min(_launcher.CurrentIndex, count - 1);
            var first = Math.Max(0, current - Behind);
            var end = Math.Min(count, current + Ahead + 1);
            _view.Show(first, end - first, current, _launcher);
            return UniTask.CompletedTask;
        }

        public void Dispose() => _view.Bind(null);

        public void OnNodeTapped(int index)
        {
            if (index > _launcher.CurrentIndex) _view.RefuseLocked(index);
            else _list.OpenAt(index);
        }
    }
}
