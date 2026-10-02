using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class DeflectedTongueChecks
    {
        public static void Run()
        {
            foreach (int level in new[] { 1, 10, 19, 24, 25, 100 })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                session.Progress.Reset(level);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 1, 0);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 3, 0);
                grid.RefreshSpecials();
                player.transform.position = new Vector3(blue.transform.position.x, -4.6f, 0);
                tongue.TryFire(EnemyColor.Red, 10);
                tongue.Tick(.34f, grid);
                Check(tongue.IsDeflected && player.Stunned && tongue.Retracting, "Special Blue starts the slow stunned return");
                float beat = player.Music != null ? player.Music.SecondsForBeats(1) : TongueShot.ShieldShockSeconds;
                Check(Mathf.Abs(tongue.ShockDuration - beat) < .0001f,
                    "Shield electricity lasts one soundtrack beat when the tongue has enough return distance");
                float before = tongue.Length;
                tongue.Tick(.02f, grid);
                float expected = 20 * Mathf.Max(.01f, .25f - .01f * level);
                Check(Mathf.Abs((before - tongue.Length) - expected * .02f) < .0001f,
                    "Deflected return matches archived speed at level " + level);
                Check(tongue.TipBulb != null && tongue.TipBulb.enabled &&
                    ColorDistance(tongue.TipBulb.color, tongue.GetComponent<LineRenderer>().colorGradient.Evaluate(1)) < .01f,
                    "Deflected bulb follows the electrically traversed tongue tip");
                tongue.Tick(100, grid);
                Check(!tongue.Active && !player.Stunned, "Even high-level deflection finishes and releases stun");
                tongue.TryFire(EnemyColor.Red, 1);
                tongue.Tick(.1f);
                Check(!tongue.IsDeflected && Mathf.Abs(tongue.Length - (1 - 20 * (.1f - 1f / 14))) < .0001f,
                    "Following ordinary shot restores normal return speed");
                tongue.Cancel();
            });
            float coarse = DeflectedLength(.6f), fine = DeflectedLength(.01f);
            Check(Mathf.Abs(coarse - fine) < .0001f, "Long collision frames use the slow speed immediately, just like stepped frames");
            Debug.Log("Deflected tongue checks passed: archived rates, same-frame return, high-level floor, stun release and normal-shot reset.");
        }
        private static float DeflectedLength(float step)
        {
            float length = 0;
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 1, 0); ProgressionChecks.Add(grid, EnemyColor.Blue, 3, 0);
                grid.RefreshSpecials();
                player.transform.position = new Vector3(blue.transform.position.x, -4.6f, 0);
                tongue.TryFire(EnemyColor.Red, 10);
                int frames = Mathf.RoundToInt(.6f / step);
                for (int i = 0; i < frames; i++) tongue.Tick(step, grid);
                length = tongue.Length;
            });
            return length;
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Deflected tongue check failed: " + message); }
        private static float ColorDistance(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a);
    }
}
