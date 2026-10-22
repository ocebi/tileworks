using PrimeTween;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Match3.View
{
    // The hammer's aim and the booster effects on the board, beside the board view.
    public sealed class Match3BoosterBoard : MonoBehaviour, IHammerBoard, IBoosterFxBoard
    {
        private const int AimOrder = 40;

        [SerializeField, ReadOnlyInInspector] private Match3BoardView _board;
        [SerializeField, ReadOnlyInInspector] private BoosterFxConfig _fx;

        private SpriteRenderer _aim;

        public BoosterFxConfig Config => _fx;
        public Transform Root => _board.transform;
        public Sprite FlashSprite => _board.Skin.Flash;
        public Sprite RingSprite => _board.Skin.Ring;
        public Sprite SparkSprite => _board.Skin.Spark;
        public float UnitsPerPixel => _board.Camera.orthographicSize * 2f / _board.Camera.pixelHeight;

        public Rect ScreenRect
        {
            get
            {
                var camera = _board.Camera;
                var half = new Vector3(_board.Width * 0.5f + _board.Skin.FrameMargin, _board.Height * 0.5f + _board.Skin.FrameMargin, 0f);
                var min = camera.WorldToScreenPoint(_board.WorldOf(-half));
                var max = camera.WorldToScreenPoint(_board.WorldOf(half));
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }

        public SpriteRenderer Fx(Sprite sprite, Vector3 local, Vector2 scale, Color colour) => _board.Fx(sprite, local, scale, colour);

        public void Release(SpriteRenderer fx) => _board.Release(fx);

        public Vector3 LocalAt(Vector2 screen) => _board.LocalOf(World(screen));

        public int CellAtScreen(Vector2 screen) => _board.CellAt(World(screen));

        // The ring lives under the board's content, so a new level's board takes it away and the next aim makes a new one.
        public void Aim(int cell)
        {
            if (cell < 0)
            {
                if (_aim != null) _aim.gameObject.SetActive(false);
                return;
            }

            if (_aim == null)
            {
                _aim = _board.Fx(_fx.AimRing, Vector3.zero, Vector2.one * (_fx.AimSize / _fx.AimRing.bounds.size.x), _fx.AimColour);
                _aim.sortingOrder = AimOrder;
            }

            _aim.gameObject.SetActive(true);
            _aim.transform.localPosition = _board.CellPosition(cell);
        }

        public void RefuseAim(int cell)
        {
            _board.Sfx.Play(Match3Cue.Refuse);
            var piece = _board.PieceAt(cell);
            if (piece == null) return;
            Tween.StopAll(piece.transform);
            piece.transform.localRotation = Quaternion.identity;
            Tween.ShakeLocalRotation(piece.transform, new Vector3(0f, 0f, _fx.RefuseDegrees), _fx.RefuseDuration, frequency: 14f);
        }

        private Vector3 World(Vector2 screen)
        {
            var camera = _board.Camera;
            return camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
        }
    }
}
