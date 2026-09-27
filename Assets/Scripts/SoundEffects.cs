using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public enum SoundEffect { TongueWhistle = 0 }

    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SoundEffects : MonoBehaviour
    {
        [Serializable]
        private sealed class Cue
        {
            public SoundEffect effect;
            public AudioClip clip;
            [Range(0, 1)] public float volume = 1;
        }

        private sealed class Voice
        {
            public AudioSource source;
            public float volume = 1;
            public float gain = 1;
        }

        [SerializeField, Range(0, 1)] private float volume = .65f;
        [SerializeField] private bool muted;
        [SerializeField] private Cue[] cues = {
            new Cue { effect = SoundEffect.TongueWhistle, volume = .65f }
        };
        private readonly List<Voice> voices = new List<Voice>();
        private readonly Dictionary<SoundEffect, AudioClip> generatedClips = new Dictionary<SoundEffect, AudioClip>();
        public bool Paused { get; private set; }
        public float Volume
        {
            get => volume;
            set { volume = Mathf.Clamp01(value); RefreshVolumes(); }
        }
        public bool Muted
        {
            get => muted;
            set { muted = value; RefreshVolumes(); }
        }

        private void Awake()
        {
            if (!Application.isPlaying || FindFirstObjectByType<AudioListener>() != null) return;
            var camera = Camera.main;
            if (camera != null) camera.gameObject.AddComponent<AudioListener>();
        }

        public AudioSource CreateVoice(Transform owner)
        {
            voices.RemoveAll(voice => voice.source == null);
            var source = new GameObject("Sound effects voice").AddComponent<AudioSource>();
            source.transform.SetParent(owner, false);
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            voices.Add(new Voice { source = source });
            RefreshVolumes();
            return source;
        }

        public AudioClip GetClip(SoundEffect effect)
        {
            var cue = FindCue(effect);
            if (cue != null && cue.clip != null) return cue.clip;
            if (!generatedClips.TryGetValue(effect, out var clip))
            {
                clip = ArcadeSoundClips.Create(effect);
                if (clip != null) generatedClips.Add(effect, clip);
            }
            return clip;
        }

        public bool Play(SoundEffect effect, AudioSource source, float pitch = 1, bool loop = false)
        {
            if (!isActiveAndEnabled || source == null) return false;
            var voice = voices.Find(item => item.source == source);
            var clip = GetClip(effect);
            if (voice == null || clip == null) return false;
            voice.volume = FindCue(effect)?.volume ?? 1;
            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.pitch = Mathf.Clamp(pitch, .5f, 3);
            source.volume = volume * voice.volume * voice.gain;
            source.mute = muted;
            if (Application.isPlaying)
            {
                source.Play();
                if (Paused) source.Pause();
            }
            return true;
        }

        public void Stop(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
        }

        public void SetGain(AudioSource source, float gain)
        {
            var voice = voices.Find(item => item.source == source);
            if (voice == null || source == null) return;
            voice.gain = Mathf.Clamp01(gain);
            source.volume = volume * voice.volume * voice.gain;
        }

        public void ReleaseVoice(AudioSource source)
        {
            voices.RemoveAll(voice => voice.source == null || voice.source == source);
            if (source == null) return;
            Stop(source);
            Dispose(source.gameObject);
        }

        public void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            foreach (var voice in voices)
            {
                if (voice.source == null) continue;
                if (paused) voice.source.Pause();
                else voice.source.UnPause();
            }
        }

        private Cue FindCue(SoundEffect effect)
            => Array.Find(cues, cue => cue != null && cue.effect == effect);

        private void RefreshVolumes()
        {
            foreach (var voice in voices)
            {
                if (voice.source == null) continue;
                voice.source.volume = volume * voice.volume * voice.gain;
                voice.source.mute = muted;
            }
        }

        private void OnValidate() => RefreshVolumes();
        private void OnDisable()
        {
            foreach (var voice in voices) Stop(voice.source);
            Paused = false;
        }
        private void OnDestroy()
        {
            foreach (var voice in voices)
                if (voice.source != null) Dispose(voice.source.gameObject);
            voices.Clear();
            foreach (var clip in generatedClips.Values) Dispose(clip);
            generatedClips.Clear();
        }
        private static void Dispose(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
