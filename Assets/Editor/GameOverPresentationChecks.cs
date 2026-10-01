using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class GameOverPresentationChecks
    {
        public static void RunDustChecks()
        {
            var random = UnityEngine.Random.state;
            var sizes = Enumerable.Range(0, 64).Select(_ => EnemyPlaceholderArt.RandomDustSize()).ToArray();
            Check(sizes.All(size => size >= .055f && size <= .16f) && sizes.Distinct().Count() > 32,
                "Dust sizes are randomized within their visible bounds");
            Check(UnityEngine.Random.state.Equals(random), "Dust sizes do not consume gameplay randomness");
            ColorClearCelebrationChecks.Run();
            PlayerDeathChecks.Run();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            Run();
        }
        public static void Run()
        {
            CheckBeats();
            CheckTransition();
            CheckFatalPlayerDust();
            CheckImpactParticles();
            CheckBlackoutRendering();
            CheckShatterSpace();
            foreach (var size in new[] { new Vector2(320, 480), new Vector2(390, 844),
                new Vector2(540, 960), new Vector2(960, 540), new Vector2(1920, 1080), new Vector2(568, 320),
                new Vector2(1179, 2556) })
            foreach (bool mobile in new[] { false, true })
            {
                var canvas = size / GameSession.GuiScaleFor(mobile, size.x, size.y);
                var rect = GameSession.GameOverTitleRect(canvas.x, canvas.y);
                var title = new GUIStyle
                {
                    font = Resources.Load<Font>("Fonts/Bungee-Regular"), fontSize = 48,
                    alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset()
                };
                GameSession.FitGameOverTitle(title, rect);
                var content = new GUIContent(GameSession.GameOverTitle);
                Check(GameSession.GameOverTitle == "GAME OVER" && title.font != null,
                    "Game over uses uppercase text with the bundled main-title font");
                Check(rect.xMin >= 16 && rect.yMin >= 16 && rect.xMax <= canvas.x - 16 && rect.yMax <= canvas.y - 16 &&
                    Vector2.Distance(rect.center, new Vector2(canvas.x / 2, canvas.y * .4f)) < .001f,
                    "Game-over text stays horizontally centered and sits above the midpoint on portrait and landscape screens");
                Check(title.CalcSize(content).x <= rect.width && title.CalcHeight(content, rect.width) <= rect.height,
                    "The title fits on one line at desktop and mobile interface scales");
            }
            Debug.Log("Game-over presentation checks passed: opposite beats, fixed hitboxes, shared wave crunch, fatal player dust, white fatal impact, unlocked-color shards, stereo shatter echo/reverb, music slowdown, four-beat player-only fade, title-only reveal and input gating.");
        }

        private static void CheckBeats()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var enemy = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var art = new GameObject("Pulse player art").transform;
                art.SetParent(player.transform, false);
                var body = new GameObject("Body", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                body.transform.SetParent(art, false);
                body.sprite = EnemyPlaceholderArt.Triangle;
                var playerVisuals = CharacterVisuals.Ensure(player.gameObject);
                playerVisuals.Configure(body, art);
                var enemyBounds = enemy.HitBounds;
                var playerBounds = player.HitBounds;
                var scale = enemy.Visuals.Root.localScale;
                for (int beat = 0; beat < 4; beat++)
                {
                    enemy.Visuals.RefreshBeat(beat, false);
                    playerVisuals.RefreshBeat(beat, true);
                    Check(Near(enemy.Visuals.BeatScale, 1.09f) && Near(playerVisuals.BeatScale, 1), "Enemies peak on the downbeat");
                    enemy.Visuals.RefreshBeat(beat + .5f, false);
                    playerVisuals.RefreshBeat(beat + .5f, true);
                    Check(Near(enemy.Visuals.BeatScale, 1) && Near(playerVisuals.BeatScale, 1.09f), "Player peaks on the opposite half-beat");
                    Check(enemy.HitBounds == enemyBounds && player.HitBounds == playerBounds && enemy.Visuals.Root.localScale == scale,
                        "Beat animation leaves gameplay bounds and authored art scale unchanged");
                }
                enemy.GetComponent<EnemyAbilities>().BeginSpawnEffect();
                enemy.Visuals.RefreshBeat(4, false);
                Check(enemy.Visuals.Body.transform.localScale == Vector3.zero, "Beat cannot reveal a zero-scale spawning enemy");
                enemy.GetComponent<EnemyPresentation>().Tick(.8f, EnemyColor.Red, 0);
                var clone = ProgressionChecks.Add(grid, EnemyColor.Yellow, 1, 0);
                clone.Visuals.CopyVisualScale(enemy.Visuals);
                Check(Vector3.Distance(clone.Visuals.Root.lossyScale, enemy.Visuals.Root.lossyScale / enemy.Visuals.BeatScale) < .0001f,
                    "Disguises do not bake a transient beat into their copied size");
                enemy.Visuals.enabled = false;
                if (!Application.isPlaying) Invoke(enemy.Visuals, "OnDisable");
                Check(enemy.Visuals.BeatScale == 1, "Disabling visuals restores the resting scale");
            });
        }

        private static void CheckTransition()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                UseChildPlayerArtwork(grid, player, tongue);
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) Invoke(session, "OnEnable");
                Invoke(session, "Start");
                var spawnMusic = grid.GetComponent<GameplayMusicPlayer>();
                var frameRoot = new GameObject("Spawn rim fixture", typeof(LineRenderer), typeof(PlayfieldFrame));
                try
                {
                    var boundary = frameRoot.GetComponent<LineRenderer>();
                    boundary.useWorldSpace = false;
                    boundary.positionCount = 4;
                    boundary.SetPositions(new[] { new Vector3(-3, -5.5f), new Vector3(-3, 5.5f),
                        new Vector3(3, 5.5f), new Vector3(3, -5.5f) });
                    var frame = frameRoot.GetComponent<PlayfieldFrame>();
                    frame.Configure(player);
                    var lines = frame.GetComponentsInChildren<LineRenderer>();
                    var widths = lines.Select(line => line.startWidth).ToArray();
                    var positions = lines.Select(line => line.GetPosition(0)).ToArray();
                    var restingScale = frame.transform.localScale;
                    var camera = EnsureCamera();
                    var framing = camera.GetComponent<GameplayFraming>();
                    framing.SetWaveCrunch(0);
                    float restingViewHeight = camera.orthographicSize;
                    session.StartRun();
                    Check(!frame.SpawnPulseActive, "The rim waits for actual fleet arrival");
                    session.Tick(2);
                    Check(frame.SpawnPulseActive && grid.Model.Count > 0, "The fleet-spawn event starts the rim pulse");
                    Check(Near(frame.SpawnPulseDuration, spawnMusic.BeatDuration), "The spawn rim crunch lasts exactly one beat");
                    frame.Refresh(0);
                    float pulseDuration = frame.SpawnPulseDuration;
                    var colors = lines.Select(line => line.startColor).ToArray();
                    var playerBounds = player.HitBounds;
                    var enemy = grid.GetComponentInChildren<GridEnemy>();
                    var enemyBounds = enemy.HitBounds;
                    var contentPoints = new[] { player.transform.position, enemy.transform.position,
                        tongue.transform.position, new Vector3(-2, 1), new Vector3(1, -3) };
                    var contentPositions = contentPoints.Select(point => ViewOffset(camera, point)).ToArray();
                    var rimPositions = lines.Select((line, i) => ViewOffset(camera, line.transform.TransformPoint(positions[i]))).ToArray();
                    Check(framing.WaveCrunchScale == 1 && frame.transform.localScale == restingScale,
                        "The crunch starts at the normal view size");
                    frame.Tick(pulseDuration / 4);
                    float quarterScale = framing.WaveCrunchScale;
                    float quarterThickness = lines[0].startWidth;
                    Check(quarterScale < 1 && quarterThickness > widths[0], "The view contracts while the rim thickens");
                    frame.Tick(pulseDuration / 4);
                    frame.Refresh(0);
                    float apexScale = 1 - GameplayFraming.WaveCrunchCompression;
                    Check(Near(GameplayFraming.WaveCrunchCompression, .084f),
                        "The spawn rim crunch moves inward about thirty percent less than the old twelve-percent compression");
                    Check(Near(framing.WaveCrunchScale, apexScale) && framing.WaveCrunchScale < quarterScale &&
                        lines[0].startWidth > quarterThickness, "Maximum compression and thickness coincide at the apex");
                    for (int i = 0; i < lines.Length; i++)
                    {
                        Check(Near(lines[i].startWidth * apexScale, widths[i] * PlayfieldFrame.SpawnCrunchThickness) &&
                            lines[i].startColor == colors[i] && lines[i].GetPosition(0) == positions[i],
                            "All rim segments reach triple visible thickness without an added color flash or geometry edits");
                        Check(Vector2.Distance(ViewOffset(camera, lines[i].transform.TransformPoint(positions[i])), rimPositions[i] * apexScale) < .0001f,
                            "The entire rim moves inward toward the view center");
                    }
                    for (int i = 0; i < contentPoints.Length; i++)
                    {
                        Check(Vector2.Distance(ViewOffset(camera, contentPoints[i]), contentPositions[i] * apexScale) < .0001f,
                            "Player, enemies, tongue, projectiles and effects share the same visual compression");
                        Check(Vector3.Distance(camera.ScreenToWorldPoint(camera.WorldToScreenPoint(contentPoints[i])), contentPoints[i]) < .0001f,
                            "Pointer projection follows the compressed view");
                    }
                    Check(player.HitBounds == playerBounds && enemy.HitBounds == enemyBounds && Near(PlayerMovement.Wrap(3.1f), -2.9f) &&
                        frame.transform.localScale == restingScale && camera.orthographicSize == restingViewHeight,
                        "The crunch never changes gameplay positions, hitboxes, wrap boundaries or logical view dimensions");
                    framing.Refresh();
                    Check(Vector2.Distance(ViewOffset(camera, contentPoints[0]), contentPositions[0] * apexScale) < .0001f,
                        "Camera refresh preserves the crunch instead of erasing its projection");
                    session.Pause();
                    var pausedProjection = camera.projectionMatrix;
                    frame.Tick(2);
                    Check(camera.projectionMatrix == pausedProjection && Near(lines[0].startWidth * apexScale, widths[0] * PlayfieldFrame.SpawnCrunchThickness),
                        "Pause freezes both compression and rim thickness");
                    session.Resume();
                    frame.Tick(pulseDuration / 4);
                    Check(Near(framing.WaveCrunchScale, quarterScale) && Near(lines[0].startWidth, quarterThickness),
                        "The view releases while the rim returns to its normal thickness");
                    frame.Tick(pulseDuration / 4 + .001f);
                    Check(!frame.SpawnPulseActive && framing.WaveCrunchScale == 1 &&
                        Vector2.Distance(ViewOffset(camera, contentPoints[0]), contentPositions[0]) < .0001f &&
                        lines.Select((line, i) => Near(line.startWidth, widths[i])).All(value => value),
                        "The view and rim return exactly to normal");
                    frame.transform.localScale = restingScale * .95f;
                    frame.TriggerSpawnPulse();
                    pulseDuration = frame.SpawnPulseDuration;
                    frame.Tick(pulseDuration / 2);
                    frame.TriggerSpawnPulse();
                    pulseDuration = frame.SpawnPulseDuration;
                    frame.Tick(pulseDuration / 2);
                    Check(Near(framing.WaveCrunchScale, apexScale) && frame.transform.localScale == restingScale * .95f,
                        "Repeated arrivals do not compound the crunch or overwrite authored frame scale");
                    frame.enabled = false;
                    if (!Application.isPlaying) Invoke(frame, "OnDisable");
                    Check(framing.WaveCrunchScale == 1 && frame.transform.localScale == restingScale * .95f &&
                        Vector2.Distance(ViewOffset(camera, contentPoints[0]), contentPositions[0]) < .0001f &&
                        lines.Select((line, i) => Near(line.startWidth, widths[i])).All(value => value),
                        "Disabling a mid-crunch frame restores normal projection and thickness");
                    framing.SetWaveCrunch(1);
                    framing.enabled = false;
                    Check(framing.WaveCrunchScale == 1 && Vector2.Distance(ViewOffset(camera, contentPoints[0]), contentPositions[0]) < .0001f,
                        "Disabling camera presentation also removes the temporary projection");
                    framing.enabled = true;

                    var sounds = grid.GetComponent<SoundEffects>();
                    int gameOverCues = 0, shatters = 0;
                    sounds.CuePlayed += (effect, pitch) =>
                    {
                        if (effect == SoundEffect.GameOver) gameOverCues++;
                        if (effect == SoundEffect.PlayerShatter) shatters++;
                    };
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    var settings = new SerializedObject(music);
                    settings.FindProperty("volume").floatValue = .3f;
                    settings.FindProperty("muted").boolValue = true;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    music.UpdatePlayback();
                    var track = music.Source.clip;
                    float musicVolume = music.Source.volume;
                    float fadeSeconds = music.SecondsForBeats(4);
                    var playerArt = CharacterVisuals.Ensure(player.gameObject).Root;
                    var playerPosition = player.transform.position;
                    var fadeStart = VisualCenterViewport(playerArt);
                    session.Progress.Reset(9);
                    Invoke(session, "BeginPlayerDeath");
                    Check(grid.GetComponentInChildren<FatalImpactBackdrop>() == null,
                        "The fatal impact waits until the four-beat blackout completes");
                    Check(music.Source.clip == track && music.ShouldPlayMusic && Near(music.Source.volume, musicVolume) &&
                        music.Source.pitch == 1 && session.GameOverMusicGain == 1 && session.GameOverBlackoutOpacity == 0 && shatters == 0,
                        "Fatal hit keeps the current music intact before the four-beat collapse advances");
                    Check(!session.HandleMenuKey(KeyCode.R) && !session.HandleMenuKey(KeyCode.M), "Death cannot be skipped by menu keys");
                    Check(Near(session.GameOverBlackoutSeconds, fadeSeconds), "The fade captures four mapped beats before the music stops");
                    session.Tick(session.GameOverBlackoutSeconds / 2);
                    var fadeMid = VisualCenterViewport(playerArt);
                    Check(music.Source.clip == track && Near(music.Source.volume, musicVolume * .5f) &&
                        Near(music.Source.pitch, .2f) && music.Source.mute,
                        "Halfway through the blackout, the music runs at one fifth speed and half volume without overriding mute");
                    Check(Near(session.GameOverBlackoutOpacity, .5f) && Near(session.GameOverScoreOpacity, .5f) &&
                        shatters == 0 && session.GameOverTitleOpacity == 0 && CharacterVisuals.Ensure(player.gameObject).Body.enabled &&
                        grid.GetComponentInChildren<FatalImpactBackdrop>() == null,
                        "Scene and score fade together while the intact player stays visible without an impact");
                    Check(Vector2.Distance(fadeMid, Vector2.one * .5f) < Vector2.Distance(fadeStart, Vector2.one * .5f) &&
                        player.transform.position == playerPosition,
                        "The held player artwork moves toward centerstage without moving the gameplay object");
                    session.Tick(session.GameOverBlackoutSeconds / 4);
                    Check(Near(session.GameOverBlackoutOpacity, Mathf.SmoothStep(0, 1, .75f)) &&
                        CharacterVisuals.Ensure(player.gameObject).Body.enabled && shatters == 0 &&
                        grid.GetComponentInChildren<FatalImpactBackdrop>() == null && session.GameOverTitleOpacity == 0,
                        "After three beats the player is still intact and all later death effects are waiting");
                    session.Tick(session.GameOverBlackoutSeconds / 4);
                    var impact = grid.GetComponentInChildren<FatalImpactBackdrop>();
                    Check(impact != null && impact.Active, "The impact begins once the four-beat fade has finished");
                    Check(session.GameOverBlackoutOpacity == 1 && session.GameOverScoreOpacity == 0 && shatters == 0,
                        "Everything behind the player reaches black before the fracture");
                    Check(!music.ShouldPlayMusic && !music.Source.isPlaying && music.Source.volume == 0,
                        "The imploding track reaches silence before the fatal dust and shatter");
                    var cover = grid.GetComponentInChildren<GameOverBlackout>().GetComponent<SpriteRenderer>();
                    Check(cover.enabled && cover.color == Color.black && cover.sortingOrder == GameOverBlackout.CoverOrder,
                        "A fully opaque black cover obscures enemies, projectiles, effects and rim");
                    if (Application.isPlaying)
                    {
                        var cue = (PresentationCue)typeof(GameSession).GetField("deathCue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                        Check(cue.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder > FatalImpactBackdrop.SortingOrder,
                            "Player-death artwork renders above the blackout and impact, including replacement prefabs");
                        Check(Vector2.Distance(Camera.main.WorldToViewportPoint(cue.transform.position), Vector2.one * .5f) < .01f,
                            "Player-death artwork begins at centerstage after the blackout fade");
                        Check(cue.GetComponentInChildren<PlayerFatalDustBurst>() != null &&
                            cue.GetComponentsInChildren<PlayerDeathBurst>().Length == 0,
                            "Game-over death uses the fatal dust burst instead of the ordinary player shatter burst");
                        Check(cue.GetComponentsInChildren<SpriteRenderer>().Any(sprite =>
                                sprite.name == "Player fatal dust" && sprite.enabled && sprite.color.a > 0),
                            "Fatal player dust remains visible at full scene blackout");
                    }
                    session.Tick(PlayerDeathBurst.ShatterSeconds + .0001f);
                    Check(shatters == 1 && gameOverCues == 0 && session.State == GameSession.RunState.Dying,
                        "The impact sound starts after the fatal dust explosion begins, without the old cadence");
                    session.Tick(.3f);
                    Check(shatters == 1 && session.GameOverTitleOpacity == 0, "Shatter is one-shot and death finishes before the title appears");
                    Check(ParticleCenters(impact, EnemyColor.Orange).Length > 0,
                        "The fatal sequence uses the run's unlocked colors, not just the opening fleet's colors");
                    session.Tick(.5f);
                    Check(session.State == GameSession.RunState.GameOver && shatters == 1 && session.GameOverTitleOpacity == 0,
                        "Finishing death does not replay a sound or snap the title into view");
                    Check(!impact.Active && !impact.GetComponent<MeshRenderer>().enabled,
                        "Impact artwork ends before the game-over title fades in");
                    Check(!session.HandleMenuKey(KeyCode.Return) && !session.HandleMenuKey(KeyCode.R) && !session.HandleMenuKey(KeyCode.M),
                        "Early menu keys cannot bypass the fade");
                    Check(!(bool)typeof(GameSession).GetMethod("ActivateMenuTouch", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(session, new object[] { new Vector2(Screen.width / 2f, Screen.height / 2f) }),
                        "Touches cannot skip the title reveal");
                    Check(session.GameOverScoreOpacity == 0,
                        "The score display is absent from the title-only game-over screen");
                    session.Tick(GameSession.GameOverTitleFadeSeconds / 2);
                    Check(Near(session.GameOverTitleOpacity, .5f) && !session.GameOverTitleReady && music.Source.volume == 0 &&
                        !music.Source.isPlaying && music.Source.mute && session.GameOverBlackoutOpacity == 1,
                        "The title fades immediately after death over black without resuming music or overriding mute");
                    float opacity = session.GameOverTitleOpacity;
                    session.Tick(-1);
                    Check(session.GameOverTitleOpacity == opacity, "Negative ticks cannot reverse the fade");
                    session.Pause();
                    Check(!session.IsPaused, "Pause cannot obscure the game-over transition");
                    session.Tick(GameSession.GameOverTitleFadeSeconds / 2 + .001f);
                    Check(session.GameOverTitleReady && session.GameOverTitleOpacity == 1, "Input unlocks only after the title is fully visible");
                    session.Tick(2);
                    Invoke(session, "EndGame");
                    Check(gameOverCues == 0 && shatters == 1 && session.GameOverTitleReady && music.Source.volume == 0 && !music.ShouldPlayMusic &&
                        !music.Source.isPlaying, "Repeated game-over calls never replay the cue or restart the music");
                    var clip = sounds.GetClip(SoundEffect.PlayerShatter);
                    var samples = new float[clip.samples * clip.channels];
                    Check(clip.channels == 2 && Near(clip.length, ArcadeSoundClips.ShatterTailSeconds) &&
                        clip.GetData(samples, 0) && samples.Any(value => Mathf.Abs(value) > .1f) &&
                        samples.All(value => !float.IsNaN(value) && Mathf.Abs(value) < 1) && Mathf.Abs(samples.Last()) < .001f,
                        "Stereo shattering audio is audible, unclipped and has a clean reverberant tail");
                    Check(samples.Skip((int)(.8f * clip.frequency * clip.channels)).Any(value => Mathf.Abs(value) > .001f),
                        "The actual shatter remains audibly reverberant after its original dry recording ends");
                }
                finally { UnityEngine.Object.DestroyImmediate(frameRoot); }
            });
        }

        private static Vector2 ViewOffset(Camera camera, Vector3 point) =>
            (Vector2)camera.WorldToViewportPoint(point) - Vector2.one * .5f;
        private static void UseChildPlayerArtwork(EnemyGrid grid, PlayerMovement player, TongueShot tongue)
        {
            // Match the scene's separate artwork root so the fade can move it without moving the player.
            var original = CharacterVisuals.Ensure(player.gameObject).Body;
            var body = new GameObject("Player artwork", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            body.transform.SetParent(player.transform, false);
            EditorUtility.CopySerialized(original, body);
            UnityEngine.Object.DestroyImmediate(original);
            player.Configure(grid, tongue, body);
            CharacterVisuals.Ensure(player.gameObject).Configure(body, body.transform);
        }
        private static Vector2 VisualCenterViewport(Transform artwork)
        {
            var sprites = artwork.GetComponentsInChildren<SpriteRenderer>(true);
            if (sprites.Length == 0) return EnsureCamera().WorldToViewportPoint(artwork.position);
            var bounds = sprites[0].bounds;
            for (int i = 1; i < sprites.Length; i++) bounds.Encapsulate(sprites[i].bounds);
            return EnsureCamera().WorldToViewportPoint(bounds.center);
        }
        private static Camera EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Game-over check camera", typeof(Camera), typeof(GameplayFraming));
                cameraObject.tag = "MainCamera";
                camera = cameraObject.GetComponent<Camera>();
            }
            if (camera.GetComponent<GameplayFraming>() == null) camera.gameObject.AddComponent<GameplayFraming>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
            return camera;
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Game-over presentation check failed: " + message); }

        private static void CheckImpactParticles()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                foreach (int level in new[] { 1, 2, 3, 4, 5, 6, 8, 9, 19, 99 })
                {
                    var cover = GameOverBlackout.Create(grid.transform, null);
                    try
                    {
                        cover.BeginImpact(player.transform.position, level);
                        var impact = cover.GetComponentInChildren<FatalImpactBackdrop>();
                        Check(impact.GetComponentsInChildren<SpriteRenderer>().All(particle => particle.sprite == EnemyPlaceholderArt.SpaceDust),
                            "Fatal particles share the soft spacedust artwork");
                        var mesh = impact.GetComponent<MeshFilter>().sharedMesh;
                        var camera = EnsureCamera();
                        Vector2 origin = camera.WorldToViewportPoint(player.transform.position);
                        foreach (float age in new[] { 0f, .07f, PlayerDeathBurst.ShatterSeconds, .28f, .5f })
                        {
                            cover.Present(1, age);
                            Check(mesh.vertexCount > 0 && mesh.vertices.All(vertex =>
                            {
                                Vector2 offset = (Vector2)camera.WorldToViewportPoint(impact.transform.TransformPoint(vertex)) - origin;
                                offset.x *= camera.aspect;
                                return offset.magnitude > .035f;
                            }), "Only curved shockwaves surround the impact; no triangular spikes radiate from its center");
                        }
                        cover.Present(1, PlayerDeathBurst.ShatterSeconds);
                        Check(mesh.colors.All(color => Near(color.r, color.g) && Near(color.g, color.b)),
                            "Arcs and local shockwaves are neutral white, with no colored lightning");
                        Check(HasOnlyLocalImpactGeometry(impact),
                            "Fatal impact backdrop avoids screen-spanning bars");
                        foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                            Check(ParticleCenters(impact, color).All(point => Vector2.Distance(point, origin) < .001f),
                                "Restored multicolor space dust starts at the player when the shatter begins");
                        var random = UnityEngine.Random.state;
                        cover.Present(1, .2f);
                        var firstVertices = mesh.vertices;
                        var firstColors = mesh.colors;
                        var first = Enum.GetValues(typeof(EnemyColor)).Cast<EnemyColor>()
                            .ToDictionary(color => color, color => ParticleCenters(impact, color));
                        cover.Present(1, .4f);
                        foreach (var pair in first)
                        {
                            Check(pair.Value.Length == (RunProgress.IsUnlocked(pair.Key, level) ? 8 : 0),
                                "Every unlocked color, and no locked color, contributes shards at level " + level + ": " + pair.Key);
                            var later = ParticleCenters(impact, pair.Key);
                            for (int i = 0; i < pair.Value.Length; i++)
                                Check(Vector2.Distance(later[i], origin) > Vector2.Distance(pair.Value[i], origin) + .02f,
                                    "Each shard travels outward from the player's impact");
                        }
                        cover.Present(1, .2f);
                        Check(mesh.vertices.SequenceEqual(firstVertices) && mesh.colors.SequenceEqual(firstColors) &&
                            UnityEngine.Random.state.Equals(random),
                            "Particle motion is deterministic at a given animation age and leaves gameplay randomness alone");
                        cover.Present(1, FatalImpactBackdrop.Duration);
                        Check(!impact.Active && !impact.GetComponent<MeshRenderer>().enabled,
                            "All shards and shockwaves disappear before the game-over title appears");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(cover.gameObject); }
                }
            });
        }

        private static void CheckFatalPlayerDust()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                const int level = 9;
                var body = CharacterVisuals.Ensure(player.gameObject).Body;
                var random = UnityEngine.Random.state;
                var cue = PresentationCue.Spawn(null, PresentationCue.Kind.PlayerFatalDust, body, EnemyColor.Blue, level);
                try
                {
                    var burst = cue.GetComponentInChildren<PlayerFatalDustBurst>();
                    var particles = cue.GetComponentsInChildren<SpriteRenderer>(true)
                        .Where(sprite => sprite.name == "Player fatal dust").ToArray();
                    var unlocked = Enum.GetValues(typeof(EnemyColor)).Cast<EnemyColor>()
                        .Where(color => RunProgress.IsUnlocked(color, level)).ToArray();
                    Check(burst != null && cue.GetComponentsInChildren<PlayerDeathBurst>().Length == 0,
                        "Fatal player death creates a dust burst, not the recoverable shatter burst");
                    Check(particles.Length == unlocked.Length * PlayerFatalDustBurst.GrainsPerColor &&
                        particles.All(particle => particle.sprite == EnemyPlaceholderArt.SpaceDust),
                        "Fatal player dust uses the soft spacedust artwork for every unlocked color");
                    cue.Tick(.15f);
                    var first = particles.Select(particle => particle.transform.localPosition).ToArray();
                    var core = particles.Where((particle, index) =>
                        index % PlayerFatalDustBurst.GrainsPerColor < PlayerFatalDustBurst.CoreGrainsPerColor).ToArray();
                    var ring = particles.Where((particle, index) =>
                        index % PlayerFatalDustBurst.GrainsPerColor >= PlayerFatalDustBurst.CoreGrainsPerColor).ToArray();
                    cue.Tick(.2f);
                    var later = particles.Select(particle => particle.transform.localPosition).ToArray();
                    Check(later.Zip(first, Vector3.Distance).Average() > .05f &&
                        later.Zip(first, (after, before) => Vector3.Distance(after, before) > .015f).Count(value => value) > particles.Length * .8f,
                        "Fatal player dust drifts outward instead of holding a static snapshot");
                    float coreReach = core.Select(particle => ((Vector2)particle.transform.localPosition).magnitude).Average();
                    float ringReach = ring.Select(particle => Mathf.Abs(particle.transform.localPosition.x)).Average();
                    float ringHeight = ring.Select(particle => Mathf.Abs(particle.transform.localPosition.y)).Average();
                    Check(ring.Length == unlocked.Length * PlayerFatalDustBurst.ShockRingGrainsPerColor &&
                        ringReach > coreReach * 1.2f && ringHeight < ringReach * .24f,
                        "Fatal player dust forms a fast, flat equatorial shock ring ahead of the core cloud");
                    cue.Tick(.35f);
                    foreach (var color in unlocked)
                    {
                        var palette = EnemyPalette.Get(color);
                        int count = particles.Count(particle =>
                            Near(particle.color.r, palette.r) && Near(particle.color.g, palette.g) &&
                            Near(particle.color.b, palette.b));
                        Check(count == PlayerFatalDustBurst.GrainsPerColor,
                            "Fatal player dust includes every unlocked color: " + color);
                    }
                    Check(UnityEngine.Random.state.Equals(random), "Fatal player dust leaves gameplay randomness alone");
                }
                finally { UnityEngine.Object.DestroyImmediate(cue.gameObject); }
            });
        }

        private static Vector2[] ParticleCenters(FatalImpactBackdrop impact, EnemyColor enemyColor)
        {
            var palette = EnemyPalette.Get(enemyColor);
            return impact.GetComponentsInChildren<SpriteRenderer>()
                .Where(particle => Near(particle.color.r, palette.r) && Near(particle.color.g, palette.g) && Near(particle.color.b, palette.b))
                .Select(particle => (Vector2)EnsureCamera().WorldToViewportPoint(particle.transform.position)).ToArray();
        }

        private static bool HasOnlyLocalImpactGeometry(FatalImpactBackdrop impact)
        {
            var mesh = impact.GetComponent<MeshFilter>().sharedMesh;
            return mesh != null && mesh.vertexCount > 0 && mesh.triangles.Length > 0 &&
                mesh.bounds.extents.x < 3.25f && mesh.bounds.extents.y < 2.25f &&
                mesh.bounds.extents.sqrMagnitude > .001f;
        }

        private static void CheckBlackoutRendering()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                UseChildPlayerArtwork(grid, player, tongue);
                ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                var cue = PresentationCue.Spawn(null, PresentationCue.Kind.PlayerDefeat,
                    CharacterVisuals.Ensure(player.gameObject).Body, EnemyColor.Blue, 1);
                cue.enabled = false;
                cue.Tick(.3f);
                var cover = GameOverBlackout.Create(grid.transform, cue);
                var playerArt = CharacterVisuals.Ensure(player.gameObject).Root;
                var playerGroup = playerArt.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
                playerGroup.sortingOrder = 123;
                playerGroup.enabled = false;
                var camera = EnsureCamera();
                cue.transform.position = camera.ViewportToWorldPoint(new Vector3(.5f, .5f,
                    camera.WorldToViewportPoint(cue.transform.position).z));
                var framing = camera.GetComponent<GameplayFraming>();
                var previousTarget = camera.targetTexture;
                var previousActive = RenderTexture.active;
                try
                {
                    foreach (var size in new[] { new Vector2Int(320, 480), new Vector2Int(540, 960), new Vector2Int(960, 540) })
                    foreach (float crunch in new[] { 0f, 1f })
                    {
                        var target = new RenderTexture(size.x, size.y, 24);
                        var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                        try
                        {
                            camera.targetTexture = target;
                            framing.SetWaveCrunch(crunch);
                            framing.Refresh();
                            Rect region = default;
                            foreach (bool holdingPlayer in new[] { true, false })
                            {
                                cue.gameObject.SetActive(!holdingPlayer);
                                foreach (var sprite in playerArt.GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = holdingPlayer;
                                if (holdingPlayer) cover.HoldPlayer(playerArt);
                                else cover.ShowDeath(cue);
                                Check(holdingPlayer ? playerGroup.enabled && playerGroup.sortingOrder > GameOverBlackout.CoverOrder :
                                    !playerGroup.enabled && playerGroup.sortingOrder == 123,
                                    "Player artwork is lifted above the fade, then its authored sorting is restored");
                                foreach (float opacity in new[] { 0f, .5f, 1f })
                                {
                                    cover.Present(opacity);
                                    var position = holdingPlayer ? playerArt.position : cue.transform.position;
                                    var center = camera.WorldToViewportPoint(position);
                                    var edge = camera.WorldToViewportPoint(position + new Vector3(1.2f, 1.2f));
                                    region = new Rect((2 * center.x - edge.x) * size.x, (2 * center.y - edge.y) * size.y,
                                        2 * (edge.x - center.x) * size.x, 2 * (edge.y - center.y) * size.y);
                                    camera.Render();
                                    RenderTexture.active = target;
                                    pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                                    pixels.Apply();
                                    int inside = 0, outside = 0;
                                    var data = pixels.GetPixels32();
                                    for (int i = 0; i < data.Length; i++)
                                    {
                                        var pixel = data[i];
                                        if (pixel.r + pixel.g + pixel.b <= 30) continue;
                                        if (region.Contains(new Vector2(i % size.x, i / size.x))) inside++;
                                        else outside++;
                                    }
                                    Check(inside > 5, "Intact player and subsequent death remain visible through the blackout at " + size +
                                        ", holding=" + holdingPlayer + ", opacity=" + opacity + ", inside=" + inside + ", outside=" + outside + ", region=" + region);
                                    Check(opacity < 1 ? outside > 100 : outside == 0,
                                        "Blackout hides all world pixels outside the player, including at peak crunch: " + size +
                                        ", holding=" + holdingPlayer + ", opacity=" + opacity + ", outside=" + outside);
                                    if (opacity == 1 && crunch == 0)
                                        System.IO.File.WriteAllBytes("TestResults/fatal-" + (holdingPlayer ? "held-player-" : "blackout-") +
                                            size.x + "x" + size.y + ".png", pixels.EncodeToPNG());
                                }
                            }
                            cover.BeginImpact(cue.transform.position, 9);
                            Color32[] previousFrame = null;
                            foreach (float age in new[] { PlayerDeathBurst.ShatterSeconds, .36f, FatalImpactBackdrop.Duration })
                            {
                                cover.Present(1, age);
                                camera.Render();
                                RenderTexture.active = target;
                                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                                pixels.Apply();
                                var data = pixels.GetPixels32();
                                int white = 0, changed = 0, outside = 0;
                                for (int i = 0; i < data.Length; i++)
                                {
                                    var pixel = data[i];
                                    if (pixel.r > 50 && Math.Abs(pixel.r - pixel.g) < 4 && Math.Abs(pixel.g - pixel.b) < 4) white++;
                                    if (previousFrame != null && !pixel.Equals(previousFrame[i])) changed++;
                                    if (!region.Contains(new Vector2(i % size.x, i / size.x)) && pixel.r + pixel.g + pixel.b > 30) outside++;
                                }
                                // Local arcs scale with viewport height, not the full screen's area.
                                Check(age < FatalImpactBackdrop.Duration ? white > size.y / 2 : outside == 0,
                                    "White local impact arcs stay visible, then fully clear back to black: " + size +
                                    ", white=" + white + ", outside=" + outside);
                                if (Near(age, .36f))
                                    foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                                    {
                                        Color.RGBToHSV(EnemyPalette.Get(color), out float hue, out _, out _);
                                        int colored = data.Count(pixel =>
                                        {
                                            Color.RGBToHSV(pixel, out float actualHue, out float saturation, out float value);
                                            return saturation > .25f && value > .1f &&
                                                Mathf.Abs(Mathf.DeltaAngle(hue * 360, actualHue * 360)) < 8;
                                        });
                                        Check(colored > 2, "Every unlocked color has visible dispersed particles at " + size + ": " + color);
                                    }
                                if (previousFrame != null && age < FatalImpactBackdrop.Duration)
                                    Check(changed > size.y * 2,
                                        "The impact backdrop animates rather than remaining a static image: changed=" + changed);
                                if (crunch == 0)
                                    System.IO.File.WriteAllBytes("TestResults/fatal-impact-" + size.x + "x" + size.y + "-" + age.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".png", pixels.EncodeToPNG());
                                previousFrame = data;
                            }
                        }
                        finally
                        {
                            camera.targetTexture = previousTarget;
                            RenderTexture.active = previousActive;
                            UnityEngine.Object.DestroyImmediate(pixels);
                            target.Release();
                            UnityEngine.Object.DestroyImmediate(target);
                        }
                    }
                }
                finally
                {
                    cover.HoldPlayer(playerArt);
                    framing.SetWaveCrunch(0);
                    framing.Refresh();
                    if (!Application.isPlaying) Invoke(cover, "OnDestroy");
                    UnityEngine.Object.DestroyImmediate(cover.gameObject);
                    Check(!playerGroup.enabled && playerGroup.sortingOrder == 123,
                        "Destroying a blackout mid-fade restores the player's authored sorting");
                    UnityEngine.Object.DestroyImmediate(cue.gameObject);
                }
            });
        }

        private static void CheckShatterSpace()
        {
            var impulse = (float[])typeof(ArcadeSoundClips).GetMethod("ShatterSpace", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { new[] { 1f } });
            int leftEcho = Mathf.RoundToInt(ArcadeSoundClips.ShatterEchoSeconds * ArcadeSoundClips.SampleRate);
            int rightEcho = Mathf.RoundToInt((ArcadeSoundClips.ShatterEchoSeconds + .035f) * ArcadeSoundClips.SampleRate);
            Check(impulse[0] > .9f && impulse[1] > .9f, "Room processing preserves the immediate dry impact");
            Check(impulse[leftEcho * 2] > .3f && impulse[rightEcho * 2 + 1] > .3f,
                "Separate left and right echoes land at their configured delays");
            Check(impulse[Mathf.RoundToInt(.0311f * ArcadeSoundClips.SampleRate) * 2] > .1f,
                "Room reflections start before the distinct echo");
            int diffuse = 0;
            double middleEnergy = 0, lateEnergy = 0, stereoDifference = 0;
            int frames = impulse.Length / 2;
            for (int i = 0; i < frames; i++)
            {
                float left = impulse[i * 2], right = impulse[i * 2 + 1];
                Check(!float.IsNaN(left) && !float.IsNaN(right) && Mathf.Abs(left) < 1 && Mathf.Abs(right) < 1,
                    "Feedback stays finite and below clipping");
                stereoDifference += Mathf.Abs(left - right);
                if (i > .08f * ArcadeSoundClips.SampleRate && Mathf.Abs(left) > .00001f) diffuse++;
                if (i > .3f * ArcadeSoundClips.SampleRate && i < .6f * ArcadeSoundClips.SampleRate) middleEnergy += left * left;
                if (i > 1.5f * ArcadeSoundClips.SampleRate && i < 1.8f * ArcadeSoundClips.SampleRate) lateEnergy += left * left;
            }
            Check(diffuse > 1000 && stereoDifference > 1 && middleEnergy > lateEnergy * 10 && lateEnergy > 0,
                "Reverb forms a diffuse stereo tail that naturally decays after the original shatter");
            Check(impulse[impulse.Length - 1] == 0 && impulse[impulse.Length - 2] == 0,
                "Both reverb channels end at silence without a cutoff click");
        }
    }
}
