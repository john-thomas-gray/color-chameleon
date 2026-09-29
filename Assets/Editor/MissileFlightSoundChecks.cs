using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MissileFlightSoundChecks
    {
        public static void RunInPlayMode()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            SessionState.SetBool("CandyCruisers.MissileFlightChecks", true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod]
        private static void RestorePlayModeCheck()
        {
            if (SessionState.GetBool("CandyCruisers.MissileFlightChecks", false))
                EditorApplication.playModeStateChanged += OnPlayMode;
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            SessionState.SetBool("CandyCruisers.MissileFlightChecks", false);
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            Near(MissileFlightSound.StereoPan(Vector2.left), -.95f, "A direct left flyby reaches the left headphone");
            Near(MissileFlightSound.StereoPan(Vector2.right), .95f, "A direct right flyby reaches the right headphone");
            Near(MissileFlightSound.StereoPan(Vector2.up), 0, "A missile directly ahead stays centered");
            Near(MissileFlightSound.StereoPan(Vector2.down), 0, "A missile directly behind stays centered");
            Near(MissileFlightSound.StereoPan(Vector2.zero), 0, "An overlapping missile has a finite centered pan");
            Near(MissileFlightSound.StereoPan(new Vector2(-1, 1)), -Mathf.Sqrt(.5f) * .95f,
                "Diagonal missiles balance the channels by direction");
            Near(MissileFlightSound.StereoPan(new Vector2(-2, 2)), MissileFlightSound.StereoPan(new Vector2(-1, 1)),
                "The same direction has the same pan at different audible distances");
            float lastPan = -.95f;
            for (float x = -2; x <= 2; x += .01f)
            {
                float pan = MissileFlightSound.StereoPan(new Vector2(x, 0));
                Check(pan >= -1 && pan <= 1 && pan >= lastPan && pan - lastPan < .04f,
                    "A close crossing moves continuously from the left ear through center to the right ear");
                lastPan = pan;
            }
            var offset = MissileFlightSound.PlanarOffset(new Vector3(-2.8f, 1, 20), new Vector3(2.8f, 1, 0));
            Near(offset.x, -5.6f, "Opposite screen edges are separated by their actual distance");
            Near(offset.y, 0, "Rendering depth does not affect proximity");
            Near(MissileFlightSound.StereoPan(offset), -.95f, "A missile across the field to the left sounds on the left");
            Near(MissileFlightSound.StereoPan(-offset), .95f, "A missile across the field to the right sounds on the right");
            float previous = 1;
            for (float distance = 0; distance < 5; distance += .01f)
            {
                float gain = MissileFlightSound.DistanceGain(distance, .65f, 3.5f);
                Check(gain >= 0 && gain <= previous && gain <= 1, "Distance attenuation is bounded and monotonic");
                if (distance >= 3.5f) Check(gain == 0, "Beyond the radius the missile is completely silent");
                previous = gain;
            }
            Check(MissileFlightSound.DistanceGain(3.5f, .65f, 3.5f) == 0 &&
                MissileFlightSound.DistanceGain(0, .65f, 3.5f) == 1, "Both range endpoints are exact");
            Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab")
                .GetComponent<MissileFlightSound>().AudibleDistance > 0 &&
                MissileFlightSound.DistanceGain(1.175f, .35f, 2) < .13f,
                "The configurable hearing range concentrates volume sharply near the player");
            CheckBeatClock();
            CheckDirectionalGate();
            foreach (int mode in new[] { 0, 1, 2 }) CheckProjectile(mode);
            Debug.Log("Missile flight sound checks passed: beat flash tempo, beep samples, proximity, cutoff, panning, all projectile types, pause, mute and cleanup.");
        }

        private static void CheckBeatClock()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                var music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                var clip = AudioClip.Create("Missile flash clock fixture", 44100 * 8, 1, 44100, false);
                var root = new GameObject("Beat missile", typeof(SpriteRenderer), typeof(EnemyMissile), typeof(MissileFlightSound));
                try
                {
                    music.Source.clip = clip;
                    root.transform.position = player.transform.position + Vector3.up * 7;
                    var missile = root.GetComponent<EnemyMissile>();
                    var settings = new SerializedObject(missile);
                    settings.FindProperty("speed").floatValue = .01f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    missile.SetTarget(player);
                    root.GetComponent<MissileFlightSound>().Configure(missile, sounds);
                    missile.Tick(.001f);
                    float before = missile.FlashCycles;
                    float targetBeat = music.BeatPosition + 1;
                    music.Source.timeSamples = Mathf.RoundToInt(music.SecondsAtBeat(targetBeat) * clip.frequency);
                    missile.Tick(music.BeatDuration);
                    Check(Mathf.Abs(missile.FlashCycles - before - 1 / EnemyMissile.DefaultFarFlashBeats) < .03f,
                        "One song beat advances the far missile flash by its beat period");
                    float farPeriod = missile.CurrentFlashPeriodBeats;
                    root.transform.position = player.transform.position + Vector3.up * .45f;
                    float nearPeriod = missile.CurrentFlashPeriodBeats;
                    Check(nearPeriod < farPeriod && Mathf.Abs(nearPeriod - EnemyMissile.DefaultNearFlashBeats) < .01f,
                        "The missile flash period shortens in beats as it nears the player");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    music.Source.clip = null;
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            });
        }

        private static void CheckDirectionalGate()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"));
                try
                {
                    var missile = root.GetComponent<EnemyMissile>();
                    var flight = root.GetComponent<MissileFlightSound>();
                    var settings = new SerializedObject(flight);
                    settings.FindProperty("audibleDistance").floatValue = 2;
                    settings.FindProperty("fullVolumeDistance").floatValue = .35f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    missile.LaunchAimed(player, Vector3.down);
                    flight.Configure(missile, sounds);
                    root.transform.position = player.transform.position + new Vector3(1.2f, 1, 0);
                    flight.Refresh();
                    Check(!missile.PointedAtTarget && flight.Source.volume == 0,
                        "Nearby missiles stay silent when their current heading cannot hit the player");

                    missile.LaunchAimed(player, Vector3.left);
                    root.transform.position = player.transform.position + Vector3.right;
                    flight.Refresh();
                    Check(missile.PointedAtTarget && flight.Source.volume > 0,
                        "Missiles become audible when their heading points through the player");

                    missile.LaunchAimed(player, Vector3.right);
                    root.transform.position = player.transform.position + Vector3.right;
                    flight.Refresh();
                    Check(!missile.PointedAtTarget && flight.Source.volume == 0,
                        "Missiles become silent after passing the player");
                }
                finally
                {
                    EditorMessage(root.GetComponent<MissileFlightSound>(), "OnDestroy");
                    UnityEngine.Object.DestroyImmediate(root);
                }
            });
        }

        private static void CheckProjectile(int mode)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"));
                try
                {
                    var missile = root.GetComponent<EnemyMissile>();
                    if (mode == 2) missile.LaunchAimed(player, Vector3.down);
                    else missile.SetTarget(player, mode == 1);
                    root.transform.position = player.transform.position + Vector3.up * 4;
                    var flight = root.GetComponent<MissileFlightSound>();
                    Check(flight != null, "The missile prefab exposes its proximity settings");
                    var settings = new SerializedObject(flight);
                    settings.FindProperty("audibleDistance").floatValue = 2;
                    settings.FindProperty("fullVolumeDistance").floatValue = .35f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    flight.Configure(missile, sounds);
                    var voice = flight.Source;
                    Check(voice.clip == null && !voice.loop &&
                        voice.transform.parent == root.transform && voice.volume == 0 && voice.spatialBlend == 0 && voice.dopplerLevel == 0,
                        "Each missile owns a silent beat-beep voice measured from the player, not the camera");
                    Check(sounds.GetClip(SoundEffect.RedFire) == null && !sounds.PlayCue(SoundEffect.RedFire),
                        "The retired launch sound cannot be triggered");
                    var clip = sounds.GetClip(SoundEffect.MissileFlight);
                    Check(clip == sounds.GetClip(SoundEffect.MissileFlight) && clip.length < .12f && voice.pitch == 1,
                        "Shared generated missile beeps are cached and stay short");
                    Check(!clip.name.ToLowerInvariant().Contains("air") && !clip.name.ToLowerInvariant().Contains("descending"),
                        "The missile cue no longer uses the old continuous flight texture");
                    var samples = new float[clip.samples];
                    Check(clip.GetData(samples, 0), "Generated flight samples can be inspected");
                    Check(samples.All(value => !float.IsNaN(value) && Mathf.Abs(value) < 1) &&
                        Mathf.Abs(samples[0]) < .0001f && Mathf.Abs(samples[samples.Length - 1]) < .0001f,
                        "The beep waveform is finite, unclipped and tapered at both edges");
                    Check(samples.Any(value => Mathf.Abs(value) > .2f), "The missile beep has a bright arcade transient");
                    float lastVolume = 0;
                    foreach (float distance in new[] { 1.9f, 1.4f, .8f, .2f })
                    {
                        root.transform.position = player.transform.position + Vector3.up * distance;
                        flight.Refresh();
                        Check(voice.volume > lastVolume, "Approaching increases beep loudness without playing off-beat");
                        lastVolume = voice.volume;
                    }
                    root.transform.position = player.transform.position + Vector3.up * 1.5f;
                    flight.Refresh();
                    int beforeBeeps = flight.PlayedBeeps;
                    TickUntilBeep(missile, flight);
                    Check(flight.PlayedBeeps > beforeBeeps && voice.clip == clip && !voice.loop,
                        "The warning beep plays only when the missile flash reaches its beat peak");
                    if (Application.isPlaying) Check(voice.isPlaying, "The real audio engine plays the flash beep");
                    float nearVolume = voice.volume;
                    root.transform.position += Vector3.forward * 20;
                    flight.Refresh();
                    Near(voice.volume, nearVolume, "Rendering depth does not change two-dimensional proximity");
                    root.transform.position = player.transform.position + new Vector3(2.5f, 2.5f);
                    flight.Refresh();
                    Check(voice.volume == 0, "Diagonal distance respects the same circular cutoff");
                    root.transform.position = player.transform.position + Vector3.left;
                    flight.Refresh();
                    Near(voice.panStereo, -.95f, "A missile on the player's left sounds strongly on the left");
                    root.transform.position = player.transform.position + Vector3.right;
                    flight.Refresh();
                    Near(voice.panStereo, .95f, "A missile on the player's right sounds strongly on the right");
                    player.transform.position += Vector3.right * 2;
                    flight.Refresh();
                    Near(voice.panStereo, -.95f, "Panning follows the moving player instead of screen or camera center");
                    player.transform.position -= Vector3.right * 2;
                    root.transform.position = player.transform.position + new Vector3(-1, 1, 20);
                    flight.Refresh();
                    Near(voice.panStereo, -Mathf.Sqrt(.5f) * .95f, "Rendering depth does not alter the flyby direction");
                    CheckIndependentPan(player, sounds, flight);
                    root.transform.position = player.transform.position + Vector3.up * 1.5f;
                    flight.Refresh();
                    float beforeMove = voice.volume;
                    root.transform.position = player.transform.position + Vector3.down * 1.5f;
                    flight.Refresh();
                    Check(!missile.PointedAtTarget && voice.volume == 0,
                        "Missiles are silent after passing the player");
                    root.transform.position = player.transform.position + Vector3.up * 1.5f;
                    flight.Refresh();
                    beforeMove = voice.volume;
                    var playerPosition = player.transform.position;
                    player.transform.position += Vector3.up * .5f;
                    flight.Refresh();
                    Check(voice.volume > beforeMove, "Moving the player also changes the missile volume");
                    player.transform.position = playerPosition;
                    flight.Refresh();
                    sounds.Volume *= .5f;
                    Near(voice.volume, beforeMove * .5f, "Master effect volume preserves distance attenuation");
                    sounds.Muted = true;
                    Check(voice.mute, "Master mute reaches active missile voices");
                    sounds.Muted = false;
                    Check(!voice.mute, "Unmuting restores the voice without recreating it");
                    sounds.SetPaused(true);
                    missile.Suspended = true;
                    Check(voice.volume == 0 && !voice.isPlaying && voice.clip == null,
                        "Pause immediately silences and clears missile beeps");
                    sounds.SetPaused(false);
                    missile.Suspended = false;
                    Check(voice.volume > 0 && voice.clip == null,
                        "Resume re-arms the next flash beep without restarting a loop");
                    missile.Suspended = true;
                    Check(voice.volume == 0 && !voice.isPlaying && voice.clip == null,
                        "Game-over suspension works without globally pausing effects");
                    missile.Suspended = false;
                    flight.Configure(missile, sounds);
                    Check(root.GetComponentsInChildren<AudioSource>().Length == 1, "Repeated configuration never duplicates a voice");
                    missile.SetTarget(null);
                    flight.Refresh();
                    Check(voice.volume == 0 && !voice.isPlaying, "A missing player is silent");
                    if (mode == 2) missile.LaunchAimed(player, Vector3.down);
                    else missile.SetTarget(player, mode == 1);
                    flight.Refresh();
                    flight.enabled = false;
                    if (!Application.isPlaying) typeof(MissileFlightSound).GetMethod("OnDisable",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(flight, null);
                    Check(voice.clip == null && voice.volume == 0, "Disabling the flight component stops its source immediately");
                    flight.enabled = true;
                    if (!Application.isPlaying) flight.Refresh();
                    Check(voice.clip == null && voice.volume > 0, "Re-enabling restores the armed beep voice");
                    TickUntilBeep(missile, flight);
                    Check(voice.clip == clip, "The re-enabled missile beeps on its next flash");
                    root.transform.position = player.transform.position + Vector3.up;
                    missile.Tick(1);
                    EditorMessage(flight, "OnDisable");
                    Check(missile.Finished && !root.activeSelf && voice.clip == null && !voice.isPlaying,
                        "Impact stops the flash beep before delayed object destruction");
                }
                finally
                {
                    EditorMessage(root.GetComponent<MissileFlightSound>(), "OnDestroy");
                    UnityEngine.Object.DestroyImmediate(root);
                }
                var voices = (System.Collections.ICollection)typeof(SoundEffects).GetField("voices",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(sounds);
                Check(voices.Count == 0, "Destroyed missiles release their registered audio voices");
            });
        }

        private static void CheckIndependentPan(PlayerMovement player, SoundEffects sounds, MissileFlightSound first)
        {
            var other = new GameObject("Opposite missile", typeof(EnemyMissile), typeof(MissileFlightSound));
            var centered = sounds.CreateVoice(sounds.transform);
            var flight = other.GetComponent<MissileFlightSound>();
            try
            {
                other.transform.position = player.transform.position + Vector3.right;
                var missile = other.GetComponent<EnemyMissile>();
                missile.SetTarget(player);
                flight.Configure(missile, sounds);
                Near(flight.Source.panStereo, .95f, "A second missile has its own right-side voice");
                Check(first.Source.panStereo < 0 && first.Source != flight.Source,
                    "Simultaneous missiles on opposite sides retain independent stereo positions");
                Near(centered.panStereo, 0, "Other sound effects keep their centered default");
            }
            finally
            {
                sounds.ReleaseVoice(centered);
                EditorMessage(flight, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        private static void EditorMessage(MissileFlightSound flight, string message)
        {
            if (!Application.isPlaying) typeof(MissileFlightSound).GetMethod(message,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(flight, null);
        }

        private static void TickUntilBeep(EnemyMissile missile, MissileFlightSound flight, float maxSeconds = .75f)
        {
            int before = flight.PlayedBeeps;
            int frames = Mathf.CeilToInt(maxSeconds * 120);
            for (int i = 0; i < frames && missile != null && !missile.Finished; i++)
            {
                missile.Tick(1f / 120);
                if (flight.PlayedBeeps > before) return;
            }
            throw new Exception("Missile flight sound check failed: Flash beat did not produce a beep");
        }
        private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .0001f, message);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Missile flight sound check failed: " + message); }
    }
}
