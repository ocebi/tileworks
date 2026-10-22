using UnityEditor;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // Left button paints and right button erases. A drag keeps painting, reports each cell once and undoes as one step.
    public sealed class BoardPainter
    {
        private int _strokeGroup;
        private Vector2Int _lastCell;

        public bool Paint(Rect board, float cell, out Vector2Int hit, out bool erase)
        {
            var e = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);
            hit = default;
            erase = e.button == 1;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when (e.button == 0 || e.button == 1) && board.Contains(e.mousePosition):
                    GUIUtility.hotControl = id;
                    _strokeGroup = Undo.GetCurrentGroup();
                    _lastCell = new Vector2Int(-1, -1);
                    e.Use();
                    return Hit(board, cell, e.mousePosition, out hit);
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    e.Use();
                    return Hit(board, cell, e.mousePosition, out hit);
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    Undo.CollapseUndoOperations(_strokeGroup);
                    e.Use();
                    return false;
                case EventType.ContextClick when board.Contains(e.mousePosition):
                    e.Use();
                    return false;
            }

            return false;
        }

        private bool Hit(Rect board, float cell, Vector2 mouse, out Vector2Int hit)
        {
            hit = new Vector2Int(Mathf.FloorToInt((mouse.x - board.x) / cell), Mathf.FloorToInt((mouse.y - board.y) / cell));
            if (!board.Contains(mouse) || hit == _lastCell) return false;
            _lastCell = hit;
            GUI.changed = true;
            return true;
        }
    }
}
