using UnityEngine;

namespace CandyCruisers
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        public const float HalfWidth = 3f;
        [SerializeField, Min(0.1f)] private float speed = 5f;
        [SerializeField, Min(0.1f)] private float touchSpeed = 12f;
        [SerializeField] private EnemyGrid grid;
        [SerializeField] private TongueShot tongue;
        [SerializeField] private SpriteRenderer body;
        private Camera view;
        private int finger = -1;
        private Vector2 pointerStart;
        private float pointerTime;
        private float pointerTravel;
        private float targetX;
        private bool movingToTarget;
        public EnemyColor? ReadyColor { get; private set; }
        public int MagicCharges { get; private set; }
        private EnemyColor? nextWaveColor;
        public bool ShotActive => tongue != null && tongue.Active;
        public void PrepareNextWave(EnemyColor[] plan)
        {
            if (plan == null || plan.Length == 0) return;
            var choices = new System.Collections.Generic.List<EnemyColor>();
            foreach (var color in plan)
                if (!choices.Contains(color)) choices.Add(color);
            if (choices.Count > 1 && ReadyColor.HasValue) choices.Remove(ReadyColor.Value);
            nextWaveColor = choices[Random.Range(0, choices.Count)];
            RefreshColor();
        }
        public Color DisplayColor => body != null ? body.color : Color.gray;
        public void RefreshPresentation(float time)
        {
            if (body == null) return;
            bool magic = MagicCharges > 0 || tongue != null && tongue.Active && tongue.IsMagic;
            body.color = magic ? EnemyPalette.Get((EnemyColor)(Mathf.FloorToInt(time * 5) % 5)) :
                tongue != null && tongue.Active ? EnemyPalette.Get(tongue.ShotColor) :
                ReadyColor.HasValue ? EnemyPalette.Get(ReadyColor.Value) : Color.gray;
        }
        private EnemyGrid subscribedGrid;
        private void OnEnable() => SubscribeToGrid();
        private void OnDisable() => UnsubscribeFromGrid();
        private void OnDestroy() => UnsubscribeFromGrid();
        private void SubscribeToGrid()
        {
            if (grid == subscribedGrid) return;
            UnsubscribeFromGrid();
            subscribedGrid = grid;
            if (subscribedGrid == null) return;
            subscribedGrid.ColorCleared += AwardMagic;
            subscribedGrid.FleetCleared += ResetMagic;
        }
        private void UnsubscribeFromGrid()
        {
            if (subscribedGrid == null) return;
            subscribedGrid.ColorCleared -= AwardMagic;
            subscribedGrid.FleetCleared -= ResetMagic;
            subscribedGrid = null;
        }
        private void AwardMagic(EnemyColor color) => MagicCharges = Mathf.Min(2, MagicCharges + 1);
        private void ResetMagic() => MagicCharges = 0;
        private float recoveryRemaining;
        public bool ControlsLocked { get; set; }
        public void CancelShot() => tongue.Cancel();
        public void StopActions()
        {
            tongue.Cancel();
            movingToTarget = false;
            finger = -1;
        }
        public bool Alive => recoveryRemaining <= 1.5f;
        public bool Invulnerable => recoveryRemaining > 0;
        public Bounds HitBounds => body.bounds;

        public bool Hit()
        {
            if (ControlsLocked || Invulnerable) return false;
            recoveryRemaining = 3f;
            ResetMagic();
            tongue.Cancel();
            movingToTarget = false;
            finger = -1;
            ShowPlayer(false);
            return true;
        }

        public void TickSurvival(float seconds)
        {
            if (recoveryRemaining <= 0) return;
            bool wasAlive = Alive;
            recoveryRemaining = Mathf.Max(0, recoveryRemaining - Mathf.Max(0, seconds));
            if (!wasAlive && Alive) RefreshColor(true);
            ShowPlayer(Alive && (!Invulnerable || Mathf.FloorToInt(recoveryRemaining * 8) % 2 == 0));
        }

        private void ShowPlayer(bool visible)
        {
            foreach (var visual in GetComponentsInChildren<SpriteRenderer>()) visual.enabled = visible;
        }

        public void Configure(EnemyGrid enemyGrid, TongueShot shot, SpriteRenderer bodyRenderer)
        { grid = enemyGrid; tongue = shot; body = bodyRenderer; SubscribeToGrid(); }

        private void Start()
        {
            Input.simulateMouseWithTouches = false;
            view = Camera.main;
            SubscribeToGrid();
            RefreshColor();
        }
        public void RefreshColor(bool reroll = false)
        {
            if (tongue.Active) return;
            var colors = grid.Model.AvailableColors();
            if (nextWaveColor.HasValue)
            {
                ReadyColor = nextWaveColor.Value;
                if (colors.Count > 0) nextWaveColor = null;
            }
            else if (colors.Count == 0)
            {
                RefreshPresentation(Time.time);
                return;
            }
            else if (reroll || !ReadyColor.HasValue || !colors.Contains(ReadyColor.Value))
                ReadyColor = colors[Random.Range(0, colors.Count)];
            RefreshPresentation(Time.time);
        }

        public bool Fire()
        {
            if (ControlsLocked || !Alive || grid.Model.Count == 0) return false;
            RefreshColor();
            bool magic = MagicCharges > 0;
            if (!ReadyColor.HasValue || !tongue.TryFire(ReadyColor.Value,
                Mathf.Max(0.1f, 5.3f - tongue.transform.position.y), magic)) return false;
            if (magic) MagicCharges--;
            return true;
        }

        public static float Wrap(float x) => Mathf.Repeat(x + HalfWidth, 2 * HalfWidth) - HalfWidth;
        public static float MoveTowardWrapped(float x, float target, float distance)
        {
            float delta = Wrap(target - x);
            return Wrap(x + Mathf.Clamp(delta, -distance, distance));
        }

        public void Move(float axis, float seconds)
        {
            if (ControlsLocked || !Alive) return;
            var position = transform.position;
            position.x = Wrap(position.x + Mathf.Clamp(axis, -1, 1) * speed * seconds);
            transform.position = position;
        }

        public void BeginPointer(Vector2 position)
        {
            pointerStart = position;
            pointerTime = Time.unscaledTime;
            pointerTravel = 0;
            UpdatePointer(position);
        }
        public void UpdatePointer(Vector2 position)
        {
            pointerTravel = Mathf.Max(pointerTravel, Vector2.Distance(position, pointerStart));
            if (view == null) view = Camera.main;
            targetX = Mathf.Clamp(view.ScreenToWorldPoint(position).x, -HalfWidth + 0.01f, HalfWidth - 0.01f);
            movingToTarget = true;
        }
        public static bool IsTap(float duration, float travel, float screenShortSide) =>
            duration <= 0.3f && travel <= screenShortSide * 0.025f;
        public void EndPointer(Vector2 position, bool canceled)
        {
            UpdatePointer(position);
            movingToTarget = false;
            if (!canceled && IsTap(Time.unscaledTime - pointerTime, pointerTravel, Mathf.Min(Screen.width, Screen.height))) Fire();
        }

        private void Update()
        {
            if (ControlsLocked) return;
            TickSurvival(Time.deltaTime);
            if (!Alive) return;
            bool wasActive = tongue.Active;
            tongue.Tick(Time.deltaTime, grid);
            RefreshColor(wasActive && !tongue.Active);
            RefreshPresentation(Time.time);
            float axis = (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1 : 0)
                - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1 : 0);
            if (axis != 0) { movingToTarget = false; Move(axis, Time.deltaTime); }

            if (Input.touchCount > 0)
            {
                foreach (Touch touch in Input.touches)
                {
                    if (finger == -1 && touch.phase == TouchPhase.Began)
                    { finger = touch.fingerId; BeginPointer(touch.position); }
                    if (touch.fingerId != finger) continue;
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    { EndPointer(touch.position, touch.phase == TouchPhase.Canceled); finger = -1; }
                    else UpdatePointer(touch.position);
                }
            }
            else if (finger != -1) { finger = -1; movingToTarget = false; }
            else
            {
                if (Input.GetMouseButtonDown(0)) BeginPointer(Input.mousePosition);
                else if (Input.GetMouseButton(0)) UpdatePointer(Input.mousePosition);
                if (Input.GetMouseButtonUp(0)) EndPointer(Input.mousePosition, false);
            }
            if (movingToTarget)
            {
                var position = transform.position;
                position.x = MoveTowardWrapped(position.x, targetX, touchSpeed * Time.deltaTime);
                transform.position = position;
            }
            if (Input.GetKeyDown(KeyCode.Space)) Fire();
        }
        private void OnApplicationFocus(bool focused)
        { if (!focused) { finger = -1; movingToTarget = false; } }
    }
}
