using System.Collections.Generic;

namespace LookismFighter
{
    public enum HitLevel { High, Mid, Low }

    /// <summary>One attack. Timings are fractions (0..1) of the animation after its speed-up.</summary>
    public class MoveData
    {
        public string State;          // Animator state name = animation file name
        public float Speed = 1f;      // playback speed (higher = snappier)
        public float ActiveStart;     // when the hit can start landing (0..1 of the move)
        public float ActiveEnd;       // when it stops being able to land
        public float Range;           // reach, metres between fighter centres
        public HitLevel Level;        // high: duck under it, mid: block standing, low: block crouching
        public int Damage;
        public float HitStun;         // seconds the opponent can't act after being hit
        public float BlockStun;       // seconds the opponent can't act after blocking
        public float Push;            // knock-back speed, m/s
        public bool Knockdown;        // puts the opponent on the floor
        public bool Crouching;        // attacker is low during this move (highs whiff over it)
        public bool Heavy;            // bigger hit effects
        public string CancelButton;   // press this during the move to chain...
        public string CancelInto;     // ...into this move
        public float CancelFrom = 1f; // ...after this fraction of the move
    }


    /// <summary>Feel and camera settings. Change, save, press Play.</summary>
    public static class FightConfig
    {
        // fighters
        public const float PersonalSpace = 1.0f;     // closest the two fighters can stand (m)
        public const float WalkForward = 1.7f;       // m/s
        public const float WalkBack = 1.4f;          // m/s
        public const float Acceleration = 14f;       // how fast walking starts/stops (m/s²) - higher = snappier
        public const float TurnSpeed = 900f;         // degrees per second when turning to face the opponent
        public const float BodyTurnToCamera = 22f;   // degrees the fighters angle their chest toward the camera
        public const bool MirrorWhenFacingLeft = true; // mirror the fighter on the right side so both show their chest (like Tekken)
        public const float MoveBlend = 0.15f;        // seconds to blend between idle/walk/crouch animations
        public const float AttackBlend = 0.04f;      // seconds to blend into attacks (keep small = responsive)

        // camera
        public const float CamHeight = 1.75f;
        public const float CamLookHeight = 1.05f;
        public const float CamMinDistance = 5.2f;
        public const float CamMaxDistance = 10f;
        public const float CamBaseDistance = 3.4f;
        public const float CamDistancePerMeter = 0.9f;
        public const float CamFieldOfView = 32f;
        public const float CamSmoothing = 6f;
    }

    /// <summary>
    /// All tunable fight data lives here. Change numbers, save, and press Play.
    /// Buttons: 1 = left punch, 2 = right punch, 3 = left kick, 4 = right kick.
    /// </summary>
    public static class MoveTable
    {
        public static readonly Dictionary<string, MoveData> Moves = new Dictionary<string, MoveData>
        {
            // 1 : fast jab, chains into 2 (1,2)
            ["jab"] = new MoveData { State = "jab", Speed = 1.5f, ActiveStart = 0.22f, ActiveEnd = 0.45f, Range = 1.55f, Level = HitLevel.High,
                                     Damage = 5, HitStun = 0.32f, BlockStun = 0.18f, Push = 1.2f, CancelButton = "2", CancelInto = "cross", CancelFrom = 0.4f },
            // 2 : cross, chains into 4 (1,2,4)
            ["cross"] = new MoveData { State = "cross", Speed = 1.4f, ActiveStart = 0.28f, ActiveEnd = 0.5f, Range = 1.65f, Level = HitLevel.High,
                                       Damage = 8, HitStun = 0.4f, BlockStun = 0.2f, Push = 1.8f, CancelButton = "4", CancelInto = "roundhouse_kick", CancelFrom = 0.5f },
            // forward + 1 : hook
            ["hook"] = new MoveData { State = "hook", Speed = 1.3f, ActiveStart = 0.3f, ActiveEnd = 0.5f, Range = 1.50f, Level = HitLevel.High,
                                      Damage = 10, HitStun = 0.45f, BlockStun = 0.22f, Push = 2f, Heavy = true },
            // down-forward + 2 : uppercut, knocks down
            ["uppercut"] = new MoveData { State = "uppercut", Speed = 1.25f, ActiveStart = 0.3f, ActiveEnd = 0.5f, Range = 1.45f, Level = HitLevel.Mid,
                                          Damage = 14, HitStun = 0.6f, BlockStun = 0.3f, Push = 1.5f, Knockdown = true, Heavy = true },
            // 3 : side kick (mid)
            ["side_kick"] = new MoveData { State = "side_kick", Speed = 1.3f, ActiveStart = 0.3f, ActiveEnd = 0.5f, Range = 1.80f, Level = HitLevel.Mid,
                                           Damage = 11, HitStun = 0.45f, BlockStun = 0.25f, Push = 2.5f },
            // 4 : roundhouse (high)
            ["roundhouse_kick"] = new MoveData { State = "roundhouse_kick", Speed = 1.25f, ActiveStart = 0.35f, ActiveEnd = 0.55f, Range = 1.85f, Level = HitLevel.High,
                                                 Damage = 15, HitStun = 0.5f, BlockStun = 0.28f, Push = 3f, Heavy = true },
            // down + 3 : low kick
            ["low_kick"] = new MoveData { State = "low_kick", Speed = 1.3f, ActiveStart = 0.3f, ActiveEnd = 0.5f, Range = 1.70f, Level = HitLevel.Low,
                                          Damage = 8, HitStun = 0.35f, BlockStun = 0.2f, Push = 1.2f },
            // down + 4 : leg sweep, knocks down
            ["leg_sweep"] = new MoveData { State = "leg_sweep", Speed = 1.2f, ActiveStart = 0.3f, ActiveEnd = 0.55f, Range = 1.80f, Level = HitLevel.Low,
                                           Damage = 10, HitStun = 0.5f, BlockStun = 0.28f, Push = 1.5f, Knockdown = true, Crouching = true },
        };

        /// <summary>Every Animator state the game uses: (state/file name, playback speed).</summary>
        public static readonly (string state, float speed)[] AllStates =
        {
            ("fighting_idle", 1f), ("step_forward", 1.2f), ("step_backward", 1.2f), ("crouch_idle", 1f), ("jump", 1.2f),
            ("jab", 1.5f), ("cross", 1.4f), ("hook", 1.3f), ("uppercut", 1.25f),
            ("side_kick", 1.3f), ("roundhouse_kick", 1.25f), ("low_kick", 1.3f), ("leg_sweep", 1.2f),
            ("block_high", 1f), ("block_low", 1f),
            ("head_hit", 1.3f), ("stomach_hit", 1.3f), ("knocked_down", 1.2f), ("getting_up", 1.6f),
            ("victory", 1f), ("taunt", 1.2f),
        };

        public static float SpeedOf(string state)
        {
            foreach (var s in AllStates) if (s.state == state) return s.speed;
            return 1f;
        }
    }
}
