using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class AimedRedChecks
    {
        public static void Run()
        {
            var existingPlayers = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
            foreach (var player in existingPlayers) player.gameObject.SetActive(false);
            try
            {
                foreach (float side in new[] { -2f, 2f })
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                    ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                    ProgressionChecks.Add(grid, EnemyColor.Red, 3, 0);
                    grid.RefreshSpecials();
                    player.transform.position = new Vector3(side, -4.6f, 0);
                    var ability = red.GetComponent<EnemyAbilities>();
                    var body = red.Visuals.Body.transform;
                    var rest = body.localRotation;
                    var bounds = red.HitBounds;
                    var before = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                    ability.Tick(ability.CooldownRemaining - .46f);
                    Check(Quaternion.Angle(rest, body.localRotation) < .001f, "Red rests before its warning");
                    ability.Tick(.02f);
                    float turned = Quaternion.Angle(rest, body.localRotation);
                    Check(turned > 1 && turned < 20, "Warning begins a gradual turn toward the player");
                    Check(NewMissiles(before).Length == 0, "Turning does not fire early");
                    ability.Suspended = true;
                    var paused = body.localRotation;
                    float wait = ability.CooldownRemaining;
                    ability.Tick(10);
                    Check(body.localRotation == paused && ability.CooldownRemaining == wait, "Pause freezes aim and cooldown");
                    ability.Suspended = false;
                    for (int i = 0; i < 15; i++) ability.Tick(.02f);
                    Vector3 direction = (player.transform.position - red.transform.position).normalized;
                    Check(Vector3.Angle(body.rotation * Vector3.down, direction) < .01f, "Triangle points at the player before firing");
                    ability.Tick(ability.CooldownRemaining + .001f);
                    var missile = NewMissiles(before).Single();
                    Check(missile.Aimed && !missile.Homing, "Special Red fires aimed, not homing");
                    Check(Vector3.Angle(missile.transform.rotation * Vector3.down, direction) < .01f,
                        "Projectile heading matches the triangle and target at launch");
                    Check(red.HitBounds == bounds && red.transform.rotation == Quaternion.identity,
                        "Aim leaves gameplay bounds and fleet transform unchanged");
                    var origin = missile.transform.position;
                    player.transform.position = new Vector3(-side, -4.6f, 0);
                    missile.Tick(.2f);
                    Check(Vector3.Distance(missile.transform.position, origin + direction * .7f) < .001f,
                        "Shot flies straight at existing speed after player moves");
                    ability.Tick(.01f);
                    Check(Quaternion.Angle(rest, body.localRotation) > .1f && Quaternion.Angle(rest, body.localRotation) < turned + 25,
                        "Return begins gradually after firing");
                    ability.Tick(.4f);
                    Check(Quaternion.Angle(rest, body.localRotation) < .001f, "Triangle returns to its resting orientation");
                    ability.Tick(ability.CooldownRemaining - .2f);
                    Check(Quaternion.Angle(rest, body.localRotation) > 1, "Next shot aims at the player's new side");
                    grid.SetColor(red.Id, EnemyColor.Blue);
                    ability.Tick(0);
                    Check(Quaternion.Angle(rest, body.localRotation) < .001f, "Color changes clear the aiming pose");
                });
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    player.transform.position = new Vector3(2, -4.6f, 0);
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                    var before = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                    red.GetComponent<EnemyAbilities>().Tick(100);
                    var missile = NewMissiles(before).Single();
                    Check(!missile.Aimed && !missile.Homing, "Ordinary Red still fires downward");
                    var start = missile.transform.position;
                    missile.Tick(.2f);
                    Check(Vector3.Distance(missile.transform.position, start + Vector3.down) < .001f,
                        "Ordinary projectile speed and heading are unchanged");
                });
            }
            finally { foreach (var player in existingPlayers) if (player != null) player.gameObject.SetActive(true); }
            Debug.Log("Aimed Red checks passed: warning turn, launch direction, fixed flight, return, pause, color reset and ordinary fire.");
        }

        private static EnemyMissile[] NewMissiles(EnemyMissile[] before) =>
            UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Where(missile => !before.Contains(missile)).ToArray();
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Aimed Red check failed: " + message); }
    }
}
