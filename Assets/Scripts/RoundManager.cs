using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LookismFighter
{
    /// <summary>
    /// Runs the match: rounds, timer, KO, hit-stop, hit sparks, and draws the HUD (health bars, timer, announcements).
    /// Keys: F1 = VS CPU, F2 = 2 players, F3 = training, F5 = CPU difficulty, H = controls, Esc = pause, Enter = rematch.
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        public enum Mode { VsCpu, TwoPlayers, Training }

        public static RoundManager Instance { get; private set; }
        public static bool Frozen => Instance != null && Time.unscaledTime < Instance.freezeUntil;

        public Fighter player1, player2;
        public Mode mode = Mode.VsCpu;
        [Range(0, 2)] public int cpuLevel = 1;
        public int roundTime = 60;
        public int roundsToWin = 2;
        public float startDistance = 3.2f;

        int round = 1, wins1, wins2;
        float timer, freezeUntil, announceUntil, counterUntil1, counterUntil2;
        string announce = "", subAnnounce = "";
        bool fighting, matchOver, paused, showHelp = true;
        Coroutine flow;
        static readonly string[] Levels = { "EASY", "NORMAL", "HARD" };

        void Awake()
        {
            Instance = this;
            if (!GetComponent<AudioManager>()) gameObject.AddComponent<AudioManager>();
        }

        void Start()
        {
            if (!player1 || !player2) { Debug.LogError("RoundManager: assign player1 and player2 (run Tools > Lookism Fighter > Setup Fight Scene)."); enabled = false; return; }
            player1.opponent = player2; player2.opponent = player1;
            StartMatch(mode);
        }

        void StartMatch(Mode m)
        {
            mode = m;
            Time.timeScale = 1f; paused = false; matchOver = false;
            wins1 = wins2 = 0; round = 1;
            int pads = Gamepad.all.Count;
            switch (mode)
            {
                case Mode.VsCpu:
                    player1.input = new KeyboardInput(KeyboardInput.Layout.Solo, pads > 0 ? 0 : -1);
                    player2.input = new CpuOpponent(cpuLevel);
                    break;
                case Mode.TwoPlayers:
                    player1.input = new KeyboardInput(KeyboardInput.Layout.Player1, pads > 0 ? 0 : -1);
                    player2.input = new KeyboardInput(KeyboardInput.Layout.Player2, pads > 1 ? 1 : -1);
                    break;
                case Mode.Training:
                    player1.input = new KeyboardInput(KeyboardInput.Layout.Solo, pads > 0 ? 0 : -1);
                    player2.input = null; // standing dummy
                    break;
            }
            if (flow != null) StopCoroutine(flow);
            flow = StartCoroutine(RoundLoop());
        }

        IEnumerator RoundLoop()
        {
            while (true)
            {
                player1.ResetForRound(-startDistance / 2f);
                player2.ResetForRound(startDistance / 2f);
                player1.controlsEnabled = player2.controlsEnabled = false;
                timer = roundTime;
                bool finalRound = wins1 == roundsToWin - 1 && wins2 == roundsToWin - 1;
                Announce(finalRound ? "FINAL ROUND" : "ROUND " + round, "", 1.3f);
                if (AudioManager.Instance) AudioManager.Instance.RoundBell();
                yield return new WaitForSecondsRealtime(1.4f);
                Announce("FIGHT!", "", 0.7f);
                player1.controlsEnabled = player2.controlsEnabled = true;
                fighting = true;

                // fight until KO or time-out
                while (true)
                {
                    if (mode != Mode.Training) timer -= Time.deltaTime;
                    if (player1.State == FState.KO || player2.State == FState.KO || timer <= 0f) break;
                    if (mode == Mode.Training && player2.Health < player2.maxHealth * 0.25f) player2.ResetForRound(player2.X);
                    yield return null;
                }
                fighting = false;
                timer = Mathf.Max(0f, timer);

                Fighter winner = null;
                if (player1.State == FState.KO && player2.State != FState.KO) winner = player2;
                else if (player2.State == FState.KO && player1.State != FState.KO) winner = player1;
                else if (player1.Health != player2.Health) winner = player1.Health > player2.Health ? player1 : player2;

                bool ko = player1.State == FState.KO || player2.State == FState.KO;
                if (ko)
                {
                    bool perfect = winner != null && winner.Health == winner.maxHealth;
                    Announce(perfect ? "PERFECT" : "K.O.", "", 2.2f);
                    if (AudioManager.Instance) AudioManager.Instance.KO();
                    Time.timeScale = 0.3f;
                    yield return new WaitForSecondsRealtime(1.3f);
                    Time.timeScale = 1f;
                }
                else Announce("TIME", winner == null ? "DRAW" : "", 2f);

                player1.controlsEnabled = player2.controlsEnabled = false;
                yield return new WaitForSecondsRealtime(1.2f);
                if (winner == player1) wins1++; else if (winner == player2) wins2++;
                if (winner != null) winner.Celebrate();
                yield return new WaitForSecondsRealtime(1.8f);

                if (wins1 >= roundsToWin || wins2 >= roundsToWin)
                {
                    matchOver = true;
                    string who = mode == Mode.VsCpu ? (wins1 >= roundsToWin ? "YOU WIN" : "YOU LOSE") : (wins1 >= roundsToWin ? "1P WINS" : "2P WINS");
                    Announce(who, "Press ENTER for a rematch", 9999f);
                    yield break;
                }
                round++;
            }
        }

        void Announce(string big, string small, float seconds) { announce = big; subAnnounce = small; announceUntil = Time.unscaledTime + seconds; }

        // ------------------------------------------------------------------ called by fighters
        public void OnHit(Fighter attacker, Fighter defender, int damage, bool counter, bool heavy)
        {
            freezeUntil = Time.unscaledTime + (heavy ? 0.13f : 0.07f);
            if (AudioManager.Instance) AudioManager.Instance.Hit(heavy);
            Spark(Between(attacker, defender, heavy ? 1.2f : 1.4f), counter ? new Color(1f, 0.45f, 0.2f) : Color.white, heavy ? 0.7f : 0.45f);
            if (FightCamera.Instance) FightCamera.Instance.Shake(heavy ? 0.12f : 0.05f);
            if (counter) { if (attacker == player1) counterUntil1 = Time.unscaledTime + 0.8f; else counterUntil2 = Time.unscaledTime + 0.8f; }
        }

        public void OnBlock(Fighter attacker, Fighter defender)
        {
            freezeUntil = Time.unscaledTime + 0.05f;
            if (AudioManager.Instance) AudioManager.Instance.Blocked();
            Spark(Between(attacker, defender, 1.3f), new Color(0.5f, 0.8f, 1f), 0.35f);
        }

        static Vector3 Between(Fighter a, Fighter b, float y) => new Vector3((a.X + b.X) * 0.5f, y, -0.25f);

        // ------------------------------------------------------------------ hit sparks
        static Material sparkMat;
        void Spark(Vector3 pos, Color color, float size) => StartCoroutine(SparkRoutine(pos, color, size));

        IEnumerator SparkRoutine(Vector3 pos, Color color, float size)
        {
            if (sparkMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                sparkMat = new Material(sh);
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.material = sparkMat;
            r.material.color = color;
            if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.position = pos;
            float t = 0f;
            while (t < 0.16f)
            {
                t += Time.unscaledDeltaTime;
                float k = t / 0.16f;
                go.transform.localScale = Vector3.one * size * (k < 0.4f ? k / 0.4f : 1f - (k - 0.4f) / 0.6f * 0.9f);
                yield return null;
            }
            Destroy(go);
        }

        // ------------------------------------------------------------------ menu keys
        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame) { paused = !paused; Time.timeScale = paused ? 0f : 1f; }
            if (k.hKey.wasPressedThisFrame) showHelp = !showHelp;
            if (k.f1Key.wasPressedThisFrame) StartMatch(Mode.VsCpu);
            if (k.f2Key.wasPressedThisFrame) StartMatch(Mode.TwoPlayers);
            if (k.f3Key.wasPressedThisFrame) StartMatch(Mode.Training);
            if (k.f5Key.wasPressedThisFrame) { cpuLevel = (cpuLevel + 1) % 3; if (mode == Mode.VsCpu) player2.input = new CpuOpponent(cpuLevel); Announce(Levels[cpuLevel], "CPU difficulty", 1f); }
            if (matchOver && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) StartMatch(mode);
        }

        // ------------------------------------------------------------------ HUD
        GUIStyle big, small, nameStyle, timerStyle, help;
        Texture2D white;

        void InitStyles()
        {
            white = Texture2D.whiteTexture;
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            small = new GUIStyle(big);
            nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            timerStyle = new GUIStyle(big);
            help = new GUIStyle(GUI.skin.label) { wordWrap = true };
        }

        void Shadowed(Rect r, string text, GUIStyle s, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.85f); GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, s);
            GUI.color = c; GUI.Label(r, text, s);
            GUI.color = old;
        }

        void Bar(Rect r, float fraction, bool rightToLeft, Color fill)
        {
            GUI.color = Color.black; GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), white);
            GUI.color = new Color(0.25f, 0.06f, 0.06f); GUI.DrawTexture(r, white);
            float w = r.width * Mathf.Clamp01(fraction);
            GUI.color = fill;
            GUI.DrawTexture(rightToLeft ? new Rect(r.xMax - w, r.y, w, r.height) : new Rect(r.x, r.y, w, r.height), white);
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            if (!player1 || !player2) return;
            if (big == null) InitStyles();
            float s = Screen.height / 1080f;
            big.fontSize = Mathf.RoundToInt(130 * s); small.fontSize = Mathf.RoundToInt(34 * s);
            nameStyle.fontSize = Mathf.RoundToInt(30 * s); timerStyle.fontSize = Mathf.RoundToInt(64 * s); help.fontSize = Mathf.RoundToInt(20 * s);

            float W = Screen.width, pad = 30 * s, barW = W * 0.38f, barH = 30 * s, top = 40 * s;
            Color low = new Color(1f, 0.25f, 0.2f), ok = new Color(1f, 0.82f, 0.2f);
            float f1 = player1.Health / (float)player1.maxHealth, f2 = player2.Health / (float)player2.maxHealth;
            Bar(new Rect(pad, top, barW, barH), f1, true, f1 < 0.3f ? low : ok);
            Bar(new Rect(W - pad - barW, top, barW, barH), f2, false, f2 < 0.3f ? low : ok);

            string n2 = player2.displayName + (mode == Mode.VsCpu ? " · CPU " + Levels[cpuLevel] : mode == Mode.Training ? " · DUMMY" : " · 2P");
            Shadowed(new Rect(pad, top + barH + 6 * s, barW, 40 * s), player1.displayName + (mode == Mode.TwoPlayers ? " · 1P" : ""), nameStyle, new Color(0.3f, 0.75f, 1f));
            nameStyle.alignment = TextAnchor.UpperRight;
            Shadowed(new Rect(W - pad - barW, top + barH + 6 * s, barW, 40 * s), n2, nameStyle, new Color(1f, 0.35f, 0.4f));
            nameStyle.alignment = TextAnchor.UpperLeft;

            // round wins
            for (int i = 0; i < roundsToWin; i++)
            {
                GUI.color = i < wins1 ? ok : new Color(0.2f, 0.2f, 0.25f);
                GUI.DrawTexture(new Rect(pad + barW - (i + 1) * 26 * s, top + barH + 12 * s, 18 * s, 18 * s), white);
                GUI.color = i < wins2 ? ok : new Color(0.2f, 0.2f, 0.25f);
                GUI.DrawTexture(new Rect(W - pad - barW + i * 26 * s + 8 * s, top + barH + 12 * s, 18 * s, 18 * s), white);
            }
            GUI.color = Color.white;

            Shadowed(new Rect(W / 2 - 80 * s, top - 18 * s, 160 * s, 80 * s), mode == Mode.Training ? "∞" : Mathf.CeilToInt(timer).ToString(), timerStyle, Color.white);

            // combos and counter hits
            float now = Time.unscaledTime;
            if (player1.Combo >= 2 && now < player1.ComboShownUntil) Shadowed(new Rect(pad, 160 * s, 300 * s, 60 * s), player1.Combo + " HITS", small, ok);
            if (player2.Combo >= 2 && now < player2.ComboShownUntil) Shadowed(new Rect(W - pad - 300 * s, 160 * s, 300 * s, 60 * s), player2.Combo + " HITS", small, ok);
            if (now < counterUntil1) Shadowed(new Rect(pad, 210 * s, 300 * s, 60 * s), "COUNTER HIT", small, low);
            if (now < counterUntil2) Shadowed(new Rect(W - pad - 300 * s, 210 * s, 300 * s, 60 * s), "COUNTER HIT", small, low);

            if (now < announceUntil)
            {
                Shadowed(new Rect(0, Screen.height * 0.32f, W, 160 * s), announce, big, Color.white);
                if (!string.IsNullOrEmpty(subAnnounce)) Shadowed(new Rect(0, Screen.height * 0.32f + 150 * s, W, 50 * s), subAnnounce, small, ok);
            }
            if (paused) Shadowed(new Rect(0, Screen.height * 0.4f, W, 160 * s), "PAUSED", big, Color.white);

            if (showHelp)
            {
                string controls = mode == Mode.TwoPlayers
                    ? "1P: WASD move · F G punch · C V kick      2P: arrows move · K L (Num4 Num5) punch · , . (Num1 Num2) kick"
                    : "WASD move/jump/crouch · U I punch · J K kick · T taunt";
                string text = controls +
                    "\nHold BACK to block high/mid · DOWN+BACK to block low · Forward+U hook · Down+Fwd+I uppercut · Down+J low kick · Down+K sweep · U,I,K string" +
                    "\nF1 VS CPU · F2 2 Players · F3 Training · F5 CPU difficulty · Esc pause · H hide this";
                GUI.color = new Color(0, 0, 0, 0.55f);
                GUI.DrawTexture(new Rect(pad - 10 * s, Screen.height - 110 * s, W - 2 * pad + 20 * s, 95 * s), white);
                GUI.color = Color.white;
                GUI.Label(new Rect(pad, Screen.height - 105 * s, W - 2 * pad, 90 * s), text, help);
            }
        }
    }
}
