using UnityEngine;

namespace CandyCruisers
{
    [DisallowMultipleComponent]
    public sealed class TongueSoundEffects : MonoBehaviour
    {
        [SerializeField, Min(1)] private float baseFrequency = 420;
        [SerializeField, Min(.1f)] private float octaveLength = 5.5f;
        [SerializeField, Min(.01f)] private float fadeLength = .7f;
        private TongueShot tongue;
        private SoundEffects sounds;
        private AudioSource voice;

        public void Configure(TongueShot shot, SoundEffects effects)
        {
            if (tongue == shot && sounds == effects && voice != null) return;
            Unsubscribe();
            ReleaseVoice();
            tongue = shot;
            sounds = effects;
            if (tongue == null || sounds == null) return;
            voice = sounds.CreateVoice(transform);
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() { Unsubscribe(); Stop(); }
        private void OnDestroy() => ReleaseVoice();
        private void Subscribe()
        {
            if (tongue == null) return;
            Unsubscribe();
            tongue.ExtensionStarted += Extend;
            tongue.MotionUpdated += FollowMotion;
            tongue.Finished += Stop;
        }
        private void Unsubscribe()
        {
            if (tongue == null) return;
            tongue.ExtensionStarted -= Extend;
            tongue.MotionUpdated -= FollowMotion;
            tongue.Finished -= Stop;
        }
        private void Extend()
        {
            if (sounds == null) return;
            sounds.SetGain(voice, 0);
            sounds.Play(SoundEffect.TongueWhistle, voice, MusicalPitch(0), true);
            FollowMotion();
        }
        private void FollowMotion()
        {
            if (sounds == null || voice == null || voice.clip == null || !tongue.Active || sounds.Paused) return;
            // Follow physical length through scale notes, including slow deflections and early returns.
            voice.pitch = MusicalPitch(tongue.Length);
            sounds.SetGain(voice, Mathf.SmoothStep(0, 1, tongue.Length / fadeLength));
        }
        private float MusicalPitch(float length)
        {
            float register = sounds.CurrentTonic > 6 ? 2 : 1;
            float frequency = ArcadeSoundClips.KeyFrequency(baseFrequency * register * Mathf.Pow(2, length / octaveLength), sounds.CurrentTonic);
            float pitch = frequency / ArcadeSoundClips.TongueFrequency;
            while (pitch < .5f) pitch *= 2;
            while (pitch > 3) pitch /= 2;
            return pitch;
        }
        private void Stop()
        {
            if (sounds != null) sounds.Stop(voice);
        }
        private void ReleaseVoice()
        {
            if (sounds != null) sounds.ReleaseVoice(voice);
            voice = null;
        }
    }
}
