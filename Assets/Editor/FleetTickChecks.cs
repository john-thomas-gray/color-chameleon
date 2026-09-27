using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class FleetTickChecks
    {
        public static void Run()
        {
            foreach (int frames in new[] { 1, 60, 600 })
            {
                var root = new GameObject("Fleet tick fixture", typeof(EnemyGrid), typeof(EnemyGridMovement));
                try
                {
                    var grid = root.GetComponent<EnemyGrid>();
                    var movement = root.GetComponent<EnemyGridMovement>();
                    grid.Model.TryAdd(1, EnemyColor.Green, 0, 0);
                    grid.Model.TryAdd(2, EnemyColor.Green, 1, 0);
                    grid.Model.TryAdd(3, EnemyColor.Green, 2, 0);
                    var pulses = new List<int>();
                    movement.GreenPulsed += pulses.Add;
                    movement.Tick(1);
                    Check(root.transform.position.x == 0 && pulses.Count == 0, "Fleet holds still between ticks");
                    for (int i = 0; i < frames; i++) movement.Tick(9.0 / frames);
                    Check(Mathf.Abs(root.transform.position.x - .9f) < .0001f, "Stepped motion preserves average speed across frame sizes");
                    Check(pulses.SequenceEqual(new[] { 1, 2, 3, 1, 2, 3 }), "Every Green pulses once before any repeats");
                    grid.Model.Remove(1);
                    grid.Model.SetColor(2, EnemyColor.Blue);
                    movement.Tick(5);
                    Check(pulses.Last() == 3 && pulses.Count == 7, "Dead and converted Greens leave the rotation");
                    movement.Tick(2);
                    movement.enabled = false;
                    movement.Tick(100);
                    Check(pulses.Count == 7, "Pause cannot accrue movement");
                    movement.enabled = true;
                    movement.Tick(3);
                    Check(pulses.Count == 8, "Resume preserves partial step progress");
                    movement.Tick(4);
                    movement.ResetSweep();
                    movement.Tick(1);
                    Check(root.transform.position.x == 0 && pulses.Count == 8, "New wave discards previous partial movement");
                    grid.Model.TryAdd(4, EnemyColor.Green, 0, 0);
                    movement.Tick(4.5);
                    Check(pulses.Skip(8).SequenceEqual(new[] { 4, 3 }), "New Greens join deterministic row order after reset");
                    Check(grid.Model.TryAdd(5, EnemyColor.Green, 3, 0), "New Green occupies an empty cell");
                    movement.Tick(.15 / movement.CurrentSpeed);
                    Check(pulses.Last() == 5, "A joining Green gets a turn before an earlier Green repeats");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 0, 0);
                var visual = green.GetComponent<EnemyPresentation>();
                visual.Tick(1, EnemyColor.Green, 0);
                Color resting = green.Visuals.Body.color;
                visual.MovementPulse();
                Check(green.Visuals.Body.color != resting, "Selected Green visibly flashes");
                var bolts = green.GetComponentsInChildren<LineRenderer>().Where(line => line.name.StartsWith("Green surge")).ToArray();
                Check(bolts.Length == 10 && bolts.All(line => line.enabled && !line.useWorldSpace),
                    "Green surge has a corona and four branches with bright cores that follow the enemy");
                Vector3 point = bolts[0].GetPosition(1);
                visual.Tick(.06f, EnemyColor.Green, 0);
                Check(bolts[0].GetPosition(1) != point, "Electricity changes shape during the pulse");
                visual.Tick(EnemyPresentation.MovementPulseSeconds, EnemyColor.Green, 0);
                Check(bolts.All(line => !line.enabled), "Electrical arcs disappear after the pulse");
                Check(green.Visuals.Body.color == resting, "Pulse returns to original color without changing geometry");
                visual.MovementPulse();
                visual.Clear();
                Check(bolts.All(line => !line.enabled), "Disabling or recoloring cannot leave electricity behind");
            });
            Debug.Log("Fleet tick checks passed: discrete steps, average speed, frame timing, Green rotation, removals, pause, reset and pulse visuals.");
        }
        public static void CapturePreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            for (int col = 0; col < 5; col++)
                ProgressionChecks.Add(grid, col == 2 ? EnemyColor.Green : EnemyColor.Blue, col, 2);
            var green = grid.GetComponentsInChildren<GridEnemy>().First(enemy => enemy.Color == EnemyColor.Green);
            green.GetComponent<EnemyPresentation>().MovementPulse();
            GameplayChecks.Capture(540, 960, "green-electric-surge");
            GameplayChecks.Capture(960, 540, "green-electric-surge-landscape");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Fleet tick check failed: " + message); }
    }
}
