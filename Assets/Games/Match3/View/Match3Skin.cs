using Match3.Model;
using UnityEngine;

namespace Match3.View
{
    // Pieces are drawn in their final colours; gems have one sprite per colour (1-6, in order). Specials are colourless.
    [CreateAssetMenu(menuName = "Tileworks/Match3/Skin")]
    public sealed class Match3Skin : ScriptableObject
    {
        [SerializeField] private Sprite[] _gems = new Sprite[0];
        [SerializeField] private Sprite _rocketRow;
        [SerializeField] private Sprite _rocketColumn;
        [SerializeField] private Sprite _bomb;
        [SerializeField] private Sprite _disco;
        [SerializeField] private Sprite _flyer;
        [SerializeField, Tooltip("Box with two layers.")] private Sprite _box;
        [SerializeField, Tooltip("Box with one layer left.")] private Sprite _boxLastLayer;
        [SerializeField, Tooltip("Floor with two layers.")] private Sprite _floor;
        [SerializeField, Tooltip("Floor with one layer left.")] private Sprite _floorLastLayer;
        [SerializeField] private Sprite _chain;
        [SerializeField] private Sprite _balloon;
        [SerializeField] private Sprite _duck;
        [SerializeField] private Sprite _cellLight;
        [SerializeField] private Sprite _cellDark;
        [SerializeField, Tooltip("9-sliced rim drawn around each band of playable rows, under every fill.")] private Sprite _boardRim;
        [SerializeField, Tooltip("9-sliced fill drawn over the rims, inset by the rim width.")] private Sprite _boardFill;
        [SerializeField, Min(0f), Tooltip("World units the frame reaches past the cells.")] private float _frameMargin = 0.28f;
        [SerializeField, Min(0f), Tooltip("World units of rim left showing around the fill.")] private float _frameRimWidth = 0.07f;
        [SerializeField] private Sprite _selectRing;
        [SerializeField] private Sprite _hintGlow;
        [SerializeField] private Sprite _flash;
        [SerializeField] private Sprite _ring;
        [SerializeField] private Sprite _spark;
        [SerializeField] private Material _spriteMaterial;
        [SerializeField, Tooltip("Effect tints per gem colour, 1-6 in order.")] private Color[] _colours = new Color[0];

        public Sprite Chain => _chain;
        public Sprite BoardRim => _boardRim;
        public Sprite BoardFill => _boardFill;
        public float FrameMargin => _frameMargin;
        public float FrameRimWidth => _frameRimWidth;
        public Sprite SelectRing => _selectRing;
        public Sprite HintGlow => _hintGlow;
        public Sprite Flash => _flash;
        public Sprite Ring => _ring;
        public Sprite Spark => _spark;
        public Material SpriteMaterial => _spriteMaterial;

        public Sprite Cell(int x, int y) => (x + y) % 2 == 0 ? _cellLight : _cellDark;

        public Sprite Floor(int layers) => layers > 1 ? _floor : _floorLastLayer;

        public Color ColourOf(byte colour) => _colours.Length == 0 || colour == 0 ? Color.white : _colours[(colour - 1) % _colours.Length];

        public Sprite SpriteOf(Piece piece, byte colour, int hp) => piece switch
        {
            Piece.Gem => _gems.Length == 0 ? null : _gems[(Mathf.Max(1, (int)colour) - 1) % _gems.Length],
            Piece.RocketRow => _rocketRow,
            Piece.RocketColumn => _rocketColumn,
            Piece.Bomb => _bomb,
            Piece.Disco => _disco,
            Piece.Flyer => _flyer,
            Piece.Box => hp > 1 ? _box : _boxLastLayer,
            Piece.Balloon => _balloon,
            Piece.Duck => _duck,
            _ => null,
        };

        // The sprite on a goal's chip.
        public Sprite GoalSprite(in Match3Goal goal) => goal.Kind switch
        {
            GoalKind.Box => _box,
            GoalKind.Balloon => _balloon,
            GoalKind.Duck => _duck,
            GoalKind.Floor => _floor,
            GoalKind.Chain => _chain,
            _ => SpriteOf(Piece.Gem, goal.Colour, 0),
        };

        public void Configure(Sprite[] gems, Sprite[] specials, Sprite[] blockers, Sprite[] board, Sprite[] effects, Material spriteMaterial, Color[] colours)
        {
            _gems = gems;
            (_rocketRow, _rocketColumn, _bomb, _disco, _flyer) = (specials[0], specials[1], specials[2], specials[3], specials[4]);
            (_box, _boxLastLayer, _floor, _floorLastLayer, _chain, _balloon, _duck) = (blockers[0], blockers[1], blockers[2], blockers[3], blockers[4], blockers[5], blockers[6]);
            (_cellLight, _cellDark, _boardRim, _boardFill, _selectRing, _hintGlow) = (board[0], board[1], board[2], board[3], board[4], board[5]);
            (_flash, _ring, _spark) = (effects[0], effects[1], effects[2]);
            (_spriteMaterial, _colours) = (spriteMaterial, colours);
        }
    }
}
