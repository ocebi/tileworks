using PrimeTween;
using Tileworks.Kit.Core;
using UnityEngine;

namespace Tileworks.Kit.UI
{
    // Opens and closes one popup canvas; a new transition replaces the one still playing.
    public sealed class PopupMotion
    {
        private readonly UiMotion _motion;
        private readonly CanvasGroup _group;
        private readonly Transform _panel;
        private Sequence _transition;

        public PopupMotion(UiMotion motion, CanvasGroup group, Transform panel)
        {
            _motion = Guard.NotNull(motion, nameof(motion));
            _group = Guard.NotNull(group, nameof(group));
            _panel = Guard.NotNull(panel, nameof(panel));
        }

        public bool IsOpen => _group.blocksRaycasts;

        public void Open()
        {
            Stop();
            _transition = _motion.Open(_group, _panel);
        }

        public void Close()
        {
            if (!IsOpen) return;
            Stop();
            _transition = _motion.Close(_group, _panel);
        }

        public void HideNow()
        {
            Stop();
            UiMotion.HideNow(_group);
        }

        private void Stop()
        {
            if (_transition.isAlive) _transition.Stop();
        }
    }
}
