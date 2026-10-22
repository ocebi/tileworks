using UnityEngine;

namespace TileMatch.View
{
    // Kind → icon plus the tile base and modifier overlays; swapping art never touches code.
    [CreateAssetMenu(menuName = "Tileworks/Tile Match/Tile Skin")]
    public sealed class TileSkin : ScriptableObject
    {
        [SerializeField] private Sprite[] _icons = new Sprite[0];
        [SerializeField] private Sprite _face;
        [SerializeField] private Sprite _shadow;
        [SerializeField] private Sprite _hiddenBack;
        [SerializeField] private Sprite[] _ice = new Sprite[0];
        [SerializeField] private Sprite _link;
        [SerializeField] private Color _coveredTint = new Color(0.55f, 0.58f, 0.66f, 1f);
        [SerializeField] private float _iconScale = 0.62f;

        public Sprite Face => _face;
        public Sprite Shadow => _shadow;
        public Sprite HiddenBack => _hiddenBack;
        public Sprite Link => _link;
        public Color CoveredTint => _coveredTint;
        public float IconScale => _iconScale;
        public int IconCount => _icons.Length;

        public Sprite IconOf(int kind) => _icons.Length == 0 ? null : _icons[kind % _icons.Length];
        public Sprite IceOf(int layers) => layers <= 0 || _ice.Length == 0 ? null : _ice[Mathf.Min(layers, _ice.Length) - 1];

        public void Configure(Sprite[] icons, Sprite face, Sprite shadow, Sprite hiddenBack, Sprite[] ice, Sprite link)
        {
            _icons = icons;
            _face = face;
            _shadow = shadow;
            _hiddenBack = hiddenBack;
            _ice = ice;
            _link = link;
        }
    }
}
