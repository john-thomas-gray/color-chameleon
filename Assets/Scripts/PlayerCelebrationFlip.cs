using UnityEngine;

namespace CandyCruisers
{
    [DefaultExecutionOrder(200)]
    public sealed class PlayerCelebrationFlip : MonoBehaviour
    {
        private Transform pivot;
        private float elapsed, duration;
        private float rotationSign = -1;
        public bool Active => duration > 0 && elapsed < duration;

        public static bool TryDirection(float bodyLeft, float bodyRight, float barLeft, float barRight,
            float movement, out bool clockwise)
        {
            clockwise = true;
            float width = bodyRight - bodyLeft;
            float left = Mathf.Max(bodyLeft, barLeft), right = Mathf.Min(bodyRight, barRight);
            float overlap = right - left;
            if (width <= 0 || overlap <= 0 || overlap + width * .000001f < width * .1f) return false;
            // Positive rotation lifts the right side; broad support instead follows travel.
            clockwise = overlap > width * .5f && movement != 0
                ? movement > 0 : (left + right) <= (bodyLeft + bodyRight);
            return true;
        }

        public void Begin(float seconds, bool clockwise = true)
        {
            CharacterVisuals.Ensure(gameObject).ResetJumpPose();
            if (pivot == null)
            {
                var art = CharacterVisuals.Ensure(gameObject);
                if (art.Root == null || art.Root == transform) return;
                pivot = new GameObject("Celebration trampoline").transform;
                pivot.SetParent(art.Root.parent, false);
                art.Root.SetParent(pivot, false);
            }
            elapsed = 0;
            rotationSign = clockwise ? -1 : 1;
            duration = Mathf.Max(.001f, seconds);
            Present();
        }

        private void LateUpdate()
        {
            var player = GetComponent<PlayerMovement>();
            if (player != null && !player.Alive) { ResetPose(); return; }
            Tick(Time.deltaTime);
        }

        public void Tick(float seconds)
        {
            if (!Active) return;
            float next = elapsed + Mathf.Max(0, seconds);
            float overshoot = Mathf.Max(0, next - duration);
            bool landed = next >= duration;
            elapsed = Mathf.Min(duration, next);
            Present();
            if (landed) CharacterVisuals.Ensure(gameObject).BeginLanding(overshoot);
        }

        private void Present()
        {
            if (pivot == null) return;
            float t = Mathf.Clamp01(elapsed / duration);
            pivot.localPosition = Vector3.up * (.8f * 4 * t * (1 - t));
            pivot.localRotation = Quaternion.Euler(0, 0, rotationSign * 360 * Mathf.SmoothStep(0, 1, t));
            if (t >= 1) ResetPose();
        }

        private void ResetPose()
        {
            duration = 0;
            if (pivot == null) return;
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
        }

        private void OnDisable() => ResetPose();
    }
}
