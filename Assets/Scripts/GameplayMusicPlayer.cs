using UnityEngine;

namespace CandyCruisers
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class GameplayMusicPlayer : MonoBehaviour
    {
        public const string DefaultResourceName = "DiscoDescent";
        public const string MenuResourceName = "AnotherJoe";
        [System.Serializable]
        public sealed class Song
        {
            public string resourceName;
            [Min(1)] public float beatsPerMinute;
            public float beatOffsetSeconds;
            [Range(0, 11), Tooltip("Relative-major tonic: C=0, C#=1, ... B=11. For minor songs use their relative major.")]
            public int relativeMajorTonic;
            public Song(string name, float tempo, float offset, int tonic = 0)
            { resourceName = name; beatsPerMinute = tempo; beatOffsetSeconds = offset; relativeMajorTonic = tonic; }
        }
        [SerializeField] private Song[] soundtrack = {
            new Song("DiscoDescent", 115.03f, .08f, 0), new Song("GameplayMusic", 142f, .09f, 8),
            new Song("AnotherJoe", 140f, .1f, 2), new Song("PotentialForAnything", 125f, 0f, 5)
        };
        private readonly System.Collections.Generic.List<int> remainingSongs = new System.Collections.Generic.List<int>();
        private readonly System.Random shuffle = new System.Random();
        private readonly System.Collections.Generic.Dictionary<AudioClip, SongBeatMap> beatMaps = new();
        private int songIndex = -1;
        private bool pausedSource;
        private bool wasInMainMenu;
        private AudioClip carriedMenuTrack;
        public int TrackCount => soundtrack.Length;
        [SerializeField, Range(0, 11)] private int customTrackRelativeMajorTonic;
        private Song CurrentSong
        {
            get
            {
                // Beat timing and key follow the clip actually playing, including the menu carryover.
                var clip = Source.clip;
                if (clip != null)
                    foreach (var song in soundtrack)
                        if (song.resourceName == clip.name) return song;
                return null;
            }
        }
        public int CurrentRelativeMajorTonic => CurrentSong?.relativeMajorTonic ?? customTrackRelativeMajorTonic;
        private float FixedBeatDuration => 60f / Mathf.Max(1, CurrentSong?.beatsPerMinute ?? beatsPerMinute);
        public float CurrentTempo => 60f / BeatDuration;
        public float CurrentBeatOffset => CurrentBeatMap?.FirstBeatTime ?? CurrentSong?.beatOffsetSeconds ?? beatOffsetSeconds;
        public bool UsesBeatMap => CurrentBeatMap != null;
        private SongBeatMap CurrentBeatMap
        {
            get
            {
                var clip = Source.clip;
                if (clip == null) return null;
                var song = CurrentSong;
                if (song == null) return null;
                if (beatMaps.TryGetValue(clip, out var cached)) return cached;
                SongBeatMap map = null;
                var asset = Resources.Load<TextAsset>("BeatMaps/" + song.resourceName);
                if (asset != null && !SongBeatMap.TryParse(asset.text, clip.length, out map))
                    Debug.LogWarning("Invalid beat map for " + clip.name + "; using its fixed tempo.", this);
                beatMaps[clip] = map;
                return map;
            }
        }
        public const float DefaultBeatsPerMinute = 115.03f;
        [SerializeField, Min(1)] private float beatsPerMinute = DefaultBeatsPerMinute;
        [SerializeField] private float beatOffsetSeconds = .08f;
        public const float DefaultVolume = .05f;
        [SerializeField] private GameSession session;
        [SerializeField] private AudioClip track;
        [SerializeField] private AudioClip menuTrack;
        [SerializeField, Range(0, 1)] private float volume = DefaultVolume;
        [SerializeField] private bool muted;
        private AudioSource source;
        private AudioClip resourceTrack;
        private AudioClip resourceMenuTrack;
        private bool wasInRun;
        private bool warnedFailedLoad;

        public AudioSource Source { get { EnsureSource(); return source; } }
        public float PlaybackSeconds => Source.clip != null ?
            Source.timeSamples / (float)Mathf.Max(1, Source.clip.frequency) : 0;
        public float BeatDuration => CurrentBeatMap?.DurationAtTime(PlaybackSeconds) ?? FixedBeatDuration;
        public static float BeatPulse(float beatPosition, bool offbeat = false) =>
            Mathf.Pow(Mathf.Max(0, Mathf.Cos((beatPosition - (offbeat ? .5f : 0)) * Mathf.PI * 2)), 4);
        public float BeatPosition => BeatPositionAtTime(PlaybackSeconds);
        public float BeatPositionAtTime(float seconds) => CurrentBeatMap?.BeatAtTime(seconds) ??
            (seconds - CurrentBeatOffset) / FixedBeatDuration;
        public float SecondsAtBeat(float beat) => CurrentBeatMap?.TimeAtBeat(beat) ??
            CurrentBeatOffset + beat * FixedBeatDuration;
        public float CompletedBeatDuration(int beat) => SecondsAtBeat(beat) - SecondsAtBeat(beat - 1);
        public float SecondsForBeats(float count)
        {
            float time = PlaybackSeconds;
            return Mathf.Max(0, SecondsAtBeat(BeatPositionAtTime(time) + Mathf.Max(0, count)) - time);
        }
        public float SecondsToNextBeat => Mathf.Max(0, SecondsAtBeat(Mathf.Floor(BeatPosition) + 1) - PlaybackSeconds);
        public float[] DurationsForBeats(int count)
        {
            var durations = new float[Mathf.Max(0, count)];
            float time = PlaybackSeconds;
            float beat = BeatPositionAtTime(time);
            for (int i = 0; i < durations.Length; i++)
            {
                float next = SecondsAtBeat(beat + i + 1);
                durations[i] = next - time;
                time = next;
            }
            return durations;
        }
        public AudioClip MenuTrack => menuTrack != null ? menuTrack :
            resourceMenuTrack != null ? resourceMenuTrack : SelectMenuTrack();
        public AudioClip Track
        {
            get
            {
                if (carriedMenuTrack != null) return carriedMenuTrack;
                if (track != null) return track;
                if (resourceTrack == null) SelectNextTrack();
                return resourceTrack;
            }
        }
        public bool ContainsTrack(string name) => System.Array.Exists(soundtrack, song => song.resourceName == name);
        public void SelectNextTrack()
        {
            carriedMenuTrack = null;
            TrySelectNextResourceTrack();
        }

        private bool TrySelectNextResourceTrack()
        {
            resourceTrack = null;
            if (soundtrack.Length == 0) return false;
            for (int attempts = 0; attempts < soundtrack.Length; attempts++)
            {
                if (remainingSongs.Count == 0) RefillShuffleBag();
                int index = remainingSongs[0]; remainingSongs.RemoveAt(0);
                var clip = Resources.Load<AudioClip>(soundtrack[index].resourceName);
                if (clip == null) continue;
                songIndex = index;
                resourceTrack = clip;
                return true;
            }
            return false;
        }

        private void RefillShuffleBag()
        {
            remainingSongs.Clear();
            for (int i = 0; i < soundtrack.Length; i++) remainingSongs.Add(i);
            for (int i = remainingSongs.Count - 1; i > 0; i--)
            {
                int j = shuffle.Next(i + 1);
                int swap = remainingSongs[i]; remainingSongs[i] = remainingSongs[j]; remainingSongs[j] = swap;
            }
            if (remainingSongs.Count > 1 && remainingSongs[0] == songIndex)
            { int swap = remainingSongs[0]; remainingSongs[0] = remainingSongs[1]; remainingSongs[1] = swap; }
        }

        private AudioClip SelectMenuTrack()
        {
            if (soundtrack.Length > 0)
            {
                int start = shuffle.Next(soundtrack.Length);
                for (int offset = 0; offset < soundtrack.Length; offset++)
                {
                    int index = (start + offset) % soundtrack.Length;
                    resourceMenuTrack = Resources.Load<AudioClip>(soundtrack[index].resourceName);
                    if (resourceMenuTrack == null) continue;
                    songIndex = index;
                    return resourceMenuTrack;
                }
            }
            songIndex = System.Array.FindIndex(soundtrack, song => song.resourceName == MenuResourceName);
            return resourceMenuTrack = Resources.Load<AudioClip>(MenuResourceName);
        }
        public bool InGameplayRun => session != null &&
            (session.State == GameSession.RunState.Playing || session.State == GameSession.RunState.Refilling);
        public bool ShouldPlayMusic => session != null && !session.IsPaused &&
            (InGameplayRun || session.State == GameSession.RunState.MainMenu);

        public void Configure(GameSession gameSession)
        {
            session = gameSession;
            UpdatePlayback();
        }

        private void Awake() => EnsureSource();
        private void OnEnable()
        {
            if (session == null) session = GetComponent<GameSession>() ?? FindFirstObjectByType<GameSession>();
            UpdatePlayback();
        }
        private void Update()
        {
            if ((Application.isEditor || Debug.isDebugBuild) && Input.GetKeyDown(KeyCode.F8)) SkipCurrentSong();
            UpdatePlayback();
        }
        public bool SkipCurrentSong()
        {
            if (!(Application.isEditor || Debug.isDebugBuild) || !InGameplayRun || track != null || soundtrack.Length < 2) return false;
            carriedMenuTrack = null;
            if (!TrySelectNextResourceTrack()) return false;
            UpdatePlayback();
            return true;
        }
        private void OnDisable()
        {
            if (source != null) source.Pause();
            wasInRun = false;
        }
        private void OnValidate()
        {
            beatMaps.Clear();
            EnsureSource();
            ApplySourceSettings();
        }

        public void UpdatePlayback()
        {
            EnsureSource();
            if (session == null) session = GetComponent<GameSession>();
            if (session != null && (session.State == GameSession.RunState.Dying || session.State == GameSession.RunState.GameOver))
            {
                ApplySourceSettings();
                StopPlayback();
                pausedSource = false;
                return;
            }
            bool inMenu = session != null && session.State == GameSession.RunState.MainMenu;
            if (wasInMainMenu && InGameplayRun && source.clip != null)
            {
                // Adopt the menu song without stopping, seeking or replaying its AudioSource.
                carriedMenuTrack = source.clip;
                resourceTrack = null;
                songIndex = System.Array.FindIndex(soundtrack, song => song.resourceName == carriedMenuTrack.name);
                RefillShuffleBag();
                remainingSongs.Remove(songIndex);
            }
            wasInMainMenu = inMenu;
            var clip = inMenu ? MenuTrack : Track;
            if (clip == null)
            {
                source.Stop();
                wasInRun = false;
                return;
            }
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState == AudioDataLoadState.Failed)
            {
                if (Application.isPlaying && !warnedFailedLoad)
                {
                    Debug.LogWarning("Gameplay music track failed to load audio data: " + clip.name, this);
                    warnedFailedLoad = true;
                }
                source.Stop();
                wasInRun = false;
                return;
            }
            if (source.clip != clip)
            {
                source.Stop();
                source.clip = clip;
                wasInRun = false;
                pausedSource = false;
            }
            ApplySourceSettings();

            bool inRun = InGameplayRun || session != null && session.State == GameSession.RunState.MainMenu;
            bool shouldPlay = ShouldPlayMusic;
            if (!Application.isPlaying)
            {
                wasInRun = inRun;
                return;
            }
            if (clip.loadState == AudioDataLoadState.Loading) return;

            if (!inRun)
            {
                source.Stop();
                wasInRun = false;
                return;
            }

            if (!wasInRun)
            {
                source.Stop();
                source.time = 0;
                source.Play();
                if (!shouldPlay) { source.Pause(); pausedSource = true; }
            }
            else if (shouldPlay)
            {
                if (pausedSource) { source.UnPause(); pausedSource = false; }
                else if (!source.isPlaying && InGameplayRun && (track == null || carriedMenuTrack != null))
                {
                    if (track != null) carriedMenuTrack = null;
                    else SelectNextTrack();
                    wasInRun = false;
                    // Configure the next clip on the next frame, without restarting the run or its beat effects.
                }
            }
            else if (source.isPlaying)
            {
                source.Pause();
                pausedSource = true;
            }
            if (source.clip == (session != null && session.State == GameSession.RunState.MainMenu ? MenuTrack : Track)) wasInRun = inRun;
        }

        public void StopPlayback()
        {
            EnsureSource();
            source.Stop();
            wasInRun = false;
        }

        private void EnsureSource()
        {
            if (source != null) return;
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            ApplySourceSettings();
        }

        private void ApplySourceSettings()
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.loop = !InGameplayRun || track != null && carriedMenuTrack == null;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.volume = volume * MusicLoudness.GainFor(source.clip) * (session != null ? session.GameOverMusicGain : 1);
            source.mute = muted;
        }
    }
}
