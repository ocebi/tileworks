using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using TileMatch.Model;
using UnityEngine;

namespace TileMatch.View
{
    public sealed class TileUncoveredPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TileUncoveredPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TileUncovered;

        public float Present(in BoardEvent e, float delay) => TintPresenter.Tint(_kit, e.Entity, Color.white, delay);
    }

    public sealed class TileCoveredPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TileCoveredPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TileCovered;

        public float Present(in BoardEvent e, float delay) => TintPresenter.Tint(_kit, e.Entity, _kit.Board.Skin.CoveredTint, delay);
    }

    internal static class TintPresenter
    {
        public static float Tint(PresenterKit kit, int tile, Color target, float delay)
        {
            var view = kit.Board.ViewOf(tile);
            if (view == null) return 0f;
            var duration = kit.Duration(kit.Feel.UncoverDuration);
            Tween.Custom(view, view.Tint, target, duration, (v, c) => v.SetTint(c), startDelay: delay);
            return duration;
        }

        public static float Flip(PresenterKit kit, TileView view, Sprite icon, float delay)
        {
            var half = kit.Duration(kit.Feel.FlipDuration) * 0.5f;
            var t = view.transform;
            view.PendingIcon = icon;
            Sequence.Create()
                .ChainDelay(delay)
                .Chain(Tween.ScaleX(t, 0f, half, Ease.InQuad))
                .ChainCallback(view, v => v.ApplyPendingIcon())
                .Chain(Tween.ScaleX(t, 1f, half, Ease.OutQuad));
            return half * 2f;
        }
    }

    public sealed class TileRevealedPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TileRevealedPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TileRevealed;

        public float Present(in BoardEvent e, float delay)
        {
            var revealed = TileRevealed.From(e);
            var view = _kit.Board.ViewOf(revealed.Tile);
            return view == null ? 0f : TintPresenter.Flip(_kit, view, _kit.Board.Skin.IconOf(revealed.Kind), delay);
        }
    }

    public sealed class TileKindChangedPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TileKindChangedPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TileKindChanged;

        public float Present(in BoardEvent e, float delay)
        {
            var changed = TileKindChanged.From(e);
            var view = _kit.Board.ViewOf(changed.Tile);
            var stagger = (changed.Tile % 7) * 0.02f;
            return view == null ? 0f : stagger + TintPresenter.Flip(_kit, view, _kit.Board.Skin.IconOf(changed.Kind), delay + stagger);
        }
    }

    public sealed class ModifierChangedPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public ModifierChangedPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.ModifierChanged;

        public float Present(in BoardEvent e, float delay)
        {
            var changed = ModifierChanged.From(e);
            var view = _kit.Board.ViewOf(changed.Tile);
            if (view == null || changed.ModifierType != ModifierTypes.Frozen) return 0f;

            var duration = _kit.Duration(_kit.Feel.UncoverDuration * 2f);
            _kit.Feedback(Sfx.Crack, HapticKind.Light, delay);
            Tween.PunchScale(view.transform, Vector3.one * 0.08f, duration, startDelay: delay);
            if (changed.Value > 0) view.SetOverlay(_kit.Board.Skin.IceOf(changed.Value));
            else view.FadeOverlay(duration, delay);
            return duration;
        }
    }

    public sealed class TileSpawnedPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TileSpawnedPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TileSpawned;

        public float Present(in BoardEvent e, float delay)
        {
            var view = _kit.Board.Spawn(TileSpawned.From(e).Tile);
            var duration = _kit.Duration(_kit.Feel.SpawnDuration);
            view.transform.localScale = Vector3.zero;
            Tween.Scale(view.transform, 1f, duration, Ease.OutBack, startDelay: delay);
            _kit.Sfx.Play(Sfx.Spawn, delay);
            return duration;
        }
    }

    public sealed class TileReturnedToBoardPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TileReturnedToBoardPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TileReturnedToBoard;

        public float Present(in BoardEvent e, float delay)
        {
            var view = _kit.Board.ViewOf(TileReturnedToBoard.From(e).Tile);
            if (view == null) return 0f;
            var duration = _kit.Duration(_kit.Feel.ReturnDuration);
            view.State = TileViewState.Board;
            view.TraySlot = -1;
            view.SetBoardSorting(view.BoardSorting);
            view.SetShadowVisible(true);
            Tween.Position(view.transform, view.BoardPosition, duration, Ease.OutCubic, startDelay: delay);
            Tween.Scale(view.transform, 1f, duration, startDelay: delay);
            return duration;
        }
    }

    public sealed class LevelWonPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public LevelWonPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.LevelWon;

        public float Present(in BoardEvent e, float delay)
        {
            _kit.Feedback(Sfx.Win, HapticKind.Success, delay);
            return _kit.Duration(_kit.Feel.ResultDelay);
        }
    }

    public sealed class LevelLostPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public LevelLostPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.LevelLost;

        public float Present(in BoardEvent e, float delay)
        {
            _kit.Feedback(Sfx.Lose, HapticKind.Failure, delay);
            Tween.ShakeLocalPosition(_kit.Tray.transform, new Vector3(0.12f, 0f, 0f), 0.35f, startDelay: delay);
            return _kit.Duration(_kit.Feel.ResultDelay);
        }
    }
}
