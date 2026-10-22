using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using TileMatch.Model;
using UnityEngine;

namespace TileMatch.View
{
    public sealed class TileLeftBoardPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TileLeftBoardPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TileLeftBoard;

        public float Present(in BoardEvent e, float delay)
        {
            var view = _kit.Board.ViewOf(e.Entity);
            if (view == null) return 0f;
            view.SetSorting(20000 + e.Entity);
            view.SetTint(Color.white);
            _kit.Feedback(Sfx.Tap, HapticKind.Selection, delay);
            return 0f;
        }
    }

    public sealed class TileMovedToTrayPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TileMovedToTrayPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TileMovedToTray;

        public float Present(in BoardEvent e, float delay)
        {
            var moved = TileMovedToTray.From(e);
            var view = _kit.Board.ViewOf(moved.Tile);
            if (view == null) return 0f;

            var feel = _kit.Feel;
            var duration = _kit.Duration(feel.FlightDuration);
            view.TraySlot = moved.Slot;
            view.BeginFlight(_kit.Tray, feel.ArcHeight, feel.TrayScale);
            view.SetShadowVisible(false);
            Tween.Custom(view, 0f, 1f, duration, (v, t) => v.UpdateFlight(t), feel.FlightEase, startDelay: delay)
                .OnComplete(view, v => v.State = TileViewState.Tray);
            Tween.PunchScale(view.transform, new Vector3(feel.LandSquash, -feel.LandSquash, 0f), _kit.Duration(0.12f), startDelay: delay + duration);
            _kit.Sfx.Play(Sfx.Land, delay + duration);
            return duration;
        }
    }

    // The slot updates at once (a flight in progress re-targets itself); a resting tile slides after any clear pops.
    public sealed class TraySlotChangedPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TraySlotChangedPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TraySlotChanged;

        public float Present(in BoardEvent e, float delay)
        {
            var changed = TraySlotChanged.From(e);
            var view = _kit.Board.ViewOf(changed.Tile);
            if (view == null) return 0f;
            view.TraySlot = changed.Slot;
            if (view.State != TileViewState.Tray) return 0f;

            var start = Mathf.Max(delay, _kit.Tray.ClearBusyUntil - _kit.Now);
            var duration = _kit.Duration(_kit.Feel.SlideDuration);
            Tween.Position(view.transform, _kit.Tray.AnchorOf(changed.Slot), duration, _kit.Feel.SlideEase, startDelay: start);
            return start - delay + duration;
        }
    }

    public sealed class TripleClearedPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TripleClearedPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TripleCleared;

        public float Present(in BoardEvent e, float delay)
        {
            var triple = TripleCleared.From(e);
            var middle = _kit.Board.ViewOf(triple.Second);
            var gather = _kit.Duration(_kit.Feel.ClearGather);
            var pop = _kit.Duration(_kit.Feel.ClearPop);
            Clear(triple.First, middle, delay, gather, pop);
            Clear(triple.Second, middle, delay, gather, pop);
            Clear(triple.Third, middle, delay, gather, pop);
            _kit.Tray.ClearBusyUntil = Mathf.Max(_kit.Tray.ClearBusyUntil, _kit.Now + delay + gather + pop * 0.6f);
            _kit.Feedback(Sfx.Triple, HapticKind.Medium, delay + gather);
            return gather + pop;
        }

        private void Clear(int tile, TileView middle, float delay, float gather, float pop)
        {
            var view = _kit.Board.ViewOf(tile);
            if (view == null) return;
            view.State = TileViewState.Leaving;
            if (middle != null && view != middle)
                Tween.Position(view.transform, middle.transform.position, gather, Ease.InQuad, startDelay: delay);
            Tween.Scale(view.transform, 0f, pop, Ease.InBack, startDelay: delay + gather)
                .OnComplete(view, v => v.ReleaseToOwner());
        }
    }

    public sealed class TrayCapacityChangedPresenter : IEventPresenter
    {
        private readonly PresenterKit _kit;
        public TrayCapacityChangedPresenter(PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.TrayCapacityChanged;

        public float Present(in BoardEvent e, float delay)
        {
            var duration = _kit.Duration(_kit.Feel.SlideDuration * 2f);
            _kit.Tray.SetCapacity(TrayCapacityChanged.From(e).Capacity, duration, delay);
            foreach (var view in _kit.Board.TrayViews())
                Tween.Position(view.transform, _kit.Tray.AnchorOf(view.TraySlot), duration, _kit.Feel.SlideEase, startDelay: delay);
            _kit.Feedback(Sfx.Booster, HapticKind.Light, delay);
            return duration;
        }
    }
}
