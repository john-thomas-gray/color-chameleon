using UnityEngine;

namespace CandyCruisers
{
    [DisallowMultipleComponent]
    public sealed class MissileFlightSound : MonoBehaviour
    {
        public const float DefaultAudibleDistance = 2f;
        [SerializeField, Min(.1f), Tooltip("World-space distance from the player beyond which the missile is silent.")]
        private float audibleDistance = DefaultAudibleDistance;
        [SerializeField, Min(0), Tooltip("World-space distance at which the missile reaches full cue volume.")]
        private float fullVolumeDistance = .35f;
        private EnemyMissile missile;
        private SoundEffects sounds;
        private AudioSource voice;
        private int playedBeeps;
        public AudioSource Source => voice;
        public float AudibleDistance => Mathf.Max(.1f, audibleDistance);
        public int PlayedBeeps => playedBeeps;

        public void Configure(EnemyMissile projectile, SoundEffects effects)
        {
            if (missile == projectile && sounds == effects && voice != null) { Refresh(); return; }
            ReleaseVoice();
            missile = projectile;
            sounds = effects;
            if (missile == null || sounds == null) return;
            missile.ConfigureFlashClock(sounds.GetComponent<GameplayMusicPlayer>());
            voice = sounds.CreateVoice(transform);
            voice.gameObject.name = "Missile flash beep voice";
            sounds.SetGain(voice, 0);
            Refresh();
        }

        public static float DistanceGain(float distance, float nearDistance, float farDistance)
        {
            farDistance = Mathf.Max(.1f, farDistance);
            nearDistance = Mathf.Clamp(nearDistance, 0, farDistance - .01f);
            if (distance >= farDistance) return 0;
            float proximity = 1 - Mathf.InverseLerp(nearDistance, farDistance, distance);
            return Mathf.Pow(Mathf.SmoothStep(0, 1, proximity), 3);
        }

        public static Vector2 PlanarOffset(Vector3 position, Vector3 listener) => (Vector2)(position - listener);

        public static float StereoPan(Vector2 offset) =>
            Mathf.Clamp(offset.x / Mathf.Max(.25f, offset.magnitude), -1, 1) * .95f;

        private void LateUpdate() => Refresh();
        private void OnEnable() => Refresh();
        public void Refresh()
        {
            if (voice == null || sounds == null || missile == null) return;
            var player = missile.Target;
            bool active = isActiveAndEnabled && missile.isActiveAndEnabled && !missile.Finished &&
                !missile.Suspended && player != null && player.Alive && sounds.isActiveAndEnabled && !sounds.Paused;
            Vector2 offset = player != null ? PlanarOffset(transform.position, player.transform.position) : Vector2.zero;
            bool audible = active && missile.FlightSoundEligible;
            float gain = audible ? DistanceGain(offset.magnitude, fullVolumeDistance, audibleDistance) : 0;
            sounds.SetGain(voice, gain);
            voice.panStereo = StereoPan(offset);
            if (!active)
            {
                sounds.Stop(voice);
                return;
            }
            int beeps = missile.ConsumeFlashBeeps();
            if (!audible)
            {
                sounds.Stop(voice);
                return;
            }
            if (beeps > 0 && gain > 0)
            {
                sounds.Play(SoundEffect.MissileFlight, voice);
                playedBeeps += beeps;
            }
        }

        private void OnDisable()
        {
            if (sounds != null) { sounds.SetGain(voice, 0); sounds.Stop(voice); }
        }
        private void OnDestroy() => ReleaseVoice();
        private void ReleaseVoice()
        {
            if (sounds != null) sounds.ReleaseVoice(voice);
            voice = null;
        }
    }
}
