using UnityEngine;

namespace LookismFighter
{
    /// <summary>Simple CPU player. Level 0 = easy, 1 = normal, 2 = hard.</summary>
    public class CpuOpponent : IInputSource
    {
        readonly float blockChance, reaction, aggression, lowRead;
        float think, holdTime, blockTime, chainAt = -1f;
        bool holdFwd, holdBack, holdDown, blockLow;
        string chainButton;
        MoveData seenMove;

        public CpuOpponent(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            blockChance = new[] { 0.2f, 0.45f, 0.75f }[level];
            reaction    = new[] { 0.45f, 0.28f, 0.16f }[level];
            aggression  = new[] { 0.4f, 0.55f, 0.7f }[level];
            lowRead     = new[] { 0.15f, 0.4f, 0.75f }[level];
        }

        static void Press(ref FighterInput i, string b)
        {
            if (b == "1") i.B1 = true; else if (b == "2") i.B2 = true; else if (b == "3") i.B3 = true; else if (b == "4") i.B4 = true;
        }

        public FighterInput Read(Fighter me)
        {
            var i = new FighterInput();
            var o = me.opponent;
            if (o == null) return i;
            float dt = Time.deltaTime;
            float dist = Mathf.Abs(o.X - me.X);
            bool fwdIsRight = me.Facing > 0;

            // 1) react to the opponent's attack by blocking
            var om = o.CurrentMove;
            if (om != null && om != seenMove)
            {
                seenMove = om;
                if (dist < om.Range + 0.4f && Random.value < blockChance)
                {
                    blockTime = o.CurrentMoveDuration;
                    blockLow = om.Level == HitLevel.Low ? Random.value < lowRead : (om.Level == HitLevel.High && Random.value < lowRead * 0.4f);
                }
            }
            if (om == null) seenMove = null;
            if (blockTime > 0f)
            {
                blockTime -= dt;
                if (fwdIsRight) i.Left = true; else i.Right = true;
                i.Down = blockLow;
                return i;
            }

            // 2) chained follow-ups (1,2 / 1,2,4)
            if (chainButton != null && me.State == FState.Attack && me.AttackTime >= chainAt) { Press(ref i, chainButton); chainButton = null; }
            if (me.State != FState.Attack && chainAt >= 0f && me.AttackTime > 1f) chainButton = null;

            // 3) decide something new every so often
            think -= dt;
            if (think <= 0f && me.IsActionable)
            {
                think = reaction * Random.Range(0.6f, 1.4f);
                holdFwd = holdBack = holdDown = false; holdTime = 0f;
                float r = Random.value;
                bool punish = o.State == FState.GetUp || (o.State == FState.Attack && o.CurrentMoveDuration > 0 && o.AttackTime > o.CurrentMoveDuration * 0.65f);

                if (punish && dist < 1.65f && Random.value < blockChance + 0.2f)
                {
                    if (Random.value < 0.5f) { i.Down = true; if (fwdIsRight) i.Right = true; else i.Left = true; i.B2 = true; } // uppercut
                    else { i.B1 = true; chainButton = "2"; chainAt = 0.25f; }
                }
                else if (dist > 2.75f) { holdFwd = true; holdTime = Random.Range(0.3f, 0.7f); }
                else if (dist > 1.8f)
                {
                    if (r < 0.7f) { holdFwd = true; holdTime = Random.Range(0.15f, 0.4f); }
                    else if (r < 0.85f) { holdBack = true; holdTime = 0.3f; }
                }
                else if (r < aggression)
                {
                    float pick = Random.value;
                    if (pick < 0.25f) { i.B1 = true; if (Random.value < 0.6f) { chainButton = "2"; chainAt = 0.2f; } }
                    else if (pick < 0.4f) i.B2 = true;
                    else if (pick < 0.55f) i.B3 = true;
                    else if (pick < 0.68f) i.B4 = true;
                    else if (pick < 0.8f) { i.Down = true; i.B3 = true; }
                    else if (pick < 0.9f) { i.Down = true; i.B4 = true; }
                    else { i.Down = true; if (fwdIsRight) i.Right = true; else i.Left = true; i.B2 = true; }
                }
                else if (r < aggression + 0.25f) { holdBack = true; holdTime = Random.Range(0.25f, 0.6f); }
                else { holdDown = true; holdTime = 0.3f; }
            }

            if (holdTime > 0f)
            {
                holdTime -= dt;
                if (holdFwd) { if (fwdIsRight) i.Right = true; else i.Left = true; }
                if (holdBack) { if (fwdIsRight) i.Left = true; else i.Right = true; }
                if (holdDown) i.Down = true;
            }
            return i;
        }
    }
}
