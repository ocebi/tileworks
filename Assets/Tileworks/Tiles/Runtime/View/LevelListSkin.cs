using UnityEngine;

namespace Tileworks.Tiles.View
{
    // The level list's tile looks and selection feel; a tile background is 9-sliced at its multiplier.
    [CreateAssetMenu(menuName = "Tileworks/Tiles/Level List Skin")]
    public sealed class LevelListSkin : ScriptableObject
    {
        [Header("Tiles")]
        [SerializeField] private Sprite _cleared;
        [SerializeField, Min(0.01f)] private float _clearedSlice = 1f;
        [SerializeField] private Sprite _current;
        [SerializeField, Min(0.01f)] private float _currentSlice = 1f;
        [SerializeField] private Sprite _locked;
        [SerializeField, Min(0.01f)] private float _lockedSlice = 1f;
        [SerializeField] private Color _lockedTint = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color _clearedNumber = new Color32(42, 14, 11, 255);
        [SerializeField, Tooltip("White number with the current tile's outline.")] private Material _currentNumber;

        [Header("Selection")]
        [SerializeField, Min(1f)] private float _selectedScale = 1.08f;
        [SerializeField, Min(0f)] private float _selectDuration = 0.25f;

        [Header("Caption")]
        [SerializeField] private Color _caption = new Color32(74, 51, 48, 255);
        [SerializeField] private Color _refusal = new Color32(200, 40, 30, 255);

        public Sprite Cleared => _cleared;
        public float ClearedSlice => _clearedSlice;
        public Sprite Current => _current;
        public float CurrentSlice => _currentSlice;
        public Sprite Locked => _locked;
        public float LockedSlice => _lockedSlice;
        public Color LockedTint => _lockedTint;
        public Color ClearedNumber => _clearedNumber;
        public Material CurrentNumber => _currentNumber;
        public float SelectedScale => _selectedScale;
        public float SelectDuration => _selectDuration;
        public Color Caption => _caption;
        public Color Refusal => _refusal;

        public void Configure(Sprite cleared, float clearedSlice, Sprite current, float currentSlice, Sprite locked, float lockedSlice, Material currentNumber)
        {
            _cleared = cleared;
            _clearedSlice = clearedSlice;
            _current = current;
            _currentSlice = currentSlice;
            _locked = locked;
            _lockedSlice = lockedSlice;
            _currentNumber = currentNumber;
        }
    }
}
