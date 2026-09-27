# Sound effects

The tongue plays one continuous chiptune slide-whistle throughout its movement.
Its pitch follows its visible length on every animation update: extending raises
the pitch and retracting lowers it. Hits and shields do not restart the sound or
jump to a preset return pitch. Slow deflections stretch out the downward slide;
magic slides faster because the tongue itself moves faster. Completed and canceled
shots stop the tone. There are no other sounds or music yet.

`ArcadeSoundClips` supplies one seamless oscillator cycle, not a recorded sweep.
`TongueSoundEffects` starts it looping once and uses `TongueShot.MotionUpdated` to
change pitch and gain. No timer controls the sound's duration or pitch trajectory.
The initial frequency is 420 cycles per second, doubling per 5.5 world units of
tongue length. Gain fades near the mouth. Tune Base Frequency, Octave Length, and
Fade Length on `TongueSoundEffects` to change this response.

## Playback and replacement

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
and session pause wiring. It is included in the main editor suite. Runtime checks
verify sustained audible playback beyond the old clip duration, frozen pitch and
playback while paused, resume, and scene reload.
