using UnityEngine;
using UnityEngine.InputSystem;

namespace LookismFighter
{
    /// <summary>
    /// On-screen controls for phones and tablets (also in mobile browsers).
    /// Left half: floating joystick (move, jump, crouch, hold back to block). Right side: 1 2 3 4 attack buttons.
    /// Appears automatically on mobile, or as soon as the screen is touched. Drives Player 1.
    /// </summary>
    [DefaultExecutionOrder(-100)] // read touches before the fighters read their input
    public class TouchControls : MonoBehaviour
    {
        public static TouchControls Instance { get; private set; }

        [Tooltip("Show the touch controls in the editor too (click and drag with the mouse to test).")]
        public bool showInEditor = false;

        // ---- state read by KeyboardInput (Player 1) ----
        public bool Left { get; private set; }
        public bool Right { get; private set; }
        public bool Up { get; private set; }
        public bool Down { get; private set; }
        public bool B1 { get; private set; }
        public bool B2 { get; private set; }
        public bool B3 { get; private set; }
        public bool B4 { get; private set; }
        public bool PausePressed { get; private set; }
        public bool TappedAnywhere { get; private set; }
        public bool Visible => visible;

        bool visible;
        int stickFinger = -1;
        Vector2 stickOrigin, stickPos;          // GUI coordinates (y down)
        readonly bool[] held = new bool[4];
        readonly float[] flash = new float[4];
        Texture2D circle, ring;
        GUIStyle label;

        static readonly string[] Labels = { "1", "2", "3", "4" };
        static readonly string[] Names = { "L PUNCH", "R PUNCH", "L KICK", "R KICK" };

        void Awake()
        {
            Instance = this;
            visible = Application.isMobilePlatform || (Application.isEditor && showInEditor);
            circle = MakeCircle(128, false);
            ring = MakeCircle(128, true);
        }

        // ---------------------------------------------------------------- layout (GUI coordinates)
        float Unit => Mathf.Min(Screen.width, Screen.height);
        Vector2 DefaultStick => new Vector2(Screen.width * 0.16f, Screen.height * 0.72f);
        float StickRadius => Unit * 0.13f;
        float ButtonSize => Unit * 0.17f;

        Rect ButtonRect(int i)
        {
            // 2x2 grid: 1 2 on top, 3 4 below, like Tekken Mobile
            float b = ButtonSize, gap = b * 0.18f;
            float right = Screen.width - Unit * 0.06f, bottom = Screen.height - Unit * 0.08f;
            float col = i % 2, row = i / 2;
            float x = right - (2 - col) * b - (1 - col) * gap;
            float y = bottom - (2 - row) * b - (1 - row) * gap;
            return new Rect(x, y, b, b);
        }

        Rect PauseRect => new Rect(Screen.width / 2f - Unit * 0.05f, Unit * 0.16f, Unit * 0.1f, Unit * 0.08f);

        static Vector2 ToGui(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y);

        // ---------------------------------------------------------------- input
        void Update()
        {
            B1 = B2 = B3 = B4 = PausePressed = TappedAnywhere = false;
            for (int i = 0; i < 4; i++) { held[i] = false; flash[i] = Mathf.Max(0f, flash[i] - Time.unscaledDeltaTime); }
            bool stickSeen = false;

            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed && !t.press.wasReleasedThisFrame) continue;
                    visible = true;
                    int id = t.touchId.ReadValue();
                    Vector2 p = ToGui(t.position.ReadValue());
                    bool began = t.press.wasPressedThisFrame;
                    HandlePointer(id, p, began, t.press.isPressed, ref stickSeen);
                }
            }
            else if (Application.isEditor && showInEditor && Mouse.current != null)
            {
                var m = Mouse.current;
                if (m.leftButton.isPressed || m.leftButton.wasReleasedThisFrame)
                    HandlePointer(9999, ToGui(m.position.ReadValue()), m.leftButton.wasPressedThisFrame, m.leftButton.isPressed, ref stickSeen);
            }

            if (!stickSeen) { stickFinger = -1; stickPos = stickOrigin = DefaultStick; }

            // joystick vector -> directions (y up)
            Vector2 v = (stickPos - stickOrigin) / StickRadius;
            v.y = -v.y;
            Left = v.x < -0.35f; Right = v.x > 0.35f; Up = v.y > 0.55f; Down = v.y < -0.45f;
        }

        void HandlePointer(int id, Vector2 p, bool began, bool pressed, ref bool stickSeen)
        {
            if (began) TappedAnywhere = true;

            if (began && PauseRect.Contains(p)) { PausePressed = true; return; }

            for (int b = 0; b < 4; b++)
            {
                if (!ButtonRect(b).Contains(p)) continue;
                if (began)
                {
                    if (b == 0) B1 = true; else if (b == 1) B2 = true; else if (b == 2) B3 = true; else B4 = true;
                    flash[b] = 0.15f;
                }
                held[b] = pressed;
                return;
            }

            // joystick: any touch that starts on the left half becomes the stick; it floats to where you put your thumb
            if (id == stickFinger || (stickFinger < 0 && began && p.x < Screen.width * 0.5f))
            {
                if (!pressed) { stickFinger = -1; return; }
                if (stickFinger < 0) { stickFinger = id; stickOrigin = p; }
                Vector2 d = p - stickOrigin;
                if (d.magnitude > StickRadius) { stickOrigin = p - d.normalized * StickRadius; d = p - stickOrigin; } // drag the base along
                stickPos = stickOrigin + d;
                stickSeen = true;
            }
        }

        // ---------------------------------------------------------------- drawing
        void OnGUI()
        {
            if (!visible) return;
            GUI.depth = -10;
            if (label == null) label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            // joystick
            float r = StickRadius;
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(stickOrigin.x - r, stickOrigin.y - r, r * 2f, r * 2f), ring);
            GUI.color = new Color(1f, 1f, 1f, stickFinger >= 0 ? 0.55f : 0.3f);
            float k = r * 0.45f;
            GUI.DrawTexture(new Rect(stickPos.x - k, stickPos.y - k, k * 2f, k * 2f), circle);

            // attack buttons
            Color punch = new Color(1f, 0.82f, 0.25f), kick = new Color(1f, 0.35f, 0.3f);
            for (int b = 0; b < 4; b++)
            {
                Rect rc = ButtonRect(b);
                Color c = b < 2 ? punch : kick;
                bool lit = held[b] || flash[b] > 0f;
                GUI.color = new Color(c.r, c.g, c.b, lit ? 0.75f : 0.35f);
                GUI.DrawTexture(rc, circle);
                GUI.color = new Color(1f, 1f, 1f, 0.9f);
                label.fontSize = Mathf.RoundToInt(rc.height * 0.36f);
                GUI.Label(new Rect(rc.x, rc.y - rc.height * 0.08f, rc.width, rc.height), Labels[b], label);
                label.fontSize = Mathf.RoundToInt(rc.height * 0.12f);
                GUI.Label(new Rect(rc.x, rc.y + rc.height * 0.28f, rc.width, rc.height * 0.4f), Names[b], label);
            }

            // pause
            Rect pr = PauseRect;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(pr, circle);
            GUI.color = Color.white;
            label.fontSize = Mathf.RoundToInt(pr.height * 0.5f);
            GUI.Label(pr, "II", label);
            GUI.color = Color.white;
        }

        static Texture2D MakeCircle(int size, bool ringOnly)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01((1f - d) * size * 0.5f);              // anti-aliased edge
                if (ringOnly) a *= Mathf.Clamp01((d - 0.86f) * size * 0.5f);   // hollow centre
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }
    }
}
