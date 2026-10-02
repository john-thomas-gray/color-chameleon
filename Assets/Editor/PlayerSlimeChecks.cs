using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlayerSlimeChecks
    {
        public static void Run()
        {
            CheckMissileGaze();
            foreach (float lean in new[] { -24f, -18, 0, 18, 24 })
                Check(PlayerSlimeVisual.JellyOffset(0, lean) == 0, "Bottom row is pinned at every lean");
            Check(PlayerSlimeVisual.JellyOffset(1, 18) < PlayerSlimeVisual.JellyOffset(.5f, 18),
                "Upper body bends farther opposite motion than the lower body");
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                VisualUpgrade.Upgrade(player.gameObject);
                var art = CharacterVisuals.Ensure(player.gameObject);
                var hitBounds = art.HitBounds;
                var rootRotation = art.Root.rotation;
                var slime = player.gameObject.AddComponent<PlayerSlimeVisual>();
                slime.Configure(art);
                var texture = art.Body.sprite.texture;
                var baseline = texture.GetPixels32();
                var copy = new LifeSlimeArtwork(); copy.Bind(art);
                var sprites = art.Root.GetComponentsInChildren<SpriteRenderer>(true);
                Check(copy.PartCount == sprites.Length, "Life copies include every original sprite piece");
                for (int i = 0; i < copy.PartCount; i++)
                    Check(Array.Exists(sprites, part => part.sprite == copy.PartSprite(i)),
                        "Life copies use original sprites rather than separately drawn eyes");
                var neutralBounds = copy.Bounds;
                float floor = art.Body.bounds.min.y;
                for (int step = 0; step < 20; step++) slime.TickMotion(.12f, .02f);
                var deformed = texture.GetPixels32();
                bool changed = false;
                for (int pixel = 0; pixel < baseline.Length; pixel++)
                {
                    if (pixel < texture.width) Check(baseline[pixel].Equals(deformed[pixel]), "Base pixels never move");
                    else changed |= !baseline[pixel].Equals(deformed[pixel]);
                }
                Check(changed && art.Root.rotation == rootRotation && art.HitBounds == hitBounds,
                    "Jelly bends without rotating the whole body or changing collision bounds");
                var movingCopy = new LifeSlimeArtwork(); movingCopy.Bind(art);
                Check(Vector2.Distance(movingCopy.Bounds.size, neutralBounds.size) < .0001f,
                    "Life proportions ignore the live player's current jelly lean");
                for (int i = 0; i < copy.PartCount; i++)
                    Check(Vector2.Distance(copy.PartRect(i).position, movingCopy.PartRect(i).position) < .0001f,
                        "Copied eyes retain their neutral positions while the player moves");
                var fitted = copy.Fit(new Rect(0, 0, 25, 18));
                Check(Mathf.Abs(fitted.width / fitted.height - neutralBounds.width / neutralBounds.height) < .0001f,
                    "Miniatures retain the player's exact proportions at small sizes");
                copy.Dispose(); movingCopy.Dispose();
                art.RefreshBeat(.5f, true);
                Check(Mathf.Abs(art.Body.bounds.min.y - floor) < .0001f,
                    "Even peak beat growth keeps the base on the ground");
                var neutralSize = art.Body.bounds.size;
                foreach (float scale in new[] { .84f, 1.13f, 1f })
                {
                    art.SetJumpStretch(scale);
                    Check(Mathf.Abs(art.Body.bounds.size.y - neutralSize.y * scale) < .0001f &&
                        Mathf.Abs(art.Body.bounds.min.y - floor) < .0001f && art.HitBounds == hitBounds,
                        "Jump squash and stretch affects art from its feet, never collision bounds");
                }
                for (int step = 0; step < 300; step++) slime.TickMotion(0, .02f);
                art.BeginLanding(); art.TickLanding(CharacterVisuals.LandingSeconds * .2f);
                Check(art.LandingActive && art.Body.bounds.size.y < neutralSize.y * .8f &&
                    Mathf.Abs(art.Body.bounds.min.y - floor) < .0001f && art.HitBounds == hitBounds,
                    "Landing compresses from the feet without changing collision bounds");
                var pausedSize = art.Body.bounds.size;
                art.TickLanding(0); art.TickLanding(-1);
                Check(art.Body.bounds.size == pausedSize, "Pause and negative ticks preserve landing pose");
                art.SetJumpStretch(1);
                Check(art.Body.bounds.size == pausedSize, "Ordinary movement presentation cannot overwrite landing squash");
                art.TickLanding(CharacterVisuals.LandingSeconds * .5f);
                Check(art.Body.bounds.size.y > neutralSize.y * 1.09f, "Landing rebounds into a short vertical stretch");
                art.TickLanding(1);
                Check(!art.LandingActive && Vector3.Distance(art.Body.bounds.size, neutralSize) < .0001f,
                    "Landing settles exactly back to neutral even after a long frame");
                art.BeginLanding(); art.TickLanding(.05f); player.ResetForRun();
                Check(!art.LandingActive && Vector3.Distance(art.Body.bounds.size, neutralSize) < .0001f,
                    "Restart cancels any unfinished landing deformation");
                var settled = texture.GetPixels32();
                for (int pixel = 0; pixel < baseline.Length; pixel++)
                    Check(Mathf.Abs(baseline[pixel].a - settled[pixel].a) <= 1, "Jelly settles back to its resting silhouette");
            });
            Debug.Log("Player slime checks passed: smooth in-field missile gaze, all four exit boundaries, target handoff, neutral life copies, pinned base, upper-body deformation, stable hitboxes, grounded beat and settling.");
        }

        private static void CheckMissileGaze()
        {
            var existing = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
            var enabled = existing.Select(missile => missile.enabled).ToArray();
            foreach (var missile in existing) missile.enabled = false;
            try
            {
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var source = player.GetComponentInChildren<SpriteRenderer>().sprite;
                    foreach (float x in new[] { -.17f, .17f })
                    foreach (bool pupil in new[] { false, true })
                    {
                        var part = new GameObject(pupil ? "Pupil" : "Eye", typeof(SpriteRenderer));
                        part.transform.SetParent(player.transform, false);
                        part.transform.localPosition = new Vector3(x, pupil ? .20f : .16f, 0);
                        part.transform.localScale = Vector3.one * (pupil ? .085f : .21f);
                        part.GetComponent<SpriteRenderer>().sprite = source;
                    }
                    VisualUpgrade.Upgrade(player.gameObject);
                    var art = CharacterVisuals.Ensure(player.gameObject);
                    var slime = player.gameObject.AddComponent<PlayerSlimeVisual>(); slime.Configure(art);
                    var pupils = art.Root.GetComponentsInChildren<SpriteRenderer>().Where(part => part.name == "Pupil").ToArray();
                    var eyes = art.Root.GetComponentsInChildren<SpriteRenderer>().Where(part => part.name == "Eye").ToArray();
                    var eyeCenter = (eyes[0].transform.position + eyes[1].transform.position) / 2;
                    var neutral = pupils.Select(part => part.transform.localPosition).ToArray();
                    var eyePositions = eyes.Select(part => part.transform.localPosition).ToArray();
                    var bounds = art.HitBounds;
                    var neutralCopy = new LifeSlimeArtwork(); neutralCopy.Bind(art);
                    var near = new GameObject("Near missile", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                    var far = new GameObject("Far missile", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                    near.transform.position = eyeCenter + Vector3.right;
                    far.transform.position = player.transform.position + Vector3.left * 3;
                    slime.TickGaze(.02f);
                    Check(slime.GazeTarget == near && pupils.All(part => part.transform.localPosition.x >
                        neutral[Array.IndexOf(pupils, part)].x && part.transform.localPosition.x < neutral[Array.IndexOf(pupils, part)].x + .04f),
                        "Both pupils begin easing toward the nearest missile without snapping");
                    var first = pupils[0].transform.localPosition;
                    slime.TickGaze(0); slime.TickGaze(-1);
                    Check(pupils[0].transform.localPosition == first, "Paused and negative time leave the gaze fixed");
                    slime.TickGaze(1);
                    Check(Mathf.Abs(pupils[0].transform.localPosition.x - neutral[0].x - .04f) < .0001f &&
                        eyes.Select(part => part.transform.localPosition).SequenceEqual(eyePositions) && art.HitBounds == bounds,
                        "Gaze settles within the eye without moving the eye whites or collision bounds");
                    far.transform.position = player.transform.position + Vector3.left * .5f;
                    slime.TickGaze(.02f);
                    Check(slime.GazeTarget == far && pupils[0].transform.localPosition.x > neutral[0].x,
                        "A closer missile changes the target immediately but not the pupil position abruptly");
                    slime.TickGaze(.5f);
                    Check(pupils[0].transform.localPosition.x < neutral[0].x, "Pupils continue smoothly toward the new side");
                    slime.TickMotion(.12f, .02f);
                    var lookingCopy = new LifeSlimeArtwork(); lookingCopy.Bind(art);
                    for (int i = 0; i < neutralCopy.PartCount; i++)
                        Check(Vector2.Distance(neutralCopy.PartRect(i).position, lookingCopy.PartRect(i).position) < .0001f,
                            "Spare-life artwork never inherits the live player's gaze or jelly deformation");
                    neutralCopy.Dispose(); lookingCopy.Dispose();
                    far.enabled = false; slime.TickGaze(.02f);
                    Check(slime.GazeTarget == near, "Disabled missiles are not gaze targets");
                    far.enabled = true; far.gameObject.SetActive(false); slime.TickGaze(.02f);
                    Check(slime.GazeTarget == near, "Inactive missiles are not gaze targets");
                    UnityEngine.Object.DestroyImmediate(near.gameObject);
                    slime.TickGaze(.02f);
                    Check(slime.GazeTarget == null, "Destroying the last active missile clears the target safely");
                    for (int i = 0; i < 300; i++) slime.TickMotion(0, .02f);
                    slime.TickGaze(2);
                    Check(pupils.Select((part, i) => Vector3.Distance(part.transform.localPosition, neutral[i])).All(distance => distance < .0001f),
                        "No missiles returns the eyes smoothly to their authored resting gaze");
                    far.gameObject.SetActive(true); far.transform.position = player.transform.position + Vector3.right;
                    slime.TickGaze(.1f);
                    var coarse = pupils[0].transform.localPosition;
                    far.gameObject.SetActive(false); slime.TickGaze(2); far.gameObject.SetActive(true);
                    for (int i = 0; i < 10; i++) slime.TickGaze(.01f);
                    Check(Vector3.Distance(pupils[0].transform.localPosition, coarse) < .0001f,
                        "Gaze smoothing agrees across large and small frames");
                    eyeCenter = (eyes[0].transform.position + eyes[1].transform.position) / 2;
                    far.transform.position = eyeCenter + Vector3.up * .5f;
                    slime.TickGaze(1);
                    var upward = pupils[0].transform.localPosition;
                    far.transform.position = eyeCenter + Vector3.down * .08f;
                    Check(far.transform.position.y > player.transform.position.y,
                        "Downward regression target lies below the eyes but above the body origin");
                    slime.TickGaze(.02f);
                    Check(pupils[0].transform.localPosition.y < upward.y &&
                        pupils[0].transform.localPosition.y > eyePositions[0].y,
                        "An upward-to-downward gaze eases through the eye instead of snapping");
                    slime.TickGaze(1);
                    Check(pupils.All(part => part.transform.position.y < eyeCenter.y),
                        "Missiles below eye level move both pupils into the lower halves of the eyes");
                    for (int angle = 0; angle < 360; angle += 45)
                    {
                        var direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0);
                        far.transform.position = eyeCenter + direction * .5f;
                        slime.TickGaze(1);
                        for (int i = 0; i < pupils.Length; i++)
                        {
                            var offset = pupils[i].transform.localPosition - eyePositions[i];
                            Check(Vector3.Angle(offset, direction) < .1f && Mathf.Abs(offset.magnitude - .04f) < .0001f,
                                "Pupils track the entire 360-degree circle within the eye boundary");
                        }
                    }
                    var remaining = new GameObject("In-field gaze fallback", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                    foreach (var outward in new[] { Vector3.left, Vector3.right, Vector3.up, Vector3.down })
                    {
                        var edge = outward * (outward.x != 0 ? PlayerMovement.HalfWidth : 5.5f);
                        player.transform.position = edge - outward * .2f;
                        remaining.transform.position = edge - outward * 1.5f;
                        remaining.gameObject.SetActive(true);
                        far.transform.position = edge;
                        slime.TickGaze(.02f);
                        Check(slime.GazeTarget == far, "A missile on the playfield boundary remains a gaze target");
                        far.transform.position = edge + outward * .05f;
                        slime.TickGaze(.02f);
                        Check(slime.GazeTarget == remaining && far.isActiveAndEnabled && !far.Finished,
                            "Crossing any rim drops the closer current target while the off-field missile awaits cleanup");
                        remaining.gameObject.SetActive(false);
                        slime.TickGaze(.02f);
                        Check(slime.GazeTarget == null, "A still-active off-field missile cannot be selected again");
                        slime.TickGaze(2);
                        Check(pupils.Select((part, i) => Vector3.Distance(part.transform.localPosition, neutral[i])).All(distance => distance < .0001f),
                            "With no in-field missiles the pupils return to their resting gaze");
                        far.transform.position = edge - outward * .05f;
                        slime.TickGaze(.02f);
                        Check(slime.GazeTarget == far, "A missile inside the rim becomes eligible again");
                    }
                });
            }
            finally
            {
                for (int i = 0; i < existing.Length; i++)
                    if (existing[i] != null) existing[i].enabled = enabled[i];
            }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Player slime check failed: " + message); }
    }
}
