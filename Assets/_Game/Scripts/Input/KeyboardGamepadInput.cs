using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Dino.Input
{
    /// <summary>
    /// Keyboard + mouse + gamepad, for testing in the editor now and PC/controller later.
    /// WASD move, mouse (right button held) or right stick look, Shift run, Ctrl/C sneak, Space jump,
    /// E eat/drink, left click / F attack, Q call, R ability. Gamepad: A jump, X action, B attack, Y call, RB ability, LT sneak, RT run.
    /// </summary>
    public class KeyboardGamepadInput : IDinoInputSource
    {
        public float MouseSensitivity = 0.15f;
        public float StickLookSpeed = 180f;

        public DinoInputState Read()
        {
            var s = new DinoInputState();
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector2 m = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) m.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) m.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) m.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) m.x -= 1;
                s.Move = Vector2.ClampMagnitude(m, 1f);
                s.Run = kb.leftShiftKey.isPressed;
                s.Sneak = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
                s.Jump = kb.spaceKey.wasPressedThisFrame;
                s.Action = kb.eKey.isPressed;
                s.Attack = kb.fKey.wasPressedThisFrame;
                s.Call = kb.qKey.wasPressedThisFrame;
                s.Ability = kb.rKey.wasPressedThisFrame;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed) s.Look += mouse.delta.ReadValue() * MouseSensitivity;
                s.Attack |= mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi();
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 m = pad.leftStick.ReadValue();
                if (m.sqrMagnitude > s.Move.sqrMagnitude) s.Move = m;
                s.Look += pad.rightStick.ReadValue() * StickLookSpeed * Time.deltaTime;
                s.Run |= pad.rightTrigger.isPressed;
                s.Sneak |= pad.leftTrigger.isPressed;
                s.Jump |= pad.buttonSouth.wasPressedThisFrame;
                s.Action |= pad.buttonWest.isPressed;
                s.Attack |= pad.buttonEast.wasPressedThisFrame;
                s.Call |= pad.buttonNorth.wasPressedThisFrame;
                s.Ability |= pad.rightShoulder.wasPressedThisFrame;
            }
#else
            s.Move = Vector2.ClampMagnitude(new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical")), 1f);
            if (UnityEngine.Input.GetMouseButton(1))
                s.Look = new Vector2(UnityEngine.Input.GetAxis("Mouse X"), UnityEngine.Input.GetAxis("Mouse Y")) * 3f;
            s.Run = UnityEngine.Input.GetKey(KeyCode.LeftShift);
            s.Sneak = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.C);
            s.Jump = UnityEngine.Input.GetKeyDown(KeyCode.Space);
            s.Action = UnityEngine.Input.GetKey(KeyCode.E);
            s.Attack = UnityEngine.Input.GetKeyDown(KeyCode.F);
            s.Call = UnityEngine.Input.GetKeyDown(KeyCode.Q);
            s.Ability = UnityEngine.Input.GetKeyDown(KeyCode.R);
#endif
            return s;
        }

        static bool IsPointerOverUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
