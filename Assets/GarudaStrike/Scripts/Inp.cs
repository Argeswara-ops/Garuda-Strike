using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GarudaStrike
{
    public enum Act { Forward, Back, Left, Right, Jump, Sprint, Crouch, Reload, Knife, Grenade, Pause, Slot1, Slot2, Slot3, Slot4 }

    /// <summary>Pembungkus input: otomatis memakai Input System baru atau Input Manager lama.</summary>
    public static class Inp
    {
#if ENABLE_INPUT_SYSTEM
        static Key Map(Act a)
        {
            switch (a)
            {
                case Act.Forward: return Key.W;
                case Act.Back: return Key.S;
                case Act.Left: return Key.A;
                case Act.Right: return Key.D;
                case Act.Jump: return Key.Space;
                case Act.Sprint: return Key.LeftShift;
                case Act.Crouch: return Key.LeftCtrl;
                case Act.Reload: return Key.R;
                case Act.Knife: return Key.V;
                case Act.Grenade: return Key.G;
                case Act.Pause: return Key.Escape;
                case Act.Slot1: return Key.Digit1;
                case Act.Slot2: return Key.Digit2;
                case Act.Slot3: return Key.Digit3;
                default: return Key.Digit4;
            }
        }

        public static bool Held(Act a)
        {
            var k = Keyboard.current;
            if (k == null) return false;
            if (a == Act.Crouch && k[Key.C].isPressed) return true;
            return k[Map(a)].isPressed;
        }

        public static bool Down(Act a)
        {
            var k = Keyboard.current;
            if (k == null) return false;
            if (a == Act.Crouch && k[Key.C].wasPressedThisFrame) return true;
            return k[Map(a)].wasPressedThisFrame;
        }

        public static bool MouseHeld(int b)
        {
            var m = Mouse.current;
            if (m == null) return false;
            return b == 0 ? m.leftButton.isPressed : m.rightButton.isPressed;
        }

        public static bool MouseDown(int b)
        {
            var m = Mouse.current;
            if (m == null) return false;
            return b == 0 ? m.leftButton.wasPressedThisFrame : m.rightButton.wasPressedThisFrame;
        }

        public static Vector2 MouseDelta
        {
            get { var m = Mouse.current; return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f; }
        }

        public static float Scroll
        {
            get
            {
                var m = Mouse.current;
                if (m == null) return 0f;
                float y = m.scroll.ReadValue().y;
                return y > 0.01f ? 1f : (y < -0.01f ? -1f : 0f);
            }
        }
#else
        static KeyCode Map(Act a)
        {
            switch (a)
            {
                case Act.Forward: return KeyCode.W;
                case Act.Back: return KeyCode.S;
                case Act.Left: return KeyCode.A;
                case Act.Right: return KeyCode.D;
                case Act.Jump: return KeyCode.Space;
                case Act.Sprint: return KeyCode.LeftShift;
                case Act.Crouch: return KeyCode.LeftControl;
                case Act.Reload: return KeyCode.R;
                case Act.Knife: return KeyCode.V;
                case Act.Grenade: return KeyCode.G;
                case Act.Pause: return KeyCode.Escape;
                case Act.Slot1: return KeyCode.Alpha1;
                case Act.Slot2: return KeyCode.Alpha2;
                case Act.Slot3: return KeyCode.Alpha3;
                default: return KeyCode.Alpha4;
            }
        }

        public static bool Held(Act a)
        {
            if (a == Act.Crouch && Input.GetKey(KeyCode.C)) return true;
            return Input.GetKey(Map(a));
        }

        public static bool Down(Act a)
        {
            if (a == Act.Crouch && Input.GetKeyDown(KeyCode.C)) return true;
            return Input.GetKeyDown(Map(a));
        }

        public static bool MouseHeld(int b) { return Input.GetMouseButton(b); }
        public static bool MouseDown(int b) { return Input.GetMouseButtonDown(b); }

        public static Vector2 MouseDelta
        {
            get { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); }
        }

        public static float Scroll
        {
            get
            {
                float y = Input.mouseScrollDelta.y;
                return y > 0.01f ? 1f : (y < -0.01f ? -1f : 0f);
            }
        }
#endif
    }
}
