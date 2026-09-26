using UnityEngine;

namespace CandyCruisers
{
    public sealed class EnemyMissile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 5f;
        private PlayerMovement target;
        private float age;
        public bool Suspended { get; set; }
        public bool Finished { get; private set; }
        public void SetTarget(PlayerMovement player) => target = player;
        private void Update() => Tick(Time.deltaTime);

        public void Tick(float seconds)
        {
            if (Suspended || Finished || seconds <= 0) return;
            Vector3 before = transform.position;
            Vector3 after = before + Vector3.down * (speed * seconds);
            transform.position = after;
            age += seconds;
            if (target != null && target.Alive)
            {
                var bounds = target.HitBounds;
                if (before.x + 0.07f >= bounds.min.x && before.x - 0.07f <= bounds.max.x &&
                    before.y + 0.16f >= bounds.min.y && after.y - 0.16f <= bounds.max.y)
                {
                    target.Hit();
                    Finish();
                    return;
                }
            }
            if (after.y < -6.5f || age >= 6f) Finish();
        }

        private void Finish()
        {
            Finished = true;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
        }
    }
}
