using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class SoundEffectsChecks
    {
        [MenuItem("Candy Cruisers/Run Sound Checks")]
        public static void Run()
        {
            CheckClips();
            CheckTongue();
            foreach (bool magic in new[] { false, true }) CheckEarlyReturn(magic);
            CheckShieldReturn();
            CheckSlowReturn();
            Debug.Log("Sound effects checks passed: oscillator continuity, animation-driven pitch, early and slow returns, cancellation, volume, mute and pause.");
        }

        private static void CheckClips()
        {
            var root = new GameObject("Sound clip checks", typeof(SoundEffects));
            AudioClip replacement = null;
            AudioClip tone = null;
            try
            {
                var sounds = root.GetComponent<SoundEffects>();
                tone = sounds.GetClip(SoundEffect.TongueWhistle);
                Check(tone == sounds.GetClip(SoundEffect.TongueWhistle), "Generated oscillator is cached");
                ReadWhistle(tone);

                replacement = AudioClip.Create("Replacement sound", 4410, 1, ArcadeSoundClips.SampleRate, false);
                var serialized = new SerializedObject(sounds);
                serialized.FindProperty("cues").GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Check(sounds.GetClip(SoundEffect.TongueWhistle) == replacement, "Assigned tone replaces the synthesized default");
                UnityEngine.Object.DestroyImmediate(root);
                if (!Application.isPlaying) Check(tone == null, "Generated oscillator is released with its service");
                Check(replacement != null, "Service does not destroy externally supplied clips");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
            }
        }

        private static float[] ReadWhistle(AudioClip clip)
        {
            Check(clip != null && clip.channels == 1 && clip.length < .005f, "Single-cycle mono oscillator, not a timed sweep");
            var data = new float[clip.samples];
            Check(clip.GetData(data, 0), "Whistle sample data is available");
            double energy = 0;
            float largestStep = 0;
            for (int i = 0; i < data.Length; i++)
            {
                Check(!float.IsNaN(data[i]) && !float.IsInfinity(data[i]) && Mathf.Abs(data[i]) < .9f, "Finite samples with clipping headroom");
                energy += data[i] * data[i];
                if (i > 0) largestStep = Mathf.Max(largestStep, Mathf.Abs(data[i] - data[i - 1]));
            }
            Check(energy / data.Length > .02 && largestStep < .2f, "Audible signal without abrupt sample jumps");
            Check(Crossings(data, 0, 1) == 1, "The wavetable contains one complete cycle");
            Check(Mathf.Abs(data[1] - data[0] - (data[0] - data[data.Length - 1])) < .0001f,
                "Loop boundary preserves the waveform slope");
            return data;
        }

        private static int Crossings(float[] data, float start, float end)
        {
            int count = 0;
            for (int i = Mathf.Max(1, (int)(data.Length * start)); i < data.Length * end; i++)
                if (data[i - 1] <= 0 && data[i] > 0) count++;
            return count;
        }

        private static void CheckTongue()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                session.Configure(player);
                var sounds = grid.GetComponent<SoundEffects>();
                var sources = tongue.GetComponentsInChildren<AudioSource>();
                Check(sources.Length == 1, "Repeated configuration creates one voice");
                var voice = sources[0];
                Check(!voice.playOnAwake && !voice.loop && voice.spatialBlend == 0 && voice.dopplerLevel == 0,
                    "Tongue has a nonspatial, nonlooping voice independent of camera distance");
                int extensions = 0, returns = 0;
                tongue.ExtensionStarted += () => extensions++;
                tongue.RetractionStarted += () => returns++;
                Check(!tongue.TryFire(EnemyColor.Red, 0) && extensions == 0 && voice.clip == null, "Invalid fire is silent");
                Check(tongue.TryFire(EnemyColor.Red, 10), "Accepted shot");
                var tone = voice.clip;
                float basePitch = voice.pitch;
                Check(tone == sounds.GetClip(SoundEffect.TongueWhistle) && voice.loop && extensions == 1,
                    "Accepted shot starts one sustained oscillator");
                Check(!tongue.TryFire(EnemyColor.Red, 10) && extensions == 1, "Blocked repeat does not restart sound");
                Check(voice.volume == 0, "Tone fades in from the mouth");
                tongue.Tick(.05f);
                sounds.Volume = .5f;
                Check(Mathf.Abs(voice.volume - .325f) < .0001f, "Shared volume updates a live voice");
                sounds.Muted = true;
                Check(voice.mute, "Mute applies to live voices");
                sounds.Muted = false;
                session.Pause();
                float pausedPitch = voice.pitch;
                tongue.Tick(0);
                Check(sounds.Paused && tongue.Active, "Pause preserves the shot and pauses its sound service");
                Check(voice.pitch == pausedPitch, "Frozen animation holds pitch");
                session.Resume();
                Check(!sounds.Paused && !voice.mute, "Resume and unmute restore playback settings");
                tongue.Tick(.25f);
                Check(voice.pitch > pausedPitch && voice.clip == tone && voice.loop,
                    "Pitch keeps rising beyond the old sound's duration");
                tongue.Tick((10 - tongue.Length) / 14);
                float peakPitch = voice.pitch;
                Check(returns == 1 && tongue.Retracting && voice.clip == tone,
                    "Full reach keeps the same oscillator through turnaround");
                tongue.Tick(.01f);
                Check(voice.pitch < peakPitch && returns == 1, "Pitch falls as the tongue retracts");
                tongue.Tick(1);
                Check(!tongue.Active && voice.clip == null && !voice.isPlaying, "Completed shot stops its sound");
                tongue.TryFire(EnemyColor.Blue, 10, true);
                Check(Mathf.Abs(voice.pitch - basePitch) < .0001f, "Magic starts at the same physical length and pitch");
                tongue.Tick(.1f);
                float magicPitch = voice.pitch;
                tongue.Cancel();
                Check(returns == 1 && voice.clip == null, "Cancellation is silent and stops extension");
                tongue.TryFire(EnemyColor.Red, 10);
                Check(voice.pitch == basePitch, "Next shot resets pitch");
                tongue.Tick(.4f);
                Check(Mathf.Abs(voice.pitch - magicPitch) < .0001f, "Equal tongue lengths sound alike regardless of animation speed");
                tongue.Tick(10);
                Check(returns == 2 && voice.clip == null, "Long frames deliver one return then completion");
                tongue.TryFire(EnemyColor.Red, 10);
                tongue.GetComponent<TongueSoundEffects>().enabled = false;
                if (!Application.isPlaying) typeof(TongueSoundEffects).GetMethod("OnDisable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(tongue.GetComponent<TongueSoundEffects>(), null);
                Check(voice.clip == null, "Disabling the sound binding stops its voice");
            });
        }

        private static void CheckEarlyReturn(bool magic)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                tongue.gameObject.AddComponent<TongueSoundEffects>().Configure(tongue, sounds);
                var target = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                player.transform.position = new Vector3(target.transform.position.x, -4.6f, 0);
                float distance = target.GetComponentInChildren<SpriteRenderer>().bounds.min.y - tongue.transform.position.y;
                int returns = 0;
                tongue.RetractionStarted += () => returns++;
                tongue.TryFire(EnemyColor.Red, 10, magic);
                tongue.Tick(distance / (magic ? 56 : 14) + .001f, grid);
                var voice = tongue.GetComponentInChildren<AudioSource>();
                Check(grid.Model.Count == 0 && tongue.Retracting && returns == 1 &&
                    voice.clip == sounds.GetClip(SoundEffect.TongueWhistle),
                    "Last-enemy hit preserves the live oscillator, magic=" + magic);
                float hitPitch = voice.pitch;
                float hitLength = tongue.Length;
                Check(hitPitch < 1.2f, "An early hit does not jump to a full-reach return pitch");
                tongue.Tick(.01f, grid);
                Check(voice.pitch < hitPitch && tongue.Length < hitLength,
                    "An early return immediately lowers pitch with the animation");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 10, magic);
                tongue.Tick(.01f, grid);
                Check(returns == 2 && !tongue.Active, "Empty field emits one return and completes");
            });
        }

        private static void CheckShieldReturn()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                tongue.gameObject.AddComponent<TongueSoundEffects>().Configure(tongue, sounds);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                var ability = blue.GetComponent<EnemyAbilities>();
                ability.Tick(1);
                ability.Tick(ability.CooldownRemaining);
                ability.Tick(.35f);
                Check(ability.ShieldActive, "Shield ready");
                player.transform.position = new Vector3(blue.transform.position.x, -4.6f, 0);
                int returns = 0;
                tongue.RetractionStarted += () => returns++;
                tongue.TryFire(EnemyColor.Red, 10);
                for (int i = 0; i < 100 && !tongue.Retracting; i++) tongue.Tick(.01f, grid);
                Check(returns == 1 && tongue.Retracting && grid.Model.Count == 1 &&
                    tongue.GetComponentInChildren<AudioSource>().clip == sounds.GetClip(SoundEffect.TongueWhistle),
                    "Shield turnaround preserves the oscillator without requiring an enemy clear");
            });
        }

        private static void CheckSlowReturn()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                session.Progress.Reset(19);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 1, 0);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 3, 0);
                grid.RefreshSpecials();
                player.transform.position = new Vector3(blue.transform.position.x, -4.6f, 0);
                tongue.TryFire(EnemyColor.Red, 10);
                tongue.Tick(.34f, grid);
                Check(tongue.IsDeflected && tongue.Retracting, "Special shield triggers slow return");
                var voice = tongue.GetComponentInChildren<AudioSource>();
                var tone = voice.clip;
                for (int i = 0; i < 8; i++)
                {
                    float before = voice.pitch;
                    tongue.Tick(.25f, grid);
                    Check(tongue.Active && voice.loop && voice.clip == tone && voice.pitch < before,
                        "Whistle sustains and keeps falling throughout a multi-second deflection");
                }
                tongue.Tick(100, grid);
                Check(!tongue.Active && voice.clip == null, "Slow return stops its tone only on completion");
            });
        }

        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Sound effects check failed: " + message); }
    }
}
