using Blast.Model;
using UnityEngine;

namespace Blast.View
{
    // Pieces are drawn in their final colours; cubes and disco balls have one sprite per colour (1-6, in order).
    [CreateAssetMenu(menuName = "Tileworks/Blast/Skin")]
    public sealed class BlastSkin : ScriptableObject
    {
        [SerializeField] private Sprite[] _cubes = new Sprite[0];
        [SerializeField] private Sprite[] _discos = new Sprite[0];
        [SerializeField] private Sprite _rocketRow;
        [SerializeField] private Sprite _rocketColumn;
        [SerializeField] private Sprite _bomb;
        [SerializeField, Tooltip("Box with two layers.")] private Sprite _box;
        [SerializeField, Tooltip("Box with one layer left.")] private Sprite _boxLastLayer;
        [SerializeField] private Sprite _balloon;
        [SerializeField] private Sprite _duck;
        [SerializeField] private Sprite _cell;
        [SerializeField, Tooltip("White 9-sliced rounded rect the board is built from.")] private Sprite _boardShape;
        [SerializeField] private Color _boardRim = new Color32(42, 14, 11, 255);
        [SerializeField] private Color _boardFill = new Color32(58, 26, 44, 255);
        [SerializeField, Min(0f), Tooltip("World units; one cell is one unit.")] private float _boardRimWidth = 0.047f;
        [SerializeField, Min(0f), Tooltip("World units the frame reaches past the cells.")] private float _frameMargin = 0.25f;
        [SerializeField] private Sprite _flash;
        [SerializeField] private Sprite _ring;
        [SerializeField] private Sprite _spark;
        [SerializeField] private Material _spriteMaterial;
        [SerializeField, Tooltip("Effect tints per cube colour, 1-6 in order.")] private Color[] _colours = new Color[0];

        public Sprite Cell => _cell;
        public Sprite BoardShape => _boardShape;
        public Color BoardRim => _boardRim;
        public Color BoardFill => _boardFill;
        public float BoardRimWidth => _boardRimWidth;
        public float FrameMargin => _frameMargin;
        public Sprite Flash => _flash;
        public Sprite Ring => _ring;
        public Sprite Spark => _spark;
        public Material SpriteMaterial => _spriteMaterial;

        public Color ColourOf(byte colour) => _colours.Length == 0 || colour == 0 ? Color.white : _colours[(colour - 1) % _colours.Length];

        public Sprite SpriteOf(Piece piece, byte colour, int hp) => piece switch
        {
            Piece.Cube => ByColour(_cubes, colour),
            Piece.Disco => ByColour(_discos, colour),
            Piece.RocketRow => _rocketRow,
            Piece.RocketColumn => _rocketColumn,
            Piece.Bomb => _bomb,
            Piece.Box => hp > 1 ? _box : _boxLastLayer,
            Piece.Balloon => _balloon,
            Piece.Duck => _duck,
            _ => null,
        };

        public void Configure(Sprite[] cubes, Sprite[] discos, Sprite rocketRow, Sprite rocketColumn, Sprite bomb, Sprite box, Sprite boxLastLayer,
            Sprite balloon, Sprite duck, Sprite cell, Sprite boardShape, Sprite flash, Sprite ring, Sprite spark, Material spriteMaterial, Color[] colours)
        {
            (_cubes, _discos, _rocketRow, _rocketColumn, _bomb) = (cubes, discos, rocketRow, rocketColumn, bomb);
            (_box, _boxLastLayer, _balloon, _duck, _cell, _boardShape) = (box, boxLastLayer, balloon, duck, cell, boardShape);
            (_flash, _ring, _spark, _spriteMaterial, _colours) = (flash, ring, spark, spriteMaterial, colours);
        }

        private static Sprite ByColour(Sprite[] sprites, byte colour) => sprites.Length == 0 ? null : sprites[(Mathf.Max(1, (int)colour) - 1) % sprites.Length];
    }
}
