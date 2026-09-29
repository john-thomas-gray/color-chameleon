using System;
using UnityEngine;

namespace CandyCruisers
{
    public static class ArcadeSoundClips
    {
        public const int SampleRate = 44100;
        private const int TongueCycleSamples = 60;
        private static readonly float[] ChimeNotes = { 1f, 1.25f, 1.5f, 2f };
        public const float TongueFrequency = (float)SampleRate / TongueCycleSamples;
        public const float ShatterEchoSeconds = .18f;
        public const float ShatterTailSeconds = 2.1f;

        public static AudioClip Create(SoundEffect effect, int tonic = 0, int noteShift = 0)
        {
            switch (effect)
            {
                case SoundEffect.TongueWhistle: return TongueTone();
                case SoundEffect.Jackpot: return Jackpot(tonic, noteShift);
                case SoundEffect.GameOver: return GameOver(tonic, noteShift);
                case SoundEffect.PlayerShatter: return PlayerShatter();
                case SoundEffect.MissileFlight: return MissileFlight(tonic, noteShift);
                case SoundEffect.RedFire: return null;
                default: return Enum.IsDefined(typeof(SoundEffect), effect) ? EventTone(effect, tonic, noteShift) : null;
            }
        }

        private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };
        public static float KeyFrequency(float frequency, int tonic, int noteShift = 0)
        {
            int root = (tonic % 12 + 12) % 12;
            if (root > 6) root -= 12;
            float note = 69 + 12 * Mathf.Log(Mathf.Max(1, frequency) / 440, 2) + noteShift;
            int octave = Mathf.FloorToInt(note / 12);
            float closest = 0, distance = float.MaxValue;
            for (int oct = octave - 1; oct <= octave + 1; oct++)
                foreach (int degree in MajorScale)
                {
                    float candidate = oct * 12 + degree;
                    if (Mathf.Abs(candidate - note) < distance)
                    { closest = candidate; distance = Mathf.Abs(candidate - note); }
                }
            return 440 * Mathf.Pow(2, (closest + root - 69) / 12);
        }

        private static AudioClip PlayerShatter()
        {
            const float duration = .7f;
            var samples = new float[(int)(duration * SampleRate)];
            var noise = new System.Random(417);
            double previous = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double time = (double)i / SampleRate;
                double white = noise.NextDouble() * 2 - 1;
                double crack = (white - previous) * Math.Exp(-time * 70);
                previous = white;
                double glass = 0;
                for (int shard = 0; shard < 9; shard++)
                {
                    double age = time - shard * .018;
                    if (age < 0) continue;
                    double frequency = 1700 + shard * 347 + shard * shard * 31;
                    double phase = age * frequency * Math.PI * 2;
                    double envelope = Math.Min(1, age / .001) * Math.Exp(-age * (12 + shard));
                    glass += (Math.Sin(phase) + .3 * Math.Sin(phase * 1.47)) * envelope / 9;
                }
                double fade = Math.Pow(1 - time / duration, 2);
                double attack = Math.Min(1, time / .0005);
                samples[i] = (float)(Math.Tanh((crack * .85 + glass * 1.9) * attack) * .8 * fade);
            }
            var spacious = ShatterSpace(samples);
            var clip = AudioClip.Create("Player glass shatter with stereo echo and reverb", spacious.Length / 2, 2, SampleRate, false);
            clip.SetData(spacious, 0);
            return clip;
        }

        private static float[] ShatterSpace(float[] dry)
        {
            int frames = Mathf.CeilToInt(ShatterTailSeconds * SampleRate);
            var stereo = new float[frames * 2];
            float[] reflections = { .0311f, .0437f, .0533f, .0679f };
            for (int channel = 0; channel < 2; channel++)
            {
                var wet = new float[frames];
                // Parallel damped delay lines diffuse the crack into a short room tail.
                foreach (float reflection in reflections)
                {
                    int delay = Mathf.RoundToInt((reflection + channel * .0043f) * SampleRate);
                    var line = new float[delay];
                    float damped = 0;
                    for (int i = 0; i < frames; i++)
                    {
                        int slot = i % delay;
                        float returned = line[slot];
                        damped = Mathf.Lerp(damped, returned, .55f);
                        line[slot] = (i < dry.Length ? dry[i] : 0) + damped * .79f;
                        wet[i] += returned * .19f;
                    }
                }
                // Distinct stereo repeats sit above the diffuse reflections.
                for (int repeat = 1; repeat <= 5; repeat++)
                {
                    int delay = Mathf.RoundToInt((ShatterEchoSeconds + channel * .035f) * repeat * SampleRate);
                    float gain = .38f * Mathf.Pow(.48f, repeat - 1);
                    for (int i = delay; i < frames && i - delay < dry.Length; i++)
                        wet[i] += dry[i - delay] * gain;
                }
                for (int i = 0; i < frames; i++)
                {
                    float direct = i < dry.Length ? dry[i] : 0;
                    float release = Mathf.Clamp01((frames - 1 - i) / (SampleRate * .12f));
                    stereo[i * 2 + channel] = Mathf.Clamp(direct + wet[i], -.95f, .95f) * release;
                }
            }
            return stereo;
        }

        private static AudioClip GameOver(int tonic, int noteShift)
        {
            const float duration = 1.5f;
            int[] notes = { 7, 3, 0, -12 };
            var samples = new float[(int)(duration * SampleRate)];
            double phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / SampleRate;
                int note = Mathf.Min(3, (int)(time / .22f));
                float age = time - note * .22f;
                float length = note == 3 ? duration - .66f : .22f;
                double frequency = KeyFrequency(523.2511f * Mathf.Pow(2, notes[note] / 12f), tonic, noteShift);
                phase += frequency * 2 * Math.PI / SampleRate;
                double tone = Math.Sin(phase) + .22 * Math.Sin(phase * 3) + .08 * Math.Sin(phase * 5);
                float envelope = Mathf.Min(1, age / .008f) * Mathf.Pow(Mathf.Max(0, 1 - age / length), 1.6f);
                samples[i] = (float)(tone * envelope * .38);
            }
            var clip = AudioClip.Create("Game over descending arcade cadence", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip Jackpot(int tonic, int noteShift)
        {
            const float duration = 1.8f;
            var samples = new float[(int)(duration * SampleRate)];
            int[] run = { 0, 4, 7, 12, 4, 7, 12, 16, 7, 12, 16, 19 };
            int[] chord = { 0, 4, 7, 12 };
            void Bell(int semitone, float start, float length, float gain)
            {
                double frequency = KeyFrequency(523.2511f * Mathf.Pow(2, semitone / 12f), tonic, noteShift);
                int first = (int)(start * SampleRate);
                int count = Math.Min((int)(length * SampleRate), samples.Length - first);
                for (int i = 0; i < count; i++)
                {
                    double time = (double)i / SampleRate;
                    double phase = time * frequency * Math.PI * 2;
                    double envelope = Math.Min(1, time / .003) * Math.Exp(-time * 5 / length) * (1 - time / length);
                    double bell = Math.Sin(phase) + .28 * Math.Sin(phase * 2) + .12 * Math.Sin(phase * 3.98) * Math.Exp(-time * 16);
                    samples[first + i] += (float)(bell * envelope * gain);
                }
            }
            for (int i = 0; i < run.Length; i++) Bell(run[i], i * .065f, .28f, .3f);
            foreach (int note in chord) Bell(note, .86f, .94f, .18f);
            for (int i = 0; i < samples.Length; i++) samples[i] = (float)Math.Tanh(samples[i] * .85f);
            var clip = AudioClip.Create("Jackpot bell cascade", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip MissileFlight(int tonic, int noteShift)
        {
            const double duration = .09;
            var samples = new float[(int)(duration * SampleRate)];
            double frequency = KeyFrequency(1046.502f, tonic, noteShift);
            double phase = 0;
            float held = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double time = (double)i / SampleRate;
                double bend = 1 + .055 * Math.Exp(-time * 52);
                phase += frequency * bend * 2 * Math.PI / SampleRate;
                double square = Math.Sin(phase) >= 0 ? 1 : -1;
                double chirp = Math.Sin(phase * 2) * .18 + Math.Sin(phase * 3) * .08;
                float shaped = (float)(square + chirp);
                if (i % 9 == 0) held = shaped;
                double attack = Math.Min(1, time / .0025);
                double release = Math.Pow(Math.Max(0, 1 - time / duration), 2.7);
                samples[i] = (float)(Math.Tanh(held * .64) * attack * release * .72);
            }
            var clip = AudioClip.Create("Missile beat warning beep", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip EventTone(SoundEffect effect, int tonic, int noteShift)
        {
            float duration = .3f, start = 360, end = 700;
            switch (effect)
            {
                case SoundEffect.GreenStep: duration = .12f; start = 160; end = 420; break;
                case SoundEffect.GreenDash: duration = .26f; start = 180; end = 1200; break;
                case SoundEffect.ShieldPower: duration = .35f; start = 260; end = 1040; break;
                case SoundEffect.YellowTransform: duration = .65f; start = 220; end = 440; break;
                case SoundEffect.YellowHide: duration = .4f; start = 640; end = 240; break;
                case SoundEffect.YellowReveal: duration = .23f; start = 420; end = 1200; break;
                case SoundEffect.PurpleWarp: duration = .55f; start = 130; end = 780; break;
                case SoundEffect.EnemyDefeat: duration = .13f; start = 261.6256f; end = start; break;
                case SoundEffect.WaveSpawn: duration = .55f; start = 260; end = 520; break;
                case SoundEffect.LevelUp: duration = .65f; start = 440; end = 880; break;
                case SoundEffect.OneUp: duration = .75f; start = 660; end = 1320; break;
                case SoundEffect.ColorClear: duration = .5f; start = 523.25f; end = 523.25f; break;
                case SoundEffect.BarPowerDown: duration = .28f; start = 523.25f; end = 130.81f; break;
                case SoundEffect.BarPowerUp: duration = .28f; start = 261.63f; end = 523.25f; break;
            }
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            double phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / (samples.Length - 1);
                float frequency = Mathf.Lerp(start, end, t);
                if (effect == SoundEffect.LevelUp || effect == SoundEffect.OneUp || effect == SoundEffect.WaveSpawn)
                {
                    int note = Mathf.Min(3, (int)(t * 4));
                    frequency = start * ChimeNotes[note];
                }
                frequency = KeyFrequency(frequency, tonic, noteShift);
                phase += 2 * Math.PI * frequency / SampleRate;
                double wave = Math.Sin(phase) + .2 * Math.Sin(phase * 2);
                if (effect == SoundEffect.GreenStep || effect == SoundEffect.GreenDash || effect == SoundEffect.PurpleWarp)
                    wave += .15 * Math.Sin(phase * 7 + Math.Sin(phase * .13) * 4);
                float envelope = Mathf.Min(1, t * 35) * Mathf.Pow(1 - t, 1.4f);
                samples[i] = (float)(wave * .34 * envelope);
            }
            var clip = AudioClip.Create(effect.ToString(), samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
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
