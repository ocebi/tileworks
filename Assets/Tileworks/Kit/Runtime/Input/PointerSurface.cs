using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tileworks.Kit.Input
{
    public interface IPointerSurface
    {
        event Action<int, Vector2> Pressed;
        event Action<int, Vector2> DragStarted;
        event Action<int, Vector2> Dragged;
        event Action<int, Vector2> Released;
    }

    // Invisible full-screen raycast target below every other canvas. The EventSystem gives each press to one target
    // only, so a press a button takes never reaches the world behind it.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PointerSurface : Graphic, IPointerSurface, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler
    {
        public event Action<int, Vector2> Pressed;
        public event Action<int, Vector2> DragStarted;
        public event Action<int, Vector2> Dragged;
        public event Action<int, Vector2> Released;

        public static PointerSurface Create(Transform parent)
        {
            var go = new GameObject("PointerSurface", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MinValue;
            go.AddComponent<GraphicRaycaster>();
            return go.AddComponent<PointerSurface>();
        }

        public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke(eventData.pointerId, eventData.position);
        public void OnBeginDrag(PointerEventData eventData) => DragStarted?.Invoke(eventData.pointerId, eventData.pressPosition);
        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData.pointerId, eventData.position);
        public void OnPointerUp(PointerEventData eventData) => Released?.Invoke(eventData.pointerId, eventData.position);

        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }
}
