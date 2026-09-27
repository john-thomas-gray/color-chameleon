using UnityEngine;

namespace CandyCruisers
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        public const int MaxLives = 3;
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
        public int Lives { get; private set; } = MaxLives;
        public EnemyColor? ReadyColor { get; private set; }
        public int MagicCharges { get; private set; }
        private EnemyColor? nextWaveColor;
        public bool ShotActive => tongue != null && tongue.Active;
        public bool Stunned { get; private set; }
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
            if (Stunned) { body.color = Color.gray; return; }
            bool magic = MagicCharges > 0 || tongue != null && tongue.Active && tongue.IsMagic;
            body.color = magic ? EnemyPalette.Get((EnemyColor)(Mathf.FloorToInt(time * 10) % 6)) :
                tongue != null && tongue.Active ? EnemyPalette.Get(tongue.ShotColor) :
                ReadyColor.HasValue ? EnemyPalette.Get(ReadyColor.Value) : Color.gray;
        }
        private EnemyGrid subscribedGrid;
        private TongueShot subscribedTongue;
        private bool shotClearedColor;
        private bool shotHitEnemy;
        public event System.Action ShotAccepted;
        public event System.Action<bool> ShotCompleted;
        public event System.Action PlayerHit;
        public event System.Action FatalHit;
        private void OnEnable() => SubscribeToGrid();
        private void OnDisable() { UnsubscribeFromGrid(); ClearRecoveryCue(); }
        private void OnDestroy() { UnsubscribeFromGrid(); ClearRecoveryCue(); }
        private void SubscribeToGrid()
        {
            if (grid == subscribedGrid && tongue == subscribedTongue) return;
            UnsubscribeFromGrid();
            subscribedTongue = tongue;
            if (subscribedTongue != null) subscribedTongue.Finished += OnShotFinished;
            if (subscribedTongue != null) subscribedTongue.Deflected += OnDeflected;
            subscribedGrid = grid;
            if (subscribedGrid == null) return;
            subscribedGrid.ColorCleared += AwardMagic;
            subscribedGrid.MatchCleared += OnMatchCleared;
            subscribedGrid.LastYellowTransformed += OnLastYellowTransformed;
            subscribedGrid.FleetCleared += ResetMagic;
        }
        private void UnsubscribeFromGrid()
        {
            if (subscribedTongue != null) subscribedTongue.Finished -= OnShotFinished;
            if (subscribedTongue != null) subscribedTongue.Deflected -= OnDeflected;
            subscribedTongue = null;
            if (subscribedGrid == null) return;
            subscribedGrid.ColorCleared -= AwardMagic;
            subscribedGrid.MatchCleared -= OnMatchCleared;
            subscribedGrid.LastYellowTransformed -= OnLastYellowTransformed;
            subscribedGrid.FleetCleared -= ResetMagic;
            subscribedGrid = null;
        }
        private void AwardMagic(EnemyColor color)
        {
            MagicCharges = Mathf.Min(2, MagicCharges + 1);
            if (ShotActive) shotClearedColor = true;
        }
        private void OnMatchCleared(int count, bool fleetCleared, int scoreWeight)
        {
            if (ShotActive) shotHitEnemy = true;
        }
        private void OnShotFinished()
        {
            Stunned = false;
            RefreshPresentation(Time.time);
            if (!shotClearedColor && grid != null) grid.ResetColorClearStreak();
            ShotCompleted?.Invoke(shotHitEnemy);
            shotClearedColor = false;
            shotHitEnemy = false;
        }
        private void OnDeflected()
        {
            Stunned = true;
            movingToTarget = false;
            finger = -1;
            RefreshPresentation(Time.time);
        }
        private void ResetMagic() => MagicCharges = 0;
        private void OnLastYellowTransformed()
        {
            if (ReadyColor == EnemyColor.Yellow) Hit();
            RefreshColor();
        }
        private float recoveryRemaining;
        private PresentationCue recoveryCue;
        private void ClearRecoveryCue()
        {
            if (recoveryCue == null) return;
            if (Application.isPlaying) Destroy(recoveryCue.gameObject);
            else DestroyImmediate(recoveryCue.gameObject);
            recoveryCue = null;
        }
        public bool ControlsLocked { get; set; }
        public void CancelPointer() { finger = -1; movingToTarget = false; }
        public void CancelShot() => tongue.Cancel();
        public void StopActions()
        {
            tongue.Cancel();
            movingToTarget = false;
            finger = -1;
        }
        public bool FatallyDefeated { get; private set; }
        public bool Alive => !FatallyDefeated && recoveryRemaining <= 1.5f && (recoveryCue == null || recoveryCue.Finished);
        public bool Invulnerable => recoveryRemaining > 0;
        public Bounds HitBounds => CharacterVisuals.Ensure(gameObject).HitBounds;

        public PresentationCue BeginFatalDefeat()
        {
            if (FatallyDefeated) return null;
            FatallyDefeated = true;
            ControlsLocked = true;
            ClearRecoveryCue();
            var cue = CharacterVisuals.Ensure(gameObject).PlayerDefeat(ReadyColor ?? EnemyColor.Blue);
            StopActions();
            ShowPlayer(false);
            return cue;
        }

        public void ResetForRun()
        {
            Lives = MaxLives;
            FatallyDefeated = false;
            recoveryRemaining = 0;
            Stunned = false;
            movingToTarget = false;
            finger = -1;
            ReadyColor = null;
            nextWaveColor = null;
            ClearRecoveryCue();
            ResetMagic();
            if (tongue != null) tongue.Cancel();
            ShowPlayer(true);
            RefreshColor(true);
        }

        public bool GrantLife()
        {
            if (Lives >= MaxLives || FatallyDefeated) return false;
            Lives++;
            return true;
        }

        public bool Hit(bool ignoreInvulnerability = false)
        {
            if (ControlsLocked || FatallyDefeated || !Alive || (!ignoreInvulnerability && Invulnerable)) return false;
            ClearRecoveryCue();
            Lives = Mathf.Max(0, Lives - 1);
            ResetMagic();
            tongue.Cancel();
            PlayerHit?.Invoke();
            movingToTarget = false;
            finger = -1;
            if (Lives == 0)
            {
                FatalHit?.Invoke();
                if (!FatallyDefeated) BeginFatalDefeat();
                return true;
            }
            recoveryRemaining = 3f;
            recoveryCue = CharacterVisuals.Ensure(gameObject).PlayerDefeat(ReadyColor ?? EnemyColor.Blue);
            if (recoveryCue != null) recoveryCue.enabled = false;
            ShowPlayer(false);
            return true;
        }

        public void TickSurvival(float seconds)
        {
            if (FatallyDefeated) return;
            if (recoveryRemaining <= 0 && (recoveryCue == null || recoveryCue.Finished)) return;
            bool wasAlive = Alive;
            if (recoveryCue != null) recoveryCue.Tick(seconds);
            recoveryRemaining = Mathf.Max(0, recoveryRemaining - Mathf.Max(0, seconds));
            if (!wasAlive && Alive) RefreshColor(true);
            ShowPlayer(Alive && (!Invulnerable || Mathf.FloorToInt(recoveryRemaining * 8) % 2 == 0));
        }

        private void ShowPlayer(bool visible)
        {
            foreach (var visual in GetComponentsInChildren<SpriteRenderer>()) visual.enabled = visible;
        }

        public void Configure(EnemyGrid enemyGrid, TongueShot shot, SpriteRenderer bodyRenderer)
        { grid = enemyGrid; tongue = shot; body = bodyRenderer;
            CharacterVisuals.Ensure(gameObject).Configure(bodyRenderer); SubscribeToGrid(); }

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
                if (colors.Count > 0)
                {
                    var choices = grid.SelectablePlayerColors();
                    if (choices.Count > 0 && !choices.Contains(ReadyColor.Value))
                        ReadyColor = choices[Random.Range(0, choices.Count)];
                    nextWaveColor = null;
                }
            }
            else if (colors.Count == 0)
            {
                RefreshPresentation(Time.time);
                return;
            }
            else if (reroll || !ReadyColor.HasValue || !colors.Contains(ReadyColor.Value))
            {
                // Keep an existing Yellow, but never newly select an all-transforming color.
                var choices = grid.SelectablePlayerColors();
                if (choices.Count > 0) ReadyColor = choices[Random.Range(0, choices.Count)];
            }
            RefreshPresentation(Time.time);
        }

        public bool Fire()
        {
            if (ControlsLocked || Stunned || !Alive || grid.Model.Count == 0) return false;
            RefreshColor();
            bool magic = MagicCharges > 0;
            if (!ReadyColor.HasValue || !tongue.TryFire(ReadyColor.Value,
                Mathf.Max(0.1f, 5.3f - tongue.transform.position.y), magic)) return false;
            shotClearedColor = false;
            shotHitEnemy = false;
            ShotAccepted?.Invoke();
            if (magic) MagicCharges--;
            CharacterVisuals.Ensure(gameObject).Fire(ReadyColor.Value);
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
            if (ControlsLocked || Stunned || !Alive) return;
            MoveHorizontal(Mathf.Clamp(axis, -1, 1) * speed * seconds);
        }

        private void MoveHorizontal(float distance)
        {
            var session = grid != null ? grid.GetComponent<GameSession>() : null;
            // Wrap is a teleport, not a sweep through the middle of the field.
            while (Mathf.Abs(distance) > .000001f && !ControlsLocked)
            {
                float direction = Mathf.Sign(distance);
                float edge = direction * HalfWidth;
                float segment = direction * Mathf.Min(Mathf.Abs(distance), Mathf.Abs(edge - transform.position.x));
                float fraction = session != null ? session.ContactFraction(-segment) : 1;
                transform.position += Vector3.right * (segment * fraction);
                if (session != null) session.FinishContactMove(fraction);
                if (ControlsLocked) return;
                distance -= segment;
                if (Mathf.Abs(transform.position.x - edge) < .000001f && (direction > 0 || Mathf.Abs(distance) > .000001f))
                {
                    var position = transform.position;
                    position.x = -edge;
                    transform.position = position;
                    if (session != null && session.CheckPlayerContact()) return;
                }
            }
        }

        public void BeginPointer(Vector2 position)
        {
            var session = grid != null ? grid.GetComponent<GameSession>() : null;
            if (session != null && session.PointerOverMenu(position)) { session.Pause(); CancelPointer(); return; }
            if (Stunned) return;
            pointerStart = position;
            pointerTime = Time.unscaledTime;
            pointerTravel = 0;
            UpdatePointer(position);
        }
        public void UpdatePointer(Vector2 position)
        {
            if (Stunned || ControlsLocked) return;
            pointerTravel = Mathf.Max(pointerTravel, Vector2.Distance(position, pointerStart));
            if (view == null) view = Camera.main;
            targetX = Mathf.Clamp(view.ScreenToWorldPoint(position).x, -HalfWidth + 0.01f, HalfWidth - 0.01f);
            movingToTarget = true;
        }
        public static bool IsTap(float duration, float travel, float screenShortSide) =>
            duration <= 0.3f && travel <= screenShortSide * 0.025f;
        public void EndPointer(Vector2 position, bool canceled)
        {
            if (ControlsLocked) { CancelPointer(); return; }
            if (grid != null && grid.GetComponent<GameSession>()?.PointerOverMenu(position) == true) { CancelPointer(); return; }
            UpdatePointer(position);
            movingToTarget = false;
            if (!canceled && IsTap(Time.unscaledTime - pointerTime, pointerTravel, Mathf.Min(Screen.width, Screen.height))) Fire();
        }

        private void Update()
        {
            if (ControlsLocked || grid != null && grid.GetComponent<GameSession>()?.BlocksGameplayInput == true) return;
            TickSurvival(Time.deltaTime);
            if (!Alive) return;
            if (grid != null && grid.GetComponent<GameSession>()?.CheckPlayerContact() == true) return;
            bool wasActive = tongue.Active;
            tongue.Tick(Time.deltaTime, grid);
            RefreshColor(wasActive && !tongue.Active);
            RefreshPresentation(Time.time);
            if (Stunned) return;
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
                MoveHorizontal(Mathf.Clamp(Wrap(targetX - transform.position.x), -touchSpeed * Time.deltaTime, touchSpeed * Time.deltaTime));
            }
            if (Input.GetKeyDown(KeyCode.Space)) Fire();
        }
        private void OnApplicationFocus(bool focused)
        { if (!focused) { finger = -1; movingToTarget = false; } }
    }
}
