using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Dino.World
{
    /// <summary>
    /// Free-fly camera so the island can be explored on its own (press Play in Island.unity).
    /// Removes itself as soon as another MainCamera exists (the real player camera).
    /// Keyboard: WASD move, Q/E down/up, Shift fast. Hold right mouse to look. Touch: one finger looks, two fly forward.
    /// </summary>
    public class IslandPreviewCamera : MonoBehaviour
    {
        public float speed = 25f;
        public float fastMultiplier = 4f;
        public float lookSensitivity = 0.15f;

        float _yaw, _pitch;

        void Start()
        {
            var e = transform.eulerAngles;
            _yaw = e.y; _pitch = e.x;
        }

        void Update()
        {
            foreach (var cam in Camera.allCameras)
            {
                if (cam.gameObject != gameObject && cam.CompareTag("MainCamera")) { Destroy(gameObject); return; }
            }

            Vector3 move = Vector3.zero;
            Vector2 look = Vector2.zero;
            bool fast = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.z += 1;
                if (kb.sKey.isPressed) move.z -= 1;
                if (kb.dKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed) move.x -= 1;
                if (kb.eKey.isPressed) move.y += 1;
                if (kb.qKey.isPressed) move.y -= 1;
                fast = kb.leftShiftKey.isPressed;
            }
            if (mouse != null && mouse.rightButton.isPressed) look = mouse.delta.ReadValue();
            var touch = Touchscreen.current;
            if (touch != null)
            {
                int active = 0;
                foreach (var t in touch.touches) if (t.press.isPressed) active++;
                if (active == 1) look = touch.primaryTouch.delta.ReadValue() * 0.5f;
                if (active >= 2) move.z += 1;
            }
#else
            move = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            if (Input.GetMouseButton(1)) look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
            fast = Input.GetKey(KeyCode.LeftShift);
#endif
            _yaw += look.x * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * lookSensitivity, -85f, 85f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position += transform.TransformDirection(move) * speed * (fast ? fastMultiplier : 1f) * Time.deltaTime;
        }
    }
}
