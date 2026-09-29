using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class SpecialEnemyChecks
    {
        public static void CapturePreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            var effects = new System.Collections.Generic.List<EnemyDeathBurst>();
            try
            {
                Add(grid, EnemyColor.Blue, 0, 0);
                Add(grid, EnemyColor.Blue, 1, 0);
                Add(grid, EnemyColor.Blue, 1, 1);
                Add(grid, EnemyColor.Red, 3, 0);
                Add(grid, EnemyColor.Red, 4, 0);
                Add(grid, EnemyColor.Red, 4, 1);
                foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>()) ability.Tick(0);
                grid.RefreshSpecials();
                for (int x = 0; x < 5; x++)
                {
                    var enemy = Add(grid, EnemyColor.Purple, x, 4);
                    var effect = EnemyDeathBurst.Create(enemy.GetComponentInChildren<SpriteRenderer>(), EnemyColor.Purple, Mathf.Abs(x - 2) + 1);
                    effect.Tick(.18f);
                    effects.Add(effect);
                    grid.Unregister(enemy);
                    UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                }
                var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
                player.RefreshColor();
                GameplayChecks.Capture(540, 960, "special-enemies-portrait");
                GameplayChecks.Capture(960, 540, "special-enemies-landscape");
                GameplayChecks.Capture(390, 844, "special-enemies-phone");
                foreach (var effect in effects)
                {
                    var text = effect.GetComponentInChildren<TextMesh>();
                    Check(text.text == "" || text.GetComponent<MeshRenderer>().bounds.size.x > .05f, "Visible death digit has renderable glyph geometry");
                }
            }
            finally
            {
                foreach (var effect in effects) UnityEngine.Object.DestroyImmediate(effect.gameObject);
            }
            Debug.Log("Special enemy previews rendered in portrait, landscape and tall phone views.");
        }

        public static void Run()
        {
            var model = new GridModel();
            model.TryAdd(1, EnemyColor.Red, 2, 2);
            model.TryAdd(2, EnemyColor.Red, 1, 2);
            model.TryAdd(3, EnemyColor.Red, 3, 2);
            model.TryAdd(4, EnemyColor.Red, 2, 1);
            model.TryAdd(5, EnemyColor.Red, 1, 1);
            model.TryAdd(6, EnemyColor.Yellow, 3, 1);
            model.BeginImitation(6, 3);
            var depths = model.MatchingDepths(1, EnemyColor.Red);
            Check(depths[1] == 1 && depths[2] == 2 && depths[3] == 2 && depths[4] == 2 && depths[5] == 3 && depths[6] == 3,
                "Death digits use shortest connection depth, including branches, loops and Yellow links");
            Check(model.MatchingDepths(1, EnemyColor.Blue).Count == 0 && model.Count == 6, "Depth preview neither kills nor accepts a mismatch");
            Check(model.ClearMatchingChain(1, EnemyColor.Red).Count == 6 && model.Count == 0, "Every planned branch clears exactly once");

            Fixture((grid, player, tongue) =>
            {
                var red = Add(grid, EnemyColor.Red, 4, 0);
                player.RefreshColor();
                var blue = Add(grid, EnemyColor.Blue, 0, 0);
                Add(grid, EnemyColor.Blue, 1, 0);
                grid.RefreshSpecials();
                Check(!blue.IsSpecial && grid.GroupShield.EdgeCount == 0, "A Blue pair remains ordinary");
                var lower = Add(grid, EnemyColor.Blue, 1, 1);
                grid.RefreshSpecials();
                Check(blue.Tier == 2 && lower.Tier == 2 && !red.IsSpecial && grid.GroupShield.EdgeCount == 8,
                    "Three connected Blues upgrade and produce only the eight exterior edges of an L group");
                Check(blue.GetComponentInChildren<SpriteRenderer>().sprite == EnemyPlaceholderArt.Triangle &&
                    !blue.Visuals.Root.Find("Shield").GetComponentInChildren<SpriteRenderer>().enabled,
                    "Tier two uses triangle art and replaces individual shields with a group perimeter");
                player.transform.position = new Vector3(lower.transform.position.x, -4.6f, 0);
                Check(player.Fire(), "Fire a mismatched ordinary shot at the rigid shield");
                tongue.Tick(.26f, grid);
                Check(tongue.Active && tongue.Retracting && player.Stunned && player.DisplayColor == Color.gray && grid.Model.Count == 4,
                    "Rigid shield survives and makes the player grey during retraction");
                var position = player.transform.position;
                player.Move(1, 1);
                player.BeginPointer(Vector2.zero);
                Check(player.transform.position == position && !player.Fire(), "Stun blocks movement and firing");
                tongue.Tick(1, grid);
                Check(!player.Stunned && !tongue.Active && player.DisplayColor != Color.gray && player.Alive && !player.Invulnerable,
                    "Retraction restores color and controls without a death or invulnerability period");
                player.Move(1, .1f);
                Check(player.transform.position != position, "Movement resumes after tongue return");
                player.transform.position = position;
                // The return now selects a new ready color; explicitly repeat the mismatched shot.
                tongue.TryFire(EnemyColor.Red, 10); tongue.Tick(.26f, grid);
                Check(player.Stunned, "Second mismatch also deflects without breaking shield");
                player.CancelShot();
                Check(!player.Stunned, "Cancellation cannot strand the player in grey stun");
                tongue.TryFire(EnemyColor.Blue, 10);
                tongue.Tick(1, grid);
                Check(grid.Model.ColorCount(EnemyColor.Blue) == 0 && grid.GroupShield.EdgeCount == 0 && !player.Stunned,
                    "Matching Blue passes through and clears the shielded group and outline");
            });

            Fixture((grid, player, tongue) =>
            {
                var a = Add(grid, EnemyColor.Blue, 0, 0);
                Add(grid, EnemyColor.Blue, 1, 0);
                Add(grid, EnemyColor.Blue, 2, 0);
                var separate = Add(grid, EnemyColor.Blue, 4, 2);
                grid.RefreshSpecials();
                Check(!grid.GroupShield.Protects(separate.Id), "Disconnected Blue group is not shielded by another group");
                foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>()) ability.BeginSpawnEffect();
                grid.RefreshSpecials();
                Check(grid.GroupShield.EdgeCount == 0, "Rigid shield waits for arrival and power-up");
                foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>()) ability.Tick(1.15f);
                grid.RefreshSpecials();
                Check(grid.GroupShield.EdgeCount == 8, "Three-cell line has only its exterior perimeter after power-up");
                player.transform.position = new Vector3(a.transform.position.x, -4.6f, 0);
                tongue.TryFire(EnemyColor.Red, 10, true);
                tongue.Tick(1, grid);
                Check(grid.Model.ColorCount(EnemyColor.Blue) == 1 && !player.Stunned, "Magic retains its shield bypass");
            });

            Fixture((grid, player, tongue) =>
            {
                var red = Add(grid, EnemyColor.Red, 1, 0);
                Add(grid, EnemyColor.Red, 2, 0);
                Add(grid, EnemyColor.Red, 3, 0);
                Add(grid, EnemyColor.Green, 0, 3);
                Add(grid, EnemyColor.Green, 1, 3);
                Add(grid, EnemyColor.Green, 2, 3);
                grid.RefreshSpecials();
                Check(red.IsSpecial && grid.GetComponentsInChildren<GridEnemy>().Where(e => e.Color == EnemyColor.Green).All(e => e.IsSpecial),
                    "Red and Green groups both receive tier two");
                red.GetComponent<EnemyAbilities>().Tick(100);
                var homing = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Last(m => m.Aimed);
                Check(!homing.Homing, "Special Red launches an aimed straight shot, not a homing missile");
                // Retain checks for the unused legacy homing mode.
                homing.SetTarget(player, true);
                homing.transform.position = new Vector3(-2, 2, 0);
                player.transform.position = new Vector3(2, -4.6f, 0);
                homing.Tick(.4f);
                Check(homing.transform.position.x > -1.99f && homing.transform.position.x < -1.85f &&
                    Mathf.Abs(Mathf.DeltaAngle(0, homing.transform.eulerAngles.z) - 7.2f) < .01f &&
                    Mathf.Abs(homing.transform.position.y - .6f) < .02f,
                    "Homing turns directly toward the player with original gentle steering and 3.5-unit speed");
                homing.Suspended = true;
                var point = homing.transform.position;
                homing.Tick(1);
                Check(homing.transform.position == point, "Suspended homing missile cannot move");
                homing.Suspended = false;
                homing.Tick(6);
                Check(homing.Finished, "Homing missile collides or expires within its lifetime");
                player.TickSurvival(10);
                var coarse = new GameObject("Coarse homing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                var fine = new GameObject("Fine homing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                coarse.SetTarget(player, true); fine.SetTarget(player, true);
                coarse.transform.position = fine.transform.position = new Vector3(-2, 2, 0);
                coarse.Tick(.5f);
                for (int i = 0; i < 60; i++) fine.Tick(1f / 120);
                Check(Vector3.Distance(coarse.transform.position, fine.transform.position) < .001f,
                    "Long-frame and stepped homing agree");
                UnityEngine.Object.DestroyImmediate(coarse.gameObject);
                UnityEngine.Object.DestroyImmediate(fine.gameObject);
                var limited = new GameObject("Limited homing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                limited.SetTarget(player, true);
                limited.transform.position = new Vector3(-2.9f, 5.29f, 0);
                for (int step = 0; step < 360; step++)
                {
                    player.transform.position = new Vector3(limited.transform.position.x + 2, 10, 0);
                    limited.Tick(1f / 120);
                }
                Check(!limited.Finished && Mathf.Abs(Mathf.DeltaAngle(0, limited.transform.eulerAngles.z) - 50) < .01f,
                    "Homing tilt is capped at 50 degrees even when the target is above");
                float previousY = limited.transform.position.y;
                for (int step = 0; step < 60; step++)
                {
                    player.transform.position = new Vector3(limited.transform.position.x - 2, 10, 0);
                    limited.Tick(1f / 120);
                }
                Check(limited.transform.position.y < previousY &&
                    Mathf.Abs(Mathf.DeltaAngle(0, limited.transform.eulerAngles.z) - 41) < .01f,
                    "Reversing the target eases steering without looping upward");
                limited.Tick(3);
                Check(limited.Finished, "Missile leaving the bottom is retired permanently");
                UnityEngine.Object.DestroyImmediate(limited.gameObject);
                foreach (float side in new[] { -1f, 1f })
                {
                    var escaped = new GameObject("Offscreen homing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                    escaped.SetTarget(player, true);
                    escaped.transform.position = new Vector3(side * (PlayerMovement.HalfWidth + .01f), 0, 0);
                    var outside = escaped.transform.position;
                    escaped.Tick(.5f);
                    Check(escaped.Finished && escaped.transform.position == outside,
                        "Inaudible offscreen homing missiles retire without wrapping back into the field");
                    UnityEngine.Object.DestroyImmediate(escaped.gameObject);
                }
                player.transform.position = new Vector3(2, -4.6f, 0);
                var incoming = new GameObject("Incoming homing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                incoming.SetTarget(player, true);
                incoming.transform.position = player.transform.position + Vector3.up * 2;
                incoming.Tick(1);
                Check(incoming.Finished && !player.Alive, "Swept homing collision cannot tunnel through the player");
                UnityEngine.Object.DestroyImmediate(incoming.gameObject);
                var ordinary = new GameObject("Straight missile", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                ordinary.SetTarget(player);
                Check(ordinary.SmokeTrail != null && !ordinary.SmokeTrail.enabled, "Ordinary missile smoke trail starts empty");
                ordinary.transform.position = new Vector3(-2, 2, 0);
                ordinary.Tick(.5f);
                Check(ordinary.transform.position.x == -2 && !ordinary.Homing, "Ordinary missile stays straight");
                var smoke = ordinary.SmokeTrail;
                Check(smoke != null && smoke.enabled && smoke.positionCount == 4, "Missiles draw a smoke trail");
                Check(Mathf.Abs(smoke.startWidth - smoke.endWidth) < .001f &&
                    Mathf.Abs(smoke.startWidth - EnemyMissile.FallbackMissileWidth * EnemyMissile.SmokeTrailWidthRatio) < .001f,
                    "Missile smoke trail keeps a uniform half-missile width");
                Vector3 smokeTail = smoke.GetPosition(0), smokeHead = smoke.GetPosition(smoke.positionCount - 1);
                float screenThird = (Camera.main != null && Camera.main.orthographic ?
                    Camera.main.orthographicSize * 2 : EnemyMissile.FallbackViewHeight) * EnemyMissile.SmokeTrailScreenRatio;
                Check(Vector3.Distance(smokeTail, smokeHead) < screenThird &&
                    Mathf.Abs(Vector3.Distance(smokeTail, smokeHead) - 2.5f) < .02f,
                    "Ordinary missile smoke trail grows from flight distance before reaching full length");
                ordinary.Tick(.4f);
                smokeTail = smoke.GetPosition(0); smokeHead = smoke.GetPosition(smoke.positionCount - 1);
                Check(Mathf.Abs(Vector3.Distance(smokeTail, smokeHead) - screenThird) < .02f,
                    "Missile smoke trail reaches about one third of the screen");
                Check(Vector3.Angle(smokeHead - smokeTail, Vector3.down) < .05f,
                    "Ordinary missile smoke trail follows its heading");
                UnityEngine.Object.DestroyImmediate(ordinary.gameObject);
                var flashing = new GameObject("Flashing homing", typeof(SpriteRenderer), typeof(EnemyMissile));
                var renderer = flashing.GetComponentInChildren<SpriteRenderer>();
                renderer.color = EnemyPalette.Get(EnemyColor.Red);
                var original = renderer.color;
                var pulse = flashing.GetComponent<EnemyMissile>();
                flashing.transform.position = Vector3.up * 4.5f;
                pulse.SetTarget(null, true);
                float halfFlash = 30f / GameplayMusicPlayer.DefaultBeatsPerMinute * EnemyMissile.DefaultFarFlashBeats;
                pulse.Tick(halfFlash);
                Check(renderer.color.g > original.g + .5f && renderer.color.a == original.a,
                    "Homing missile brightens on its beat flash without becoming transparent");
                var bright = renderer.color;
                pulse.Suspended = true;
                pulse.Tick(1);
                Check(renderer.color == bright, "Suspension freezes the flash with the missile");
                pulse.Suspended = false;
                pulse.Tick(halfFlash);
                Check(Vector4.Distance(renderer.color, original) < .02f, "Beat pulse returns to its original color after one far flash cycle");
                pulse.SetTarget(null, false);
                pulse.Tick(halfFlash);
                Check(renderer.color.g > original.g + .5f, "Ordinary missiles share the beat flash");
                UnityEngine.Object.DestroyImmediate(flashing);
            });

            foreach (var color in new[] { EnemyColor.Green, EnemyColor.Purple, EnemyColor.Yellow })
                Fixture((grid, player, tongue) =>
                {
                    var first = Add(grid, color, 0, 0);
                    Add(grid, color, 1, 0);
                    var diagonal = Add(grid, color, 2, 1);
                    grid.RefreshSpecials();
                    Check(!first.IsSpecial && !diagonal.IsSpecial, "Pairs and diagonals do not promote " + color);
                    Add(grid, color, 1, 1);
                    grid.RefreshSpecials();
                    Check(grid.GetComponentsInChildren<GridEnemy>().All(e => e.IsSpecial &&
                        e.GetComponentInChildren<SpriteRenderer>().sprite == EnemyPlaceholderArt.Triangle), "Connected group promotes every " + color);
                    grid.SetColor(first.Id, EnemyColor.Red);
                    Check(!first.IsSpecial, "Changing color resets the upgraded tier");
                });

            Fixture((grid, player, tongue) =>
            {
                var movement = grid.GetComponent<EnemyGridMovement>();
                var green = Add(grid, EnemyColor.Green, 0, 0);
                Add(grid, EnemyColor.Green, 1, 0);
                var ability = green.GetComponent<EnemyAbilities>();
                ability.Tick(100);
                Check(!ability.IsCasting && !ability.Warning && !movement.UseGreenDashes && grid.transform.position == Vector3.zero,
                    "Ordinary Greens remain count-based without warnings or dashes");
                Add(grid, EnemyColor.Green, 2, 0);
                grid.RefreshSpecials();
                ability.Tick(0);
                Check(!ability.IsCasting && !ability.Warning, "Promotion starts a fresh cooldown instead of releasing an overdue dash");
                Check(Mathf.Abs(movement.CurrentSpeed - .09f) < .001f, "Special Greens still contribute normal count-based movement");
                for (int i = 0; i < 160 && !ability.IsCasting; i++) ability.Tick(.1f);
                Check(ability.IsCasting && ability.Warning && grid.transform.position == Vector3.zero, "Special Green warns before its dash");
                ability.Suspended = true;
                ability.Tick(5);
                Check(ability.IsCasting && grid.transform.position == Vector3.zero, "Suspension freezes the warning");
                ability.Suspended = false;
                ability.Tick(.6f);
                ability.Tick(.18f);
                Check(Mathf.Abs(grid.transform.position.x - .1f) < .001f && !movement.UseGreenDashes,
                    "Tier-two Green restores the short fast-forward step without enabling global legacy mode");
                grid.SetColor(green.Id, EnemyColor.Red);
                ability.Tick(0);
                var position = grid.transform.position;
                ability.Tick(.18f);
                Check(grid.transform.position == position, "Changing color cancels remaining Green movement");
            });

            Fixture((grid, player, tongue) =>
            {
                var source = Add(grid, EnemyColor.Red, 0, 0).GetComponentInChildren<SpriteRenderer>();
                var first = EnemyDeathBurst.Create(source, EnemyColor.Red, 1);
                var third = EnemyDeathBurst.Create(source, EnemyColor.Blue, 3);
                try
                {
                    Check(first.Exploded && !third.Exploded, "Impact explodes before the outer branches");
                    third.Tick(EnemyDeathBurst.RingDelay * 2);
                    Color digitColor = third.GetComponentInChildren<TextMesh>().color;
                    Check(third.Exploded && third.GetComponentInChildren<TextMesh>().text == "3" &&
                        Vector4.Distance(digitColor, EnemyPalette.Get(EnemyColor.Blue)) < .01f, "Branch becomes its own colored digit");
                    Check(third.GetComponentInChildren<GridEnemy>() == null && third.GetComponentInChildren<EnemyAbilities>() == null,
                        "Death placeholders cannot fire, be hit or score again");
                    first.Tick(1); third.Tick(1);
                    Check(first.Finished && third.Finished, "Death effects cleanly finish");
                }
                finally
                {
                    if (first != null) UnityEngine.Object.DestroyImmediate(first.gameObject);
                    if (third != null) UnityEngine.Object.DestroyImmediate(third.gameObject);
                }
            });
            CheckGreenSpin();
            Debug.Log("Special enemy checks passed: branching digits, cascade timing, promotion, Green activation spin, contours, deflection, stun recovery, matching/magic bypass and homing.");
        }

        private static void CheckGreenSpin()
        {
            Fixture((grid, player, tongue) =>
            {
                var green = Add(grid, EnemyColor.Green, 0, 0);
                var ability = green.GetComponent<EnemyAbilities>();
                var body = green.Visuals.Body.transform;
                var resting = Quaternion.Euler(0, 0, 17);
                body.localRotation = resting;
                ability.Tick(100);
                Check(body.localRotation == resting, "Ordinary count-based Greens do not spin");
                var neighbor = Add(grid, EnemyColor.Green, 1, 0);
                Add(grid, EnemyColor.Green, 2, 0);
                grid.RefreshSpecials();
                ability.Tick(0);
                ability.Tick(ability.CooldownRemaining);
                Check(ability.IsCasting && body.localRotation == resting, "Special Green waits until activation to spin");
                var localPosition = green.transform.localPosition;
                var boundsSize = green.HitBounds.size;
                var boundsOffset = green.HitBounds.center - green.transform.position;
                ability.Tick(10);
                Check(!ability.IsCasting && body.localRotation == resting,
                    "A long frame completing the windup cannot consume the new activation spin");
                float cooldown = ability.CooldownRemaining;
                ability.Tick(EnemyPresentation.GreenSpinSeconds / 4);
                Check(Quaternion.Angle(body.localRotation, resting * Quaternion.Euler(0, 0, -90)) < .01f,
                    "The activating special Green turns a quarter revolution");
                Check(neighbor.Visuals.Body.transform.localRotation == Quaternion.identity,
                    "The dash does not spin other fleet members");
                Check(green.transform.localPosition == localPosition && green.transform.localRotation == Quaternion.identity &&
                    green.HitBounds.size == boundsSize &&
                    Vector3.Distance(green.HitBounds.center - green.transform.position, boundsOffset) < .0001f,
                    "Spin changes only artwork, preserving grid placement and collision dimensions");
                var pausedRotation = body.localRotation;
                var pausedPosition = grid.transform.position;
                float pausedCooldown = ability.CooldownRemaining;
                ability.Suspended = true;
                ability.Tick(5);
                Check(body.localRotation == pausedRotation && grid.transform.position == pausedPosition &&
                    ability.CooldownRemaining == pausedCooldown, "Pause freezes spin, dash and cooldown together");
                ability.Suspended = false;
                ability.Tick(-1);
                ability.Tick(0);
                Check(body.localRotation == pausedRotation, "Nonpositive time does not advance the spin");
                ability.Tick(EnemyPresentation.GreenSpinSeconds / 4);
                Check(Quaternion.Angle(body.localRotation, resting * Quaternion.Euler(0, 0, -180)) < .01f,
                    "Resuming continues the spin from its paused angle");
                ability.Tick(EnemyPresentation.GreenSpinSeconds / 2);
                Check(Quaternion.Angle(body.localRotation, resting) < .01f &&
                    Mathf.Abs(grid.transform.position.x - .1f) < .0001f &&
                    Mathf.Abs(ability.CooldownRemaining - (cooldown - EnemyPresentation.GreenSpinSeconds)) < .0001f,
                    "One full spin restores the authored pose without changing dash distance or cooldown duration");
                ability.Tick(ability.CooldownRemaining);
                ability.Tick(.6f);
                ability.Tick(.05f);
                Check(Quaternion.Angle(body.localRotation, resting) > 1, "Each later special activation spins again");
                grid.SetColor(green.Id, EnemyColor.Red);
                ability.Tick(0);
                Check(Quaternion.Angle(body.localRotation, resting) < .01f, "Changing color clears a partial Green spin");
            });
            foreach (float step in new[] { .01f, .05f, 2f })
                Fixture((grid, player, tongue) =>
                {
                    var green = Add(grid, EnemyColor.Green, 0, 0);
                    var presentation = green.GetComponent<EnemyPresentation>();
                    var body = green.Visuals.Body.transform;
                    var resting = body.localRotation;
                    presentation.SpinGreen();
                    float elapsed = 0;
                    while (elapsed < EnemyPresentation.GreenSpinSeconds)
                    {
                        presentation.Tick(step, EnemyColor.Green, 0);
                        elapsed += step;
                    }
                    Check(Quaternion.Angle(body.localRotation, resting) < .01f, "Spin completes with short or long frames");
                    presentation.SpinGreen();
                    presentation.Tick(.05f, EnemyColor.Green, 0);
                    var ability = green.GetComponent<EnemyAbilities>();
                    ability.enabled = false;
                    if (!Application.isPlaying) typeof(EnemyAbilities).GetMethod("OnDisable",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(ability, null);
                    Check(Quaternion.Angle(body.localRotation, resting) < .01f, "Disabling an ability restores its artwork pose");
                });
            Fixture((grid, player, tongue) =>
            {
                grid.GetComponent<EnemyGridMovement>().UseGreenDashes = true;
                var green = Add(grid, EnemyColor.Green, 0, 0);
                var ability = green.GetComponent<EnemyAbilities>();
                ability.Tick(ability.CooldownRemaining);
                ability.Tick(.6f);
                ability.Tick(.05f);
                Check(green.Visuals.Body.transform.localRotation == Quaternion.identity,
                    "Ordinary legacy dashes do not gain the special-only spin");
            });
        }

        private static GridEnemy Add(EnemyGrid grid, EnemyColor color, int x, int y) => ProgressionChecks.Add(grid, color, x, y);
        internal static void Fixture(Action<EnemyGrid, PlayerMovement, TongueShot> check)
        {
            var random = UnityEngine.Random.state;
            var existingMissiles = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
            var root = new GameObject("Special fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            var controller = new GameObject("Special test player", typeof(PlayerMovement), typeof(SpriteRenderer));
            var mouth = new GameObject("Special test tongue", typeof(LineRenderer), typeof(TongueShot));
            mouth.transform.SetParent(controller.transform, false);
            controller.transform.position = Vector3.down * 4.6f;
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(Prefab(EnemyColor.Blue), Prefab(EnemyColor.Red));
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow), Prefab(EnemyColor.Orange));
                grid.ConfigureAbilities(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png"));
                var body = controller.GetComponentInChildren<SpriteRenderer>();
                body.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PlayerPlaceholder.png");
                var player = controller.GetComponent<PlayerMovement>();
                player.Configure(grid, mouth.GetComponent<TongueShot>(), body);
                check(grid, player, mouth.GetComponent<TongueShot>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controller);
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var missile in UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!existingMissiles.Contains(missile)) UnityEngine.Object.DestroyImmediate(missile.gameObject);
                UnityEngine.Random.state = random;
            }
        }
        private static GameObject Prefab(EnemyColor color) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Special enemy check failed: " + message); }
    }
}
