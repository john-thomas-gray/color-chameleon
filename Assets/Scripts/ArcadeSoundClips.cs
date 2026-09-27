using System;
using UnityEngine;

namespace CandyCruisers
{
    public static class ArcadeSoundClips
    {
        public const int SampleRate = 44100;
        private const int TongueCycleSamples = 60;
        public const float TongueFrequency = (float)SampleRate / TongueCycleSamples;

        public static AudioClip Create(SoundEffect effect)
        {
            switch (effect)
            {
                case SoundEffect.TongueWhistle: return TongueTone();
                default: return null;
            }
        }

        private static AudioClip TongueTone()
        {
            // One seamless oscillator cycle, with no baked-in pitch sweep or duration.
            // Animation controls its playback pitch and gain for the whole shot.
            var samples = new float[TongueCycleSamples];
            for (int i = 0; i < samples.Length; i++)
            {
                double phase = 2 * Math.PI * i / samples.Length;
                double wave = Math.Sin(phase) + .19 * Math.Sin(phase * 3) + .07 * Math.Sin(phase * 5);
                samples[i] = (float)(wave * .48);
            }
            var clip = AudioClip.Create("Tongue oscillator", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
