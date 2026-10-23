using UnityEngine;

namespace CandyCruisers
{
    public static class MetronomeSettings
    {
        public const string VoiceKey = "CandyCruisers.Metronome.Voice";
        public const string VisualKey = "CandyCruisers.Metronome.Visual";
        public static bool VoiceEnabled
        {
            get => PlayerPrefs.GetInt(VoiceKey, 0) != 0;
            set { PlayerPrefs.SetInt(VoiceKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool VisualEnabled
        {
            get => PlayerPrefs.GetInt(VisualKey, 0) != 0;
            set { PlayerPrefs.SetInt(VisualKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
