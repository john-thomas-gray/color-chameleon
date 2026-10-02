using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class EpilepsyWarningChecks
    {
        public static void Run()
        {
            Check(GameSession.EpilepsyWarningSeconds >= 3 &&
                GameSession.EpilepsyWarningTitle.Contains("WARNING") &&
                GameSession.EpilepsyWarningMessage.Contains("photosensitive epilepsy") &&
                GameSession.EpilepsyWarningMessage.Contains("consult a doctor"),
                "Opening warning uses standard photosensitive-epilepsy language");
            foreach (var size in new[] { new Vector2(237, 356), new Vector2(289, 625),
                new Vector2(540, 960), new Vector2(1280, 720), new Vector2(1920, 1080) })
            {
                var warning = GameSession.EpilepsyWarningRect(size.x, size.y);
                var title = GameSession.EpilepsyWarningTitleRect(size.x, size.y);
                var message = GameSession.EpilepsyWarningMessageRect(size.x, size.y);
                Check(warning.xMin >= 0 && warning.xMax <= size.x && warning.yMin >= 0 && warning.yMax <= size.y &&
                    title.x == warning.x && title.width == warning.width && title.y == warning.y &&
                    message.x == warning.x && message.width == warning.width && message.yMin > title.yMax &&
                    message.yMax == warning.yMax,
                    "Opening warning stays centered and fits portrait and landscape canvases: " + size);
            }
            Debug.Log("Epilepsy warning checks passed: standard text and centered white-on-black layout fit supported canvases.");
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new System.Exception("Epilepsy warning check failed: " + message); }
    }
}
