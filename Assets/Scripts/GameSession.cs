using UnityEngine;
using UnityEngine.SceneManagement;

namespace CandyCruisers
{
    [RequireComponent(typeof(EnemyGrid), typeof(EnemyRowSpawner), typeof(EnemyGridMovement))]
    public sealed class GameSession : MonoBehaviour
    {
        public enum RunState { Playing, Refilling, GameOver, MainMenu, Dying }
        private PresentationCue deathCue;
        private bool hasDeathCue;
        private float deathRemaining;
        private static bool startAfterReload;
        private static int rememberedStartLevel = RunProgress.MinLevel;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStartup()
        {
            startAfterReload = false;
            rememberedStartLevel = RunProgress.MinLevel;
        }
        private float previousTimeScale = 1;
        private int inputBlockedThrough = -1;
        private bool started;
        public bool IsPaused { get; private set; }
        public bool BlocksGameplayInput => IsPaused || State == RunState.MainMenu || Time.frameCount <= inputBlockedThrough;
        [SerializeField, Min(0.1f)] private float refillDelay = 1f;
        [SerializeField] private PlayerMovement player;
        private EnemyGrid grid;
        private EnemyRowSpawner spawner;
        private EnemyGridMovement movement;
        private PlayerMovement subscribedPlayer;
        private SoundEffects soundEffects;
        private float refillRemaining;
        private EnemyColor[] nextBatch;
        private bool restarting;
        private bool leaderboardRecorded;
        [SerializeField, Range(RunProgress.MinLevel, RunProgress.MaxMenuStartLevel)] private int startLevel = RunProgress.MinLevel;
        public RunState State { get; private set; }
        public RunProgress Progress { get; } = new RunProgress();
        public event System.Action WaveSpawned;
        public int LastLeaderboardRank { get; private set; }
        public Color ProgressBarColor => player != null ? player.DisplayColor : Color.gray;
        public int StartLevel
        {
            get => startLevel;
            set
            {
                startLevel = RunProgress.ClampMenuStartLevel(value);
                rememberedStartLevel = startLevel;
            }
        }
        private float feedbackRemaining;
        private string feedback = "";
        private void OnMatchCleared(int count, bool fleetCleared, int scoreWeight)
            => AwardClear(count, fleetCleared, scoreWeight, true);
        private void OnOrangeBurstCleared(int count, bool fleetCleared, int scoreWeight)
            => AwardClear(count, fleetCleared, scoreWeight, false);
        private void AwardClear(int count, bool fleetCleared, int scoreWeight, bool applyCombo)
        {
            int before = Progress.Level;
            int combo = applyCombo ? Progress.ScoringComboMultiplier : 1;
            long earned = Progress.RegisterClear(count, fleetCleared, scoreWeight, applyCombo);
            feedback = "+" + earned.ToString("N0") + (fleetCleared ? "  Fleet cleared" : "");
            if (combo > 1) feedback += "  /  Combo x" + combo;
            if (Progress.Level > before)
            {
                feedback = "Level " + Progress.Level;
                foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
                    if (RunProgress.UnlockLevel(color) > before && RunProgress.UnlockLevel(color) <= Progress.Level)
                        feedback += "  /  " + color + " unlocked";
            }
            if (grid != null && grid.AllColorClearBarsFilled && player != null && player.GrantLife())
                feedback += "  /  +1 Life";
            feedbackRemaining = 3f;
        }
        public void Configure(PlayerMovement controller)
        {
            player = controller;
            SubscribeToPlayer();
        }
        private void Start()
        {
            started = true;
            if (grid.Model.Count == 0 && State == RunState.Playing)
            {
                bool start = startAfterReload;
                startAfterReload = false;
                StartLevel = rememberedStartLevel;
                State = RunState.MainMenu;
                Suspend(true);
                if (start) StartRun();
            }
        }

        public void StartRun()
        {
            if (State != RunState.MainMenu) return;
            inputBlockedThrough = Time.frameCount + 1;
            leaderboardRecorded = false;
            LastLeaderboardRank = 0;
            Progress.Reset(StartLevel);
            if (player != null) player.ResetForRun();
            State = RunState.Playing;
            BeginBatch(StartLevel == RunProgress.MinLevel ? spawner.PlanOpening() : spawner.PlanBatch(Progress.BatchRows));
        }
        public void Pause()
        {
            if (IsPaused || State == RunState.MainMenu || State == RunState.GameOver || State == RunState.Dying) return;
            IsPaused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            if (soundEffects != null) soundEffects.SetPaused(true);
            player.CancelPointer();
            Suspend(true);
        }
        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = previousTimeScale;
            if (soundEffects != null) soundEffects.SetPaused(false);
            inputBlockedThrough = Time.frameCount + 1;
            Suspend(State != RunState.Playing);
        }
        private void OnApplicationFocus(bool focused) { if (started && !focused) Pause(); }
        private void OnApplicationPause(bool paused) { if (started && paused) Pause(); }
        private Rect PauseButton
        {
            get
            {
                var camera = Camera.main;
                var corner = camera != null ? camera.WorldToScreenPoint(new Vector3(3, 5.3f, 0)) : new Vector3(Screen.width, Screen.height - 8);
                return new Rect(corner.x - 58, Screen.height - corner.y, 48, 44);
            }
        }
        public bool PointerOverMenu(Vector2 point) => IsPaused || State == RunState.MainMenu || State == RunState.GameOver ||
            PauseButton.Contains(new Vector2(point.x, Screen.height - point.y));

        private void OnEnable()
        {
            soundEffects = GetComponent<SoundEffects>();
            if (soundEffects == null) soundEffects = gameObject.AddComponent<SoundEffects>();
            grid = GetComponent<EnemyGrid>();
            spawner = GetComponent<EnemyRowSpawner>();
            movement = GetComponent<EnemyGridMovement>();
            grid.FleetCleared += BeginRefill;
            grid.MatchCleared += OnMatchCleared;
            grid.OrangeBurstCleared += OnOrangeBurstCleared;
            SubscribeToPlayer();
        }
        private void OnDisable()
        {
            if (IsPaused) { Time.timeScale = previousTimeScale; IsPaused = false; }
            if (soundEffects != null) soundEffects.SetPaused(false);
            if (grid != null) grid.FleetCleared -= BeginRefill;
            if (grid != null) grid.MatchCleared -= OnMatchCleared;
            if (grid != null) grid.OrangeBurstCleared -= OnOrangeBurstCleared;
            UnsubscribeFromPlayer();
        }

        private void SubscribeToPlayer()
        {
            if (player != null && soundEffects != null)
            {
                var shot = player.GetComponentInChildren<TongueShot>();
                if (shot != null)
                {
                    var audio = shot.GetComponent<TongueSoundEffects>();
                    if (audio == null) audio = shot.gameObject.AddComponent<TongueSoundEffects>();
                    audio.Configure(shot, soundEffects);
                }
            }
            if (player == subscribedPlayer) return;
            UnsubscribeFromPlayer();
            subscribedPlayer = player;
            if (subscribedPlayer == null) return;
            subscribedPlayer.ShotAccepted += Progress.BeginShot;
            subscribedPlayer.ShotCompleted += Progress.FinishShot;
            subscribedPlayer.PlayerHit += Progress.ResetCombo;
            subscribedPlayer.FatalHit += BeginPlayerDeath;
        }

        private void UnsubscribeFromPlayer()
        {
            if (subscribedPlayer == null) return;
            subscribedPlayer.ShotAccepted -= Progress.BeginShot;
            subscribedPlayer.ShotCompleted -= Progress.FinishShot;
            subscribedPlayer.PlayerHit -= Progress.ResetCombo;
            subscribedPlayer.FatalHit -= BeginPlayerDeath;
            subscribedPlayer = null;
        }

        private void Update()
        {
            if (IsPaused || State == RunState.MainMenu || State == RunState.GameOver)
                foreach (var touch in Input.touches)
                    if (touch.phase == TouchPhase.Ended && ActivateMenuTouch(touch.position)) return;
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) { if (IsPaused) Resume(); else Pause(); }
            if (State == RunState.MainMenu && Input.GetKeyDown(KeyCode.Return)) StartRun();
            if (State != RunState.MainMenu && Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
            Tick(Time.deltaTime);
        }

        private bool ActivateMenuTouch(Vector2 point)
        {
            point.y = Screen.height - point.y;
            float width = Mathf.Min(300, Screen.width - 32);
            float x = (Screen.width - width) / 2, y = Screen.height / 2f;
            if (MenuPrimaryButtonRect(x, y, width).Contains(point))
            { if (State == RunState.MainMenu) StartRun(); else if (IsPaused) Resume(); else Restart(); return true; }
            if (State == RunState.MainMenu && MenuLevelDownRect(x, y, width).Contains(point))
            { StartLevel--; return true; }
            if (State == RunState.MainMenu && MenuLevelUpRect(x, y, width).Contains(point))
            { StartLevel++; return true; }
            if (IsPaused && new Rect(x, y + 42, width, 48).Contains(point)) { Restart(); return true; }
            if (State != RunState.MainMenu && new Rect(x, y + (IsPaused ? 102 : 42), width, 48).Contains(point))
            { ReturnToMenu(); return true; }
            return false;
        }

        public void Tick(float seconds)
        {
            if (IsPaused || State == RunState.MainMenu) return;
            if (State == RunState.Dying)
            {
                deathRemaining -= Mathf.Max(0, seconds);
                if (deathCue != null) deathCue.Tick(seconds);
                if (hasDeathCue ? deathCue == null || deathCue.Finished : deathRemaining <= 0) EndGame();
                return;
            }
            if (CheckPlayerContact()) return;
            feedbackRemaining = Mathf.Max(0, feedbackRemaining - Mathf.Max(0, seconds));
            if (State != RunState.Refilling) return;
            refillRemaining -= Mathf.Max(0, seconds);
            if (refillRemaining > 0 || player.ShotActive || grid.HasDeathEffects) return;
            movement.ResetSweep();
            if (!spawner.SpawnBatch(nextBatch))
            {
                Debug.LogError("Cannot create the next fleet batch.", this);
                EndGame();
                return;
            }
            State = RunState.Playing;
            Suspend(false);
            grid.RefreshSpecials();
            player.RefreshColor();
            WaveSpawned?.Invoke();
        }

        private void BeginRefill()
        {
            BeginBatch(spawner.PlanBatch(Progress.BatchRows));
        }

        private void BeginBatch(EnemyColor[] plan)
        {
            if (State != RunState.Playing) return;
            State = RunState.Refilling;
            refillRemaining = refillDelay;
            nextBatch = plan;
            player.PrepareNextWave(nextBatch);
            Suspend(true);
        }

        public float ContactFraction(float enemyTravelRelativeToPlayer)
        {
            if (grid == null || State != RunState.Playing || IsPaused || player == null || !player.Alive || player.Invulnerable) return 1;
            var target = player.HitBounds;
            float fraction = 1;
            for (int column = 0; column < GridModel.Columns; column++)
            {
                var cell = grid.Model.At(column, GridModel.Rows - 1);
                if (cell == null) continue;
                var enemy = grid.View(cell.Id);
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                var body = enemy.HitBounds;
                if (body.max.y < target.min.y || body.min.y > target.max.y) continue;
                if (body.max.x >= target.min.x && body.min.x <= target.max.x) return 0;
                if (Mathf.Abs(enemyTravelRelativeToPlayer) < .000001f) continue;
                float a = (target.min.x - body.max.x) / enemyTravelRelativeToPlayer;
                float b = (target.max.x - body.min.x) / enemyTravelRelativeToPlayer;
                float enter = Mathf.Min(a, b), leave = Mathf.Max(a, b);
                if (leave >= 0 && enter <= 1) fraction = Mathf.Min(fraction, Mathf.Max(0, enter));
            }
            return fraction;
        }

        public bool CheckPlayerContact()
        {
            if (ContactFraction(0) != 0) return false;
            return player != null && player.Hit();
        }

        public void FinishContactMove(float fraction)
        {
            if (fraction < 1) player.Hit();
            else CheckPlayerContact();
        }

        private void BeginPlayerDeath()
        {
            if (State != RunState.Playing && State != RunState.Refilling) return;
            State = RunState.Dying;
            deathRemaining = PlayerDeathBurst.Duration;
            deathCue = player.BeginFatalDefeat();
            hasDeathCue = deathCue != null;
            // The session advances the cue once per frame, including replacement-prefab fallbacks.
            if (deathCue != null) deathCue.enabled = false;
            Suspend(true);
        }

        private void OnDestroy()
        {
            if (deathCue == null) return;
            if (Application.isPlaying) Destroy(deathCue.gameObject);
            else DestroyImmediate(deathCue.gameObject);
        }

        private void EndGame()
        {
            if (State == RunState.GameOver) return;
            State = RunState.GameOver;
            RecordLeaderboard();
            Suspend(true);
        }

        private void RecordLeaderboard()
        {
            if (leaderboardRecorded) return;
            leaderboardRecorded = true;
            LastLeaderboardRank = LocalLeaderboard.Submit(Progress.Score, Progress.Level);
        }

        private void Suspend(bool suspended)
        {
            movement.enabled = !suspended;
            grid.enabled = !suspended;
            spawner.enabled = !suspended;
            player.ControlsLocked = IsPaused || State == RunState.MainMenu || State == RunState.GameOver || State == RunState.Dying;
            if (State == RunState.GameOver) player.StopActions();
            foreach (var ability in GetComponentsInChildren<EnemyAbilities>()) ability.Suspended = suspended;
            foreach (var missile in FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None))
                missile.Suspended = IsPaused || State == RunState.MainMenu || State == RunState.GameOver || State == RunState.Dying;
        }

        public void Restart()
        { Reload(true); }
        public void ReturnToMenu() => Reload(false);
        private void Reload(bool start)
        {
            if (restarting) return;
            restarting = true;
            if (IsPaused) { Time.timeScale = previousTimeScale; IsPaused = false; }
            startAfterReload = start;
            rememberedStartLevel = StartLevel;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnGUI()
        {
            if (State != RunState.MainMenu) DrawProgress();
            if (!IsPaused && (State == RunState.Playing || State == RunState.Refilling))
            {
                if (GUI.Button(PauseButton, new GUIContent("", "Pause"))) Pause();
                GUI.DrawTexture(new Rect(PauseButton.x + 16, PauseButton.y + 13, 5, 18), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(PauseButton.x + 27, PauseButton.y + 13, 5, 18), Texture2D.whiteTexture);
            }
            if (!IsPaused && State != RunState.MainMenu && State != RunState.GameOver) return;
            var previous = GUI.color;
            GUI.color = new Color(0.015f, 0.025f, 0.04f, State == RunState.MainMenu ? .65f : .88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float width = Mathf.Min(300, Screen.width - 32);
            float x = (Screen.width - width) / 2;
            float y = Screen.height / 2f;
            var heading = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            heading.normal.textColor = Color.white;
            bool menu = State == RunState.MainMenu;
            GUI.Label(new Rect(x, y - 120, width, 48), menu ? "Candy Cruisers" : IsPaused ? "Paused" : "Game Over", heading);
            var scoreLabel = new GUIStyle(heading) { fontSize = 18 };
            if (!menu) GUI.Label(new Rect(x, y - 70, width, 26), "Score " + Progress.Score.ToString("N0"), scoreLabel);
            if (!menu && State == RunState.GameOver && LastLeaderboardRank > 0)
                GUI.Label(new Rect(x, y - 46, width, 22), "Leaderboard #" + LastLeaderboardRank, scoreLabel);
            DrawLeaderboard(new Rect(x, y + (menu ? 112 : IsPaused ? 164 : 104), width, 150), menu || State == RunState.GameOver);
            var button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            if (GUI.Button(MenuPrimaryButtonRect(x, y, width), menu ? "Play" : IsPaused ? "Resume" : "Restart", button))
            { if (menu) StartRun(); else if (IsPaused) Resume(); else Restart(); }
            if (menu) DrawStartLevelSelector(x, y, width, button);
            if (IsPaused && GUI.Button(new Rect(x, y + 42, width, 48), "Restart", button)) Restart();
            if (!menu && GUI.Button(new Rect(x, y + (IsPaused ? 102 : 42), width, 48), "Main Menu", button)) ReturnToMenu();
            GUI.color = previous;
        }

        private static Rect MenuPrimaryButtonRect(float x, float y, float width) => new Rect(x, y - 24, width, 54);
        private static Rect MenuLevelDownRect(float x, float y, float width) => new Rect(x, y + 42, 54, 42);
        private static Rect MenuLevelValueRect(float x, float y, float width) => new Rect(x + 62, y + 42, width - 124, 42);
        private static Rect MenuLevelUpRect(float x, float y, float width) => new Rect(x + width - 54, y + 42, 54, 42);

        private void DrawStartLevelSelector(float x, float y, float width, GUIStyle button)
        {
            var value = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            value.normal.textColor = Color.white;
            if (GUI.Button(MenuLevelDownRect(x, y, width), "-", button)) StartLevel--;
            GUI.Label(MenuLevelValueRect(x, y, width), "Start Level " + StartLevel, value);
            if (GUI.Button(MenuLevelUpRect(x, y, width), "+", button)) StartLevel++;
        }

        private void DrawProgress()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var left = camera.WorldToScreenPoint(new Vector3(-3, 5.3f, 0));
            var right = camera.WorldToScreenPoint(new Vector3(3, 5.3f, 0));
            float width = right.x - left.x - 20;
            float x = left.x + 10, y = Screen.height - left.y;
            var label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            float textWidth = width - 54;
            GUI.Label(new Rect(x, y, textWidth * .65f, 26), "SCORE " + Progress.Score.ToString("N0"), label);
            label.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(x + textWidth * .65f, y, textWidth * .35f, 26), "LEVEL " + Progress.Level, label);
            var previous = GUI.color;
            GUI.color = new Color(.3f, .4f, .45f, .7f);
            GUI.DrawTexture(new Rect(x, y + 50, width, 9), Texture2D.whiteTexture);
            GUI.color = ProgressBarColor;
            float fraction = (float)(Progress.Defeated - Progress.PreviousThreshold) /
                (Progress.NextThreshold - Progress.PreviousThreshold);
            GUI.DrawTexture(new Rect(x, y + 50, width * fraction, 9), Texture2D.whiteTexture);
            var colors = grid.SeenColors();
            float bottom = Screen.height - camera.WorldToScreenPoint(new Vector3(0, -5.25f, 0)).y;
            for (int i = 0; i < colors.Count; i++)
            {
                if (!grid.HasColorClearBar(colors[i])) continue;
                float segmentWidth = width / colors.Count;
                var color = EnemyPalette.Get(colors[i]);
                GUI.color = color;
                GUI.DrawTexture(new Rect(x + i * segmentWidth, bottom, segmentWidth - 2, 5), Texture2D.whiteTexture);
            }
            GUI.color = previous;
            label.fontSize = 13;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.UpperCenter;
            label.wordWrap = true;
            string status = player != null ? "LIVES " + player.Lives : "";
            if (player != null && player.MagicCharges > 0)
                status += (status.Length > 0 ? "  |  " : "") + "MAGIC " + player.MagicCharges;
            if (Progress.ComboStreak > 0)
                status += (status.Length > 0 ? "  |  " : "") + "COMBO x" + Progress.ComboMultiplier;
            if (feedbackRemaining > 0) status += (status.Length > 0 ? "  |  " : "") + feedback;
            if (status.Length > 0) GUI.Label(new Rect(x, y + 66, width, 44), status, label);
        }

        private void DrawLeaderboard(Rect area, bool visible)
        {
            if (!visible) return;
            var entries = LocalLeaderboard.Entries();
            var label = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter };
            label.normal.textColor = Color.white;
            GUI.Label(new Rect(area.x, area.y, area.width, 22), "Top Scores", label);
            label.fontSize = 13;
            label.alignment = TextAnchor.UpperLeft;
            if (entries.Count == 0)
            {
                label.alignment = TextAnchor.UpperCenter;
                GUI.Label(new Rect(area.x, area.y + 24, area.width, 22), "No scores yet", label);
                return;
            }
            for (int i = 0; i < entries.Count; i++)
            {
                string row = (i + 1) + ". " + entries[i].Score.ToString("N0") + "  Level " + entries[i].Level;
                GUI.Label(new Rect(area.x + 24, area.y + 24 + i * 20, area.width - 48, 20), row, label);
            }
        }
    }
}
