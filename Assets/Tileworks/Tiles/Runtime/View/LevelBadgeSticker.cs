using PrimeTween;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworks.Tiles.View
{
    // A tilted ribbon with a skull that marks a Hard or Super Hard level. It slaps on with a pop and a swing when it appears.
    public sealed class LevelBadgeSticker : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private LevelIntroConfig _config;
        [SerializeField, ReadOnlyInInspector] private Image _ribbon;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _label;
        [SerializeField, ReadOnlyInInspector] private Sprite _hardRibbon;
        [SerializeField, ReadOnlyInInspector] private Sprite _superHardRibbon;

        private LevelBadge _shown;

        public void Show(LevelBadge badge, bool animate)
        {
            Tween.StopAll(transform);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            var was = _shown;
            _shown = badge;
            gameObject.SetActive(badge != LevelBadge.None);
            if (badge == LevelBadge.None) return;
            _ribbon.sprite = badge == LevelBadge.SuperHard ? _superHardRibbon : _hardRibbon;
            _label.text = badge == LevelBadge.SuperHard ? "Super Hard!" : "Hard Level";
            if (!animate || badge == was) return;
            _uiMotion.PopIn(transform, 0f);
            _uiMotion.Play(UiSound.Badge);
            if (_uiMotion.Reduced) return;
            transform.localRotation = Quaternion.Euler(0f, 0f, _config.BadgeSwing);
            Tween.LocalRotation(transform, Quaternion.identity, _uiMotion.Config.PopDuration * 1.6f, Ease.OutElastic, useUnscaledTime: true);
        }

        private void OnDestroy() => Tween.StopAll(transform);
    }
}
