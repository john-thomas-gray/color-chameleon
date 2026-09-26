using UnityEngine;

namespace CandyCruisers
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TongueShot : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float extendSpeed = 14f;
        [SerializeField, Min(0.1f)] private float retractSpeed = 20f;
        private LineRenderer line;
        private float travel;
        private float maximumLength;
        private float normalStartWidth, normalEndWidth;
        private bool widthInitialized;
        public bool Active { get; private set; }
        public bool Retracting => Active && travel >= maximumLength;
        public float Length => Active ? (Retracting ? Mathf.Max(0, 2 * maximumLength - travel) : travel) : 0;
        public EnemyColor ShotColor { get; private set; }
        public bool IsMagic { get; private set; }

        private void Awake() { line = GetComponent<LineRenderer>(); line.enabled = false; }
        public bool TryFire(EnemyColor color, float length, bool magic = false)
        {
            if (Active || length <= 0) return false;
            if (line == null) line = GetComponent<LineRenderer>();
            if (!widthInitialized)
            {
                normalStartWidth = line.startWidth;
                normalEndWidth = line.endWidth;
                widthInitialized = true;
            }
            line.startWidth = normalStartWidth * (magic ? 2 : 1);
            line.endWidth = normalEndWidth * (magic ? 2 : 1);
            ShotColor = color;
            IsMagic = magic;
            maximumLength = length;
            travel = 0;
            Active = true;
            var plain = new Gradient();
            plain.SetKeys(new[] { new GradientColorKey(EnemyPalette.Get(color), 0), new GradientColorKey(EnemyPalette.Get(color), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            line.colorGradient = plain;
            if (magic)
            {
                var gradient = new Gradient();
                gradient.SetKeys(new[] {
                    new GradientColorKey(EnemyPalette.Get(EnemyColor.Red), 0),
                    new GradientColorKey(EnemyPalette.Get(EnemyColor.Yellow), .25f),
                    new GradientColorKey(EnemyPalette.Get(EnemyColor.Green), .5f),
                    new GradientColorKey(EnemyPalette.Get(EnemyColor.Blue), .75f),
                    new GradientColorKey(EnemyPalette.Get(EnemyColor.Purple), 1)
                }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
                line.colorGradient = gradient;
            }
            Draw();
            return true;
        }

        public void Tick(float seconds, EnemyGrid grid = null)
        {
            if (!Active || seconds <= 0) return;
            float extensionRate = extendSpeed * (IsMagic ? 2 : 1);
            float returnRate = retractSpeed * (IsMagic ? 2 : 1);
            if (grid != null && grid.Model.Count == 0 && !Retracting)
            {
                maximumLength = Mathf.Max(.0001f, Length);
                travel = maximumLength;
            }
            if (!Retracting)
            {
                float remaining = (maximumLength - travel) / extensionRate;
                float extending = Mathf.Min(seconds, remaining);
                float nextLength = travel + extending * extensionRate;
                if (IsMagic && grid != null)
                {
                    // Resolve every crossed enemy, even during a long frame. Each hit removes
                    // its same-color chain before the next nearest contact is queried.
                    bool clearedFleet = false;
                    while (grid.FindMatchingHit(transform.position, travel, nextLength,
                        ShotColor, .11f, out int magicId, out float magicHitLength, true))
                    {
                        grid.ResolveTongueHit(magicId, ShotColor, true);
                        if (!Active) return;
                        if (grid.Model.Count == 0)
                        {
                            seconds -= (magicHitLength - travel) / extensionRate;
                            maximumLength = Mathf.Max(.0001f, magicHitLength);
                            travel = maximumLength;
                            clearedFleet = true;
                            break;
                        }
                    }
                    if (!clearedFleet)
                    {
                        travel = nextLength;
                        seconds -= extending;
                    }
                }
                else if (grid != null && grid.FindMatchingHit(transform.position, travel, nextLength,
                    ShotColor, 0.055f, out int id, out float hitLength))
                {
                    seconds -= (hitLength - travel) / extensionRate;
                    grid.ResolveTongueHit(id, ShotColor);
                    if (!Active) return;
                    maximumLength = Mathf.Max(0.0001f, hitLength);
                    travel = maximumLength;
                }
                else
                {
                    travel = nextLength;
                    seconds -= extending;
                }
            }
            if (seconds > 0) travel += seconds * returnRate;
            if (travel >= 2 * maximumLength) Active = false;
            Draw();
        }

        public void Draw()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            line.enabled = Active;
            line.positionCount = IsMagic ? 17 : 2;
            for (int i = 0; i < line.positionCount; i++)
                line.SetPosition(i, Vector3.up * (Length * i / (line.positionCount - 1)));
        }
        public void Cancel() { Active = false; if (line != null) line.enabled = false; }
        private void OnDisable() => Cancel();
    }
}
