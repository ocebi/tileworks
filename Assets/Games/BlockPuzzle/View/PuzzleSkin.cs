using UnityEngine;

namespace BlockPuzzle.View
{
    [CreateAssetMenu(menuName = "Tileworks/Block Puzzle/Skin")]
    public sealed class PuzzleSkin : ScriptableObject
    {
        [SerializeField] private Sprite _block;
        [SerializeField] private Sprite _cell;
        [SerializeField] private Sprite _gem;
        [SerializeField] private Sprite _frame;
        [SerializeField] private Material _spriteMaterial;
        [SerializeField] private Color[] _colours = new Color[0];
        [SerializeField] private Color _cellTint = new Color(0.1f, 0.13f, 0.3f, 1f);
        [SerializeField] private Color _frameTint = new Color(0.06f, 0.08f, 0.2f, 1f);
        [SerializeField, Range(0f, 1f)] private float _ghostAlpha = 0.45f;

        public Sprite Block => _block;
        public Sprite Cell => _cell;
        public Sprite Gem => _gem;
        public Sprite Frame => _frame;
        public Material SpriteMaterial => _spriteMaterial;
        public Color CellTint => _cellTint;
        public Color FrameTint => _frameTint;
        public float GhostAlpha => _ghostAlpha;

        public Color ColourOf(byte colour) => _colours.Length == 0 ? Color.white : _colours[(Mathf.Max(1, colour) - 1) % _colours.Length];

        public void Configure(Sprite block, Sprite cell, Sprite gem, Sprite frame, Material spriteMaterial, Color[] colours)
        {
            _block = block;
            _cell = cell;
            _gem = gem;
            _frame = frame;
            _spriteMaterial = spriteMaterial;
            _colours = colours;
        }
    }
}
