using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MissilePersistenceChecks
    {
        public static void Run()
        {
            foreach (bool special in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                if (special)
                {
                    ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                    ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                    grid.RefreshSpecials();
                }
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 4, 0);
                var previous = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                red.GetComponent<EnemyAbilities>().Tick(100);
                var missile = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None)
                    .Single(candidate => !previous.Contains(candidate));
                Check(missile.Aimed == special && !missile.Homing && missile.transform.parent == null, "Fired missile is independent of shooter");
                if (special) missile.LaunchAimed(player, missile.transform.rotation * Vector3.down);
                else missile.SetTarget(player);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(missile != null && missile.isActiveAndEnabled && !missile.Finished && !missile.Suspended,
                    "Destroying shooter preserves its fired missile");
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                Check(session.State == GameSession.RunState.Refilling && missile.isActiveAndEnabled && !missile.Suspended,
                    "Clearing the final enemy also preserves active bullets");
                var before = missile.transform.position;
                missile.Tick(.1f);
                Check(missile.transform.position.y < before.y, "Missile continues moving during refill");
                session.Pause();
                before = missile.transform.position;
                missile.Tick(1);
                Check(missile.transform.position == before, "Pause still freezes surviving missiles");
                session.Resume();
                Check(!missile.Suspended, "Resume during refill releases surviving missiles");
                // A surviving bullet must remain dangerous even with no enemies left.
                missile.transform.position = player.transform.position - missile.transform.rotation * Vector3.down;
                missile.Tick(.5f);
                Check(missile.Finished && !player.Alive && player.Lives == PlayerMovement.MaxLives - 1,
                    "Surviving missile can hit the player during refill");
            });
            Debug.Log("Missile persistence checks passed: ordinary and aimed shooter death, fleet clear, continued travel, pause/resume and refill hits.");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Missile persistence check failed: " + message); }
    }
}
