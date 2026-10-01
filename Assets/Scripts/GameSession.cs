using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CandyCruisers
{
    [RequireComponent(typeof(EnemyGrid), typeof(EnemyRowSpawner), typeof(EnemyGridMovement))]
    public sealed class GameSession : MonoBehaviour
    {
        public enum RunState { Playing, Refilling, GameOver, MainMenu, Dying }
        public const float GameOverBlackoutBeats = 4;
        public float GameOverBlackoutSeconds { get; private set; } = GameOverBlackoutBeats * FullSetCelebration.StepDuration;
        public const float GameOverTitleFadeSeconds = .65f;
        private float gameOverElapsed;
        public float GameOverMusicGain => 1 - GameOverBlackoutOpacity;
        public float GameOverBlackoutOpacity => State == RunState.GameOver ? 1 : State == RunState.Dying ?
            Mathf.SmoothStep(0, 1, deathElapsed / GameOverBlackoutSeconds) : 0;
        public float GameOverTitleOpacity => State == RunState.GameOver ?
            Mathf.SmoothStep(0, 1, gameOverElapsed / GameOverTitleFadeSeconds) : 0;
        public float GameOverScoreOpacity => 1 - GameOverBlackoutOpacity;
        public bool GameOverTitleReady => State == RunState.GameOver && gameOverElapsed >= GameOverTitleFadeSeconds;
        private PresentationCue deathCue;
        private bool hasDeathCue;
        private float deathElapsed;
        private bool deathShattered;
        private bool deathAnimationStarted;
        private GameOverBlackout blackout;
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
        public bool BlocksGameplayInput => IsPaused || State == RunState.MainMenu || State == RunState.Dying ||
            State == RunState.GameOver || Time.frameCount <= inputBlockedThrough;
        [SerializeField] private PlayerMovement player;
        private EnemyGrid grid;
        private EnemyRowSpawner spawner;
        private EnemyGridMovement movement;
        private PlayerMovement subscribedPlayer;
        private SoundEffects soundEffects;
        private GameplayMusicPlayer musicPlayer;
        private float refillRemaining;
        private AudioClip refillMusicClip;
        private float refillMusicSeconds;
        private float refillSpawnSeconds;
        private EnemyColor[] nextBatch;
        private bool restarting;
        private bool leaderboardRecorded;
        private string menuLevelDigits = "";
        [SerializeField, Range(RunProgress.MinLevel, RunProgress.MaxMenuStartLevel)] private int startLevel = RunProgress.MinLevel;
        private static readonly EnemyColor[] PreviewRegularColors = {
            EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Yellow, EnemyColor.Orange
        };
        private static readonly EnemyColor[] PreviewSpecialColors = {
            EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Yellow
        };
        public RunState State { get; private set; }
        public RunProgress Progress { get; } = new RunProgress();
        public event System.Action WaveSpawned;
        public int LastLeaderboardRank { get; private set; }
        public Color ProgressBarColor => player != null ? player.DisplayColor : Color.gray;
        public const float ComboMilestoneAnimationSeconds = 1.1f;
        public const float ComboMilestonePulseSeconds = .45f;
        public const float ComboDeathFadeSeconds = .5f;
        public const float LargePhoneGuiScale = 2f;
        public const float MediumPhoneGuiScale = 1.6f;
        public const float SmallPhoneGuiScale = 1.35f;
        private int observedComboStreak;
        private int comboMilestoneMultiplier;
        private float comboMilestoneElapsed = ComboMilestoneAnimationSeconds;
        public ComboBreakAnimation ComboBreak { get; } = new ComboBreakAnimation();
        private float comboDeathFadeElapsed = ComboDeathFadeSeconds;
        public int ComboDeathMultiplier { get; private set; }
        public Color ComboDeathColor { get; private set; } = Color.white;
        public float ComboDeathOpacity => ComboDeathMultiplier > 1 ?
            1 - Mathf.SmoothStep(0, 1, comboDeathFadeElapsed / ComboDeathFadeSeconds) : 0;
        public Color ComboColor { get; private set; } = Color.white;
        public Font ComboFont => Resources.Load<Font>("Fonts/Bungee-Regular");
        private bool accumulatingShotScore;
        private long shotScore;
        public string ScoreFeedback => feedback;
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
        private readonly ColorClearBarAnimation clearBarAnimation = new ColorClearBarAnimation();
        public FullSetCelebration ClearCelebration { get; } = new FullSetCelebration();
        private bool partialClearCelebration;
        private int rewardedLifeSlot = -1;
        private int refillPostBarBeats = 1;
        private float? refillRewardBeat;
        private int removedBarCount;
        private void OnCelebrationColorRemoved(EnemyColor color)
        {
            if (!grid.HasColorClearBar(color)) return;
            int index = removedBarCount++;
            soundEffects?.PlayCue(partialClearCelebration ? SoundEffect.BarPowerDown : SoundEffect.BarPowerUp,
                partialClearCelebration ? Mathf.Pow(2, -index * 2f / 12) : ColorClearBarAnimation.TonePitch(index + 1));
        }
        private void OnColorCleared(EnemyColor color)
        {
            clearBarAnimation.Begin(color);
            int bars = 0;
            foreach (var seen in grid.SeenColors()) if (grid.HasColorClearBar(seen)) bars++;
            soundEffects?.PlayCue(SoundEffect.ColorClear, ColorClearBarAnimation.TonePitch(bars));
            if (grid.AllColorClearBarsFilled) soundEffects?.PlayCue(SoundEffect.Jackpot);
        }
        private void OnMatchCleared(int count, bool fleetCleared, int scoreWeight)
            => AwardClear(count, fleetCleared, scoreWeight, true);
        private void OnMatchColorScored(EnemyColor color)
        {
            if (Progress.RegisterShotColor(color)) ObserveComboStreak();
        }
        private void OnEnemyDestroyed(EnemyColor color) => ComboColor = EnemyPalette.Get(color);
        private void OnOrangeBurstCleared(int count, bool fleetCleared, int scoreWeight)
            => AwardClear(count, fleetCleared, scoreWeight, false);
        private void AwardClear(int count, bool fleetCleared, int scoreWeight, bool applyCombo)
        {
            int before = Progress.Level;
            int combo = applyCombo ? Progress.ScoringComboMultiplier : 1;
            long basePoints = (100L + 10L * (before - 1)) * scoreWeight;
            long fleetBonus = fleetCleared ? 10000L * before : 0;
            long earned = Progress.RegisterClear(count, fleetCleared, scoreWeight, applyCombo);
            long previousPoints = 0;
            if (applyCombo && accumulatingShotScore)
            {
                previousPoints = shotScore;
                shotScore += earned;
            }
            feedback = ScoreCalculation(basePoints, combo, fleetBonus, previousPoints);
            if (Progress.Level > before)
            {
                soundEffects?.PlayCue(SoundEffect.LevelUp);
                feedback += "  /  Level " + Progress.Level;
                foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
                    if (RunProgress.UnlockLevel(color) > before && RunProgress.UnlockLevel(color) <= Progress.Level)
                        feedback += "  /  " + color + " unlocked";
            }
            if (grid != null && grid.AllColorClearBarsFilled && player != null && player.GrantLife(false))
            {
                rewardedLifeSlot = player.ExtraLives - 1;
                feedback += "  /  +1 Life";
            }
            feedbackRemaining = 3f;
        }

        public static string ScoreCalculation(long basePoints, int multiplier, long fleetBonus, long previousPoints = 0) =>
            (previousPoints > 0 ? previousPoints.ToString("N0") + " + " : "") +
            basePoints.ToString("N0") + " x" + multiplier +
            (fleetBonus > 0 ? " + " + fleetBonus.ToString("N0") : "") +
            " = +" + (previousPoints + basePoints * multiplier + fleetBonus).ToString("N0");
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
                musicPlayer?.UpdatePlayback();
                if (start) StartRun();
            }
        }

        public void StartRun()
        {
            if (State != RunState.MainMenu) return;
            inputBlockedThrough = Time.frameCount + 1;
            leaderboardRecorded = false;
            gameOverElapsed = 0;
            LastLeaderboardRank = 0;
            menuLevelDigits = "";
            Progress.Reset(StartLevel);
            accumulatingShotScore = false;
            feedbackRemaining = 0;
            rewardedLifeSlot = -1;
            ResetComboPresentation();
            if (player != null) player.ResetForRun();
            State = RunState.Playing;
            musicPlayer?.UpdatePlayback();
            BeginBatch(StartLevel == RunProgress.MinLevel ? spawner.PlanOpening() : spawner.PlanBatch(Progress.BatchRows),
                musicPlayer != null ? musicPlayer.SecondsForBeats(1) : CurrentBeatDuration);
        }
        public void Pause()
        {
            if (IsPaused || State == RunState.MainMenu || State == RunState.GameOver || State == RunState.Dying) return;
            IsPaused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            if (soundEffects != null) soundEffects.SetPaused(true);
            musicPlayer?.UpdatePlayback();
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
            musicPlayer?.UpdatePlayback();
        }
        private void OnApplicationFocus(bool focused) { if (started && !focused) Pause(); }
        private void OnApplicationPause(bool paused) { if (started && paused) Pause(); }
        private Rect PauseButton
        {
            get
            {
                var camera = Camera.main;
                var corner = camera != null ? ToGuiPoint(camera.WorldToScreenPoint(new Vector3(3, 5.3f, 0))) :
                    new Vector3(GuiWidth, GuiHeight - 8);
                return new Rect(corner.x - 58, GuiHeight - corner.y, 48, 44);
            }
        }
        public bool PointerOverMenu(Vector2 point) => IsPaused || State == RunState.MainMenu || State == RunState.GameOver ||
            PauseButton.Contains(FlipGuiY(ToGuiPoint(point)));

        private void OnEnable()
        {
            soundEffects = GetComponent<SoundEffects>();
            if (soundEffects == null) soundEffects = gameObject.AddComponent<SoundEffects>();
            musicPlayer = GetComponent<GameplayMusicPlayer>();
            if (musicPlayer == null) musicPlayer = gameObject.AddComponent<GameplayMusicPlayer>();
            musicPlayer.Configure(this);
            grid = GetComponent<EnemyGrid>();
            spawner = GetComponent<EnemyRowSpawner>();
            movement = GetComponent<EnemyGridMovement>();
            grid.FleetCleared += BeginRefill;
            ClearCelebration.ColorRemoved += OnCelebrationColorRemoved;
            grid.MatchCleared += OnMatchCleared;
            grid.MatchColorScored += OnMatchColorScored;
            grid.OrangeBurstCleared += OnOrangeBurstCleared;
            grid.ColorCleared += OnColorCleared;
            grid.EnemyDestroyed += OnEnemyDestroyed;
            SubscribeToPlayer();
        }
        private void OnDisable()
        {
            ClearCelebration.Reset();
            ClearCelebration.ColorRemoved -= OnCelebrationColorRemoved;
            if (player != null) player.SetCelebrationColor(null);
            if (IsPaused) { Time.timeScale = previousTimeScale; IsPaused = false; }
            if (soundEffects != null) soundEffects.SetPaused(false);
            if (grid != null) grid.FleetCleared -= BeginRefill;
            if (grid != null) grid.MatchCleared -= OnMatchCleared;
            if (grid != null) grid.MatchColorScored -= OnMatchColorScored;
            if (grid != null) grid.OrangeBurstCleared -= OnOrangeBurstCleared;
            if (grid != null) grid.ColorCleared -= OnColorCleared;
            if (grid != null) grid.EnemyDestroyed -= OnEnemyDestroyed;
            UnsubscribeFromPlayer();
            musicPlayer?.StopPlayback();
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
            subscribedPlayer.ShotAccepted += OnShotAccepted;
            subscribedPlayer.ShotCompleted += OnShotCompleted;
            subscribedPlayer.ShotRetractionStarted += OnShotRetractionStarted;
            subscribedPlayer.PlayerHit += OnPlayerHit;
            subscribedPlayer.FatalHit += BeginPlayerDeath;
        }

        private void UnsubscribeFromPlayer()
        {
            if (subscribedPlayer == null) return;
            subscribedPlayer.ShotAccepted -= OnShotAccepted;
            subscribedPlayer.ShotCompleted -= OnShotCompleted;
            subscribedPlayer.ShotRetractionStarted -= OnShotRetractionStarted;
            subscribedPlayer.PlayerHit -= OnPlayerHit;
            subscribedPlayer.FatalHit -= BeginPlayerDeath;
            subscribedPlayer = null;
        }

        private void OnShotAccepted()
        {
            Progress.BeginShot();
            accumulatingShotScore = true;
            shotScore = 0;
        }

        private void OnShotRetractionStarted(bool hit)
        {
            if (hit) return;
            Progress.ResetCombo();
            ObserveComboStreak();
        }

        private void OnShotCompleted(bool hit)
        {
            accumulatingShotScore = false;
            Progress.FinishShot(hit);
            ObserveComboStreak();
        }

        private void OnPlayerHit()
        {
            FadeComboOnDeath();
        }

        private void Update()
        {
            if (IsPaused || State == RunState.MainMenu || State == RunState.GameOver)
                foreach (var touch in Input.touches)
                    if (touch.phase == TouchPhase.Ended && ActivateMenuTouch(touch.position)) return;
            if (State == RunState.GameOver && Input.touchCount == 0 && Input.GetMouseButtonUp(0) &&
                ActivateMenuTouch(Input.mousePosition)) return;
            foreach (var key in MenuKeys)
                if (Input.GetKeyDown(key) && HandleMenuKey(key)) return;
            Tick(State == RunState.GameOver || State == RunState.Dying ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private static readonly KeyCode[] MenuKeys = { KeyCode.P, KeyCode.Escape, KeyCode.Return,
            KeyCode.KeypadEnter, KeyCode.R, KeyCode.M, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.I, KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4,
            KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9 };

        public bool HandleMenuKey(KeyCode key)
        {
            if (State == RunState.Dying || State == RunState.GameOver && !GameOverTitleReady) return false;
            if (DeveloperOptions.Available && key == KeyCode.I)
            {
                TogglePlayerInvincible();
                return true;
            }
            if (HandleMenuLevelDigit(key)) return true;
            if (key == KeyCode.P || key == KeyCode.Escape)
            {
                if (IsPaused) Resume();
                else if (State == RunState.Playing || State == RunState.Refilling) Pause();
                else return false;
                return true;
            }
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                if (State == RunState.MainMenu) StartRun();
                else if (IsPaused) Resume();
                else if (State == RunState.GameOver) Restart();
                else return false;
                return true;
            }
            if (key == KeyCode.R && State != RunState.MainMenu) { Restart(); return true; }
            if (key == KeyCode.M && (IsPaused || State == RunState.GameOver)) { ReturnToMenu(); return true; }
            if (State == RunState.MainMenu && (key == KeyCode.LeftArrow || key == KeyCode.RightArrow))
            { menuLevelDigits = ""; StartLevel += key == KeyCode.LeftArrow ? -1 : 1; return true; }
            return false;
        }

        private bool HandleMenuLevelDigit(KeyCode key)
        {
            if (State != RunState.MainMenu) return false;
            int digit = MenuDigit(key);
            if (digit < 0) return false;
            if (menuLevelDigits.Length >= 2) menuLevelDigits = "";
            menuLevelDigits += digit.ToString();
            StartLevel = int.Parse(menuLevelDigits);
            return true;
        }

        private static int MenuDigit(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return (int)key - (int)KeyCode.Alpha0;
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return (int)key - (int)KeyCode.Keypad0;
            return -1;
        }

        private bool ActivateMenuTouch(Vector2 point)
        {
            point = FlipGuiY(ToGuiPoint(point));
            if (State == RunState.GameOver)
            {
                if (!GameOverTitleReady) return false;
                ReturnToMenu();
                return true;
            }
            float width = MenuWidth;
            float x = (GuiWidth - width) / 2, y = MenuAnchorY(State == RunState.MainMenu);
            if (MenuPrimaryButtonRect(x, y, width).Contains(point))
            { if (State == RunState.MainMenu) StartRun(); else if (IsPaused) Resume(); else Restart(); return true; }
            if (State == RunState.MainMenu && MenuLevelDownRect(x, y, width).Contains(point))
            { menuLevelDigits = ""; StartLevel--; return true; }
            if (State == RunState.MainMenu && MenuLevelUpRect(x, y, width).Contains(point))
            { menuLevelDigits = ""; StartLevel++; return true; }
            if (DeveloperOptions.Available && DevInvincibleButtonRect(x, y, width, State == RunState.MainMenu, IsPaused).Contains(point))
            { TogglePlayerInvincible(); return true; }
            if (IsPaused && new Rect(x, y + 42, width, 48).Contains(point)) { Restart(); return true; }
            if (State != RunState.MainMenu && new Rect(x, y + (IsPaused ? 102 : 42), width, 48).Contains(point))
            { ReturnToMenu(); return true; }
            return false;
        }

        private static void TogglePlayerInvincible() => DeveloperOptions.PlayerInvincible = !DeveloperOptions.PlayerInvincible;

        public void Tick(float seconds)
        {
            if (IsPaused || State == RunState.MainMenu) return;
            if (State == RunState.GameOver)
            {
                gameOverElapsed += Mathf.Max(0, seconds);
                musicPlayer?.UpdatePlayback();
                return;
            }
            ObserveComboStreak();
            TickComboPresentation(seconds);
            clearBarAnimation.Tick(seconds);
            if (State == RunState.Dying)
            {
                float previousAnimationAge = Mathf.Max(0, deathElapsed - GameOverBlackoutSeconds);
                deathElapsed += Mathf.Max(0, seconds);
                musicPlayer?.UpdatePlayback();
                if (deathElapsed < GameOverBlackoutSeconds)
                {
                    blackout?.Present(GameOverBlackoutOpacity);
                    return;
                }
                if (!deathAnimationStarted)
                {
                    deathAnimationStarted = true;
                    blackout?.Present(1);
                    deathCue = player.BeginFatalDefeat(Progress.Level);
                    hasDeathCue = deathCue != null;
                    Vector3 impactPosition = deathCue != null ? deathCue.transform.position :
                        CharacterVisuals.Ensure(player.gameObject).Body.transform.position;
                    // The session owns cue time, including the remainder of a frame crossing the fade boundary.
                    if (deathCue != null) deathCue.enabled = false;
                    blackout.ShowDeath(deathCue);
                    blackout.BeginImpact(impactPosition, Progress.Level);
                }
                float animationAge = Mathf.Max(0, deathElapsed - GameOverBlackoutSeconds);
                blackout?.Present(GameOverBlackoutOpacity, animationAge);
                if (!deathShattered && animationAge >= PlayerDeathBurst.ShatterSeconds)
                {
                    deathShattered = true;
                    soundEffects?.PlayCue(SoundEffect.PlayerShatter);
                }
                if (deathCue != null) deathCue.Tick(animationAge - previousAnimationAge);
                if (hasDeathCue ? deathCue == null || deathCue.Finished : animationAge >= PlayerDeathBurst.Duration) EndGame();
                return;
            }
            if (CheckPlayerContact()) return;
            feedbackRemaining = Mathf.Max(0, feedbackRemaining - Mathf.Max(0, seconds));
            if (State != RunState.Refilling) return;
            float celebrationSeconds = Mathf.Max(0, seconds);
            bool musicTimed = refillMusicClip != null && musicPlayer != null &&
                musicPlayer.Source.clip == refillMusicClip && musicPlayer.Source.isPlaying &&
                musicPlayer.PlaybackSeconds >= refillMusicSeconds;
            if (musicTimed)
            {
                float now = musicPlayer.PlaybackSeconds;
                celebrationSeconds = now - refillMusicSeconds;
                refillMusicSeconds = now;
            }
            else if (refillMusicClip != null)
            {
                // A stopped, replaced or rewound song releases the old clock without stalling the wave.
                refillMusicClip = null;
                if (ClearCelebration.Finished) refillRemaining = refillPostBarBeats * CurrentBeatDuration;
            }
            if (ClearCelebration.Active)
            {
                bool wasFinished = ClearCelebration.Finished;
                ClearCelebration.Tick(celebrationSeconds);
                player.SetCelebrationColor(ClearCelebration.Color);
                if (!ClearCelebration.Finished) return;
                if (!wasFinished)
                {
                    // An earned life uses the intervening beat before the planned downbeat.
                    if (rewardedLifeSlot >= 0)
                    {
                        player.LifeIcons.ReleaseGain(rewardedLifeSlot, musicPlayer, musicTimed ? refillRewardBeat : null);
                        rewardedLifeSlot = -1;
                    }
                    if (!musicTimed) refillRemaining = musicPlayer != null && musicPlayer.Source.isPlaying ?
                        musicPlayer.SecondsAtBeat(Mathf.Floor(musicPlayer.BeatPosition) + refillPostBarBeats) -
                            musicPlayer.PlaybackSeconds : refillPostBarBeats * CurrentBeatDuration;
                    return;
                }
            }
            if (musicTimed)
            {
                if (refillMusicSeconds < refillSpawnSeconds) return;
            }
            else
            {
                refillRemaining -= Mathf.Max(0, seconds);
                if (refillRemaining > 0) return;
            }
            if (ClearCelebration.Active) player.FinishWaveReturn();
            else if (player.ShotActive || grid.HasDeathEffects) return;
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
            ClearCelebration.Reset();
            refillMusicClip = null;
            player.SetCelebrationColor(null);
            player.RefreshColor();
            WaveSpawned?.Invoke();
        }

        private void BeginRefill()
        {
            var plan = spawner.PlanBatch(Progress.BatchRows);
            if (State == RunState.Playing)
            {
                partialClearCelebration = !grid.AllColorClearBarsFilled;
                refillPostBarBeats = rewardedLifeSlot >= 0 ? 2 : 1;
                refillRewardBeat = null;
                removedBarCount = 0;
                var colors = grid.SeenColors();
                var earned = colors.FindAll(grid.HasColorClearBar);
                float barBeatCount = MinimumColorClearBarSequenceBeats(earned.Count);
                float barStepBeats = ColorClearBarStepBeats(barBeatCount, earned.Count);
                float holdDuration = 0;
                float[] removalBeatDurations = null;
                refillMusicClip = null;
                if (musicPlayer != null && earned.Count > 0)
                {
                    float now = musicPlayer.PlaybackSeconds;
                    float beat = musicPlayer.BeatPositionAtTime(now);
                    float removalStartBeat = beat;
                    if (musicPlayer.Source.isPlaying)
                    {
                        float spawnBeat = RefillDownbeat(beat, barBeatCount, !partialClearCelebration,
                            musicPlayer.CurrentDownbeatOffsetBeats, rewardedLifeSlot >= 0);
                        // Stretch the bar choreography across the whole musical window before the reserved spawn beat.
                        barBeatCount = ColorClearBarSequenceBeats(beat, spawnBeat, refillPostBarBeats);
                        barStepBeats = ColorClearBarStepBeats(barBeatCount, earned.Count);
                        removalStartBeat = spawnBeat - barBeatCount - refillPostBarBeats;
                        refillRewardBeat = spawnBeat - 1;
                        refillMusicClip = musicPlayer.Source.clip;
                        refillMusicSeconds = now;
                        refillSpawnSeconds = musicPlayer.SecondsAtBeat(spawnBeat);
                    }
                    holdDuration = Mathf.Max(0, musicPlayer.SecondsAtBeat(removalStartBeat) - now);
                    removalBeatDurations = new float[earned.Count];
                    for (int i = 0; i < earned.Count; i++)
                    {
                        float stepStart = removalStartBeat + i * barStepBeats;
                        removalBeatDurations[i] = musicPlayer.SecondsAtBeat(stepStart + barStepBeats) -
                            musicPlayer.SecondsAtBeat(stepStart);
                    }
                }
                float refillDuration = holdDuration + FullSetCelebration.DurationForBars(barBeatCount, CurrentBeatDuration);
                if (removalBeatDurations != null)
                {
                    refillDuration = holdDuration;
                    for (int i = 0; i < removalBeatDurations.Length; i++)
                        refillDuration += Mathf.Max(.0001f, removalBeatDurations[i]);
                }
                if (earned.Count > 0)
                {
                    ClearCelebration.Begin(earned, plan, refillDuration, removalBeatDurations, holdDuration);
                    refillDuration = ClearCelebration.PlaybackDuration;
                }
                else ClearCelebration.Reset();
                if (rewardedLifeSlot >= 0) player.LifeIcons.ReserveGain(rewardedLifeSlot);
                // A developer override can exclude every earned color. Reserve the final reward color anyway.
                if (plan != null && plan.Length > 0 && ClearCelebration.LastColor.HasValue &&
                    System.Array.IndexOf(plan, ClearCelebration.LastColor.Value) < 0)
                {
                    var replaced = plan[0];
                    if (ClearCelebration.LastColor == EnemyColor.Orange) plan[0] = EnemyColor.Orange;
                    else for (int i = 0; i < plan.Length; i++)
                        if (plan[i] == replaced) plan[i] = ClearCelebration.LastColor.Value;
                }
                player.SetCelebrationColor(ClearCelebration.Active ? ClearCelebration.Color : (Color?)null);
                BeginBatch(plan, refillDuration);
                return;
            }
            BeginBatch(plan, 0);
        }

        private float CurrentBeatDuration => musicPlayer != null ? musicPlayer.BeatDuration : FullSetCelebration.StepDuration;

        public static float SecondsToNextBeat(float beatPosition, float beatDuration) =>
            (Mathf.Floor(beatPosition) + 1 - beatPosition) * Mathf.Max(.0001f, beatDuration);

        public static float RefillDownbeat(float clearBeat, float minimumBarBeats, bool allBarsFull,
            int offset = GameplayMusicPlayer.DownbeatOffsetBeats, bool lifeAwarded = false)
        {
            // Pick the downbeat first; the bar choreography stretches into the available space.
            float earliestSpawn = clearBeat + Mathf.Max(0, minimumBarBeats) + 1 + (lifeAwarded ? 1 : 0);
            return Mathf.Ceil((earliestSpawn - offset) / GameplayMusicPlayer.BeatsPerMeasure) *
                GameplayMusicPlayer.BeatsPerMeasure + offset;
        }

        public static float MinimumColorClearBarSequenceBeats(int earnedBars) =>
            earnedBars > 0 ? 1 : 0;

        public static float ColorClearBarSequenceBeats(float clearBeat, float spawnBeat, int postBarBeats) =>
            Mathf.Max(0, spawnBeat - clearBeat - Mathf.Max(0, postBarBeats));

        public static float ColorClearBarStepBeats(float sequenceBeats, int earnedBars) =>
            earnedBars > 0 ? Mathf.Max(.0001f, sequenceBeats) / earnedBars : 0;

        private void BeginBatch(EnemyColor[] plan, float duration)
        {
            if (State != RunState.Playing) return;
            State = RunState.Refilling;
            refillRemaining = Mathf.Max(0, duration);
            if (ClearCelebration.Active) refillRemaining = 0;
            nextBatch = plan;
            player.PrepareNextWave(nextBatch, ClearCelebration.LastColor);
            Suspend(true);
        }

        public float ContactFraction(float enemyTravelRelativeToPlayer)
        {
            if (grid == null || State != RunState.Playing || IsPaused || player == null || !player.Alive ||
                player.Invulnerable || DeveloperOptions.PlayerInvincible) return 1;
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
            if (State == RunState.Playing && !IsPaused && player != null && !DeveloperOptions.PlayerInvincible &&
                PlayerRowIsFull())
            {
                BeginPlayerDeath();
                return true;
            }
            if (ContactFraction(0) != 0) return false;
            return player != null && player.Hit();
        }

        private bool PlayerRowIsFull()
        {
            if (grid == null || spawner == null) return false;
            var target = player.HitBounds;
            for (int row = 0; row < GridModel.Rows; row++)
            {
                int occupied = 0;
                for (int column = 0; column < GridModel.Columns; column++)
                {
                    var cell = grid.Model.At(column, row);
                    var enemy = cell != null ? grid.View(cell.Id) : null;
                    if (enemy == null || !enemy.isActiveAndEnabled) continue;
                    var bounds = enemy.HitBounds;
                    if (bounds.max.y >= target.min.y && bounds.min.y <= target.max.y) occupied++;
                }
                if (occupied >= spawner.CurrentRowWidth) return true;
            }
            return false;
        }

        public void FinishContactMove(float fraction)
        {
            if (fraction < 1) player.Hit();
            else CheckPlayerContact();
        }

        private void BeginPlayerDeath()
        {
            if (DeveloperOptions.PlayerInvincible) return;
            if (State != RunState.Playing && State != RunState.Refilling) return;
            FadeComboOnDeath();
            GameOverBlackoutSeconds = musicPlayer != null ? musicPlayer.SecondsForBeats(GameOverBlackoutBeats) :
                GameOverBlackoutBeats * CurrentBeatDuration;
            State = RunState.Dying;
            deathElapsed = 0;
            deathShattered = false;
            deathAnimationStarted = false;
            musicPlayer?.UpdatePlayback();
            // Prepare the sample before the fracture frame so synthesis cannot delay the impact.
            soundEffects?.GetClip(SoundEffect.PlayerShatter);
            player.PrepareFatalDefeat();
            blackout = GameOverBlackout.Create(transform, null);
            blackout.HoldPlayer(CharacterVisuals.Ensure(player.gameObject).Root);
            Suspend(true);
        }

        private void OnDestroy()
        {
            if (blackout != null)
            {
                if (Application.isPlaying) Destroy(blackout.gameObject);
                else DestroyImmediate(blackout.gameObject);
            }
            if (deathCue == null) return;
            if (Application.isPlaying) Destroy(deathCue.gameObject);
            else DestroyImmediate(deathCue.gameObject);
        }

        private void EndGame()
        {
            if (State == RunState.GameOver) return;
            State = RunState.GameOver;
            gameOverElapsed = 0;
            RecordLeaderboard();
            Suspend(true);
            if (blackout == null) blackout = GameOverBlackout.Create(transform, null);
            blackout.Present(GameOverBlackoutOpacity);
            musicPlayer?.UpdatePlayback();
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
            var previousMatrix = GUI.matrix;
            float scale = GuiScale;
            if (!Mathf.Approximately(scale, 1))
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            try { DrawGui(); }
            finally { GUI.matrix = previousMatrix; }
        }

        private void DrawGui()
        {
            if (State != RunState.MainMenu) DrawProgress(GameOverScoreOpacity);
            if (State == RunState.GameOver) { DrawGameOver(); return; }
            if (!IsPaused && (State == RunState.Playing || State == RunState.Refilling))
            {
                var pause = PauseButton;
                if (GUI.Button(pause, new GUIContent("", "Pause (P)"))) Pause();
                GUI.DrawTexture(new Rect(pause.x + 8, pause.y + 13, 4, 18), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(pause.x + 15, pause.y + 13, 4, 18), Texture2D.whiteTexture);
                GUI.Label(new Rect(pause.x + 22, pause.y + 12, 25, 22), "(P)",
                    new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter });
            }
            if (!IsPaused && State != RunState.MainMenu && State != RunState.GameOver) return;
            var previous = GUI.color;
            GUI.color = new Color(0.015f, 0.025f, 0.04f, State == RunState.MainMenu ? .65f : .88f);
            GUI.DrawTexture(new Rect(0, 0, GuiWidth, GuiHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float width = MenuWidth;
            float x = (GuiWidth - width) / 2;
            bool menu = State == RunState.MainMenu;
            float y = MenuAnchorY(menu);
            var heading = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            heading.normal.textColor = Color.white;
            if (menu) DrawMainMenuTitle(y);
            else GUI.Label(new Rect(x, y - 120, width, 48), IsPaused ? "Paused" : "Game Over", heading);
            if (menu) DrawEnemyPreview(new Rect(x, y - 136, width, 82));
            var scoreLabel = new GUIStyle(heading) { fontSize = 18 };
            if (!menu) GUI.Label(new Rect(x, y - 70, width, 26), "Score " + Progress.Score.ToString("N0"), scoreLabel);
            if (!menu && State == RunState.GameOver && LastLeaderboardRank > 0)
                GUI.Label(new Rect(x, y - 46, width, 22), "Leaderboard #" + LastLeaderboardRank, scoreLabel);
            DrawLeaderboard(new Rect(x, y + LeaderboardOffset(menu, IsPaused), width, 150), menu || State == RunState.GameOver);
            var button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            if (GUI.Button(MenuPrimaryButtonRect(x, y, width), menu ? "Play (Enter)" : IsPaused ? "Resume (P / Enter)" : "Restart (R / Enter)", button))
            { if (menu) StartRun(); else if (IsPaused) Resume(); else Restart(); }
            if (menu) DrawStartLevelSelector(x, y, width, button);
            if (IsPaused && GUI.Button(new Rect(x, y + 42, width, 48), "Restart (R)", button)) Restart();
            if (!menu && GUI.Button(new Rect(x, y + (IsPaused ? 102 : 42), width, 48), "Main Menu (M)", button)) ReturnToMenu();
            if (DeveloperOptions.Available)
            {
                string label = "Invincible: " + (DeveloperOptions.PlayerInvincible ? "ON" : "OFF") + " (I)";
                if (GUI.Button(DevInvincibleButtonRect(x, y, width, menu, IsPaused), label, button)) TogglePlayerInvincible();
            }
            GUI.color = previous;
        }

        public const string GameOverTitle = "GAME OVER";
        public static Rect GameOverTitleRect(float screenWidth, float screenHeight)
        {
            float width = Mathf.Min(560, screenWidth - 32), height = Mathf.Min(96, screenHeight - 32);
            float top = Mathf.Max(16, screenHeight * .4f - height / 2);
            return new Rect((screenWidth - width) / 2, top, width, height);
        }

        public static int FitGameOverTitle(GUIStyle style, Rect rect)
        {
            var content = new GUIContent(GameOverTitle);
            while (style.fontSize > 20 &&
                (style.CalcSize(content).x > rect.width || style.CalcHeight(content, rect.width) > rect.height))
                style.fontSize--;
            return style.fontSize;
        }

        private void DrawGameOver()
        {
            float opacity = GameOverTitleOpacity;
            if (opacity <= 0) return;
            var previousColor = GUI.color;
            GUI.color = new Color(1, 1, 1, opacity);
            var title = CreateTitleStyle();
            var rect = GameOverTitleRect(GuiWidth, GuiHeight);
            FitGameOverTitle(title, rect);
            GUI.Label(rect, GameOverTitle, title);
            GUI.color = previousColor;
        }

        private static float MenuAnchorY(bool menu)
        {
            return MenuAnchorYFor(GuiHeight, menu);
        }

        public static float MenuAnchorYFor(float guiHeight, bool menu)
        {
            float center = guiHeight / 2f;
            return menu ? Mathf.Min(center, Mathf.Max(190, guiHeight - 278)) : center;
        }

        private static float MenuWidth => Mathf.Min(300, GuiWidth - 32);
        private static Rect MenuPrimaryButtonRect(float x, float y, float width) => new Rect(x, y - 24, width, 54);
        private static Rect MenuLevelDownRect(float x, float y, float width) => new Rect(x, y + 42, 82, 42);
        private static Rect MenuLevelValueRect(float x, float y, float width) => new Rect(x + 86, y + 42, width - 172, 42);
        private static Rect MenuLevelUpRect(float x, float y, float width) => new Rect(x + width - 82, y + 42, 82, 42);
        private static float LeaderboardOffset(bool menu, bool paused) =>
            DeveloperOptions.Available ? (menu ? 138 : paused ? 208 : 104) : (menu ? 112 : paused ? 164 : 104);
        private static Rect DevInvincibleButtonRect(float x, float y, float width, bool menu, bool paused) =>
            new Rect(x, y + (menu ? 90 : paused ? 154 : 96), width, 40);
        public const string MainMenuTitle = "BASS INVADERS";
        public const string MainMenuSubtitle = "THE RHYTHM IS OUT THERE";
        public static float GuiScaleFor(bool mobilePlatform, float screenWidth, float screenHeight)
        {
            if (!mobilePlatform) return 1;
            float shortest = Mathf.Min(screenWidth, screenHeight);
            if (shortest >= 900) return LargePhoneGuiScale;
            if (shortest >= 700) return MediumPhoneGuiScale;
            return SmallPhoneGuiScale;
        }
        private static float GuiScale => GuiScaleFor(Application.isMobilePlatform, Screen.width, Screen.height);
        private static float GuiWidth => Screen.width / GuiScale;
        private static float GuiHeight => Screen.height / GuiScale;
        private static Vector2 ToGuiPoint(Vector2 point) => point / GuiScale;
        private static Vector3 ToGuiPoint(Vector3 point) => point / GuiScale;
        private static Vector2 FlipGuiY(Vector2 point) => new Vector2(point.x, GuiHeight - point.y);
        public static Rect MainMenuTitleRect(float screenWidth, float screenHeight, float menuAnchorY)
        {
            float width = Mathf.Min(560, Mathf.Max(120, screenWidth - 32));
            float previewTop = menuAnchorY - 136;
            float bottom = Mathf.Max(28, previewTop - 8);
            float desiredHeight = screenHeight < 420 ? 62 : screenHeight < 700 ? 78 : 96;
            float y = Mathf.Max(12, bottom - desiredHeight);
            return new Rect((screenWidth - width) / 2, y, width, bottom - y);
        }

        public static Rect MainMenuTitleLineRect(Rect titleBlock)
        {
            float height = titleBlock.height * .64f;
            return new Rect(titleBlock.x, titleBlock.y, titleBlock.width, height);
        }

        public static Rect MainMenuSubtitleLineRect(Rect titleBlock)
        {
            var titleLine = MainMenuTitleLineRect(titleBlock);
            return new Rect(titleBlock.x, titleLine.yMax, titleBlock.width, titleBlock.yMax - titleLine.yMax);
        }

        public static int FitMainMenuTitle(GUIStyle style, Rect rect)
        {
            var content = new GUIContent(MainMenuTitle);
            while (style.fontSize > 20 &&
                (style.CalcSize(content).x > rect.width || style.CalcHeight(content, rect.width) > rect.height))
                style.fontSize--;
            return style.fontSize;
        }

        public static int FitMainMenuSubtitle(GUIStyle style, Rect rect)
        {
            var content = new GUIContent(MainMenuSubtitle);
            while (style.fontSize > 11 &&
                (style.CalcSize(content).x > rect.width || style.CalcHeight(content, rect.width) > rect.height))
                style.fontSize--;
            return style.fontSize;
        }

        public static Rect ComboMultiplierRestRect(float playfieldX, float textWidth, float headingTop, int viewHeight = 960)
        {
            float width = Mathf.Min(118, textWidth * .35f);
            float height = viewHeight < 400 ? 18 : 28;
            var row = PlayerLifeIcons.RowRect(playfieldX, headingTop, viewHeight);
            return new Rect(playfieldX + textWidth - width, row.center.y - height / 2, width, height);
        }

        public static List<EnemyColor> ColorClearBarColors(EnemyRowSpawner spawner, EnemyGrid grid)
        {
            var colors = spawner != null ? spawner.UnlockedColors() : grid != null ? grid.SeenColors() : new List<EnemyColor>();
            if (spawner != null)
                colors.RemoveAll(color => spawner.Prefab(color) == null ||
                    spawner.Prefab(color).GetComponentInChildren<SpriteRenderer>() == null);
            colors.Sort();
            return colors;
        }

        public static Rect ColorClearBarSlotRect(float x, float bottom, float width, int count, int index)
        {
            if (count <= 0) return new Rect(x, bottom, 0, 0);
            float segmentWidth = width / count;
            return new Rect(x + index * segmentWidth, bottom, Mathf.Max(1, segmentWidth - 2), 5);
        }

        public static float ColorClearBarMusicLevel(float beatPosition, int slot)
        {
            float primary = GameplayMusicPlayer.BeatPulse(beatPosition - slot * .17f);
            float offbeat = GameplayMusicPlayer.BeatPulse(beatPosition + slot * .11f, true) * .55f;
            return Mathf.Clamp01(.18f + .82f * Mathf.Max(primary, offbeat));
        }

        public static Rect ColorClearBarLevelRect(Rect resting, float beatPosition, int slot)
        {
            float level = ColorClearBarMusicLevel(beatPosition, slot);
            float height = Mathf.Max(resting.height, Mathf.Lerp(resting.height * 1.2f, 24, level));
            return new Rect(resting.x, resting.yMax - height, resting.width, height);
        }

        public static Color ColorClearBarTint(EnemyColor color, bool powered, float opacity = 1)
        {
            var source = EnemyPalette.Get(color);
            if (powered) return new Color(source.r, source.g, source.b, .95f * opacity);
            return new Color(Mathf.Lerp(.08f, source.r, .42f), Mathf.Lerp(.1f, source.g, .42f),
                Mathf.Lerp(.12f, source.b, .42f), .28f * opacity);
        }

        public static Rect ComboMultiplierDisplayRect(Rect resting, float screenWidth, float screenHeight, float elapsed)
        {
            if (elapsed >= ComboMilestoneAnimationSeconds) return resting;
            var center = new Vector2(screenWidth * .5f, screenHeight * .5f);
            float scale;
            if (elapsed < ComboMilestonePulseSeconds)
            {
                float pulse = Mathf.Clamp01(elapsed / ComboMilestonePulseSeconds);
                scale = 2.65f + .25f * Mathf.Sin(pulse * Mathf.PI * 2f);
            }
            else
            {
                float shrink = Mathf.Clamp01((elapsed - ComboMilestonePulseSeconds) /
                    (ComboMilestoneAnimationSeconds - ComboMilestonePulseSeconds));
                float eased = Mathf.SmoothStep(0, 1, shrink);
                center = Vector2.Lerp(center, resting.center, eased);
                scale = Mathf.Lerp(2.45f, 1, eased);
            }
            return new Rect(center.x - resting.width * scale * .5f, center.y - resting.height * scale * .5f,
                resting.width * scale, resting.height * scale);
        }

        private void DrawStartLevelSelector(float x, float y, float width, GUIStyle button)
        {
            var value = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            value.normal.textColor = Color.white;
            var stepButton = new GUIStyle(button) { fontSize = 14 };
            if (GUI.Button(MenuLevelDownRect(x, y, width), "- (Left)", stepButton)) { menuLevelDigits = ""; StartLevel--; }
            GUI.Label(MenuLevelValueRect(x, y, width), "Level " + StartLevel, value);
            if (GUI.Button(MenuLevelUpRect(x, y, width), "+ (Right)", stepButton)) { menuLevelDigits = ""; StartLevel++; }
        }

        private GUIStyle CreateTitleStyle()
        {
            var title = new GUIStyle(GUI.skin.label)
            {
                font = ComboFont,
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset()
            };
            title.normal.textColor = Color.white;
            return title;
        }

        private void DrawMainMenuTitle(float menuAnchorY)
        {
            var title = CreateTitleStyle();
            var subtitle = new GUIStyle(title)
            {
                fontSize = 18
            };

            var block = MainMenuTitleRect(GuiWidth, GuiHeight, menuAnchorY);
            var titleRect = MainMenuTitleLineRect(block);
            var subtitleRect = MainMenuSubtitleLineRect(block);
            FitMainMenuTitle(title, titleRect);
            FitMainMenuSubtitle(subtitle, subtitleRect);
            GUI.Label(titleRect, MainMenuTitle, title);
            GUI.Label(subtitleRect, MainMenuSubtitle, subtitle);
        }

        private void DrawEnemyPreview(Rect area)
        {
            if (spawner == null) return;
            float regularSize = Mathf.Min(34, (area.width - 5 * 10) / 6f);
            float specialSize = Mathf.Min(30, (area.width - 4 * 12) / 5f);
            DrawPreviewRow(PreviewRegularColors, area.x + (area.width - (regularSize * PreviewRegularColors.Length + 10 * (PreviewRegularColors.Length - 1))) / 2,
                area.y, regularSize, 10, false);
            DrawPreviewRow(PreviewSpecialColors, area.x + (area.width - (specialSize * PreviewSpecialColors.Length + 12 * (PreviewSpecialColors.Length - 1))) / 2,
                area.y + 44, specialSize, 12, true);
        }

        private void DrawPreviewRow(EnemyColor[] colors, float x, float y, float size, float gap, bool special)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                var sprite = spawner.AppearanceSprite(color, special);
                DrawPreviewSprite(new Rect(x + i * (size + gap), y, size, size), sprite, EnemyPalette.Get(color));
            }
        }

        private void ObserveComboStreak()
        {
            int streak = Progress.ComboStreak;
            if (streak == observedComboStreak) return;
            int previous = observedComboStreak;
            bool advanced = streak > observedComboStreak;
            observedComboStreak = streak;
            if (streak <= 0)
            {
                comboMilestoneMultiplier = 0;
                comboMilestoneElapsed = ComboMilestoneAnimationSeconds;
                if (previous > 0) ComboBreak.Begin(previous + 1, ComboColor);
                return;
            }
            comboDeathFadeElapsed = ComboDeathFadeSeconds;
            if (advanced && IsComboMilestone(Progress.ComboMultiplier)) BeginComboMilestone();
        }

        private void FadeComboOnDeath()
        {
            // Cancelling an in-flight tongue can reset the combo before the hit event arrives.
            int multiplier = Progress.ComboMultiplier > 1 ? Progress.ComboMultiplier : ComboBreak.Active ? ComboBreak.Multiplier : 1;
            if (multiplier > 1)
            {
                ComboDeathMultiplier = multiplier;
                ComboDeathColor = Progress.ComboMultiplier > 1 ? ComboColor : ComboBreak.SourceColor;
                comboDeathFadeElapsed = 0;
            }
            Progress.ResetCombo();
            observedComboStreak = Progress.ComboStreak;
            comboMilestoneMultiplier = 0;
            comboMilestoneElapsed = ComboMilestoneAnimationSeconds;
            ComboBreak.Reset();
        }

        private void BeginComboMilestone()
        {
            comboMilestoneMultiplier = Progress.ComboMultiplier;
            comboMilestoneElapsed = 0;
        }

        private void TickComboPresentation(float seconds)
        {
            ComboBreak.Tick(seconds);
            comboDeathFadeElapsed = Mathf.Min(ComboDeathFadeSeconds, comboDeathFadeElapsed + Mathf.Max(0, seconds));
            if (comboMilestoneMultiplier <= 1) return;
            comboMilestoneElapsed = Mathf.Min(ComboMilestoneAnimationSeconds,
                comboMilestoneElapsed + Mathf.Max(0, seconds));
        }

        private void ResetComboPresentation()
        {
            observedComboStreak = Progress.ComboStreak;
            comboMilestoneMultiplier = 0;
            comboMilestoneElapsed = ComboMilestoneAnimationSeconds;
            ComboBreak.Reset();
            ComboDeathMultiplier = 0;
            ComboDeathColor = Color.white;
            comboDeathFadeElapsed = ComboDeathFadeSeconds;
            ComboColor = Color.white;
        }

        public static bool IsComboMilestone(int multiplier) =>
            multiplier == 5 || multiplier == 25 || multiplier >= 75 && (multiplier - 25) % 50 == 0;
        public static bool ShouldDrawComboMultiplier(int multiplier) => multiplier > 1;
        public static Rect LevelProgressFillRect(Rect bar, float fraction) =>
            new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fraction), bar.height);
        public static Rect LevelProgressBankedRect(Rect bar, float fraction) =>
            new Rect(bar.x, bar.y + bar.height * .55f, bar.width * Mathf.Clamp01(fraction), Mathf.Max(2, bar.height * .45f));
        public static Rect LevelProgressSolidRect(Rect bar, bool pending, float pulse)
        {
            float expansion = pending ? 3 * Mathf.Clamp01(pulse) : 0;
            return new Rect(bar.x - expansion, bar.y - expansion,
                bar.width + expansion * 2, bar.height + expansion * 2);
        }
        public static Rect LevelProgressGlowRect(Rect bar, float pulse)
        {
            float expansion = Mathf.Lerp(3, 8, Mathf.Clamp01(pulse));
            return new Rect(bar.x - expansion, bar.y - expansion * .55f,
                bar.width + expansion * 2, bar.height + expansion * 1.1f);
        }

        private static void DrawPreviewSprite(Rect rect, Sprite sprite, Color tint)
        {
            if (sprite == null || sprite.texture == null) return;
            var previous = GUI.color;
            GUI.color = tint;
            var texture = sprite.texture;
            var spriteRect = sprite.rect;
            var texCoords = new Rect(spriteRect.x / texture.width, spriteRect.y / texture.height,
                spriteRect.width / texture.width, spriteRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(rect, texture, texCoords, true);
            GUI.color = previous;
        }

        private void DrawProgress(float opacity = 1)
        {
            var camera = Camera.main;
            if (camera == null || opacity <= 0) return;
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, opacity);
            var left = ToGuiPoint(camera.WorldToScreenPoint(new Vector3(-3, 5.3f, 0)));
            var right = ToGuiPoint(camera.WorldToScreenPoint(new Vector3(3, 5.3f, 0)));
            float width = right.x - left.x - 20;
            float x = left.x + 10, y = GuiHeight - left.y;
            int viewHeight = Mathf.RoundToInt(camera.pixelHeight / GuiScale);
            var lifeRow = PlayerLifeIcons.RowRect(x, y, viewHeight);
            float progressY = y + PlayerLifeIcons.ProgressOffset(viewHeight);
            var label = new GUIStyle(GUI.skin.label) { fontSize = viewHeight < 400 ? 11 : viewHeight < 600 ? 13 : 16, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            float textWidth = width - 54;
            float beat = musicPlayer != null ? musicPlayer.BeatPosition : Time.time / FullSetCelebration.StepDuration;
            float pulse = GameplayMusicPlayer.BeatPulse(beat);
            var progressBar = new Rect(x, progressY, width, 9);
            var solidBar = LevelProgressSolidRect(progressBar, Progress.LevelUpPending, pulse);
            GUI.color = new Color(.3f, .4f, .45f, .7f * opacity);
            GUI.DrawTexture(progressBar, Texture2D.whiteTexture);
            var progressColor = ProgressBarColor;
            if (Progress.LevelUpPending)
            {
                var glowColor = progressColor;
                glowColor.a = Mathf.Lerp(.16f, .46f, pulse) * opacity;
                GUI.color = glowColor;
                GUI.DrawTexture(LevelProgressGlowRect(progressBar, pulse), Texture2D.whiteTexture);
                progressColor = Color.Lerp(progressColor, Color.white, Mathf.Lerp(.2f, .48f, pulse));
            }
            progressColor.a *= opacity;
            GUI.color = progressColor;
            GUI.DrawTexture(LevelProgressFillRect(solidBar, Progress.ActiveLevelProgressFraction), Texture2D.whiteTexture);
            if (Progress.LevelUpPending && Progress.BankedLevelProgressFraction > 0)
            {
                var banked = Color.Lerp(progressColor, Color.white, .45f);
                banked.a = Mathf.Lerp(.65f, 1f, pulse) * opacity;
                GUI.color = banked;
                GUI.DrawTexture(LevelProgressBankedRect(solidBar, Progress.BankedLevelProgressFraction), Texture2D.whiteTexture);
            }
            var colors = ColorClearBarColors(spawner, grid);
            float bottom = GuiHeight - ToGuiPoint(camera.WorldToScreenPoint(new Vector3(0, -5.25f, 0))).y;
            for (int i = 0; i < colors.Count; i++)
            {
                var color = colors[i];
                var resting = ColorClearBarSlotRect(x, bottom, width, colors.Count, i);
                bool powered = grid != null && grid.HasColorClearBar(color) && ClearCelebration.Visible(color);
                if (!powered)
                {
                    DrawPoweredDownColorClearBar(color, resting, opacity);
                    continue;
                }
                var animated = ClearCelebration.Active ? ClearCelebration.Present(colors[i], resting) :
                    clearBarAnimation.Present(colors[i], resting);
                // The enlarged bar stays inside the playfield, including the end slots.
                animated.width = Mathf.Min(animated.width, width);
                animated.x = Mathf.Clamp(animated.x, x, x + width - animated.width);
                DrawPoweredColorClearBar(color, animated, opacity, beat, i);
            }
            var comboRect = ComboMultiplierRestRect(x, textWidth, y, viewHeight);
            int comboFontSize = viewHeight < 400 ? 11 : viewHeight < 600 ? 13 : 16;
            if (State == RunState.Playing || State == RunState.Refilling || State == RunState.Dying)
            {
                var comboStyle = new GUIStyle(GUI.skin.label)
                {
                    font = ComboFont,
                    fontSize = comboFontSize,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleRight,
                    padding = new RectOffset(),
                    wordWrap = false,
                    clipping = TextClipping.Clip
                };
                comboStyle.normal.textColor = Color.white;
                if (ComboDeathOpacity > 0 || ShouldDrawComboMultiplier(Progress.ComboMultiplier))
                    DrawComboMultiplier(comboRect, comboStyle, opacity);
                ComboBreak.Draw(comboRect, comboStyle, opacity, GuiWidth, GuiHeight);
            }
            GUI.color = new Color(1, 1, 1, opacity);
            label.fontSize = viewHeight < 600 ? 11 : 13;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.UpperLeft;
            label.wordWrap = true;
            if (player != null) player.LifeIcons.Draw(player, lifeRow, opacity, GuiWidth, GuiHeight);
            // Keep the heading above life artwork, including traveling reward/loss animations.
            GUI.color = new Color(1, 1, 1, opacity);
            var heading = new GUIStyle(label) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.UpperLeft };
            GUI.Label(new Rect(x, y, textWidth * .65f, 26), "SCORE " + Progress.Score.ToString("N0"), heading);
            heading.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(x + textWidth * .65f, y, textWidth * .35f, 26), "LEVEL " + Progress.Level, heading);
            string status = "";
            if (player != null && player.MagicCharges > 0)
                status += (status.Length > 0 ? "  |  " : "") + "MAGIC " + player.MagicCharges;
            if (feedbackRemaining > 0) status += (status.Length > 0 ? "  |  " : "") + feedback;
            if (status.Length > 0)
            {
                var statusRect = new Rect(x, progressY + 12, width, 40);
                while (label.fontSize > 9 && label.CalcHeight(new GUIContent(status), width) > statusRect.height)
                    label.fontSize--;
                GUI.Label(statusRect, status, label);
            }
            GUI.color = previous;
        }

        private static void DrawPoweredDownColorClearBar(EnemyColor color, Rect resting, float opacity)
        {
            var shadow = ColorClearBarTint(color, false, opacity);
            shadow.a *= .5f;
            GUI.color = shadow;
            GUI.DrawTexture(new Rect(resting.x, resting.y + 1, resting.width, resting.height), Texture2D.whiteTexture);
            GUI.color = ColorClearBarTint(color, false, opacity);
            GUI.DrawTexture(resting, Texture2D.whiteTexture);
        }

        private static void DrawPoweredColorClearBar(EnemyColor color, Rect resting, float opacity, float beatPosition, int slot)
        {
            var level = ColorClearBarLevelRect(resting, beatPosition, slot);
            float pulse = ColorClearBarMusicLevel(beatPosition, slot);
            var tint = ColorClearBarTint(color, true, opacity);
            var glow = tint;
            glow.a = .14f * opacity * Mathf.Lerp(.55f, 1, pulse);
            GUI.color = glow;
            GUI.DrawTexture(Expanded(level, 8, 7), Texture2D.whiteTexture);
            glow.a *= .65f;
            GUI.color = glow;
            GUI.DrawTexture(Expanded(level, 4, 4), Texture2D.whiteTexture);
            GUI.color = tint;
            GUI.DrawTexture(level, Texture2D.whiteTexture);
            var shine = Color.Lerp(tint, Color.white, .65f);
            shine.a = .35f * opacity;
            GUI.color = shine;
            GUI.DrawTexture(new Rect(level.x, level.y, level.width, Mathf.Min(3, level.height * .28f)), Texture2D.whiteTexture);
        }

        private static Rect Expanded(Rect rect, float x, float y) =>
            new Rect(rect.x - x, rect.y - y, rect.width + x * 2, rect.height + y * 2);

        private void DrawComboMultiplier(Rect resting, GUIStyle baseStyle, float opacity)
        {
            bool fading = ComboDeathOpacity > 0;
            bool animating = !fading && comboMilestoneMultiplier > 1 && comboMilestoneElapsed < ComboMilestoneAnimationSeconds;
            int multiplier = fading ? ComboDeathMultiplier : Progress.ComboMultiplier;
            var color = fading ? ComboDeathColor : ComboColor;
            if (fading) opacity *= ComboDeathOpacity;
            var rect = animating ? ComboMultiplierDisplayRect(resting, GuiWidth, GuiHeight, comboMilestoneElapsed) : resting;
            float scale = Mathf.Max(1, rect.height / resting.height);
            var style = new GUIStyle(baseStyle)
            {
                alignment = animating ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight,
                fontSize = Mathf.RoundToInt(baseStyle.fontSize * Mathf.Min(3, scale))
            };
            style.normal.textColor = Color.white;
            string text = "x" + multiplier;
            while (style.fontSize > 9 && style.CalcSize(new GUIContent(text)).x > rect.width) style.fontSize--;
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, .7f * opacity);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);
            GUI.color = new Color(color.r, color.g, color.b, opacity);
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        private void DrawLeaderboard(Rect area, bool visible)
        {
            if (!visible || area.height < 44) return;
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
            for (int i = 0; i < entries.Count && 24 + (i + 1) * 20 <= area.height; i++)
            {
                string row = (i + 1) + ". " + entries[i].Score.ToString("N0") + "  Level " + entries[i].Level;
                label.fontSize = 13;
                while (label.fontSize > 8 && label.CalcSize(new GUIContent(row)).x > area.width - 48) label.fontSize--;
                GUI.Label(new Rect(area.x + 24, area.y + 24 + i * 20, area.width - 48, 20), row, label);
            }
        }
    }
}
