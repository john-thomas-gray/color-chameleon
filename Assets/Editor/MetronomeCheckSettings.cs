using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    internal sealed class MetronomeCheckSettings : IDisposable
    {
        private readonly bool hadVoice = PlayerPrefs.HasKey(MetronomeSettings.VoiceKey);
        private readonly bool hadVisual = PlayerPrefs.HasKey(MetronomeSettings.VisualKey);
        private readonly int voice = PlayerPrefs.GetInt(MetronomeSettings.VoiceKey);
        private readonly int visual = PlayerPrefs.GetInt(MetronomeSettings.VisualKey);
        internal MetronomeCheckSettings()
        {
            PlayerPrefs.SetInt(MetronomeSettings.VoiceKey, 0);
            PlayerPrefs.SetInt(MetronomeSettings.VisualKey, 0);
        }
        public void Dispose() => Restore(hadVoice, voice, hadVisual, visual);
        private static void Restore(bool hadVoice, int voice, bool hadVisual, int visual)
        {
            if (hadVoice) PlayerPrefs.SetInt(MetronomeSettings.VoiceKey, voice); else PlayerPrefs.DeleteKey(MetronomeSettings.VoiceKey);
            if (hadVisual) PlayerPrefs.SetInt(MetronomeSettings.VisualKey, visual); else PlayerPrefs.DeleteKey(MetronomeSettings.VisualKey);
            PlayerPrefs.Save();
        }
        internal static void BeginRuntime(string key, bool voice = false, bool visual = false)
        {
            SessionState.SetBool(key + ".Invincibility", DeveloperOptions.PlayerInvincible);
            SessionState.SetBool(key + ".HadVoice", PlayerPrefs.HasKey(MetronomeSettings.VoiceKey));
            SessionState.SetBool(key + ".HadVisual", PlayerPrefs.HasKey(MetronomeSettings.VisualKey));
            SessionState.SetInt(key + ".Voice", PlayerPrefs.GetInt(MetronomeSettings.VoiceKey));
            SessionState.SetInt(key + ".Visual", PlayerPrefs.GetInt(MetronomeSettings.VisualKey));
            PlayerPrefs.SetInt(MetronomeSettings.VoiceKey, voice ? 1 : 0);
            PlayerPrefs.SetInt(MetronomeSettings.VisualKey, visual ? 1 : 0);
        }
        internal static void EndRuntime(string key)
        {
            DeveloperOptions.PlayerInvincible = SessionState.GetBool(key + ".Invincibility", false);
            Restore(SessionState.GetBool(key + ".HadVoice", false), SessionState.GetInt(key + ".Voice", 0),
                SessionState.GetBool(key + ".HadVisual", false), SessionState.GetInt(key + ".Visual", 0));
        }
    }
}
