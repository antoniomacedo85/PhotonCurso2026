using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace Starter
{
    /// <summary>
    /// On-screen control which turns finger dragging into a delta value - same as mouse delta.
    /// It is used for camera look on mobile platforms. The Look action is bound to an unused touch
    /// (Touchscreen/touch9/delta) so only dragging over this control rotates the camera.
    /// </summary>
    public sealed class OnScreenLook : OnScreenControl, IPointerDownHandler, IDragHandler
    {
        [InputControl]
        [SerializeField]
        private string _controlPath;

        private RectTransform _rectTransform;
        private Vector2 _pointerPosition;

        protected override string controlPathInternal
        {
            get => _controlPath;
            set => _controlPath = value;
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
        {
            // Remember the position where the finger touched the screen
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out _pointerPosition);
        }

        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out var pointerPosition);

            // Send how much the finger moved since the last frame to the bound control
            SendValueToControl(pointerPosition - _pointerPosition);

            _pointerPosition = pointerPosition;
        }
    }
}
