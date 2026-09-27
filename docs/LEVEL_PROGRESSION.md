# Level Progression

This document exists only to define what changes as the player reaches each
level. It intentionally leaves out mechanics that do not scale by level except
where they constrain level-based spawning.

## Level Triggers

Level is based on cumulative defeated enemies. The next level begins when the
defeated count reaches:

```text
next threshold = 9 * current level * (current level + 1)
```

| Level | Defeated enemies required | Next level at |
| --- | ---: | ---: |
| 1 | 0 | 18 |
| 2 | 18 | 54 |
| 3 | 54 | 108 |
| 4 | 108 | 180 |
| 5 | 180 | 270 |
| 6 | 270 | 378 |
| 7 | 378 | 504 |
| 8 | 504 | 648 |
| 9 | 648 | 810 |
| 10 | 810 | 990 |

## Level-by-Level Changes

The opening fleet is always two rows of five enemies. It uses Red and Blue
unless the editor-only spawn override is active. Refill batch size is the size
of future full-fleet respawns after the current fleet is cleared. New top rows
and refill rows are five enemies wide through level 6, then six enemies wide
from level 7 onward.

| Level | Newly unlocked enemy type | Eligible spawn pool | Row width | Refill batch | Enemy base points | Fleet-clear bonus | Fleet speed multiplier |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| 1 | Red, Blue | Red, Blue | 5 | 10 enemies | 100 | 10,000 | 1.00 |
| 2 | Green | Red, Blue, Green | 5 | 10 enemies | 110 | 20,000 | 1.03 |
| 3 | None | Red, Blue, Green | 5 | 15 enemies | 120 | 30,000 | 1.06 |
| 4 | Purple | Red, Blue, Green, Purple | 5 | 25 enemies | 130 | 40,000 | 1.09 |
| 5 | None | Red, Blue, Green, Purple | 5 | 25 enemies | 140 | 50,000 | 1.12 |
| 6 | Yellow | Red, Blue, Green, Purple, Yellow | 5 | 25 enemies | 150 | 60,000 | 1.15 |
| 7-8 | None | Red, Blue, Green, Purple, Yellow | 6 | 36 enemies | `100 + 10 * (level - 1)` | `10000 * level` | `min(2, 1 + 0.03 * (level - 1))` |
| 9+ | Orange | Red, Blue, Green, Purple, Yellow, Orange | 6 | 36 enemies | `100 + 10 * (level - 1)` | `10000 * level` | `min(2, 1 + 0.03 * (level - 1))` |

## Enemy Pool Changes

Level gates affect future opening plans, new top rows, refill batches and Purple
summons. Existing enemies never change type just because the level changes.

| Enemy type | First eligible level | Level-driven behavior change |
| --- | ---: | --- |
| Red | 1 | Can appear immediately. Its points and cooldown scaling follow the current level. |
| Blue | 1 | Can appear immediately. Its points and cooldown scaling follow the current level. |
| Green | 2 | Adds fleet movement because each live Green contributes speed. |
| Purple | 4 | Adds ordinary and tier-two summon threats to eligible fleets. |
| Yellow | 6 | Adds transformation, disguise and Yellow-clear reward pressure to eligible fleets. |
| Orange | 9 | Adds swap and spacing pressure to eligible fleets. |

Color-clear locks and editor spawn overrides can further filter this pool. Those
filters are temporary state, not level progression.

## Scoring Changes

Enemy base points rise by ten per level:

```text
enemy base points = 100 + 10 * (level - 1)
```

Death-cascade digits, magic-shot chain peaks and combo streaks multiply enemy
points after this level value is chosen. Defeat counts still count actual
enemies, not multiplied score weight.

Fleet-clear bonus rises linearly by level:

```text
fleet-clear bonus = 10000 * level
```

The fleet-clear bonus is not multiplied by death digits, magic-shot chain peaks
or combo streaks.

## Movement Changes

Fleet base speed scales by level:

```text
fleet speed multiplier = min(2, 1 + 0.03 * (level - 1))
```

Default fleet movement is driven by live Green enemies:

```text
fleet speed = 0.03 * live Green count * fleet speed multiplier
```

That means a Red/Blue-only level-one fleet does not move horizontally until a
Green exists. The multiplier reaches its cap at level 35.

## Cooldown Changes

Ability cooldown ranges are defined in `CombatBalance.CooldownRange`. The
current source ranges are multiplied by 0.75, then each level above one subtracts
0.25 seconds from the maximum. The maximum never goes below the minimum plus
three seconds.

| Enemy type | Level 1 range | Level 2 range | Level 3 range | Level 4 range | Level 5 range | Level 6 range | Cap |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Red | 2.25-7.50s | 2.25-7.25s | 2.25-7.00s | 2.25-6.75s | 2.25-6.50s | 2.25-6.25s | 2.25-5.25s at level 10 |
| Blue | 2.25-7.50s | 2.25-7.25s | 2.25-7.00s | 2.25-6.75s | 2.25-6.50s | 2.25-6.25s | 2.25-5.25s at level 10 |
| Green dash | 6.00-15.00s | 6.00-14.75s | 6.00-14.50s | 6.00-14.25s | 6.00-14.00s | 6.00-13.75s | 6.00-9.00s at level 25 |
| Purple | 6.00-9.00s | 6.00-9.00s | 6.00-9.00s | 6.00-9.00s | 6.00-9.00s | 6.00-9.00s | Already at cap |
| Yellow | 1.50-4.50s | 1.50-4.50s | 1.50-4.50s | 1.50-4.50s | 1.50-4.50s | 1.50-4.50s | Already at cap |

Existing countdowns are not shortened when the level changes. New cooldown rolls
use the current level.

## Visual Changes

The background has two separate progression cues:

- when a new fleet wave arrives, the current star field briefly brightens and scales up,
- when the level changes, the game cross-fades to the next generated starfield,
- the starfield set contains ten variants and cycles after level 10,
- each level increases spin speed,
- every level change reverses spin direction.

The level-progress bar continues to show progress toward the next defeated-enemy
threshold. Color-clear blocks at the bottom are not level thresholds; they track
the current fleet's color-clear streak.

## Things That Do Not Change By Level

These systems can affect difficulty, scoring or presentation, but they are not
level-progression rules:

- tier-two promotion, which is driven by same-color group size,
- combo streak count, which is driven by consecutive non-missing shots,
- magic charges, which are driven by last-of-color clears,
- color-clear spawn locks, which are driven by bottom color blocks,
- leaderboard entries, which are recorded at game over,
- the editor-only spawn override, which is a development control.
