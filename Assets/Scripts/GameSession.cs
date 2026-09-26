using UnityEngine;
using UnityEngine.SceneManagement;

namespace CandyCruisers
{
    [RequireComponent(typeof(EnemyGrid), typeof(EnemyRowSpawner), typeof(EnemyGridMovement))]
    public sealed class GameSession : MonoBehaviour
    {
        public enum RunState { Playing, Refilling, GameOver }
        [SerializeField, Min(0.1f)] private float refillDelay = 1f;
        [SerializeField] private PlayerMovement player;
        private EnemyGrid grid;
        private EnemyRowSpawner spawner;
        private EnemyGridMovement movement;
        private float refillRemaining;
        private EnemyColor[] nextBatch;
        private bool restarting;
        public RunState State { get; private set; }
        public RunProgress Progress { get; } = new RunProgress();
        public Color ProgressBarColor => player != null ? player.DisplayColor : Color.gray;
        private float feedbackRemaining;
        private string feedback = "";
        private void OnMatchCleared(int count, bool fleetCleared)
        {
            int before = Progress.Level;
            long earned = Progress.RegisterClear(count, fleetCleared);
            feedback = "+" + earned.ToString("N0") + (fleetCleared ? "  Fleet cleared" : "");
            if (Progress.Level > before)
            {
                feedback = "Level " + Progress.Level;
                foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
                    if (RunProgress.UnlockLevel(color) > before && RunProgress.UnlockLevel(color) <= Progress.Level)
                        feedback += "  /  " + color + " unlocked";
            }
            feedbackRemaining = 3f;
        }
        public void Configure(PlayerMovement controller) => player = controller;
        private void Start()
        {
            if (grid.Model.Count == 0 && State == RunState.Playing)
                BeginBatch(spawner.PlanOpening());
        }

        private void OnEnable()
        {
            grid = GetComponent<EnemyGrid>();
            spawner = GetComponent<EnemyRowSpawner>();
            movement = GetComponent<EnemyGridMovement>();
            grid.FleetCleared += BeginRefill;
            grid.MatchCleared += OnMatchCleared;
            spawner.BottomReached += EndGame;
        }
        private void OnDisable()
        {
            if (grid != null) grid.FleetCleared -= BeginRefill;
            if (grid != null) grid.MatchCleared -= OnMatchCleared;
            if (spawner != null) spawner.BottomReached -= EndGame;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
            Tick(Time.deltaTime);
        }

        public void Tick(float seconds)
        {
            feedbackRemaining = Mathf.Max(0, feedbackRemaining - Mathf.Max(0, seconds));
            if (State != RunState.Refilling) return;
            refillRemaining -= Mathf.Max(0, seconds);
            if (refillRemaining > 0 || player.ShotActive) return;
            movement.ResetSweep();
            if (!spawner.SpawnBatch(nextBatch))
            {
                Debug.LogError("Cannot create the next fleet batch.", this);
                EndGame();
                return;
            }
            State = RunState.Playing;
            Suspend(false);
            player.RefreshColor();
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
            foreach (var missile in FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None))
            {
                missile.gameObject.SetActive(false);
                Destroy(missile.gameObject);
            }
        }

        private void EndGame()
        {
            if (State == RunState.GameOver) return;
            State = RunState.GameOver;
            Suspend(true);
        }

        private void Suspend(bool suspended)
        {
            movement.enabled = !suspended;
            grid.enabled = !suspended;
            spawner.enabled = !suspended;
            player.ControlsLocked = suspended && State == RunState.GameOver;
            if (player.ControlsLocked) player.StopActions();
            foreach (var ability in GetComponentsInChildren<EnemyAbilities>()) ability.Suspended = suspended;
            foreach (var missile in FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None)) missile.Suspended = suspended;
        }

        public void Restart()
        {
            if (restarting) return;
            restarting = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnGUI()
        {
            DrawProgress();
            if (State != RunState.GameOver) return;
            var previous = GUI.color;
            GUI.color = new Color(0.015f, 0.025f, 0.04f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float width = Mathf.Min(300, Screen.width - 32);
            float x = (Screen.width - width) / 2;
            float y = Screen.height / 2f;
            var heading = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            heading.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y - 65, width, 48), "Game Over", heading);
            var scoreLabel = new GUIStyle(heading) { fontSize = 18 };
            GUI.Label(new Rect(x, y - 22, width, 26), "Score " + Progress.Score.ToString("N0"), scoreLabel);
            var button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            if (GUI.Button(new Rect(x, y + 8, width, 54), "Restart", button)) Restart();
            GUI.color = previous;
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
            GUI.Label(new Rect(x, y, width * .65f, 26), "SCORE " + Progress.Score.ToString("N0"), label);
            label.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(x + width * .65f, y, width * .35f, 26), "LEVEL " + Progress.Level, label);
            var previous = GUI.color;
            GUI.color = new Color(.3f, .4f, .45f, .7f);
            GUI.DrawTexture(new Rect(x, y + 28, width, 3), Texture2D.whiteTexture);
            GUI.color = ProgressBarColor;
            float fraction = (float)(Progress.Defeated - Progress.PreviousThreshold) /
                (Progress.NextThreshold - Progress.PreviousThreshold);
            GUI.DrawTexture(new Rect(x, y + 28, width * fraction, 3), Texture2D.whiteTexture);
            var colors = grid.SeenColors();
            float bottom = Screen.height - camera.WorldToScreenPoint(new Vector3(0, -5.25f, 0)).y;
            for (int i = 0; i < colors.Count; i++)
            {
                if (!grid.IsColorCleared(colors[i])) continue;
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
            string status = player != null && player.MagicCharges > 0 ? "MAGIC " + player.MagicCharges : "";
            if (feedbackRemaining > 0) status += (status.Length > 0 ? "  |  " : "") + feedback;
            if (status.Length > 0) GUI.Label(new Rect(x, y + 38, width, 44), status, label);
        }
    }
}
