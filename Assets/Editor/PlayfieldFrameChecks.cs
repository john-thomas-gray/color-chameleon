using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlayfieldFrameChecks
    {
        public static void Run()
        {
            var frame = GameObject.Find("Playfield Border").GetComponent<PlayfieldFrame>();
            Check(frame != null, "Scene includes the arcade frame");
            var body = CharacterVisuals.Ensure(GameObject.Find("Player")).Body;
            Color original = body.color;
            try
            {
                var lines = frame.GetComponentsInChildren<LineRenderer>();
                Check(lines.Length == 6, "Boundary, outer outline and four corner brackets");
                var boundary = frame.GetComponent<LineRenderer>();
                Check(boundary.loop && boundary.positionCount == 4, "Logical boundary stays rectangular");
                foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                {
                    body.color = EnemyPalette.Get(color);
                    frame.Refresh(0);
                    Color expected = (Color32)body.color;
                    foreach (var line in lines)
                    {
                        var actual = line.startColor;
                        Check(Mathf.Abs(actual.r - expected.r) < .005f && Mathf.Abs(actual.g - expected.g) < .005f &&
                            Mathf.Abs(actual.b - expected.b) < .005f, "Every frame element follows the player's displayed color");
                    }
                    Check(boundary.startColor.a > .8f && boundary.startWidth > .04f, "Boundary is brighter and thicker");
                }
            }
            finally { body.color = original; frame.Refresh(); }
            CheckMagicFrame();
            CheckPlayerClip(frame);
            Debug.Log("Playfield frame checks passed: geometry, player colors, earned-color magic flashes, increasing speed and player clipping at every rim edge.");
        }

        private static void CheckPlayerClip(PlayfieldFrame frame)
        {
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            var art = CharacterVisuals.Ensure(player.gameObject);
            var sprites = art.Root.GetComponentsInChildren<SpriteRenderer>(true);
            var visible = Array.ConvertAll(sprites, sprite => sprite.enabled);
            var originalPosition = player.transform.position;
            var camera = Camera.main;
            var framing = camera.GetComponent<GameplayFraming>();
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var cover = GameOverBlackout.Create(frame.transform, null);
            frame.RefreshPlayerClip();
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 480), new Vector2Int(540, 960), new Vector2Int(960, 540) })
                foreach (bool crunch in new[] { false, true })
                {
                    var target = new RenderTexture(size.x, size.y, 24);
                    var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = target;
                        frame.TriggerSpawnPulse();
                        frame.Tick(crunch ? PlayfieldFrame.SpawnPulseSeconds / 2 : PlayfieldFrame.SpawnPulseSeconds);
                        art.RefreshBeat(.5f, true);
                        foreach (bool held in new[] { false, true })
                        {
                            if (held) cover.HoldPlayer(art.Root);
                            else cover.ShowDeath(null);
                            cover.Present(held ? 1 : 0);
                            foreach (var point in new[] { new Vector3(-2.95f, -4.6f), new Vector3(2.95f, -4.6f),
                                new Vector3(0, 5.45f), new Vector3(0, -5.45f) })
                            {
                                player.transform.position = point;
                                var bounds = player.HitBounds;
                                frame.Refresh(0);
                                Check(player.HitBounds == bounds && player.transform.position == point,
                                    "Clipping does not alter the player's position or hitbox");
                                var mask = art.Root.GetComponentInChildren<SpriteMask>();
                                Check(mask != null && mask.enabled && Array.TrueForAll(sprites,
                                    sprite => sprite.maskInteraction == SpriteMaskInteraction.VisibleInsideMask),
                                    "All player parts share the local playfield mask, including above the game-over fade");
                                float inset = frame.GetComponent<LineRenderer>().startWidth / 2;
                                var min = camera.WorldToViewportPoint(new Vector3(-3 + inset, -5.5f + inset));
                                var max = camera.WorldToViewportPoint(new Vector3(3 - inset, 5.5f - inset));
                                var interior = Rect.MinMaxRect(min.x * size.x - 1, min.y * size.y - 1,
                                    max.x * size.x + 1, max.y * size.y + 1);
                                foreach (var sprite in sprites) sprite.enabled = false;
                                var background = ReadFrame(camera, target, pixels);
                                foreach (var sprite in sprites) sprite.enabled = true;
                                var clipped = ReadFrame(camera, target, pixels);
                                if (!held && !crunch)
                                    System.IO.File.WriteAllBytes("TestResults/player-clipped-" + size.x + "x" + size.y + "-" +
                                        (point.x < 0 ? "left" : point.x > 0 ? "right" : point.y > 0 ? "top" : "bottom") + ".png", pixels.EncodeToPNG());
                                foreach (var sprite in sprites) sprite.maskInteraction = SpriteMaskInteraction.None;
                                var unclipped = ReadFrame(camera, target, pixels);
                                foreach (var sprite in sprites) sprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                                int inside = 0, outside = 0, unmaskedOutside = 0;
                                for (int i = 0; i < clipped.Length; i++)
                                {
                                    bool inView = interior.Contains(new Vector2(i % size.x + .5f, i / size.x + .5f));
                                    if (Different(clipped[i], background[i]))
                                    { if (inView) inside++; else outside++; }
                                    if (!inView && Different(unclipped[i], background[i])) unmaskedOutside++;
                                }
                                Check(inside > 8 && outside == 0 && unmaskedOutside > 8,
                                    "Only the portion inside the rim is visible at " + size + ", position=" + point +
                                    ", crunch=" + crunch + ", held=" + held + ", inside=" + inside +
                                    ", outside=" + outside + ", unmaskedOutside=" + unmaskedOutside);
                            }
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
                cover.ShowDeath(null);
                frame.enabled = false;
                Check(Array.TrueForAll(sprites, sprite => sprite.maskInteraction == SpriteMaskInteraction.None),
                    "Disabling the frame restores normal sprite rendering without leaving an orphaned mask");
                frame.enabled = true;
                frame.RefreshPlayerClip();
                frame.Refresh();
                Check(Array.TrueForAll(sprites, sprite => sprite.maskInteraction == SpriteMaskInteraction.VisibleInsideMask),
                    "Re-enabling the frame restores the clip");
            }
            finally
            {
                cover.ShowDeath(null);
                UnityEngine.Object.DestroyImmediate(cover.gameObject);
                player.transform.position = originalPosition;
                for (int i = 0; i < sprites.Length; i++) sprites[i].enabled = visible[i];
                art.RefreshBeat(0, true);
                frame.Tick(PlayfieldFrame.SpawnPulseSeconds);
                framing.SetWaveCrunch(0);
                frame.Refresh();
                if (!Application.isPlaying)
                {
                    frame.enabled = false;
                    frame.enabled = true;
                }
            }
        }

        private static bool Different(Color32 a, Color32 b) =>
            Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 12;

        private static Color32[] ReadFrame(Camera camera, RenderTexture target, Texture2D pixels)
        {
            UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            return pixels.GetPixels32();
        }
        private static void CheckMagicFrame()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var root = new GameObject("Magic frame test", typeof(LineRenderer), typeof(PlayfieldFrame));
                try
                {
                    var frame = root.GetComponent<PlayfieldFrame>(); frame.Configure(player);
                    frame.RefreshBeat(0); CheckTint(frame, Color.white);
                    foreach (float beat in new[] { .25f, .75f, 1f, 2.75f, 99.5f })
                    { frame.RefreshBeat(beat); CheckTint(frame, Color.white); }
                    var colors = (EnemyColor[])Enum.GetValues(typeof(EnemyColor));
                    foreach (var color in colors) ProgressionChecks.Add(grid, color, (int)color, 0);
                    float previousInterval = float.MaxValue;
                    for (int count = 1; count <= colors.Length; count++)
                    {
                        var enemy = grid.Model.At(count - 1, 0);
                        grid.ClearMatchingChain(enemy.Id, colors[count - 1]);
                        // The last fleet clear consumes charges; exercise the full-bar presentation independently.
                        typeof(PlayerMovement).GetProperty("MagicCharges").SetValue(player, 1);
                        float interval = Mathf.Max(.2f, .4f - .04f * (count - 1));
                        Check(interval < previousInterval, "Each earned bar speeds up the flashes");
                        previousInterval = interval;
                        for (int phase = 0; phase < count * 2 + 2; phase++)
                        {
                            frame.Refresh((phase + .25f) * interval);
                            Color expected = phase % 2 == 0 ? Color.white : EnemyPalette.Get(colors[(phase / 2) % count]);
                            CheckTint(frame, expected);
                        }
                        for (int beat = 0; beat < count + 1; beat++)
                        {
                            frame.RefreshBeat(beat + .1f); CheckTint(frame, Color.white);
                            frame.RefreshBeat(beat + .75f);
                            CheckTint(frame, player.HasFleet ? player.DisplayColor : Color.white);
                        }
                    }
                    typeof(PlayerMovement).GetProperty("MagicCharges").SetValue(player, 0);
                    frame.Refresh(.1f); CheckTint(frame, player.DisplayColor);
                    typeof(PlayerMovement).GetProperty("ReadyColor").SetValue(player, EnemyColor.Red);
                    player.RefreshPresentation(0);
                    grid.ResetColorClearStreak();
                    frame.RefreshBeat(.75f); CheckTint(frame, Color.white);
                    typeof(PlayerMovement).GetProperty("MagicCharges").SetValue(player, 1);
                    grid.ResetColorClearStreak();
                    frame.Refresh(.1f); CheckTint(frame, player.DisplayColor);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            });
        }
        private static void CheckTint(PlayfieldFrame frame, Color expected)
        {
            foreach (var line in frame.GetComponentsInChildren<LineRenderer>())
            {
                var actual = line.startColor;
                Check(Mathf.Abs(actual.r - expected.r) < .005f && Mathf.Abs(actual.g - expected.g) < .005f &&
                    Mathf.Abs(actual.b - expected.b) < .005f, "Frame alternates white and earned colors only, or restores the player tint");
            }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Playfield frame check failed: " + message); }
    }
}
