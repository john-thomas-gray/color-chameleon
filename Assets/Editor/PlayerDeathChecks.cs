using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlayerDeathChecks
    {
        public static void Run()
        {
            CheckRecoverableHits();
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                var enemy = ProgressionChecks.Add(grid, EnemyColor.Red, 2, GridModel.Rows - 1);
                player.transform.position = new Vector3(enemy.transform.position.x, -4.6f, 0);
                player.RefreshColor();
                Check(player.Hit() && player.Lives == 2, "First hit spends a life");
                player.TickSurvival(3.1f);
                Check(player.Hit() && player.Lives == 1, "Second hit spends a life");
                player.TickSurvival(3.1f);
                int defeats = 0;
                CharacterVisuals.Ensure(player.gameObject).Defeated.AddListener(() => defeats++);
                var missile = new GameObject("Death-test missile", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                missile.SetTarget(player);
                Check(session.CheckPlayerContact() && session.State == GameSession.RunState.Dying, "Final contact begins death, not game-over overlay");
                Check(defeats == 1 && player.FatallyDefeated && !player.Alive && player.ControlsLocked, "Fatal defeat fires the visual hook and locks player");
                Check(player.GetComponentsInChildren<SpriteRenderer>().All(sprite => !sprite.enabled), "Live player art is hidden behind detached death art");
                Check(!grid.enabled && !grid.GetComponent<EnemyGridMovement>().enabled && !grid.GetComponent<EnemyRowSpawner>().enabled && missile.Suspended,
                    "Fleet, spawns and missiles freeze during death");
                var position = player.transform.position;
                player.Move(1, 10); player.TickSurvival(10);
                Check(player.transform.position == position && !player.Fire() && !player.Hit() && !player.Alive, "Fatal player cannot move, fire, take another hit or respawn");
                session.Pause();
                Check(!session.IsPaused, "Pause overlay cannot obscure the short death transition");
                Check(!session.CheckPlayerContact() && defeats == 1, "Repeated contact cannot restart the death");
                session.Tick(.45f);
                Check(session.State == GameSession.RunState.Dying, "Game-over overlay stays hidden mid-animation");
                if (Application.isPlaying)
                {
                    var burst = UnityEngine.Object.FindFirstObjectByType<PlayerDeathBurst>();
                    Check(burst != null && burst.GetComponentsInChildren<SpriteRenderer>().Any(sprite => sprite.enabled), "Placeholder fragments are visible during death");
                }
                session.Tick(.44f);
                Check(session.State == GameSession.RunState.Dying, "Game over waits for the whole death animation");
                session.Tick(.02f);
                Check(session.State == GameSession.RunState.GameOver && defeats == 1, "Game over follows animation exactly once");
                session.Tick(10);
                Check(!player.Alive && defeats == 1, "Finished death never reappears or retriggers");
            });

            if (Application.isPlaying)
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var custom = new GameObject("Replacement player death", typeof(PresentationCue)).GetComponent<PresentationCue>();
                try
                {
                    var settings = new UnityEditor.SerializedObject(custom);
                    settings.FindProperty("duration").floatValue = 1.5f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    CharacterVisuals.Ensure(player.gameObject).SetCuePrefabs(null, null, custom);
                    grid.transform.position = Vector3.up * 3;
                    var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                    var enemy = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, GridModel.Rows - 1);
                    player.transform.position = new Vector3(enemy.transform.position.x, -4.6f, 0);
                    player.Hit(); player.TickSurvival(3.1f);
                    player.Hit(); player.TickSurvival(3.1f);
                    Check(session.CheckPlayerContact(), "Replacement death starts");
                    session.Tick(1);
                    Check(session.State == GameSession.RunState.Dying, "Replacement duration can outlast the placeholder");
                    session.Tick(.51f);
                    Check(session.State == GameSession.RunState.GameOver, "Replacement fallback duration completes game over");
                }
                finally { UnityEngine.Object.DestroyImmediate(custom.gameObject); }
            });
            Debug.Log("Player death checks passed: animation-first contact, frozen gameplay, no respawn, exactly-once transition and replacement cues.");
        }

        private static void CheckRecoverableHits()
        {
            foreach (bool yellowPenalty in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var yellow = ProgressionChecks.Add(grid, EnemyColor.Yellow, 2, 0);
                player.RefreshColor();
                Check(player.ReadyColor == EnemyColor.Yellow, "Penalty fixture starts Yellow");
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 3, 0);
                int deaths = 0;
                CharacterVisuals.Ensure(player.gameObject).Defeated.AddListener(() => deaths++);
                if (yellowPenalty)
                {
                    var ability = yellow.GetComponent<EnemyAbilities>();
                    Check(ability.BeginImitation(red), "Last Yellow starts transformation");
                    ability.Tick(EnemyAbilities.ImitationSeconds);
                }
                else
                {
                    var missile = new GameObject("Recovery missile", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                    missile.transform.position = player.transform.position + Vector3.up;
                    missile.SetTarget(player);
                    missile.Tick(.3f);
                    Check(missile.Finished, "Missile hits the player");
                }
                Check(deaths == 1 && player.Lives == PlayerMovement.MaxLives - 1 && !player.Alive &&
                    !player.FatallyDefeated && player.Invulnerable,
                    "Missile and last-Yellow penalties trigger recoverable death cues");
                Check(!player.Hit() && deaths == 1, "Repeated hit cannot duplicate death animation");
                player.TickSurvival(.45f);
                Check(!player.Alive && player.GetComponentsInChildren<SpriteRenderer>().All(sprite => !sprite.enabled),
                    "Live art stays hidden during recoverable death");
                if (Application.isPlaying)
                    Check(UnityEngine.Object.FindObjectsByType<PlayerDeathBurst>(FindObjectsSortMode.None).Any(),
                        "Recoverable death renders the same burst as fatal contact");
                player.TickSurvival(1.05f);
                Check(player.Alive && player.Invulnerable && !player.FatallyDefeated,
                    "Normal respawn remains at 1.5 seconds with protection");
                player.TickSurvival(1.5f);
                Check(player.Alive && !player.Invulnerable && deaths == 1, "Recovery ends normally after three seconds");
            });
        }

        public static void CapturePreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            var cue = PresentationCue.Spawn(null, PresentationCue.Kind.PlayerDefeat,
                CharacterVisuals.Ensure(player.gameObject).Body, EnemyColor.Blue, 1);
            foreach (var sprite in player.GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = false;
            cue.Tick(.10f);
            GameplayChecks.Capture(540, 960, "player-death-flash");
            cue.Tick(.22f);
            GameplayChecks.Capture(540, 960, "player-death-burst");
            GameplayChecks.Capture(960, 540, "player-death-burst-landscape");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Player death check failed: " + message); }
    }
}
