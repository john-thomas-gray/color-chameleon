# Character Art And Animation Replacement

## Player Death

Fatal fleet contact and recoverable player hits invoke `CharacterVisuals.PlayerDefeat`, including the normal
Defeated event and Animator trigger. The player's existing defeat-prefab slot
replaces the entire placeholder, including its duration or completion event.
With no prefab assigned, recoverable hits use `PlayerDeathBurst`: it snapshots
the body and eyes, flashes white, then scatters colored fragments for 0.9 seconds.
Fatal game-over hits use `PlayerFatalDustBurst` instead, throwing multicolored
space-dust from every currently unlocked enemy color into a hot core cloud and a
faster flat shock ring. The real sprites stay hidden until the scene restarts for
fatal contact, or until respawn after a recoverable hit. Unlike enemy defeats, it
never displays a digit.

During `GameSession.RunState.Dying`, gameplay is suspended and the session drives
the cue timer exactly once per frame. Game over follows cue completion; the
prefab duration remains a fallback for missing animation events. Pause cannot
overlay this short fatal transition. For recoverable hits, `PlayerMovement`
advances the cue during its recovery timer, without freezing the fleet. The
default cue finishes before the existing 1.5-second respawn; invulnerability
ends at three seconds. Longer replacement cues must finish before respawn.

## Saved Hierarchy

Each enemy prefab has a gameplay root with `CharacterVisuals`, a `Visuals`
child, and a `Visuals/Body` sprite renderer. The player has the same visual
root containing its body, eyes and pupils. Its tongue remains a separate child
so firing geometry is not moved by body animation. Grid identity, movement,
abilities and scoring remain on gameplay objects.

`CharacterVisuals` stores explicit body/root references and serialized local
hit bounds. Animate the visual children, not the gameplay root. Body sprite,
offset and scale changes do not change tongue or player collision bounds.
Shield activation remains ability-driven, with hit bounds independent of its
power-up artwork. Edit the bounds deliberately only when changing gameplay.

The idempotent `Candy Cruisers > Upgrade Visual Children` editor command migrates
the five enemy prefabs and saved gameplay scene without regenerating gameplay.

## Replace Body Art

Open the relevant enemy prefab under `Assets/Prefabs` and replace the sprite on
`Visuals/Body`. Keep its body reference assigned on `CharacterVisuals`. Fit the
new artwork using the Body child's scale and offset; leave the gameplay root
and its saved hit bounds alone. Use the spawner's existing `Special Sprite`
slot to replace the shared tier-two triangle.

For animated sprite sheets, put an Animator on `Visuals`, assign it to the
actor's `CharacterVisuals.Animator` field, and animate the Body child's sprite
or transform. The existing color warnings and disguise effects still control
body tint; do not keyframe tint unless intentionally replacing that presentation.

## Firing, Matching And Defeat

The actor's Inspector has three independent `PresentationCue` prefab slots:

| Slot | Trigger | Default |
| --- | --- | --- |
| Fire Prefab | Accepted player fire or actual Red missile creation | Short colored pulse |
| Match Prefab | Each enemy actually removed by a matching clear | Expanding colored ring |
| Defeat Prefab | The same confirmed removal | Existing cascading shards and numbered digit |

An empty slot uses the current placeholder. Assigning a prefab replaces that
placeholder; it does not stack another copy on top. Match and defeat effects
are detached before the enemy is removed, so they finish after the enemy's
gameplay object has gone. Both use `(depth - 1) * 0.065` seconds of cascade delay.
Defeat scoring uses that same depth immediately, without waiting for animation.

Create a prefab with `PresentationCue` on its root and put its sprites or
Animator under an `Artwork` child. Assign that child to the cue's Artwork field
so it starts at the correct cascade time. The effect spawns at the source body
position; author its dimensions in world units. Set Duration to its maximum
playback time after the delay. Tint Sprites applies the enemy/player color once;
disable it for precolored drawings. The instantiated component exposes Color
and Depth for optional custom presentation scripts. Multiplier is the score and
label value: hide it when it is 1. Depth controls local cascade timing, while
Multiplier may continue increasing across multiple chains hit by a magic shot.

## Hooks And Clip Events

`CharacterVisuals` exposes Inspector events Fired, Matched and Defeated plus
optional Animator trigger names (Fire, Match, Defeat by default). Only valid
trigger parameters are sent. These events are notifications after gameplay has
accepted the action, not requests to perform it. Enemy body animation stops
when the enemy is removed, so put lasting match/death animation on cue prefabs.

The `VisualAnimationEvents` component on Visuals accepts the clip event
`OnAnimationFinished` and forwards it to the actor's Animation Finished event.
For detached cue prefabs, Started runs once after the branch delay. A clip on
the cue root can call `PresentationCue.Finish`. For an Animator on Artwork,
add `VisualAnimationEvents` beside it and call `OnAnimationFinished`; the relay
finds the parent cue without additional code or wiring. Duration is always
a fallback, so a missing clip event cannot leave a permanent effect behind.
Completed fires only once. A Finish call before the branch delay is ignored.

Never wire these presentation events to damage, spawning, scoring or movement.
No shipped completion callback performs those operations. Fleet refill keeps
its existing wait for detached effects, while enemy removal, score and color
clear rewards occur immediately. Scene reload clears all effects; destroying
the grid also cleans up its detached match/death effects.

## Verification

`PresentationChecks` runs in both full editor and Play mode suites. It checks
saved visual-child hierarchies, animation-independent hit detection, accepted
fire only, exactly-once match/defeat hooks, custom prefab replacement, color and
depth context, delayed starts and cleanup without a clip completion event.
