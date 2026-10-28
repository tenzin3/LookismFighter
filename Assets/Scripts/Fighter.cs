using System.Collections.Generic;
using UnityEngine;

namespace LookismFighter
{
    public enum FState { Idle, WalkForward, WalkBack, Crouch, Block, CrouchBlock, Jump, Attack, HitStun, BlockStun, Down, GetUp, KO, Victory, Taunt }

    /// <summary>
    /// One fighter: movement on the X axis, facing the opponent, attacks, blocking, getting hit.
    /// Animations are played by name from FighterAnimator (states are created by Tools > Lookism Fighter > Setup Fight Scene).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class Fighter : MonoBehaviour
    {
        [Header("Identity")]
        public int playerIndex;
        public string displayName = "GUN";
        public Fighter opponent;

        [Header("Stats")]
        public int maxHealth = 120;
        public float walkForwardSpeed = 1.5f;
        public float walkBackSpeed = 1.2f;
        public float jumpDrift = 1.6f;
        public float minDistance = 0.65f;
        public float stageHalfWidth = 7f;

        [Header("Filled in by the setup tool")]
        public string[] stateNames = new string[0];
        public float[] stateLengths = new float[0];

        // ---- public read-only state (used by CPU, HUD, camera) ----
        public IInputSource input;
        public bool controlsEnabled;
        public int Health { get; private set; }
        public FState State { get; private set; }
        public MoveData CurrentMove => state == FState.Attack ? move : null;
        public float AttackTime => t;
        public float CurrentMoveDuration => move != null ? Dur(move.State, move.Speed) : 0f;
        public float X => transform.position.x;
        public int Facing => facing;
        public int Combo { get; private set; }
        public float ComboShownUntil { get; private set; }
        public bool IsActionable => state == FState.Idle || state == FState.WalkForward || state == FState.WalkBack || state == FState.Crouch || state == FState.Block || state == FState.CrouchBlock;
        public bool IsCrouching => state == FState.Crouch || state == FState.CrouchBlock || (state == FState.BlockStun && crouchBlockStun) || (state == FState.Attack && move != null && move.Crouching);

        FState state { get => State; set => State = value; }
        Animator anim;
        readonly Dictionary<string, float> lengths = new Dictionary<string, float>();
        readonly List<(string button, float time)> buffer = new List<(string, float)>();
        MoveData move;
        bool moveConnected;
        float t, stun, downTimer, vx;
        int facing = 1;
        bool backHeld, crouchBlockStun;
        string playing;
        const float BufferWindow = 0.12f;

        void Awake()
        {
            anim = GetComponent<Animator>();
            anim.applyRootMotion = false;
            for (int i = 0; i < stateNames.Length && i < stateLengths.Length; i++) lengths[stateNames[i]] = stateLengths[i];
            Health = maxHealth;
        }

        // ------------------------------------------------------------------ helpers
        float Len(string s) => lengths.TryGetValue(s, out var l) ? l : 1f;
        float Dur(string s, float speed) => Len(s) / Mathf.Max(0.05f, speed);
        float Dur(string s) => Dur(s, MoveTable.SpeedOf(s));
        float DistanceToOpponent => opponent ? Mathf.Abs(opponent.X - X) : 99f;

        void Play(string s, bool restart = false, float fade = 0.08f)
        {
            if (!restart && playing == s) return;
            playing = s;
            anim.CrossFadeInFixedTime(s, fade, 0, 0f);
        }

        void SetState(FState s, string animState, bool restart = false)
        {
            if (state != s) t = 0f;
            state = s;
            Play(animState, restart);
        }

        bool Buffered(string b) { foreach (var p in buffer) if (p.button == b) return true; return false; }

        public void ResetForRound(float x)
        {
            Health = maxHealth; vx = 0; t = 0; stun = 0; move = null; Combo = 0; buffer.Clear();
            transform.position = new Vector3(x, 0, 0);
            facing = opponent && opponent.transform.position.x < x ? -1 : 1;
            ApplyFacing();
            state = FState.Idle; playing = null; Play("fighting_idle", true, 0f);
        }

        public void Celebrate() { state = FState.Victory; vx = 0; Play("victory", true); }

        void ApplyFacing() => transform.rotation = Quaternion.Euler(0f, facing > 0 ? 90f : -90f, 0f);

        // ------------------------------------------------------------------ main loop
        void Update()
        {
            // Read input every frame (even during hit-stop) so button presses are never lost.
            FighterInput inp = controlsEnabled && input != null ? input.Read(this) : default;
            float now = Time.unscaledTime;
            if (inp.B1) buffer.Add(("1", now));
            if (inp.B2) buffer.Add(("2", now));
            if (inp.B3) buffer.Add(("3", now));
            if (inp.B4) buffer.Add(("4", now));
            buffer.RemoveAll(p => now - p.time > BufferWindow);

            if (RoundManager.Frozen) { anim.speed = 0f; return; }
            anim.speed = 1f;

            float dt = Time.deltaTime;
            t += dt;
            bool fwd = facing > 0 ? inp.Right : inp.Left;
            bool back = facing > 0 ? inp.Left : inp.Right;
            backHeld = back;

            switch (state)
            {
                case FState.Idle: case FState.WalkForward: case FState.WalkBack:
                case FState.Crouch: case FState.Block: case FState.CrouchBlock:
                    Neutral(inp, fwd, back);
                    break;

                case FState.Jump:
                    if (t >= Dur("jump")) { state = FState.Idle; vx = 0; Play("fighting_idle"); }
                    break;

                case FState.Attack:
                {
                    float n = t / Dur(move.State, move.Speed);
                    if (!moveConnected && n >= move.ActiveStart && n <= move.ActiveEnd && DistanceToOpponent <= move.Range)
                    {
                        moveConnected = opponent.ReceiveHit(this, move);
                    }
                    if (move.CancelInto != null && n >= move.CancelFrom && Buffered(move.CancelButton))
                    {
                        StartMove(move.CancelInto);
                        break;
                    }
                    if (n >= 1f) { move = null; if (inp.Down) SetState(FState.Crouch, "crouch_idle"); else SetState(FState.Idle, "fighting_idle"); }
                    break;
                }

                case FState.HitStun: case FState.BlockStun:
                    stun -= dt;
                    vx = Mathf.MoveTowards(vx, 0f, 10f * dt);
                    if (stun <= 0f) { if (inp.Down) SetState(FState.Crouch, "crouch_idle"); else SetState(FState.Idle, "fighting_idle"); }
                    break;

                case FState.Down:
                    vx = Mathf.MoveTowards(vx, 0f, 6f * dt);
                    downTimer -= dt;
                    if (downTimer <= 0f) { SetState(FState.GetUp, "getting_up", true); vx = 0; }
                    break;

                case FState.GetUp:
                    if (t >= Dur("getting_up")) SetState(FState.Idle, "fighting_idle");
                    break;

                case FState.Taunt:
                    if (t >= Dur("taunt")) SetState(FState.Idle, "fighting_idle");
                    break;

                case FState.KO: case FState.Victory:
                    vx = Mathf.MoveTowards(vx, 0f, 6f * dt);
                    break;
            }

            // movement on the X axis only (2.5D)
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x + vx * dt, -stageHalfWidth, stageHalfWidth);
            p.y = 0f; p.z = 0f;
            // keep fighters from walking through each other
            if (opponent && state != FState.Down && state != FState.KO && opponent.State != FState.Down && opponent.State != FState.KO)
            {
                float d = p.x - opponent.X;
                if (Mathf.Abs(d) < minDistance) p.x = Mathf.Clamp(opponent.X + Mathf.Sign(d == 0 ? -facing : d) * minDistance, -stageHalfWidth, stageHalfWidth);
            }
            transform.position = p;
        }

        void Neutral(FighterInput inp, bool fwd, bool back)
        {
            if (opponent) { facing = opponent.X >= X ? 1 : -1; ApplyFacing(); }
            if (TryAttack(inp.Down, fwd)) return;
            if (inp.Taunt) { SetState(FState.Taunt, "taunt", true); vx = 0; return; }
            if (inp.Up) { SetState(FState.Jump, "jump", true); vx = (fwd ? jumpDrift : back ? -jumpDrift : 0f) * facing; return; }

            bool threat = opponent && (opponent.State == FState.Attack || opponent.State == FState.Jump) && DistanceToOpponent < 2.6f;
            if (inp.Down) { vx = 0; if (back && threat) SetState(FState.CrouchBlock, "block_low"); else SetState(FState.Crouch, "crouch_idle"); }
            else if (back && threat) { vx = 0; SetState(FState.Block, "block_high"); }
            else if (fwd) { vx = walkForwardSpeed * facing; SetState(FState.WalkForward, "step_forward"); }
            else if (back) { vx = -walkBackSpeed * facing; SetState(FState.WalkBack, "step_backward"); }
            else { vx = 0; SetState(FState.Idle, "fighting_idle"); }
        }

        bool TryAttack(bool down, bool fwd)
        {
            string m = null;
            if (Buffered("2") && down && fwd) m = "uppercut";
            else if (Buffered("4") && down) m = "leg_sweep";
            else if (Buffered("3") && down) m = "low_kick";
            else if (Buffered("1") && fwd) m = "hook";
            else if (Buffered("1")) m = "jab";
            else if (Buffered("2")) m = "cross";
            else if (Buffered("3")) m = "side_kick";
            else if (Buffered("4")) m = "roundhouse_kick";
            if (m == null) return false;
            StartMove(m);
            return true;
        }

        void StartMove(string name)
        {
            if (!MoveTable.Moves.TryGetValue(name, out var m)) return;
            move = m; moveConnected = false; buffer.Clear(); vx = 0;
            state = FState.Attack; t = 0f;
            Play(m.State, true, 0.05f);
        }

        // ------------------------------------------------------------------ getting hit
        /// <summary>Returns true if the attack connected (hit or blocked), false if it whiffed.</summary>
        public bool ReceiveHit(Fighter attacker, MoveData m)
        {
            if (state == FState.Down || state == FState.GetUp || state == FState.KO || state == FState.Victory) return false;
            if (m.Level == HitLevel.High && IsCrouching) return false;                       // ducked under it
            if (m.Level == HitLevel.Low && state == FState.Jump) { float n = t / Dur("jump"); if (n > 0.2f && n < 0.8f) return false; } // jumped over it

            bool standingBlock = state == FState.Block || state == FState.WalkBack || (state == FState.BlockStun && !crouchBlockStun) || (state == FState.Idle && backHeld);
            bool crouchingBlock = state == FState.CrouchBlock || (state == FState.Crouch && backHeld) || (state == FState.BlockStun && crouchBlockStun);
            bool blocked = (m.Level != HitLevel.Low && standingBlock) || (m.Level == HitLevel.Low && crouchingBlock);

            if (blocked)
            {
                crouchBlockStun = crouchingBlock;
                state = FState.BlockStun; t = 0f; stun = m.BlockStun;
                vx = -facing * m.Push * 0.8f;
                Play(crouchingBlock ? "block_low" : "block_high", true, 0.04f);
                RoundManager.Instance?.OnBlock(attacker, this);
                return true;
            }

            bool counter = state == FState.Attack && move != null && t / Dur(move.State, move.Speed) < move.ActiveStart;
            bool comboing = state == FState.HitStun;
            int dmg = Mathf.RoundToInt(m.Damage * (counter ? 1.25f : 1f));
            Health = Mathf.Max(0, Health - dmg);
            attacker.Combo = comboing ? attacker.Combo + 1 : 1;
            attacker.ComboShownUntil = Time.unscaledTime + 1.2f;
            move = null;

            if (Health <= 0)
            {
                state = FState.KO; t = 0f; vx = -facing * 3f;
                Play("knocked_down", true, 0.05f);
            }
            else if (m.Knockdown)
            {
                state = FState.Down; t = 0f; vx = -facing * 2.5f;
                downTimer = Dur("knocked_down") + 0.35f;
                Play("knocked_down", true, 0.05f);
            }
            else
            {
                state = FState.HitStun; t = 0f;
                stun = m.HitStun + (counter ? 0.15f : 0f);
                vx = -facing * m.Push;
                Play(m.Level == HitLevel.High ? "head_hit" : "stomach_hit", true, 0.04f);
            }
            RoundManager.Instance?.OnHit(attacker, this, dmg, counter, m.Heavy || m.Knockdown || counter);
            return true;
        }
    }
}
