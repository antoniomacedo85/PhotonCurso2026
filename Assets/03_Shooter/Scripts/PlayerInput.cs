using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Structure holding player input.
    /// </summary>
    public struct GameplayInput
    {
        public Vector2 LookRotation;
        public Vector2 MoveDirection;
        public bool Jump;
        public bool Fire;
    }

    /// <summary>
    /// PlayerInput handles accumulating player input from Unity.
    /// </summary>
    public sealed class PlayerInput : MonoBehaviour
    {
        public GameplayInput CurrentInput => _input;

        private GameplayInput _input;
        private InputActions _inputActions;

        public void ResetInput()
        {
            // Reset input after it was used to detect changes correctly again
            _input.MoveDirection = default;
            _input.Jump = false;
            _input.Fire = false;
        }

        private void OnEnable()
        {
            // InputActions class is auto-generated from the InputSystem_Actions asset.
            _inputActions ??= new InputActions();
            _inputActions.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Disable();
        }

        private void Update()
        {
            // Accumulate input only if the cursor is locked. Mobile platforms do not support cursor locking,
            // there the on-screen controls are hidden while the in-game menu is opened instead.
            if (Application.isMobilePlatform == false && Cursor.lockState != CursorLockMode.Locked)
            {
                _input.MoveDirection = default;
                return;
            }

            // Accumulate input from Keyboard/Mouse. Input accumulation is mandatory (at least for look rotation here) as Update can be
            // called multiple times before next FixedUpdateNetwork is called - common if rendering speed is faster than Fusion simulation.

            var lookValue = _inputActions.Player.Look.ReadValue<Vector2>();
            var lookRotationDelta = new Vector2(-lookValue.y, lookValue.x);

#if UNITY_WEBGL && !UNITY_EDITOR
            // Following corrections are needed for desktop browsers only. On mobile browsers the look
            // input comes from the on-screen control and is not affected by mouse related issues.
            if (Application.isMobilePlatform == false)
            {
                if (Mathf.Abs(lookRotationDelta.x) > 45 || Mathf.Abs(lookRotationDelta.y) > 45)
                {
                    // Prevent glitch in Chrome with high polling mice where cursor jumps rapidly from time to time
                    lookRotationDelta = default;
                }

                // Sensitivity in WebGL builds on desktop is much higher for some reason, decrease it
                lookRotationDelta *= 0.5f;
            }
#endif

            _input.LookRotation += lookRotationDelta;
            _input.MoveDirection = _inputActions.Player.Move.ReadValue<Vector2>();

            // Attack action is read as held down (instead of pressed this frame) so the
            // weapon keeps firing while the fire button is held. Fire cadence is handled in Player.
            _input.Fire |= _inputActions.Player.Attack.IsPressed();
            _input.Jump |= _inputActions.Player.Jump.WasPressedThisFrame();
        }
    }
}