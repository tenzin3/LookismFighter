using UnityEngine;

namespace LookismFighter
{
    /// <summary>
    /// Tekken-style camera. It mostly stays still and pulls back to keep both fighters in view;
    /// it only pans when the fighters drift together across the stage. All movement is smoothed, so it never jumps.
    /// Settings are in FightConfig (MoveTable.cs).
    /// </summary>
    public class FightCamera : MonoBehaviour
    {
        public static FightCamera Instance { get; private set; }

        public Fighter fighterA, fighterB;
        public float stageHalfWidth = 7f;

        Camera cam;
        float x, dist, panVel, zoomVel, shakeTime, shakeAmount;
        bool initialised;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            if (cam) cam.fieldOfView = FightConfig.CamFieldOfView;
        }

        public void Shake(float amount) { shakeTime = 0.25f; shakeAmount = Mathf.Max(shakeAmount, amount); }

        /// <summary>Distance the camera needs so that a half-width of "extent" metres fits on screen.</summary>
        float DistanceToFit(float extent)
        {
            float vFov = (cam ? cam.fieldOfView : FightConfig.CamFieldOfView) * Mathf.Deg2Rad;
            float aspect = cam ? cam.aspect : 16f / 9f;
            float tanHalfH = Mathf.Tan(vFov * 0.5f) * aspect;
            return extent / Mathf.Max(0.05f, tanHalfH);
        }

        void LateUpdate()
        {
            if (!fighterA || !fighterB) return;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            float a = fighterA.X, b = fighterB.X;
            float mid = (a + b) * 0.5f;

            if (!initialised) { x = mid; dist = FightConfig.CamMinDistance; initialised = true; }

            // Pan only when the fighters' midpoint leaves a dead zone around the camera.
            float targetX = Mathf.Clamp(x, mid - FightConfig.CamPanDeadZone, mid + FightConfig.CamPanDeadZone);
            float edge = stageHalfWidth + FightConfig.CamEdgeMargin;
            targetX = Mathf.Clamp(targetX, -edge + 1f, edge - 1f);

            // Pull back far enough that both fighters (plus a margin) stay on screen.
            float extent = Mathf.Max(Mathf.Abs(a - targetX), Mathf.Abs(b - targetX)) + FightConfig.CamEdgeMargin;
            float targetDist = Mathf.Clamp(DistanceToFit(extent), FightConfig.CamMinDistance, FightConfig.CamMaxDistance);

            x = Mathf.SmoothDamp(x, targetX, ref panVel, FightConfig.CamPanTime, Mathf.Infinity, dt);
            float zoomTime = targetDist > dist ? FightConfig.CamZoomOutTime : FightConfig.CamZoomInTime;
            dist = Mathf.SmoothDamp(dist, targetDist, ref zoomVel, zoomTime, Mathf.Infinity, dt);

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
