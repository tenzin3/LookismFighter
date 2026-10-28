using UnityEngine;

namespace LookismFighter
{
    /// <summary>Side-view camera that stays between both fighters and zooms out as they separate.</summary>
    public class FightCamera : MonoBehaviour
    {
        public static FightCamera Instance { get; private set; }

        public Fighter fighterA, fighterB;
        public float stageHalfWidth = 7f;
        // camera height, distance, zoom and field of view are set in FightConfig (MoveTable.cs)

        float shakeTime, shakeAmount, x, dist = 6f;

        void Awake()
        {
            Instance = this;
            var cam = GetComponent<Camera>();
            if (cam) cam.fieldOfView = FightConfig.CamFieldOfView;
        }

        public void Shake(float amount) { shakeTime = 0.25f; shakeAmount = Mathf.Max(shakeAmount, amount); }

        void LateUpdate()
        {
            if (!fighterA || !fighterB) return;
            float dt = Time.unscaledDeltaTime;
            float mid = (fighterA.X + fighterB.X) * 0.5f;
            float sep = Mathf.Abs(fighterA.X - fighterB.X);
            float targetDist = Mathf.Clamp(FightConfig.CamBaseDistance + sep * FightConfig.CamDistancePerMeter, FightConfig.CamMinDistance, FightConfig.CamMaxDistance);
            float k = 1f - Mathf.Exp(-FightConfig.CamSmoothing * dt);
            x = Mathf.Lerp(x, Mathf.Clamp(mid, -stageHalfWidth + 2f, stageHalfWidth - 2f), k);
            dist = Mathf.Lerp(dist, targetDist, k);

            Vector3 shake = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                shake = Random.insideUnitSphere * shakeAmount * (shakeTime / 0.25f);
            }
            else shakeAmount = 0f;

            transform.position = new Vector3(x, FightConfig.CamHeight, -dist) + shake;
            transform.LookAt(new Vector3(x, FightConfig.CamLookHeight, 0f) + shake * 0.5f);
        }
    }
}
