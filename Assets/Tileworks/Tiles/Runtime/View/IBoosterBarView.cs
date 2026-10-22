using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    public interface IBoosterBarHandler
    {
        // A tap on an open booster with stock, or on the picked one to put it back.
        void OnClicked(int slot);

        // A tap on the dimmed screen around the board while a booster is picked.
        void OnCancel();
    }

    // The booster buttons under the board, numbered left to right. A locked or empty booster explains itself in a bubble and
    // never reaches the handler. Screen values are in pixels.
    public interface IBoosterBarView
    {
        // Null unbinds.
        void Bind(IBoosterBarHandler handler);

        void SetCount(int slot, int count);

        // 0 opens the booster; otherwise it shows a padlock and the level it opens on.
        void SetLock(int slot, int unlockLevel);

        // Off while the board moves: taps are ignored, and nothing greys out.
        void SetInteractable(bool interactable);

        // The cards that introduce newly opened boosters, one after another; completes when the last one has closed.
        UniTask IntroduceAsync(IReadOnlyList<int> slots, CancellationToken ct);

        // Dims everything but the board and the picked button and shows the booster's aim card. -1 lifts it.
        void SetPicked(int slot, Rect boardScreen);

        Vector2 ButtonScreenPoint(int slot);

        // Pixels from the screen's bottom edge to the top of a raised button. The board belongs above it.
        float ScreenTop { get; }
    }
}
