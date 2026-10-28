using UnityEngine;
using UnityEngine.InputSystem;

namespace LookismFighter
{
    /// <summary>What a fighter wants to do this frame. Directions are held; buttons are "pressed this frame".</summary>
    public struct FighterInput
    {
        public bool Left, Right, Up, Down;
        public bool B1, B2, B3, B4;   // 1 left punch, 2 right punch, 3 left kick, 4 right kick
        public bool Taunt;
    }

    public interface IInputSource
    {
        FighterInput Read(Fighter self);
    }

    /// <summary>Keyboard (and first/second gamepad) controls.</summary>
    public class KeyboardInput : IInputSource
    {
        public enum Layout { Solo, Player1, Player2 }
        readonly Layout layout;
        readonly int padIndex;

        public KeyboardInput(Layout layout, int padIndex = -1) { this.layout = layout; this.padIndex = padIndex; }

        static bool Held(Keyboard k, params Key[] keys) { foreach (var key in keys) if (k[key].isPressed) return true; return false; }
        static bool Down(Keyboard k, params Key[] keys) { foreach (var key in keys) if (k[key].wasPressedThisFrame) return true; return false; }

        public FighterInput Read(Fighter self)
        {
            var i = new FighterInput();
            var k = Keyboard.current;
            if (k != null)
            {
                switch (layout)
                {
                    case Layout.Solo:
                        i.Left = Held(k, Key.A); i.Right = Held(k, Key.D); i.Up = Held(k, Key.W); i.Down = Held(k, Key.S);
                        i.B1 = Down(k, Key.U); i.B2 = Down(k, Key.I); i.B3 = Down(k, Key.J); i.B4 = Down(k, Key.K);
                        i.Taunt = Down(k, Key.T);
                        break;
                    case Layout.Player1:
                        i.Left = Held(k, Key.A); i.Right = Held(k, Key.D); i.Up = Held(k, Key.W); i.Down = Held(k, Key.S);
                        i.B1 = Down(k, Key.F); i.B2 = Down(k, Key.G); i.B3 = Down(k, Key.C); i.B4 = Down(k, Key.V);
                        i.Taunt = Down(k, Key.B);
                        break;
                    case Layout.Player2:
                        i.Left = Held(k, Key.LeftArrow); i.Right = Held(k, Key.RightArrow); i.Up = Held(k, Key.UpArrow); i.Down = Held(k, Key.DownArrow);
                        i.B1 = Down(k, Key.Numpad4, Key.K); i.B2 = Down(k, Key.Numpad5, Key.L);
                        i.B3 = Down(k, Key.Numpad1, Key.Comma); i.B4 = Down(k, Key.Numpad2, Key.Period);
                        i.Taunt = Down(k, Key.Numpad0, Key.Slash);
                        break;
                }
            }

            // On-screen touch controls drive Player 1
            var tc = TouchControls.Instance;
            if (tc != null && tc.Visible && layout != Layout.Player2)
            {
                i.Left |= tc.Left; i.Right |= tc.Right; i.Up |= tc.Up; i.Down |= tc.Down;
                i.B1 |= tc.B1; i.B2 |= tc.B2; i.B3 |= tc.B3; i.B4 |= tc.B4;
            }

            // Gamepad: square/X = 1, triangle/Y = 2, cross/A = 3, circle/B = 4
            if (padIndex >= 0 && padIndex < Gamepad.all.Count)
            {
                var g = Gamepad.all[padIndex];
                Vector2 s = g.leftStick.ReadValue();
                i.Left |= s.x < -0.45f || g.dpad.left.isPressed;
                i.Right |= s.x > 0.45f || g.dpad.right.isPressed;
                i.Up |= s.y > 0.6f || g.dpad.up.isPressed;
                i.Down |= s.y < -0.5f || g.dpad.down.isPressed;
                i.B1 |= g.buttonWest.wasPressedThisFrame;
                i.B2 |= g.buttonNorth.wasPressedThisFrame;
                i.B3 |= g.buttonSouth.wasPressedThisFrame;
                i.B4 |= g.buttonEast.wasPressedThisFrame;
                i.Taunt |= g.selectButton.wasPressedThisFrame;
            }
            return i;
        }
    }
}
