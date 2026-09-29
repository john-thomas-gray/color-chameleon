using UnityEngine;

namespace CandyCruisers
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TongueShot : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float extendSpeed = 14f;
        [SerializeField, Min(0.1f)] private float retractSpeed = 20f;
        public const float NormalHitRadius = .055f;
        public const float MagicHitRadius = .11f;
        private LineRenderer line;
        private float travel;
        private float maximumLength;
        private float normalStartWidth, normalEndWidth;
        private bool widthInitialized;
        private float deflectedRetractSpeed;
        private EnemyColor? returnPreviewColor;
        private bool returnPreviewMagic;
        public bool IsDeflected => Active && deflectedRetractSpeed > 0;
        // legacy-v1 Tongue.cs: maxSpeed 20, speedFactor = -0.25 + 0.01 * level.
        public static float DeflectedReturnSpeed(int level) => 20f * Mathf.Max(.01f, .25f - .01f * Mathf.Max(1, level));
        public bool Active { get; private set; }
        public bool Retracting => Active && travel >= maximumLength;
        public float Length => Active ? (Retracting ? Mathf.Max(0, 2 * maximumLength - travel) : travel) : 0;
        public float ReturnSpeed => deflectedRetractSpeed > 0 ? deflectedRetractSpeed : retractSpeed * (IsMagic ? 4 : 1);
        public float RemainingReturnSeconds => !Active ? 0 :
            Mathf.Max(0, Retracting ? 2 * maximumLength - travel : maximumLength) / Mathf.Max(.0001f, ReturnSpeed);
        public EnemyColor ShotColor { get; private set; }
        public bool IsMagic { get; private set; }
        public int MagicMultiplier { get; private set; }
        public event System.Action ExtensionStarted;
        public event System.Action RetractionStarted;
        public event System.Action MotionUpdated;
        public event System.Action Finished;
        public event System.Action Deflected;

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
            MagicMultiplier = 0;
            deflectedRetractSpeed = 0;
            returnPreviewColor = null;
            maximumLength = length;
            travel = 0;
            Active = true;
            SetVisualColor(color, magic);
            Draw();
            ExtensionStarted?.Invoke();
            return true;
        }

        public void SetReturnColor(EnemyColor color, bool magic)
        {
            if (!Retracting || returnPreviewColor == color && returnPreviewMagic == magic) return;
            returnPreviewColor = color;
            returnPreviewMagic = magic;
            SetVisualColor(color, magic);
        }

        private void SetVisualColor(EnemyColor color, bool magic)
        {
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
        }

        public void Tick(float seconds, EnemyGrid grid = null)
        {
            if (!Active || seconds <= 0) return;
            bool wasRetracting = Retracting;
            float extensionRate = extendSpeed * (IsMagic ? 4 : 1);
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
                        ShotColor, MagicHitRadius, out int magicId, out float magicHitLength, true))
                    {
                        MagicMultiplier += grid.ClearMagicChain(magicId, MagicMultiplier);
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
                else
                {
                    bool stopped = false;
                    // Revealed Yellows cease matching this shot; keep sweeping the same segment.
                    while (grid != null && grid.FindMatchingHit(transform.position, travel, nextLength,
                        ShotColor, NormalHitRadius, out int id, out float hitLength))
                    {
                        var result = grid.ResolveTongueHit(id, ShotColor);
                        if (!Active) return;
                        if (result == EnemyGrid.TongueHitResult.PassThrough) continue;
                        seconds -= (hitLength - travel) / extensionRate;
                        if (result == EnemyGrid.TongueHitResult.Deflected)
                        {
                            deflectedRetractSpeed = DeflectedReturnSpeed(grid.GetComponent<GameSession>()?.Progress.Level ?? 1);
                            line.startColor = line.endColor = Color.gray;
                            Deflected?.Invoke();
                        }
                        if (!Active) return;
                        maximumLength = Mathf.Max(.0001f, hitLength);
                        travel = maximumLength;
                        stopped = true;
                        break;
                    }
                    if (!stopped)
                    {
                        travel = nextLength;
                        seconds -= extending;
                    }
                }
            }
            if (!wasRetracting && Retracting) RetractionStarted?.Invoke();
            if (!Active) return;
            float returnRate = ReturnSpeed;
            if (seconds > 0) travel += seconds * returnRate;
            if (travel >= 2 * maximumLength)
            {
                Active = false;
                Finished?.Invoke();
            }
            Draw();
        }

        public void Draw()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            line.enabled = Active;
            line.positionCount = IsMagic ? 17 : 2;
            for (int i = 0; i < line.positionCount; i++)
                line.SetPosition(i, Vector3.up * (Length * i / (line.positionCount - 1)));
            MotionUpdated?.Invoke();
        }
        public void Cancel()
        {
            bool wasActive = Active;
            Active = false;
            deflectedRetractSpeed = 0;
            if (line != null) line.enabled = false;
            if (wasActive) Finished?.Invoke();
        }
        private void OnDisable() => Cancel();
    }
}
