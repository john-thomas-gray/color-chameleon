# Sound effects

The tongue plays one continuous chiptune whistle throughout its movement.
Its pitch follows its visible length on every animation update: extending raises
the pitch and retracting lowers it. Hits and shields do not restart the sound or
jump to a preset return pitch. Slow deflections stretch out the downward slide;
magic slides faster because the tongue itself moves faster. Completed and canceled
shots stop the tone.

`ArcadeSoundClips` supplies one seamless oscillator cycle, not a recorded sweep.
`TongueSoundEffects` starts it looping once and uses `TongueShot.MotionUpdated` to
change pitch and gain. No timer controls the sound's duration or pitch trajectory.
The requested starting frequency is 420 cycles per second, doubling per 5.5 world units of
tongue length, then snapped to the song's scale. Its register shifts by octaves
when necessary to stay inside playback limits without detuning. Gain fades near the mouth. Tune Base Frequency, Octave Length, and
Fade Length on `TongueSoundEffects` to change this response.

## Playback and replacement

Generated tonal cues use the current song's relative-major scale. Initial keys,
estimated from the supplied recordings, are Disco Descent: A minor / C major;
original GameplayMusic: F minor / Ab major; Another Joe: D major;
Potential For Anything: D minor / F major. Set Relative Major Tonic on each
`GameplayMusicPlayer` soundtrack entry to refine these estimates (C=0 through B=11).
Custom track overrides have a separate tonic setting. The actual playing clip,
not the next queued song, determines tuning, including on the menu.

`ArcadeSoundClips.KeyFrequency` maps sweeps and requested motif intervals into the
selected scale. `SoundEffects` caches each generated key/interval variant and plays
it at normal speed, preserving cue duration and envelope. Combo arpeggios and bar
progressions retain their ascending/descending structure. The legacy tonal game-over
cadence remains available but is no longer triggered. The new `PlayerShatter` is
an unpitched crack with scattered glass-like overtones, stereo echoes and room
reverberation, cached independently of
the music key. Authored replacement effect clips retain their supplied
audio; tune those recordings to the soundtrack when replacing the placeholders.
`CuePlayed` reports the requested motif interval, not the generated clip's playback speed.

`GameSession` ensures a scene-local `SoundEffects` component on Enemy Grid and
connects a `TongueSoundEffects` component to the player's tongue. The sound system
adds an audio listener to the main camera if the scene has none. No scene migration
is required. Sounds pause with the session, including focus-loss pauses, and are
cleaned up on scene reload.

To tune volume, add `SoundEffects` to Enemy Grid in the editor and save the scene.
Set Volume or Muted for all effects, or change the volume of the Tongue Whistle cue.
An empty clip slot uses the generated oscillator, with soft odd harmonics and no
external audio assets. To replace its timbre, supply a seamless steady tone with a
fundamental of 735 cycles per second, not a sound with a pitch sweep or envelope.
The animation still controls the pitch and duration of a replacement tone.

`SoundEffects.Volume`, `Muted`, and `SetPaused` also work at runtime. Volume changes
apply to existing voices. Pausing affects only this service's voices, leaving room
for menu sounds or music with independent behavior later.

## Gameplay music

`GameSession` also ensures a scene-local `GameplayMusicPlayer` on Enemy Grid. The
repository does not include full music recordings. Common audio formats under
`Assets/Resources` are ignored, along with their Unity `.meta` files, so local
recordings can stay on the developer machine without entering Git history. A clean
checkout runs silently; adding local recordings whose names match the soundtrack
entries below re-enables menu and gameplay music.

When local recordings are present, the player selects a random soundtrack song for the menu. Starting gameplay
continues that same song from its current sample position without a stop, seek,
restart, volume change or beat-phase reset. The song finishes its current pass,
then gameplay shuffles the remaining recordings. The playlist contains Another
Joe, Potential For Anything, Disco Descent, the original GameplayMusic track,
Skanska, The Third Kind, Down To Earth Part 1, Until I Collapse, War On Activism,
Intergalactic Emotional Breakdown, Shooting Robots in Space, Vertex Stage 1, and
the developer counting metronome. The carried menu song counts as the first entry
in the shuffle bag. Each song finishes before the next
begins. Every shuffle bag includes each song once and avoids an immediate repeat
at its boundary.
Playlist randomness does not consume gameplay random numbers.
F8 skips to the next gameplay song in the Unity editor and development builds,
including while paused. It does not replace the selected menu song or a single-clip
Inspector override, and it is disabled in release builds. An assigned gameplay
override takes over only after the carried menu song finishes.
Fatal death stops the current track immediately, before the death animation
advances. The scene fades to black over the duration of the next two mapped beats,
captured before playback stops, while the intact player stays visible and moves to centerstage. Only then does the game-over death
animation start. `PlayerShatter` starts once at `PlayerDeathBurst.ShatterSeconds`
(0.14 seconds into that animation, after the fade), the same marker that releases the fragments. There is no later
game-over cadence or music fade. Recoverable deaths leave music unchanged.
Pause keeps the song position and resumes from there. Supplied soundtrack
recordings can use any Unity-supported local audio format.

The soundtrack entries in `GameplayMusicPlayer` contain independently editable
tempo and beat offsets. Initial recording-based estimates cover each playlist
entry and are editable in `GameplayMusicPlayer`.

### Variable-tempo maps

`GameplayMusicPlayer` optionally loads `Resources/BeatMaps/<resourceName>.json`.
Each map stores the recording duration and an ordered `beatTimes` array of seconds
from the beginning of that exact recording. The audio sample position determines
the surrounding timestamps and interpolates beat phase between them. Tempo changes
therefore accelerate or slow the clock without resetting phase. Missing or invalid
maps fall back to the editable fixed tempo and offset above. Replacing a recording
requires regenerating its map; the loader rejects mismatched recording lengths.

Maps for We Are Not Anonymous, Poison Was the Cure, and Drive Slow are retained,
but those recordings are no longer in the playlist.
Restoring a recording and its soundtrack entry enables its matching map again.
These are machine estimates: syncopation, sparse introductions, fills and fading
outros can still produce misplaced or half/double-time beats.

`SecondsForBeats` integrates future intervals for opening delays, color-clear dust
and the game-over blackout. Clear celebrations choose the next eligible downbeat
first, then divide the available beat span before the reserved pre-spawn beat
evenly across the earned bars. Green fleet movement accounts for the duration of
each completed beat, including when a long frame crosses a tempo change. Pausing
freezes the sample clock; seeking and song changes select timing from the actual
playing clip. Musical flourish/section markers are not implemented by these beat
maps.

Generate maps offline with the version-pinned analyzer:

```sh
python3 -m venv /tmp/candy-beats
/tmp/candy-beats/bin/pip install -r tools/beat-map-requirements.txt
/tmp/candy-beats/bin/python tools/generate_beat_map.py \
  "Assets/Resources/<RecordingName>.<audio-extension>" \
  "Assets/Resources/BeatMaps/<RecordingName>.json" \
  --min-tempo 90 --max-tempo 160 --onset-max-frequency 500
/tmp/candy-beats/bin/python -m unittest discover -s tools -p 'test_*.py'
```

Choose tempo bounds that include the song's changes while excluding unwanted
half/double-time interpretations. The optional onset-frequency ceiling focuses
detection on low-frequency percussion. The analyzer uses local rhythmic evidence
with a smoothed tempo path, then the established
[librosa beat tracker](https://librosa.org/doc/0.11.0/generated/librosa.beat.beat_track.html)
with time-varying tempo. No analysis library is required by the Unity build.
The saved files record the generation settings for reproducibility.

The border, magic colors, and repeating enemy
warning flashes follow the current audio sample clock. Gameplay cooldowns remain
unchanged. While enemies are present, the border alternates white and the current
player display color, independently of earned bars. Outside a live wave it stays
white unless a clear transition is active. Fleet arrivals crunch the rim and all
world-space gameplay visuals inward together, then release them over 0.8 seconds.
At the midpoint, the view is compressed by twelve percent and the boundary,
outline and corners reach three times their normal visible thickness. Size and
thickness return smoothly to normal without an added brightness flash. This is a
camera presentation effect: actor positions, collision bounds, wrap limits and
gameplay camera dimensions stay fixed, pointer projection follows the view, and
the compression has no sound cue. Enemies
pulse on the musical downbeat and the player on the opposite half-beat. A separate
visual pivot keeps this motion independent of spawn growth, disguises and hitboxes.
Clear transitions stretch from the fleet clear toward the next eligible
current-track downbeat. The final regular bar removal lands one beat before the
spawn downbeat; earned-life clears reserve two beats so the life pulse can occupy
the intervening beat. The available bar span is divided evenly across the earned
bars. Each slot starts white, flashes that bar's color for the second half, and
removes the bar at the slot boundary. The last bar is reserved for the final
flash. The transition ends on that color, which becomes the player's color as
the new fleet appears. Partial sets play the replaceable `BarPowerDown` cue
exactly when each earned bar disappears, descending two semitones per bar.
Complete sets retain their jackpot celebration and play `BarPowerUp` at each
removal, ascending along the major scale. Each power-up cue sweeps upward in
pitch; both cues can be replaced independently in the sound settings.
Tongue return
and lingering death particles do not extend this beat-count deadline; pause freezes it.

The music player uses its own nonspatial `AudioSource`, separate from the shared
sound-effects voices. Its default volume is 0.15 so the track sits under gameplay
cues. Set Volume or Muted on `GameplayMusicPlayer` to tune music without changing
effect loudness. Assigning a clip directly on the component overrides the default
Resources track.

### Music loudness

Local music recordings are measured over their complete duration and leveled
to -14 LUFS (Loudness Units relative to Full Scale) before the music volume control.
`Resources/MusicLoudness.json` stores the measured loudness, true peak, source
fingerprint and fixed gain for each recording. `MusicLoudness` loads the profile
once; `GameplayMusicPlayer` multiplies its gain by the music volume and game-over
envelope. The currently playing clip determines the gain, including menu carryover,
shuffle changes and assigned clips. Unmeasured replacements use neutral gain.

This uses attenuation only, preserves each song's internal dynamics, and leaves
the original audio and beat maps untouched. All normalized peaks remain below
-1 dBTP (decibels true peak). The profile can also cover measured local
recordings outside the active playlist without adding them to the shuffle.

After adding or replacing music, regenerate and verify with FFmpeg installed:

```sh
python3 tools/generate_music_loudness.py Assets/Resources Assets/Resources/MusicLoudness.json
python3 tools/generate_music_loudness.py Assets/Resources Assets/Resources/MusicLoudness.json --check
python3 -m unittest discover -s tools -p 'test_generate_music_loudness.py'
```

The generator measures the actual gain-adjusted output too, requiring every song
to land within 0.1 loudness units of the common target. If a future track is quieter
or needs more peak headroom, it lowers the common target for the entire set instead
of boosting or compressing that recording. `MusicLoudnessChecks`, included in the
sound checks, verifies any local clips that are installed, while clean clones stay
music-free and still test menu continuity, gain, master volume, mute, pause and
the immediate game-over cut with generated clips.

## Adding effects

1. Add a value to `SoundEffect` and a matching Cues entry in `SoundEffects`.
2. Assign a clip in the scene, or add a synthesized default to `ArcadeSoundClips`.
3. In the relevant presentation component, request a voice with `CreateVoice` and
   call `Play` in response to gameplay events. Playback replaces the current sound
   on that voice; request separate voices for overlapping sounds. For sustained
   sounds, pass `loop: true`, then update the source pitch and use `SetGain` for
   an animation-driven envelope. Shared volume and mute remain effective.
4. Stop the voice when its action is canceled and release it when its owner is
   destroyed. Follow `TongueSoundEffects` for event subscription and cleanup.

Each voice is a nonspatial source under its owner. The service owns generated clips
and tracks voices for shared volume, mute, pause, and cleanup. It neither persists
across scenes nor changes collision, scoring, or animation timing.

## Verification

`CandyCruisers.Editor.SoundEffectsChecks.Run` checks oscillator loop continuity,
live pitch changes, early turnaround without pitch jumps, multi-second deflected
returns, magic speed, blocked fire, cancellation, tone replacement, volume, mute,
music state, and session pause wiring. It is included in the main editor suite.
Runtime checks verify sustained audible playback beyond the old clip duration,
gameplay music startup, frozen pitch and playback while paused, resume, and scene
reload.
## Gameplay Event Cues

The `SoundEffects` component exposes replaceable clips and per-cue volume for
missile flight, shield power-up, Yellow transform/hide/reveal, Purple warps, enemy
defeat, level changes, extra lives, and game over. Green movement
pulses and dashes are visual-only.
Missing clips use distinct synthesized arcade tones from `ArcadeSoundClips`.
One-shots share a 24-voice pool, separate from the continuously animated tongue
voice. They follow the existing master volume, mute and pause settings.

Enemy defeat sounds begin with the staggered `PresentationCue`, not when the
whole chain is removed from the model. The displayed multiplier selects a note
in a repeating C-major arpeggio: C4, E4, G4, C5, E5, G5. The stable C4 sample
has no pitch sweep; semitone offsets are 0, 4, 7, 12, 16, 19. Repeating keeps
long chains inside the audio engine's pitch range without clamping to an out-of-key note.
Small-chain pops stay at base pitch. Wave audio fires once per batch, and one-up audio only when
a life is actually granted.

`PlayerShatter` plays once when the fatal player's fragments appear, not after
the animation ends. Its generated fallback preserves the 0.7-second dry crack
and glass-like ringing, then adds distinct left/right echoes at 0.18/0.215 seconds
and damped room reflections, with a 2.1-second total stereo tail. Effects are
baked into this cue only, so pooled voices never carry reverb into unrelated
sounds. It supports clip replacement, per-cue volume, pause and master mute.
During the preceding four-beat blackout, the current music recording slows continuously
like a tape stop, dropping from normal speed to 4 percent speed (one fifth speed at
the midpoint) while its normalized volume fades to zero. This envelope follows the
unscaled death-fade clock captured at the fatal hit, not the slowing audio clock.
The track is neither restarted nor replaced; playlist advancement is suspended.
Music is fully stopped before the fatal dust and shatter, which retain their normal
speed and volume. Normal playback restores the original music pitch. The legacy `GameOver`
cadence remains available for explicit use but the session no longer plays it.

## Missile Flight

Red launches no longer play a firing cue. `RedFire` retains its serialized enum
value but is retired from playable cues. Regular, aimed and homing Red missiles
instead own a `MissileFlight` beep voice, independent of their shooter and the
one-shot pool.

Missile visuals flash from the soundtrack beat clock. A far missile completes one
flash cycle every two beats; as it nears the player the cycle compresses toward
one half-beat, so its warning tempo increases with danger while staying tied to
the song. The generated `MissileFlight` cue is a short chiptune warning beep,
cached per musical key. `MissileFlightSound` plays it only when the missile's
beat flash reaches its peak, so there is no continuous canned flight texture.

`MissileFlightSound` measures distance in the two-dimensional playfield from the
missile to the actual player, not the camera's audio listener. Horizontal distance
uses the actual separation, without wrapping across screen edges. Volume uses a
cubed smooth falloff, reaching full cue volume within **Full Volume Distance**
and exactly zero at **Audible Distance** or farther. Once the missile has passed
the player's expanded hit area and can no longer collide on its current heading,
the distance gain is multiplied by **Passed Target Gain**. Direction from the
player controls left/right stereo positioning; gain, pan and the armed beep voice remain stable when a missile crosses an edge.
Panning updates every frame: a left flyby is strongest in the left headphone,
a right flyby in the right, and a missile directly ahead or behind stays centered.
A 0.25-unit center region smoothly blends close crossings between ears. Each
missile pans independently; music and other cues keep their existing positions.
Edit **Full Volume Distance**, **Audible Distance** and **Passed Target Gain** on
the Red Missile prefab to tune these limits. `MissileFlight` defaults to 0.55 cue
volume, multiplied by the shared sound-effects volume and the missile gain. Its
clip and volume can be replaced in `SoundEffects` like other cues; replacements
should be short one-shot warning sounds.

Missile beep voices respect pause/resume, mute, player loss and game-over suspension.
They survive shooter destruction and fleet clears, and stop immediately on
impact or disabling the projectile. Missiles fly offscreen without wrapping;
only the player wraps. Swept collision checks the player's actual position,
never an opposite-edge copy. Side cleanup waits until the missile is beyond
hearing range, so leaving the screen does not abruptly cut off a flyby. The bottom despawn
boundary is below both the playfield and the player's full hearing radius, with
an extra 0.25-unit margin; increasing the hearing radius moves it down too.
Lifetime expiry waits until a missile is beyond hearing range, preventing an
audible flyby from being abruptly cut off.
`MissileFlightSoundChecks` covers proximity, passed-player damping, exact silence,
stereo positioning, waveform safety, pause, cleanup and all three flight modes in both full suites.
`MissileBoundaryChecks` covers non-wrapping edge exits, positional audio, direct collision,
frame timing, customized hearing ranges and silent despawn.

## Color-Clear Rewards

`ColorClear` is a separate replaceable chime emitted when the earned color bar
appears. The six bars ascend C5, D5, E5, F5, G5, A5: semitone offsets 0, 2, 4, 5, 7, 9.
It counts the active color-clear sequence, so losing the bars or beginning a
new wave resets the next chime to base pitch. Transformation alone does not
trigger this reward. Enemy-death combo tones remain separate and staggered.
## Full-Set Jackpot

Completing every reward bar plays the replaceable `Jackpot` cue in addition to
the final bar's note. The default is a 1.8-second ascending bell cascade with
a ringing C-major chord finale. It is independent of the one-up reward, so it
also plays when lives are already full. Normal volume, mute and pause apply.
