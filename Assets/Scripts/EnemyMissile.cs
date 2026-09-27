using UnityEngine;

namespace CandyCruisers
{
    public sealed class EnemyMissile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 5f;
        private PlayerMovement target;
        private float age;
        private Vector3 direction = Vector3.down;
        [SerializeField, Min(0.1f)] private float homingSpeed = 3.5f;
        // Original: 0.3 degrees per frame, expressed at a 60-frame-per-second baseline.
        [SerializeField, Min(1)] private float turnDegreesPerSecond = 18f;
        [SerializeField, Range(0, 80)] private float maxHomingAngle = 50f;
        [SerializeField, Min(.5f)] private float flashPeriod = 1.5f;
        private SpriteRenderer visual;
        private Color baseColor;
        private float headingDegrees;
        public bool Homing { get; private set; }
        public bool Aimed { get; private set; }
        public bool Suspended { get; set; }
        public bool Finished { get; private set; }
        public void SetTarget(PlayerMovement player, bool homing = false)
        { target = player; Homing = homing; Aimed = false; RefreshVisual(); }
        public void LaunchAimed(PlayerMovement player, Vector3 heading)
        {
            SetTarget(player);
            Aimed = true;
            heading.z = 0;
            direction = heading.sqrMagnitude > .000001f ? heading.normalized : Vector3.down;
            transform.rotation = Quaternion.Euler(0, 0, Vector3.SignedAngle(Vector3.down, direction, Vector3.forward));
            RefreshVisual();
        }
        private void Update() => Tick(Time.deltaTime);

        public void Tick(float seconds)
        {
            if (Suspended || Finished || seconds <= 0) return;
            if ((Homing || Aimed) && OutsideHomingField(transform.position)) { Finish(); return; }
            // Bounded steering and swept collision remain stable even during long frames.
            float lifetime = Homing || Aimed ? 5f : 6f;
            float remaining = Mathf.Min(seconds, lifetime - age);
            while (remaining > .000001f && !Finished)
            {
                float step = Mathf.Min(remaining, 1f / 120);
                Step(step);
                remaining -= step;
            }
            if (!Finished && age >= lifetime - .0001f) Finish();
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (visual == null)
            {
                visual = GetComponent<SpriteRenderer>();
                if (visual == null) return;
                baseColor = visual.color;
            }
            float pulse = .5f - .5f * Mathf.Cos(age * Mathf.PI * 2 / Mathf.Max(.5f, flashPeriod));
            var bright = new Color(1, 1, 1, baseColor.a);
            visual.color = Homing || Aimed ? Color.Lerp(baseColor, bright, pulse * .85f) : baseColor;
        }

        private void Step(float seconds)
        {
            Vector3 before = transform.position;
            if (Homing && target != null && target.Alive)
            {
                float offset = target.transform.position.x - before.x;
                float turn = offset > 0 ? 1 : offset < 0 ? -1 : 0;
                float limit = Mathf.Clamp(maxHomingAngle, 0, 80);
                headingDegrees = Mathf.Clamp(headingDegrees + turn * turnDegreesPerSecond * seconds, -limit, limit);
                direction = Quaternion.Euler(0, 0, headingDegrees) * Vector3.down;
                transform.rotation = Quaternion.Euler(0, 0, headingDegrees);
            }
            float currentSpeed = Homing || Aimed ? homingSpeed : speed;
            Vector3 after = before + direction * (currentSpeed * seconds);
            transform.position = after;
            age += seconds;
            if (target != null && target.Alive)
            {
                var bounds = target.HitBounds;
                bounds.Expand(new Vector3(.14f, .32f, 1));
                if (bounds.Contains(before) || bounds.IntersectRay(new Ray(before, direction), out float distance) && distance <= currentSpeed * seconds)
                {
                    target.Hit();
                    Finish();
                    return;
                }
            }
            if (Homing || Aimed ? OutsideHomingField(after) : after.y < -6.5f) Finish();
        }

        private static bool OutsideHomingField(Vector3 position) =>
            Mathf.Abs(position.x) > PlayerMovement.HalfWidth || Mathf.Abs(position.y) > 5.3f;

        private void Finish()
        {
            Finished = true;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
        }
    }
}
