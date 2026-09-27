using UnityEngine;

namespace CandyCruisers
{
    [RequireComponent(typeof(EnemyGrid))]
    public sealed class EnemyGridMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 0.3f;
        [SerializeField] private bool useGreenDashes;
        public bool UseGreenDashes { get => useGreenDashes; set => useGreenDashes = value; }
        private Vector3 origin;
        private bool initialized;
        private EnemyGrid grid;
        private int resetVersion;
        public int Direction { get; private set; } = 1;
        public float CurrentSpeed => speed * CombatBalance.FleetSpeedMultiplier(GetComponent<GameSession>()?.Progress.Level ?? 1)
            * (useGreenDashes ? 1 : .1f * GetComponent<EnemyGrid>().Model.ColorCount(EnemyColor.Green));
        public event System.Action SweepEnded;

        private void Awake() => Initialize();
        private void Initialize()
        {
            if (initialized) return;
            grid = GetComponent<EnemyGrid>();
            origin = transform.localPosition;
            initialized = true;
        }

        public void ResetSweep()
        {
            Initialize();
            Direction = 1;
            transform.localPosition = origin;
            resetVersion++;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(double seconds)
        {
            Initialize();
            if (!enabled || speed <= 0 || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            int version = resetVersion;
            // Spend time up to each contact so new Greens affect the remaining frame.
            while (seconds > 0 && enabled && version == resetVersion)
            {
                float currentSpeed = CurrentSpeed;
                if (currentSpeed <= 0 || !grid.OccupiedHorizontalBounds(out float left, out float right) ||
                    right - left >= 2f * PlayerMovement.HalfWidth) return;
                double distance = System.Math.Max(0, Direction > 0 ? PlayerMovement.HalfWidth - right : left + PlayerMovement.HalfWidth);
                double untilContact = distance / currentSpeed;
                if (seconds < untilContact)
                {
                    AdvanceDistance(seconds * currentSpeed);
                    return;
                }
                AdvanceDistance(distance);
                seconds -= untilContact;
                if (distance == 0) return;
            }
        }

        public void AdvanceDistance(double remaining)
        {
            Initialize();
            if (!enabled || remaining < 0 || double.IsNaN(remaining) || double.IsInfinity(remaining)) return;
            if (!useGreenDashes && grid.Model.ColorCount(EnemyColor.Green) == 0) return;
            int version = resetVersion;
            while (remaining >= 0 && enabled && version == resetVersion)
            {
                if (!grid.OccupiedHorizontalBounds(out float left, out float right)) return;
                if (right - left >= 2f * PlayerMovement.HalfWidth) return;
                double gap = Direction > 0 ? PlayerMovement.HalfWidth - right : left + PlayerMovement.HalfWidth;
                double distance = System.Math.Max(0, gap);
                if (remaining + 0.000001 < distance)
                {
                    Translate((float)(Direction * remaining));
                    return;
                }
                // Stop exactly at contact before notifying the descent/spawn listener.
                if (!Translate((float)(Direction * distance))) return;
                remaining = System.Math.Max(0, remaining - distance);
                Direction = -Direction;
                SweepEnded?.Invoke();
                if (remaining <= 0) return;
                // A callback may spawn, reindex, end the run, or reset the fleet.
                // Re-read occupancy before spending the frame's remaining movement.
                if (!useGreenDashes && grid.Model.ColorCount(EnemyColor.Green) == 0) return;
            }
        }

        private bool Translate(float distance)
        {
            var session = GetComponent<GameSession>();
            float fraction = session != null ? session.ContactFraction(distance) : 1;
            transform.position += Vector3.right * (distance * fraction);
            if (session != null) session.FinishContactMove(fraction);
            return enabled;
        }
    }
}
