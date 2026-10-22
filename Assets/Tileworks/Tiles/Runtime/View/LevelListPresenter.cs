using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.View
{
    // The Home's level list: cleared levels can be replayed, the current one played, locked ones refuse.
    public sealed class LevelListPresenter : ISceneEntry, ILevelListOpener, IDisposable
    {
        private readonly ILevelListView _view;
        private readonly ILevelLauncher _launcher;
        private int _selected;
        private bool _bound;

        public LevelListPresenter(ILevelListView view, ILevelLauncher launcher)
        {
            _view = Guard.NotNull(view, nameof(view));
            _launcher = Guard.NotNull(launcher, nameof(launcher));
        }

        public UniTask EnterAsync(object args, CancellationToken ct)
        {
            _view.OpenRequested += Open;
            _view.TileClicked += OnTile;
            _view.PlayClicked += OnPlay;
            _view.SetBadges(_launcher);
            _bound = true;
            if (args is LevelListArgs) Open();
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (!_bound) return;
            _view.OpenRequested -= Open;
            _view.TileClicked -= OnTile;
            _view.PlayClicked -= OnPlay;
        }

        private void Open() => OpenAt(Math.Min(_launcher.CurrentIndex, _launcher.LevelCount - 1));

        public void OpenAt(int index)
        {
            var count = _launcher.LevelCount;
            if (count <= 0 || index < 0 || index > _launcher.CurrentIndex) return;
            _selected = Math.Min(index, count - 1);
            _view.Open(count, _launcher.CurrentIndex, _selected, _launcher.CanLaunch(_selected));
        }

        private void OnTile(int index)
        {
            if (!_view.IsOpen) return;
            if (index > _launcher.CurrentIndex)
            {
                _view.RefuseLocked(index);
                return;
            }

            if (index == _selected) return;
            _selected = index;
            _view.Select(index, _launcher.CanLaunch(index));
        }

        private void OnPlay()
        {
            if (!_view.IsOpen || !_launcher.CanLaunch(_selected)) return;
            _view.Close();
            _launcher.Launch(_selected);
        }
    }
}
