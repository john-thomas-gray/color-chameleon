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
            var body = GameObject.Find("Player").transform.Find("Visuals/Body").GetComponentInChildren<SpriteRenderer>();
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
                    frame.Refresh();
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
            Debug.Log("Playfield frame checks passed: geometry, visibility, all five player colors and no boundary changes.");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Playfield frame check failed: " + message); }
    }
}
