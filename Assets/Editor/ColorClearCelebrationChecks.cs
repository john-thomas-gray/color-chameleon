using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class ColorClearCelebrationChecks
    {
        public static void Run()
        {
            CheckOverlappingWave();
            CheckBarUnlockTransition();
            var random = UnityEngine.Random.state;
            var sizes = Enumerable.Range(0, 4096).Select(_ => EnemyPlaceholderArt.RandomDustSize(.55f)).ToArray();
            Check(sizes.All(size => size >= .055f && size <= .16f) && sizes.Count(size => size >= .11f) > sizes.Length * .6f,
                "Most stream particles use the larger half of the dust size range");
            Check(sizes.Any(size => size < .07f) && UnityEngine.Random.state.Equals(random),
                "Larger dust retains fine glints without consuming gameplay randomness");
            var animation = new ColorClearBarAnimation();
            var slot = new Rect(20, 500, 80, 5);
            animation.Begin(EnemyColor.Blue);
            var first = animation.Present(EnemyColor.Blue, slot);
            Check(first.width > slot.width && first.height > slot.height && first.center.y < slot.center.y,
                "Earned bar starts oversized above its slot");
            animation.Tick(.3f);
            var middle = animation.Present(EnemyColor.Blue, slot);
            Check(middle.width < first.width && middle.center.y > first.center.y, "Bar shrinks and slides toward its slot");
            animation.Tick(1);
            Check(animation.Present(EnemyColor.Blue, slot) == slot, "Bar settles exactly into the resting slot");
            for (int i = 1; i < 6; i++)
                Check(ColorClearBarAnimation.TonePitch(i + 1) > ColorClearBarAnimation.TonePitch(i), "Every additional bar raises tone pitch");
            int[] majorScale = { 0, 2, 4, 5, 7, 9 };
            for (int i = 0; i < majorScale.Length; i++)
                Check(Mathf.Abs(ColorClearBarAnimation.TonePitch(i + 1) - Mathf.Pow(2, majorScale[i] / 12f)) < .00001f,
                    "Bar notes follow the first six degrees of the major scale");
            var dim = GameSession.ColorClearBarTint(EnemyColor.Red, false);
            var powered = GameSession.ColorClearBarTint(EnemyColor.Red, true);
            Check(dim.a < powered.a && dim.r < powered.r, "Powered-down bars are dimmer and more transparent than lit bars");
            var high = GameSession.ColorClearBarLevelRect(slot, 0, 0);
            var low = GameSession.ColorClearBarLevelRect(slot, .25f, 0);
            var offset = GameSession.ColorClearBarLevelRect(slot, 0, 1);
            Check(high.height > low.height && Mathf.Abs(high.height - offset.height) > .01f,
                "Powered bars visualize the beat with staggered level heights");

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var spawner = grid.GetComponent<EnemyRowSpawner>();
                Check(GameSession.ColorClearBarColors(spawner, grid).SequenceEqual(new[] { EnemyColor.Red, EnemyColor.Blue }),
                    "Level-one bar slots show unlocked colors before any color is seen");
                ProgressionChecks.Add(grid, EnemyColor.Green, 0, 0);
                Check(GameSession.ColorClearBarColors(spawner, grid).SequenceEqual(new[] { EnemyColor.Red, EnemyColor.Blue }),
                    "A locked color does not add a bar slot before its unlock level");
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                session.Progress.Reset(6);
                Check(GameSession.ColorClearBarColors(spawner, grid)
                    .SequenceEqual(new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Yellow }),
                    "Higher levels show every unlocked color slot, even before that color is cleared");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                var pitches = new List<float>();
                grid.GetComponent<SoundEffects>().CuePlayed += (cue, pitch) => { if (cue == SoundEffect.ColorClear) pitches.Add(pitch); };
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var redLast = ProgressionChecks.Add(grid, EnemyColor.Red, 3, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 1, 1);
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 4, 2);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(pitches.Count == 0, "Partial color clear has no reward tone");
                grid.ClearMatchingChain(redLast.Id, EnemyColor.Red);
                Check(pitches.Count == 1 && pitches[0] == 1, "First bar sounds at base pitch");
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                Check(pitches.Count == 2 && pitches[1] > pitches[0], "Second color-clear event sounds higher");
                if (Application.isPlaying)
                {
                    var bursts = UnityEngine.Object.FindObjectsByType<EnemyDeathBurst>(FindObjectsSortMode.None);
                    Check(bursts.Count(b => b.ColorClear) >= 2, "Actual final-color kills create spacedust bursts");
                }
                grid.ResetColorClearStreak();
                var yellow = ProgressionChecks.Add(grid, EnemyColor.Yellow, 0, 3);
                grid.SetColor(yellow.Id, EnemyColor.Purple);
                Check(pitches.Count == 2, "Transformation alone never awards a celebration");
                grid.ClearMatchingChain(green.Id, EnemyColor.Green);
                Check(pitches.Last() == 1, "Lost bar sequence resets pitch");
                var source = yellow.Visuals.Body;
                var dust = EnemyDeathBurst.Create(source, EnemyColor.Purple, 2, 4, true);
                try
                {
                    var grains = dust.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name == "Color-clear spacedust").ToArray();
                    Check(grains.Length == 32 && grains.All(s => !s.enabled && s.sortingOrder < source.sortingOrder),
                        "Spacedust waits for cascade and renders behind enemies");
                    Check(grains.All(s => s.sprite == EnemyPlaceholderArt.SpaceDust), "Dust uses soft glints instead of confetti triangles");
                    dust.Tick(EnemyDeathBurst.RingDelay + .2f);
                    Check(grains.All(s => s.enabled) && grains.Select(s => new Vector3(s.color.r, s.color.g, s.color.b)).Distinct().Count() == 6,
                        "Scattering dust contains all six enemy colors");
                    var before = grains[0].transform.localPosition.magnitude;
                    dust.Tick(.2f);
                    Check(grains[0].transform.localPosition.magnitude > before, "Dust scatters outward");
                    Check(dust.GetComponentInChildren<TextMesh>().text == "4", "Dust preserves the combo multiplier label");
                    dust.Tick(2);
                    Check(dust.Finished, "Dust cleans up after its individual arrivals");
                }
                finally { UnityEngine.Object.DestroyImmediate(dust.gameObject); }

                tongue.Cancel();
                Check(tongue.TryFire(EnemyColor.Purple, 4), "Fixture starts a timed return shot");
                tongue.Tick(4f / 14f);
                Check(tongue.Retracting && Mathf.Abs(tongue.RemainingReturnSeconds - .2f) < .001f,
                    "Tongue exposes its remaining return time");
                player.transform.position = new Vector3(0, -4.6f, 0);
                source.transform.position = new Vector3(0, 3, 0);
                var timedDust = EnemyDeathBurst.Create(source, EnemyColor.Purple, 1, 1, true);
                try
                {
                    var grains = Grains(timedDust);
                    tongue.Tick(1);
                    timedDust.Tick(EnemyDeathBurst.ColorClearScatterSeconds);
                    Check(!tongue.Active && !timedDust.Finished, "Tongue arrival does not finish dust return");
                    timedDust.Tick(EnemyDeathBurst.ColorClearStreamStaggerSeconds);
                    var before = grains.Select(grain => grain.transform.position).ToArray();
                    timedDust.Tick(0);
                    timedDust.Tick(-1);
                    Check(grains.Select(grain => grain.transform.position).SequenceEqual(before), "Paused or negative-time dust does not advance");
                    timedDust.Tick(.1f);
                    Check(grains.Select((grain, i) => Mathf.Abs(Vector3.Distance(grain.transform.position, before[i]) -
                        EnemyDeathBurst.ColorClearReturnSpeed * .1f)).All(error => error < .0001f),
                        "Every returning particle travels at the same constant world speed");
                    Check(grains.Select(s => new Vector3(s.color.r, s.color.g, s.color.b)).Distinct().Count() == 6,
                        "Returning dust retains all six colors");
                    before = grains.Select(grain => grain.transform.position).ToArray();
                    player.transform.position += Vector3.right * .3f;
                    timedDust.Tick(.1f);
                    Check(grains.Select((grain, i) => Mathf.Abs(Vector3.Distance(grain.transform.position, before[i]) -
                        EnemyDeathBurst.ColorClearReturnSpeed * .1f)).All(error => error < .0001f),
                        "Moving the player redirects the stream without accelerating or teleporting particles");
                    Check(grains.All(s => s.enabled && s.color.a >= .7f && s.transform.localScale.x >= .049f &&
                        s.transform.localScale.x == s.transform.localScale.y),
                        "Round dust glints retain their size and shimmer in flight");
                    var arrivals = new List<float>();
                    int remaining = grains.Length;
                    const float step = 1f / 120;
                    for (float time = 0; !timedDust.Finished && time < EnemyDeathBurst.ColorClearMaxSeconds; time += step)
                    {
                        timedDust.Tick(step);
                        int visible = grains.Count(grain => grain.enabled);
                        Check(visible <= remaining, "Absorbed particles never reappear");
                        for (int i = visible; i < remaining; i++) arrivals.Add(time);
                        remaining = visible;
                        var target = player.MagicAbsorptionTarget.position;
                        Check(grains.All(grain => grain.enabled || Vector3.Distance(grain.transform.position, target) < .001f),
                            "Each particle disappears at the player's current position");
                        Check(grains.Count(grain => grain.enabled && Vector3.Distance(grain.transform.position, target) < .18f) <= 8,
                            "Particles do not accumulate into a ball at the player");
                    }
                    Check(timedDust.Finished && remaining == 0 && arrivals.Count == grains.Length && arrivals.Last() - arrivals.First() > .25f,
                        "Particles arrive and vanish over a stream, not at one synchronized deadline");
                }
                finally
                {
                    tongue.Cancel();
                    UnityEngine.Object.DestroyImmediate(timedDust.gameObject);
                }
                CheckStreamTiming(source, player);
            });
            Debug.Log("Color-clear celebration checks passed: constant-speed dust streams, staggered absorption, larger grains, frame timing, final-color triggers, bars and tones.");
        }

        private static void CheckOverlappingWave()
        {
            var wave = new FullSetCelebration();
            var removed = new List<EnemyColor>();
            wave.ColorRemoved += removed.Add;
            wave.Begin(new List<EnemyColor> { EnemyColor.Blue, EnemyColor.Red, EnemyColor.Green }, duration: 1.5f);
            var resting = new Rect(0, 100, 30, 5);
            wave.Tick(.4f);
            Check(wave.Present(EnemyColor.Blue, resting).height > resting.height &&
                wave.Present(EnemyColor.Red, resting).height > resting.height && removed.Count == 0,
                "The next bar rises while the preceding bar is still falling");
            Check(wave.Present(EnemyColor.Green, resting) == resting,
                "The overlap remains local to neighboring wave bars");
            wave.Tick(.1f);
            Check(removed.SequenceEqual(new[] { EnemyColor.Red }) && !wave.Visible(EnemyColor.Red),
                "Overlapping motion preserves the exact beat-counted removal");
            wave.Tick(1);
            Check(wave.Finished && removed.Count == 3 && Mathf.Abs(wave.PlaybackDuration - 1.5f) < .0001f,
                "Overlap does not add delay to the next fleet");
        }

        private static void CheckBarUnlockTransition()
        {
            var row = new Rect(20, 500, 320, 5);
            var initial = new List<EnemyColor> { EnemyColor.Red, EnemyColor.Blue };
            var unlocked = ((EnemyColor[])Enum.GetValues(typeof(EnemyColor))).OrderBy(color => color).ToList();
            var layout = new ColorClearBarLayout();
            layout.Begin(initial, unlocked, 1);
            layout.Tick(10, false);
            Check(layout.Colors.SequenceEqual(initial) && layout.Present(EnemyColor.Green, row).width == 0 &&
                layout.Present(EnemyColor.Red, row) == GameSession.ColorClearBarSlotRect(row.x, row.y, row.width, 2, 0),
                "Pending unlocks stay hidden without squeezing the old slots during the celebration");
            layout.Tick(.5f, true);
            float previousRight = row.x;
            foreach (var color in layout.Colors)
            {
                var rect = layout.Present(color, row);
                Check(rect.width > 0 && rect.x >= previousRight && rect.xMax <= row.xMax && rect.yMax == row.yMax,
                    "Multiple new slots grow in color order without overlap or leaving the bar row");
                previousRight = rect.xMax;
            }
            var middle = layout.Present(EnemyColor.Green, row);
            layout.Tick(0, true); layout.Tick(-1, true);
            Check(layout.Present(EnemyColor.Green, row) == middle, "Zero and negative time cannot advance bar layout");
            layout.Tick(.5f, true);
            Check(!layout.Active, "Layout finishes after one captured beat without adding a wave delay");
            layout.Begin(initial, initial, 1);
            Check(!layout.Active, "Levels without new colors do not animate the layout");

            foreach (int level in new[] { 1, 3, 5, 8 })
            foreach (bool partial in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                var music = grid.GetComponent<GameplayMusicPlayer>();
                music.StopPlayback();
                session.Progress.Reset(level);
                session.Progress.RegisterClear(session.Progress.NextThreshold - session.Progress.Defeated - 2, false);
                var before = session.DisplayedColorClearBarColors;
                var original = session.DisplayedColorClearBarSlotRect(row, 0);
                int arrivals = 0;
                session.WaveSpawned += () => arrivals++;
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                if (partial) grid.ResetColorClearStreak();
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                var target = GameSession.ColorClearBarColors(grid.GetComponent<EnemyRowSpawner>(), grid);
                Check(session.Progress.Level == level + 1 && target.Count == before.Count + 1 &&
                    session.DisplayedColorClearBarColors.SequenceEqual(before),
                    "Level advances immediately but newly unlocked sound bars wait for the active bars");
                session.Tick(session.ClearCelebration.PlaybackDuration - .001f);
                Check(!session.ClearCelebration.Finished && session.DisplayedColorClearBarColors.SequenceEqual(before) &&
                    session.DisplayedColorClearBarSlotRect(row, 0) == original,
                    "Old bar widths remain fixed through the last celebration frame");
                session.Tick(.002f);
                Check(arrivals == (partial ? 1 : 0) && session.DisplayedColorClearBarColors.SequenceEqual(before),
                    "Celebration completion preserves partial and full-set spawn timing and begins at zero new-slot width");
                float beat = music.BeatDuration;
                session.Tick(beat * .25f);
                int added = target.FindIndex(color => !before.Contains(color));
                var small = session.DisplayedColorClearBarSlotRect(row, added);
                var old = session.DisplayedColorClearBarSlotRect(row, 0);
                Check(session.DisplayedColorClearBarColors.SequenceEqual(target) && small.width > 0 &&
                    small.width < GameSession.ColorClearBarSlotRect(row.x, row.y, row.width, target.Count, added).width &&
                    old.width < original.width, "New bars grow from zero as existing bars narrow together");
                session.Pause(); session.Tick(10);
                Check(session.DisplayedColorClearBarSlotRect(row, added) == small, "Pause freezes incoming bars");
                session.Resume(); music.StopPlayback();
                session.Tick(beat * .25f);
                Check(session.DisplayedColorClearBarSlotRect(row, added).width > small.width &&
                    session.DisplayedColorClearBarSlotRect(row, 0).width < old.width,
                    "Resuming continues the same smooth growth and shrink transition");
                session.Tick(beat * .51f);
                Check(arrivals == 1 && session.State == GameSession.RunState.Playing &&
                    session.DisplayedColorClearBarColors.SequenceEqual(target),
                    "Bar expansion never adds another enemy-spawn delay");
                for (int i = 0; i < target.Count; i++)
                    Check(session.DisplayedColorClearBarSlotRect(row, i) ==
                        GameSession.ColorClearBarSlotRect(row.x, row.y, row.width, target.Count, i),
                        "All bars settle exactly into the new equal-width slots");
            });
        }

        private static SpriteRenderer[] Grains(EnemyDeathBurst dust) => dust.GetComponentsInChildren<SpriteRenderer>(true)
            .Where(sprite => sprite.name == "Color-clear spacedust").ToArray();

        private static void CheckStreamTiming(SpriteRenderer source, PlayerMovement player)
        {
            var coarse = EnemyDeathBurst.Create(source, EnemyColor.Purple, 3, 3, true);
            var fine = EnemyDeathBurst.Create(source, EnemyColor.Purple, 3, 3, true);
            try
            {
                coarse.Tick(1.1f);
                for (int i = 0; i < 110; i++) fine.Tick(.01f);
                var a = Grains(coarse);
                var b = Grains(fine);
                Check(a.Select((grain, i) => Vector3.Distance(grain.transform.position, b[i].transform.position)).All(error => error < .001f),
                    "Large frames and small frames agree across cascade, scatter and staggered launch boundaries");
                coarse.Tick(10);
                Check(coarse.Finished && a.All(grain => !grain.enabled), "A long frame absorbs all remaining particles exactly once");
                coarse.Tick(10);
                fine.Tick(EnemyDeathBurst.ColorClearMaxSeconds);
                Check(fine.Finished, "Fine-step stream also finishes cleanly");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coarse.gameObject);
                UnityEngine.Object.DestroyImmediate(fine.gameObject);
            }

            var far = EnemyDeathBurst.Create(source, EnemyColor.Purple, 1, 1, true);
            var saved = source.transform.position;
            source.transform.position = player.MagicAbsorptionTarget.position + Vector3.up * 2;
            var near = EnemyDeathBurst.Create(source, EnemyColor.Purple, 1, 1, true);
            source.transform.position = saved;
            try
            {
                float nearFinish = 0, farFinish = 0;
                for (float time = .01f; time < EnemyDeathBurst.ColorClearMaxSeconds; time += .01f)
                {
                    if (!near.Finished) { near.Tick(.01f); if (near.Finished) nearFinish = time; }
                    if (!far.Finished) { far.Tick(.01f); if (far.Finished) farFinish = time; }
                    if (near.Finished && far.Finished) break;
                }
                Check(nearFinish > 0 && farFinish > nearFinish + .5f,
                    "Distant enemies produce longer streams instead of faster particles with the same arrival time");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(near.gameObject);
                UnityEngine.Object.DestroyImmediate(far.gameObject);
            }
        }

        public static void CapturePreview()
        {
            Run();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 2);
            ProgressionChecks.Add(grid, EnemyColor.Red, 1, 2);
            ProgressionChecks.Add(grid, EnemyColor.Red, 3, 2);
            var dust = EnemyDeathBurst.Create(blue.Visuals.Body, EnemyColor.Blue, 1, 3, true);
            blue.Visuals.Body.enabled = false;
            dust.Tick(.4f);
            GameplayChecks.Capture(540, 960, "color-clear-spacedust");
            GameplayChecks.Capture(960, 540, "color-clear-spacedust-landscape");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Color-clear celebration check failed: " + message); }
    }
}
