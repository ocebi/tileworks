using Tileworks.Kit.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworks.Tiles.View
{
    public enum LevelTileState
    {
        Cleared,
        Current,
        Locked,
    }

    // One level in the list. The root breathes while it is the current level; the body grows while selected.
    public sealed class LevelTile : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private Button _button;
        [SerializeField, ReadOnlyInInspector] private RectTransform _body;
        [SerializeField, ReadOnlyInInspector] private Image _background;
        [SerializeField, ReadOnlyInInspector] private Image _ring;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _number;
        [SerializeField, ReadOnlyInInspector] private GameObject _check;
        [SerializeField, ReadOnlyInInspector] private GameObject _lock;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _lockNumber;
        [SerializeField, ReadOnlyInInspector] private Image _hard;
        [SerializeField, ReadOnlyInInspector] private Sprite _hardSprite;
        [SerializeField, ReadOnlyInInspector] private Sprite _superHardSprite;

        private Material _plainNumber;

        public Button Button => _button;
        public RectTransform Body => _body;

        public void Show(int number, LevelTileState state, LevelListSkin skin, LevelBadge badge = LevelBadge.None)
        {
            _hard.gameObject.SetActive(badge != LevelBadge.None);
            _hard.sprite = badge == LevelBadge.SuperHard ? _superHardSprite : _hardSprite;
            if (_plainNumber == null) _plainNumber = _number.fontSharedMaterial;
            var text = number.ToString();
            _number.text = text;
            _lockNumber.text = text;
            _number.gameObject.SetActive(state != LevelTileState.Locked);
            _check.SetActive(state == LevelTileState.Cleared);
            _lock.SetActive(state == LevelTileState.Locked);
            _ring.enabled = false;
            switch (state)
            {
                case LevelTileState.Cleared:
                    Paint(skin.Cleared, skin.ClearedSlice, Color.white);
                    _number.color = skin.ClearedNumber;
                    _number.fontSharedMaterial = _plainNumber;
                    break;
                case LevelTileState.Current:
                    Paint(skin.Current, skin.CurrentSlice, Color.white);
                    _number.color = Color.white;
                    _number.fontSharedMaterial = skin.CurrentNumber != null ? skin.CurrentNumber : _plainNumber;
                    break;
                default:
                    Paint(skin.Locked, skin.LockedSlice, skin.LockedTint);
                    break;
            }
        }

        public void Highlight(bool on) => _ring.enabled = on;

        private void Paint(Sprite sprite, float slice, Color tint)
        {
            _background.sprite = sprite;
            _background.pixelsPerUnitMultiplier = slice;
            _background.color = tint;
        }
    }
}
