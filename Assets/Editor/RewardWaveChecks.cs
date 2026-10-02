using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class RewardWaveChecks
    {
        public static void Run()
        {
            CheckFullSetDrain();
            var wave = new FullSetCelebration();
            var colors = new List<EnemyColor> { EnemyColor.Green, EnemyColor.Blue, EnemyColor.Red };
            var order = new List<EnemyColor>(colors);
            order.Sort();
            var spikes = new List<EnemyColor>();
            wave.BarSpiked += (color, seconds) => spikes.Add(color);
            wave.Begin(colors, duration: 3.5f, holdDuration: .5f);
            var resting = new Rect(10, 100, 50, 5);
            wave.Tick(.5f);
            Check(spikes.Count == 0 && wave.Present(order[0], resting) == resting, "Hold does not spike or launch");
            for (int i = 0; i < order.Count; i++)
            {
                wave.Tick(.5f);
                var peak = wave.Present(order[i], resting);
                Check(spikes.Count == i + 1 && spikes[i] == order[i], "Spike events travel left to right once each");
                Check(peak.height > resting.height * 9 && peak.x == resting.x && peak.width == resting.width &&
                    peak.yMax == resting.yMax, "Spikes grow upward in their own slots");
                for (int j = i + 1; j < order.Count; j++)
                    Check(wave.Present(order[j], resting) == resting, "Upcoming bars wait for their turn");
                wave.Tick(.5f);
            }
            Check(wave.Finished, "The wave keeps its original refill deadline");
            spikes.Clear();
            wave.Begin(colors, duration: 3);
            wave.Tick(20);
            Check(spikes.Count == 3 && spikes[0] == order[0] && spikes[2] == order[2],
                "Long frames deliver spike events in chronological order");
            wave.Tick(20);
            Check(spikes.Count == 3, "Finished waves cannot retrigger a flip");
            spikes.Clear();
            wave.Begin(colors, duration: 1, simultaneousPowerDown: true);
            wave.Tick(2);
            Check(spikes.Count == 0, "Partial-set power-down never launches the player");
            CheckOverlap();
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                VisualUpgrade.Upgrade(player.gameObject);
                var art = CharacterVisuals.Ensure(player.gameObject);
                var hitbox = art.HitBounds;
                var position = player.transform.position;
                var restingPose = art.Root.position;
                var rotation = art.Root.rotation;
                var flip = player.gameObject.AddComponent<PlayerCelebrationFlip>();
                flip.Begin(.5f);
                flip.Tick(.25f);
                Check(!art.LandingActive, "Airborne trampoline flip does not start its landing early");
                Check(flip.Active && art.Root.position.y > restingPose.y + .7f &&
                    Quaternion.Angle(art.Root.rotation, rotation) > 170, "Trampoline hop includes a full airborne flip");
                Check(player.transform.position == position && art.HitBounds == hitbox,
                    "Trampoline animation leaves player position and collision bounds untouched");
                flip.Tick(0);
                Check(flip.Active, "Paused animation retains its pose");
                flip.Tick(.25f);
                Check(!flip.Active && Vector3.Distance(art.Root.position, restingPose) < .0001f &&
                    Quaternion.Angle(art.Root.rotation, rotation) < .001f, "Flip lands exactly upright at its starting pose");
                Check(art.LandingActive, "A completed trampoline flip starts the shared landing squash");
                foreach (bool clockwise in new[] { false, true })
                {
                    flip.Begin(.5f, clockwise); flip.Tick(.125f);
                    float angle = Mathf.DeltaAngle(rotation.eulerAngles.z, art.Root.rotation.eulerAngles.z);
                    Check(clockwise ? angle < 0 : angle > 0, "Requested flip direction rotates the actual art correctly");
                    flip.Tick(.375f);
                    Check(!flip.Active && art.HitBounds == hitbox, "Both flip directions restore the pose without moving hitboxes");
                }
                player.Move(-1, .01f);
                Check(player.HorizontalDirection == -1, "Left input supplies counterclockwise broad-support direction");
                player.transform.position = new Vector3(2.99f, player.transform.position.y, 0);
                player.Move(1, .01f);
                Check(player.transform.position.x < 0 && player.HorizontalDirection == 1,
                    "Wrapping right still reports rightward travel");
                player.Move(0, .01f);
                Check(player.HorizontalDirection == 0, "Stopping clears movement direction");
                player.Move(-1, .01f); player.ResetForRun();
                Check(player.HorizontalDirection == 0, "Restart cannot inherit a previous movement direction");
            });
            JackpotChecks.Run();
            Debug.Log("Reward wave checks passed: ordered spikes, fixed-width slots, holds, long frames, partial sets and collision-safe trampoline flips.");
        }
        private static void CheckOverlap()
        {
            bool clockwise;
            Check(!PlayerCelebrationFlip.TryDirection(0, 10, 9.01f, 20, 1, out clockwise), "Less than ten percent cannot launch");
            Check(!PlayerCelebrationFlip.TryDirection(0, 10, 10, 20, 1, out clockwise), "Touching edges alone cannot launch");
            Check(!PlayerCelebrationFlip.TryDirection(0, 0, -1, 1, 1, out clockwise), "Zero-width body cannot launch");
            foreach (float movement in new[] { -1f, 0, 1 })
            {
                Check(PlayerCelebrationFlip.TryDirection(0, 10, 9, 20, movement, out clockwise) && !clockwise,
                    "Exactly ten percent on the right lifts into a counterclockwise flip");
                Check(PlayerCelebrationFlip.TryDirection(0, 10, -10, 1, movement, out clockwise) && clockwise,
                    "Exactly ten percent on the left lifts into a clockwise flip");
                Check(PlayerCelebrationFlip.TryDirection(0, 10, 5, 20, movement, out clockwise) && !clockwise,
                    "Exactly half-supported still follows the lifted side");
            }
            Check(PlayerCelebrationFlip.TryDirection(0, 10, 4.99f, 20, 1, out clockwise) && clockwise,
                "More than half-supported follows rightward motion despite right-side lift");
            Check(PlayerCelebrationFlip.TryDirection(0, 10, -10, 5.01f, -1, out clockwise) && !clockwise,
                "More than half-supported follows leftward motion despite left-side lift");
            Check(PlayerCelebrationFlip.TryDirection(0, 10, -10, 20, 0, out clockwise) && clockwise,
                "Centered stationary player uses a deterministic clockwise fallback");
            var bar = GameSession.ColorClearBarSlotRect(0, 0, 100, 4, 0);
            Check(!PlayerCelebrationFlip.TryDirection(23, 33, bar.xMin, bar.xMax, 0, out clockwise),
                "The gap between visible bars does not count as support");
        }

        private static void CheckFullSetDrain()
        {
            var wave = new FullSetCelebration();
            var colors = new List<EnemyColor> { EnemyColor.Red, EnemyColor.Blue };
            colors.Sort();
            wave.Begin(colors, duration: 2.5f, holdDuration: .5f);
            wave.Tick(1);
            Check(wave.PowerDownProgressFor(colors[0]) == 0 && wave.PowerDownProgressFor(colors[1]) == 0,
                "Full bars stay fully lit through the hold, rise and peak");
            wave.Tick(.25f);
            Check(Mathf.Abs(wave.PowerDownProgressFor(colors[0]) - .5f) < .0001f &&
                wave.PowerDownProgressFor(colors[1]) == 0 && wave.Visible(colors[0]),
                "Only the falling bar drains, before its scheduled removal");
            wave.Tick(0); wave.Tick(-1);
            Check(Mathf.Abs(wave.PowerDownProgressFor(colors[0]) - .5f) < .0001f,
                "Pause and negative ticks cannot advance the drain");
            wave.Tick(.25f);
            Check(wave.PowerDownProgressFor(colors[0]) == 1 && !wave.Visible(colors[0]) &&
                wave.PowerDownProgressFor(colors[1]) == 0, "Drain completes at the existing removal boundary");
            wave.Tick(.75f);
            Check(Mathf.Abs(wave.PowerDownProgressFor(colors[1]) - .5f) < .0001f,
                "The next full-set bar uses the same falling-half drain");
            wave.Begin(colors, duration: 2, simultaneousPowerDown: true);
            wave.Tick(1);
            foreach (var color in colors)
                Check(wave.PowerDownProgressFor(color) == wave.PowerDownProgress,
                    "Partial-set bars retain their shared drain timing");
            wave.Reset();
            Check(wave.PowerDownProgressFor(colors[0]) == 0, "Reset removes all drain state");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Reward wave check failed: " + message); }
    }
}
