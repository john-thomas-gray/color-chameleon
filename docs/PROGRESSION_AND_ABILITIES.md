# Progression and Ordinary Abilities

## Progression

`RunProgress` owns score, defeated count, and level. A real matching clear emits
one `EnemyGrid.MatchCleared` event with its removed count and fleet-clear flag.
`GameSession` awards score before handling the separate fleet refill event.
Empty/repeated hits, shield absorption, and disabling enemies award nothing.

Each enemy awards `100 + 10 * (level - 1)` points. Clearing the fleet adds
`10000 * level`. Both use the level before that clear. Combo and magic
multipliers are not implemented in this milestone.

The next level requires `9 * level * (level + 1)` cumulative defeats. Thus level
2 begins at 18, level 3 at 54, level 4 at 108, level 5 at 180, and level 6 at 270.
A large clear can cross multiple thresholds without losing progression.

| Level | Eligible Types | Refill Size |
| --- | --- | --- |
| 1 | Red, Blue | 15 |
| 2-3 | Red, Blue, Green | 20 |
| 4-5 | Red, Blue, Green, Purple | 25 |
| 6 | Red, Blue, Green, Purple, Yellow | 25 |
| 7+ | All five | 30 |

The scene starts empty. After the one-second opening pause, ten Red/Blue
enemies phase in (or override-selected types when testing). Unlocks affect subsequent
rows, refill batches, and Purple summons. Existing enemies do not change type
when a level changes. Restart reloads a fresh score and level-one state.
The heads-up display shows score, level, progress toward the next level, and
brief score/level/unlock feedback. Game over also shows final score.

## Purple: Preserve the Original Rule

Ordinary Purple summons one random eligible type into an empty orthogonally
adjacent cell anywhere in the fleet, not necessarily beside the caller.
Selection follows the original two-stage choice: random eligible anchor, then
random empty neighboring cell. The original farthest-candidate-row exclusions
are preserved: no downward extension from that row, and no rightward extension
from its last candidate. Candidates outside the visible horizontal boundary
are also excluded to respect the rebuild's occupied-column motion.

Its rebalanced cooldown starts at 18-50 seconds and follows the shared gentle
maximum reduction below. Purple flashes brightly in the final 0.6 seconds.
The summon is registered immediately when the cooldown completes; its rift and
0.8-second phase-in are presentation only. The summoned enemy can be matched
and use its abilities during the phase-in. A full fleet produces no summon.
The original random-neighbor upper-bound bug is not reproduced.

## Yellow: Preserve Ordinary Imitation

Yellow randomly chooses an occupied orthogonal non-Yellow neighbor, permanently
adopts that type, and receives its ordinary ability. It does not remain secretly
Yellow, revert when the neighbor dies, or become a special enemy.

Its rebalanced cooldown starts at 18-40 seconds with the shared maximum
reduction below. A failed attempt with no eligible neighbor retries
after another cooldown. When imitation triggers, the model color, shot matching,
available-color counts, sprite, and ability change immediately. The presentation
then draws a growing tendril and a two-second color/sprite transition. Neighbor
death does not cancel or undo the conversion. The tendril can finish at its last
known endpoint. The animation does not change the original gameplay timing.

## Green: Intentional New Behavior

Each Green has its own random 8-20-second interval and 0.8-second flashing windup.
The interval is independently sampled at spawn and again for every activation,
spreading groups of Greens over a wider window rather than a fixed cadence.
It then contributes 0.1 world units of extra fleet motion over 0.18 seconds,
with green zigzag speed wakes across the fleet. Multiple Greens contribute
individually; there is no passive speed multiplier.

Both normal travel and bursts use `EnemyGridMovement.AdvanceDistance`, including
occupied-column edge detection, exact contact, direction reversal, descent,
new rows, reindexing, and game-over guards. Removing a Green cancels any
unspent burst; suspending the run stops its timer and movement contribution.

## Shared Difficulty Scaling

Starting cooldown ranges are Red 6-18 seconds, Blue recharge 35-75, Green
8-20, Purple 18-50, and Yellow 18-40. Each enemy independently resamples its
interval after activation. Blue starts shielded and resamples when a mismatch
breaks its shield. The lower shield arc faces down toward the player; this
changes presentation, not the existing shield-blocking rules.

Every level above one subtracts 0.25 seconds from each range's maximum, never
reducing it below the minimum plus four seconds. Minimums stay fixed. Existing
countdowns finish normally; new rolls use the current level. This replaces the
older, stronger Purple/Yellow cooldown scaling.

Fleet base speed increases by 3% of its initial value per level, capped at twice
the initial speed. With the default 0.3 units/second, level 6 moves at 0.345.
Green's 0.1-unit burst remains separate and does not grow with level.
`CombatBalance` centralizes these tuning values.

## Magic Shot

Clearing the last enemy of a color grants one magic charge, capped at two.
Partial clears, repeated hits, conversion, and disabling enemies do not award
charges. A color that later returns can earn another charge when cleared again.
The player flashes through all five colors when magic is ready or a magic shot
is active, changing color every 0.2 seconds. The level-progress fill uses the
same displayed player color; the display also shows the stored charge count.

The next successful firing consumes one charge and creates a rainbow piercing
tongue. It hits any color, bypasses shields, and clears each struck enemy's
ordinary same-color chain. Magic is twice as wide and travels at twice the
normal extension and return speeds (28 and 40 world units/second). It continues
extending rather than retracting at first contact, unless that hit clears the
fleet, in which case it immediately begins its visible return. Hits are swept over the full travel
segment so a slow frame cannot skip intermediate enemies. Retraction does not
deal damage. A missed or subsequently canceled shot still spends its charge;
a rejected fire command spends nothing. New charges earned during a magic shot
are available for following shots, not used to change the current shot.

Following the original reset rules, a player hit, fleet clear, or restart removes
stored magic. The magic multiplier and color wheel remain separate pending work.

## Color-Clear Cycle

A genuine last-of-color clear locks that color out of new rows and Purple
summons for the remainder of the fleet. Level increases and override edits do
not erase locks. Conversion and disabling enemies do not count as clears.
Locks remain visible during the refill pause; a successful new batch resets
them after the entire fleet has been cleared. Restart also starts fresh.

The colored segments at the bottom of the playfield record destroyed colors.
Uncleared colors draw nothing; clearing a color fills its slot. Slots represent
distinct colors actually encountered in the current fleet, including cleared
colors. Each slot is the full progress-track width divided by that seen count, with a two-pixel
visual gutter. Earning a segment does not change the slots or their denominator.
Spawning or transforming into a newly encountered color adds a slot. Unlocks
and override selections alone do not. Seen history resets with a new batch. The earned segments
remain through the refill pause and reset when the new batch spawns.

If an override selects only locked colors while older enemies remain alive,
the fleet continues turning and descending but adds no new row; Purple summons
also do nothing. This prevents the development control from reviving a cleared
color or stalling descent. Unconfigured prefabs remain excluded from spawning.

## Wave Transition

The opening uses the same planned-batch mechanism as a refill, with two rows
instead of the later level-scaled refill size. Player movement is available
during the empty opening, and its selected color belongs to the planned fleet.
Restart repeats this empty opening rather than loading pre-placed enemies.

All newly spawned enemies use the original Purple-summon rift geometry and
0.8-second fade. The rift now takes the arriving enemy's color, including when
Purple summons a different type. The sprite begins fully transparent immediately,
preventing a full-opacity flash before animation; Blue's shield shares the fade.
Effects are cosmetic: grid registration, matching, movement, and ability timers
remain live during phase-in. The centralized spawn path triggers the effect for
opening fleets, ordinary rows, refills, and summons without duplicate triggers.

The last hit no longer cancels the tongue. It retracts visibly while the fleet
is paused, and the refill waits for both the configured delay and the tongue's
return. Player movement remains active, but firing into an empty field is blocked.

At fleet clear, the spawner prepares the exact next batch using the new level
and current override. The player keeps the outgoing shot color until the tongue
returns, then chooses a color from that prepared batch, preferring a different
color when available. No neutral grey state is introduced. The same batch is
instantiated after the pause, guaranteeing that the selected color appears.
Override edits during the pause apply to later spawning, not the reserved batch.

## Replaceable Presentation

Five enemy prefabs hold their sprites and base dimensions. `EnemyAbilities`
owns gameplay timers; `EnemyPresentation` owns rifts, speed wakes, tendrils,
flashes, and imitation overlays. Its `PhaseIn`, `SpeedShift`, `BeginImitation`,
and `Tick` entry points are the replacement boundary for hand-drawn animation.
Animation callbacks never award score, spawn enemies, or commit imitation.

The current effects are procedural placeholders, not final illustration or
frame-by-frame animation. Full firing/hit/defeat animation coverage remains a
separate checklist item. Authored five-enemy patterns and field-aware fairness
remain pending. Generated rows now choose up to three distinct eligible colors
without replacement and allocate adjacent runs, never repeating a color in
separate parts of a new row. Run sizes and color order remain randomized.
This applies to openings, new rows and refills, not individual Purple summons
or Yellow transformations of existing enemies.

## Verification

`ProgressionChecks` extends the full editor suite with thresholds, scoring,
unlock-gated rows and summons, independent Green steps and edge contact,
permanent Yellow conversion and immediate ability adoption, fleet-wide Purple
selection, and full-grid safety. The Play mode suite checks real-hit scoring,
restart reset, and crossing an unlock threshold through a real clear.

The editor-only `RuntimeGameplayChecks.Preview` builds a temporary cue showcase
and captures `TestResults/ability-feedback.png` in a visible Unity session.
It never saves the showcase over the gameplay scene.
