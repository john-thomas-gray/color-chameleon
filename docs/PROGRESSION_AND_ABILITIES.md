# Progression and Ordinary Abilities

## Recoverable Death And Forcefield Return

Missile hits and the last-Yellow transformation penalty use the same replaceable
player-death cue as fatal contact. The player flashes and bursts into fragments,
then respawns at 1.5 seconds with protection lasting until three seconds after
the hit. The fleet keeps moving; ordinary pause also pauses recovery animation.

Special Blue forcefields slow the stunned tongue's return using the archived
`legacy-v1:Candy Cruisers/Assets/Scripts/Tongue.cs` rule: `maxSpeed = 20` (also
verified in the archived Player prefab), with `speedFactor = -0.25 + 0.01 * level`.
The positive return rate is `20 * (0.25 - 0.01 * level)`: 4.8 at level 1,
3 at level 10 and 1.2 at level 19. The rebuild floors this at 0.2 units per second
to avoid the archived zero/reversed return at level 25 and above. The rate is
captured at impact and applies immediately, including leftover time in that
frame. Matching Blue and magic shots still bypass the forcefield. Normal shots
retain their existing return speed, and stun ends when the tongue returns or is canceled.

## Progression

## Orange: Singleton Swap

Orange unlocks at level 9 and never receives a special tier. Each eligible new
wave guarantees exactly one Orange. Later row slots and Purple summons each
have a 1-in-75 chance while no Orange is present, subject to color-clear locks.
Only one actual Orange may spawn on screen; disguised special Yellows remain
Yellow and do not consume that slot. An Orange-only override starts with one
enemy and leaves subsequent slots empty unless their rare Orange roll succeeds.
The older retreat-explosion behavior remains as a defensive fallback for custom
fleets, but normal spawning can no longer produce two Oranges.

On its randomized
cooldown (6-12 seconds initially, with the existing level-based maximum reduction),
it flashes, casts for .35 seconds, then swaps positions with an eligible singleton.
The target must have no same-color or active imitation-chain connections, and
its color must match a different enemy next to the Orange's original position.
Eligibility uses true colors, not special Yellow disguises. A missing eligible
target simply consumes that attempt and waits for the next cooldown.

Spawn and swap destinations cannot put two Oranges in orthogonally adjacent
cells. Batch/new-row planners keep Orange runs to one enemy and extend an
existing neighboring color when a vertical conflict would occur. Orange-only
developer overrides deliberately leave empty cells. Summons filter illegal
Orange destinations before choosing a color. Swaps update both identities and
cells atomically, then show orange portal effects at both endpoints.

Retreat is the exception: if it brings Oranges together, their entire connected
Orange group immediately explodes. Each awards five times the current level's
base enemy points. Defeat progression, last-of-color rewards and fleet bonuses
still apply; shot combos do not multiply these environmental points or receive
hit credit. Orange explosions never create a special tier. Ordinary Yellow
cannot copy Orange, but special Yellow can wear an Orange disguise normally.

### Quiet Player Color Assistance

`EnemyGrid.SelectablePlayerColors` filters the existing selectable colors when
the player needs a new color. It does not add visible feedback or change an
already-ready color just because the formation moves.

- The front-most enemy is the nearest occupied cell to the player in each
  occupied column. Empty columns do not count. If every one of those enemies
  is a special Blue, the next selected color is Blue, even on an uneven front.
- Otherwise, exclude a color only when every enemy of that color is in the
  immediately adjacent cell above a special Blue, away from the player. A gap
  does not count. One enemy elsewhere keeps its color eligible.
- Ordinary Blues do not trigger these rules. Selection uses logical colors,
  not disguise artwork. The transforming-Yellow exclusion still applies.
- Apply the rules on a new selection or reroll, never to a tongue in flight.
  Recheck a reserved next-wave color after the fleet spawns and its special
  groups are promoted. All remaining eligible colors retain equal random odds.

An empty board does not invent a Blue enemy or interrupt the existing next-wave
color reservation. No cooldown, shield activation, score, or spawn rule changes.

### Score And Levels

`RunProgress` owns score, defeated count, and level. A real matching clear emits
one `EnemyGrid.MatchCleared` event with its removed count, fleet-clear flag and
the sum of the defeated enemies' death-cascade digits.
`GameSession` awards score before handling the separate fleet refill event.
Empty/repeated hits, shield absorption, and disabling enemies award nothing.

Each enemy awards `(100 + 10 * (level - 1)) * multiplier` points. Ordinary shots
use shortest connection depth: the hit enemy is 1, direct neighbors 2, and each
further branch adds one, including ordinary Yellow attachment links.
Only chains of three or more earn multipliers. Chains of one or two award base
points per enemy and use staggered unnumbered singleton pops for either shot type.
Magic shots add the preceding peak multiplier to each enemy's local connection
depth. Equally distant branches share a multiplier; the new peak carries into
the next qualifying chain. For example, 2, 1, 2, 3, 4, 5 followed by a chain
with local depths 2, 1, 2, 3 produces 7, 6, 7, 8. Small chains neither advance
nor reset the peak. Every new accepted shot resets it; rejected fire does not.
Both shot types hide the first multiplier's label while still awarding its
base points. Defeat progression still counts actual enemies.

The combo streak is separate from death digits and magic-chain peaks. A shot
snapshots the current combo when it is accepted. If it defeats at least one
enemy, all enemy points from that shot use that combo; when the tongue returns
or is canceled, the next accepted shot's combo increases by one. The first
successful shot uses x1, the next uninterrupted successful shot uses x2, then
x3, and so on. A shot that defeats no enemies resets the streak when it returns
or is canceled. Rejected fire commands do not reset it. A player hit resets the
streak immediately. Multi-chain magic shots use one combo snapshot for the
whole shot, so they do not increase their own combo while still in flight.

Clearing the fleet adds `10000 * level`, without a digit or combo multiplier.
Both use the level before that clear.

The next level requires `9 * level * (level + 1)` cumulative defeats. Thus level
2 begins at 18, level 3 at 54, level 4 at 108, level 5 at 180, and level 6 at 270.
A large clear can cross multiple thresholds without losing progression.

| Level | Eligible Types | Refill Size |
| --- | --- | --- |
| 1 | Red, Blue | 10 |
| 2 | Red, Blue, Green | 10 |
| 3 | Red, Blue, Green | 15 |
| 4-5 | Red, Blue, Green, Purple | 25 |
| 6 | Red, Blue, Green, Purple, Yellow | 25 |
| 7-8 | Red, Blue, Green, Purple, Yellow | 36 |
| 9+ | All six | 36 |

The scene starts empty. After the one-second opening pause, ten Red/Blue
enemies phase in (or override-selected types when testing). Unlocks affect subsequent
rows, refill batches, and Purple summons. Subsequent rows are five enemies wide
through level 6 and six enemies wide from level 7 onward. Existing enemies do
not change type when a level changes. Restart reloads a fresh score and
level-one state.
See `docs/LEVEL_PROGRESSION.md` for the dedicated level-by-level design reference.
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

Its rebalanced cooldown starts at 6-9 seconds and follows the shared gentle
maximum reduction below. Purple flashes brightly in the final 0.45 seconds.
The summon is registered immediately when the cooldown completes; its rift and
0.8-second phase-in are presentation only. The summoned enemy can be matched
and use its abilities during the phase-in. A full fleet produces no summon.
The original random-neighbor upper-bound bug is not reproduced.

Tier-two Purple removes the ordinary farthest-row restrictions. Its candidate
gaps can extend one row below the lowest occupied row, still beside an occupied
cell and inside the horizontal playfield. Row index 10 (the eleventh, bottom row)
is also eligible. Summons cannot extend beyond the grid. An occupied bottom row
blocks further descent; game over requires its enemy body to touch the player. Color locks,
level unlocks, overrides, cooldowns and rift presentation are unchanged.

## Yellow: Linked Transformation

Ordinary Yellow randomly chooses an occupied orthogonal non-Yellow neighbor and attaches
to it for two seconds. During this transition it remains logically Yellow:
Yellow shots can hit it, Yellow counts include it, and it does not yet receive
the destination type's ordinary ability.

New player-color selections exclude Yellow when every remaining Yellow is
transforming. An already-Yellow player keeps that color until the usual reroll
or completion penalty. Any untransformed Yellow, including a newly spawned one
or a canceled transformation, makes Yellow selectable again. Next-fleet color
selection still uses the planned incoming enemies.

Its rebalanced cooldown starts at 1.5-4.5 seconds with the shared maximum
reduction below. A failed attempt with no eligible neighbor retries
after another cooldown. The tendril forms a two-way hit link: a matching hit on
the Yellow clears both its Yellow group and the destination's connected color
group; a matching hit on that group also clears attached Yellows. Linked clears
are discovered before removal so multiple attachments and bridges resolve once,
with each defeated enemy scored once. Clearing the last actual Yellow by a hit
earns the Yellow bar and the usual magic reward, regardless of which side was hit.

On completion, the Yellow permanently adopts its target's color and ordinary
ability. Conversion is not a kill: it grants no score, magic or Yellow clear bar,
and leaves Yellow eligible to spawn. If this was the last Yellow and the player's
selected shot color is Yellow, the player takes a normal hit (respecting existing
invulnerability), canceling an active shot. Cosmetic magic color flashes do not
change that selected-color check. A non-Yellow player is not hit.

The logical timer lives in `EnemyAbilities`, not in an animation callback. The
presentation remains replaceable. If the target disappears outside a combat
clear or changes type mid-transition, the attachment cancels and the source
stays Yellow. After a completed conversion, the former target no longer has a
special link to the converted enemy.

## Special Yellow: Disguise

Tier-two Yellow visually imitates a non-Yellow orthogonal neighbor over the same
two-second transition, without a tendril or combat link. It copies the sprite,
scale and color, not the enemy's abilities or logical identity. It remains Yellow
for counts, player-color availability, connected clears, promotion and rewards.
Two real Reds and a disguised Yellow therefore remain a two-Red group, not a
special Red group. The disguise survives the copied enemy's removal or conversion.

Any non-Yellow ordinary tongue can hit the disguised enemy, even during its
transition. The shot passes through and reveals it without killing, scoring or stunning:
the enemy flashes white/Yellow for half a second, restores its own Yellow
triangle, and starts a fresh cooldown. The tongue keeps extending and can reveal
additional disguises or hit enemies behind it, even within the same frame.
Yellow and magic shots kill its actual
Yellow group and can earn the Yellow bar without clearing the copied group.
Disguising the last Yellow never triggers the ordinary transformation penalty.
If an ordinary Yellow promotes while transforming, its link is detached and the
remaining visual transition finishes as a disguise rather than a conversion.

## Green: Count-Based Movement

Default movement is `0.03 * liveGreenCount * levelSpeedMultiplier` world units
per second. There is no movement with zero Greens, including the Red/Blue-only
opening. Counts are read from the live grid, including Yellow conversions.
After a boundary adds a row, remaining frame time uses the updated count.

Tier-two Greens add the retained fast-forward dash: individual 0.1-unit bursts
over 0.18 seconds, preceded by a 0.6-second flashing warning and fleet-wide
speed-wake visuals. Each samples a fresh 6-15-second cooldown on promotion and
after activation, subject to level scaling. Promotion cannot immediately release
an overdue ordinary-enemy timer. Both tiers still contribute to count-based speed;
ordinary Greens do not cast or dash.

`EnemyGridMovement.UseGreenDashes` remains as an optional legacy switch, false
by default. Enabling it restores nonzero base movement and allows ordinary
Greens to dash too; upgraded Greens do not require this switch.

Both normal travel and bursts use `EnemyGridMovement.AdvanceDistance`, including
occupied-column edge detection, exact contact, direction reversal, descent,
new rows, reindexing, and game-over guards. Removing a Green cancels any
unspent burst; suspending the run stops its timer and movement contribution.

## Shared Difficulty Scaling

Starting cooldown ranges are Red 2.25-7.5 seconds, Blue recharge 2.25-7.5,
special/legacy Green dashes 6-15, Purple 6-9, and Yellow 1.5-4.5. Each enemy independently resamples its
interval after activation. Ordinary Blue starts unshielded, with its initial
cooldown counting from spawn. Only after that cooldown expires does its shield
power up for 0.35 seconds; it cannot protect or render during the spawn rift.
Yellow-to-Blue conversions also begin with a full cooldown. Upgraded Blue group
shields retain their existing arrival/promotion timing.
Its brighter near-white cyan arc expands from a point below the enemy into
its original full shape. Protection begins only when expansion is complete.
A mismatch breaks it permanently. Ordinary Blues power up only once per Blue
color lifetime; replaying spawn visuals does not restore a spent shield.

Every level above one subtracts 0.25 seconds from each range's maximum, never
reducing it below the minimum plus three seconds. Minimums stay fixed. Existing
countdowns finish normally; new rolls use the current level. This replaces the
older, stronger Purple/Yellow cooldown scaling.

Fleet base speed increases by 3% of its initial value per level, capped at twice
the initial speed. With the default 0.03 units/second per Green, level 6 moves at 0.0345 per Green.
Green's 0.1-unit burst remains separate and does not grow with level.
`CombatBalance` centralizes these tuning values.

## Magic Shot

Clearing the last enemy of a color grants one magic charge, capped at two.
Partial clears, repeated hits, conversion, and disabling enemies do not award
charges. A color that later returns can earn another charge when cleared again.
The player flashes through all five colors when magic is ready or a magic shot
is active, changing color every 0.1 seconds. The level-progress fill uses the
same displayed player color; the display also shows the stored charge count.
The arcade-style playfield frame also follows that displayed color, including
magic flashing, without changing the logical wrap or fleet-turn boundaries.

The next successful firing consumes one charge and creates a rainbow piercing
tongue. It hits any color, bypasses shields, and clears each struck enemy's
ordinary same-color chain. An accepted magic shot consumes a charge and fires
immediately, with no wind-up or charge animation. Repeated fire is blocked while
the shot is active, and hit/game-over cancellation stops the shot. The old
`MagicChargePresentation` placeholder remains unused for possible future reuse.
Magic is twice as wide and travels at four times the normal extension and return
speeds (56 and 80 world units/second), twice the previous magic speeds. It continues
extending rather than retracting at first contact, unless that hit clears the
fleet, in which case it immediately begins its visible return. Hits are swept over the full travel
segment so a slow frame cannot skip intermediate enemies. Retraction does not
deal damage. A missed or subsequently canceled shot still spends its charge;
a rejected fire command spends nothing. New charges earned during a magic shot
are available for following shots, not used to change the current shot.

Following the original reset rules, a player hit, fleet clear, or restart removes
stored magic. The original color-wheel visual direction is no longer part of
the rebuild; the bottom color-clear blocks are the retained color-progress
presentation.

## Numbered Death Cascades

Each match records shortest connection distance from the struck enemy before
removing anything. On ordinary shots the first enemy has no numeral, its
neighbors show `2`, their next neighbors show `3`, and so on. Siblings share a
number and burst together. Active Yellow tendrils count as links, and each
digit uses the dying enemy's actual color. Magic shots display a continuous
per-enemy count across all groups: blank, `2`, `3`, `4`, and so on. Local branch
depth still controls each group's animation timing independently of its score
multiplier, so later hits do not acquire long delays merely from a large count.

`EnemyDeathBurst.RingDelay` is 0.065 seconds between rings; `BurstSeconds` is
0.5 seconds for the shards and digit pop/fade. The effect holds a sprite snapshot
until its ring begins. Combat occupancy, ability shutdown and scoring resolve
immediately, so visual remnants cannot shoot, be hit again or duplicate rewards.
Refills wait for the outgoing effects as well as the returning tongue. The
presentation component can be replaced without changing matching logic.

## Tier Two Groups

Enemy tier is separate from game level. As in the original promotion rule, a
same-color orthogonal group of three or more upgrades its members. All five
colors now upgrade. They retain tier two when the group splits; changing
color resets the tier until the new group qualifies. Diagonal neighbors and
Yellow tendrils do not count toward promotion.

Tier-two ships use equilateral triangles pointing downward. `EnemyRowSpawner`'s
optional `specialSprite` field can replace the procedural placeholder art.
Special Purple adds forward-row summons, including the bottom row. Special Yellow uses a visual-only
disguise instead of ordinary linked transformation, as detailed above.

An active tier-two Blue shields its whole connected Blue group with a rigid
outline along the exposed cell edges, including concave boundaries. Internal
edges and individual arcs are hidden; disconnected groups have separate shields.
The shield waits for the existing arrival/power-up sequence. A non-Blue ordinary
tongue hits the perimeter and retracts without damaging the shield. The player
and tongue turn grey, movement and firing stop, and the stun ends exactly when
the tongue finishes or is canceled. Blue shots and magic bypass the shield.
Ordinary Blues retain their existing breakable arc shields.

Tier-two Reds retain the existing cooldown and .45-second firing warning.
During the warning, the triangle rotates toward the player's current position
at 540 degrees per second. At launch, it aligns exactly with the shot direction,
fires from its forward tip, then smoothly returns to its resting downward pose.
Only the body artwork rotates; grid placement and collision bounds do not.
Pause freezes the turn, and color changes or disabling the enemy clear the pose.

The aimed shot keeps its launch direction for its entire flight: no tracking,
turning or loops. It keeps the former special missile's 3.5-unit speed and opaque
1.5-second white pulse, and retires at the playfield edges or after five seconds.
Swept collision handles slow frames. If no living player is available at launch,
the fallback direction is downward. Ordinary Reds retain their steady-colored,
downward 5-unit-per-second missiles. The legacy homing mode remains available in
code but is no longer used by special Reds.

## Color-Clear Cycle

A genuine last-of-color clear locks that color out of new rows and Purple
summons while its color-clear bar remains visible. Level increases and override edits do
not erase locks. Conversion and disabling enemies do not count as clears.
Locks remain active during the refill pause; a successful new batch resets
them after the entire fleet has been cleared. Restart also starts fresh.

The colored segments at the bottom of the playfield record a color-clear streak.
Each shot must destroy at least one entire color to retain the existing segments
and add the newly cleared colors. A miss, partial-group clear or shield-only hit
clears all segments when the tongue finishes returning. Canceling an unsuccessful
shot also ends the streak. Ordinary and magic shots follow the same rule; a magic
shot can clear multiple colors, and one successful clear is enough. Breaking the
streak releases the spawn locks for the disappearing bars, making those colors
eligible for new rows and Purple summons again, subject to level and override
settings. Seen-color history, score and magic rewards are not reset.
The next genuine color clear starts a fresh streak without restoring old bars.
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
0.8-second fade. Batch, row and ordinary Purple summons use the arriving enemy's
color. Special Purple summons always use a purple rift, regardless of the new
enemy's color; its body, logical color and abilities remain unchanged.
The sprite begins fully transparent immediately,
preventing a full-opacity flash before animation. Blue's shield stays hidden
during the rift, then runs its separate 0.35-second power-up.
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
separate checklist item. Authored row patterns and field-aware fairness remain
pending. Generated rows now choose up to three distinct eligible colors without
replacement and allocate adjacent runs, never repeating a color in separate
parts of a new row. Run sizes and color order remain randomized. This applies
to openings, new rows and refills, not individual Purple summons or Yellow
transformations of existing enemies.

## Verification

`ProgressionChecks` extends the full editor suite with thresholds, scoring,
unlock-gated rows and summons, independent Green steps and edge contact,
timed Yellow conversion, linked clears and ability adoption on completion, fleet-wide Purple
selection, and full-grid safety. The Play mode suite checks real-hit scoring,
restart reset, and crossing an unlock threshold through a real clear.

The editor-only `RuntimeGameplayChecks.Preview` builds a temporary cue showcase
and captures `TestResults/ability-feedback.png` in a visible Unity session.
It never saves the showcase over the gameplay scene.
