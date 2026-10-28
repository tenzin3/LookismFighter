using UnityEngine;

namespace LookismFighter
{
    /// <summary>Side-view camera that stays between both fighters and zooms out as they separate.</summary>
    public class FightCamera : MonoBehaviour
    {
        public static FightCamera Instance { get; private set; }

        public Fighter fighterA, fighterB;
        public float height = 1.45f;
        public float lookHeight = 1.05f;
        public float minDistance = 4.2f;
        public float maxDistance = 9f;
        public float distancePerMeter = 0.85f;
        public float smoothing = 5f;
        public float stageHalfWidth = 7f;

        float shakeTime, shakeAmount, x, dist = 5f;

        void Awake() { Instance = this; }

        public void Shake(float amount) { shakeTime = 0.25f; shakeAmount = Mathf.Max(shakeAmount, amount); }

        void LateUpdate()
        {
            if (!fighterA || !fighterB) return;
            float dt = Time.unscaledDeltaTime;
            float mid = (fighterA.X + fighterB.X) * 0.5f;
            float sep = Mathf.Abs(fighterA.X - fighterB.X);
            float targetDist = Mathf.Clamp(3.2f + sep * distancePerMeter, minDistance, maxDistance);
            float k = 1f - Mathf.Exp(-smoothing * dt);
            x = Mathf.Lerp(x, Mathf.Clamp(mid, -stageHalfWidth + 2f, stageHalfWidth - 2f), k);
            dist = Mathf.Lerp(dist, targetDist, k);

            Vector3 shake = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                shake = Random.insideUnitSphere * shakeAmount * (shakeTime / 0.25f);
            }
            else shakeAmount = 0f;

            transform.position = new Vector3(x, height, -dist) + shake;
            transform.LookAt(new Vector3(x, lookHeight, 0f) + shake * 0.5f);
        }
    }
}
