# Level map

Edit **`Assets/Resources/LevelMap.json`** in a text editor. This is the only
source for authored level progression. The former Level Progression window,
level ScriptableObjects, and Initial Wave Lines setting have been removed.

Save the file and start a new run. The Unity Editor reads the saved file again
at run start; an in-progress run keeps its loaded settings and reservations.
Desktop and iOS (iPhone Operating System) builds package the same file. Rebuild
an installed game to include file changes.

## Format

The outer `levels` object uses consecutive keys starting at `"1"`. Each key
identifies a level, so there is no redundant `levelNumber` setting. Object key
order does not matter. Levels beyond the last entry reuse the last entry's
settings, including its defeat target; their spawn cursors still reset.

```json
{
  "schemaVersion": 1,
  "levels": {
    "1": {
      "enemiesToDefeat": 20,
      "numberOfColors": 2,
      "possibleColors": ["Red", "Blue"],
      "requiredColors": [],
      "backgroundImage": "",
      "possibleSoundtrackTracks": [],
      "fleetSpawns": [
        "0r0r0r",
        "brbrbr",
        "0r0r0r/b0b0b0",
        "rbrbrb"
      ],
      "lineSpawns": ["0r0r0r", "brbrbr"]
    }
  }
}
```

| Field | Meaning |
| --- | --- |
| `enemiesToDefeat` | Additional enemy defeats needed to earn the next level; at least 1. Advancement waits for the current fleet to clear. |
| `numberOfColors` | Size of the active palette selected for a round. The actual opening can contain fewer colors when the script deliberately introduces others later. |
| `possibleColors` | Pool for palette selection and numbered color groups. Use full color names: Red, Blue, Green, Purple, Yellow, Orange. |
| `requiredColors` | Colors that must actually appear in every fresh fleet; also included in possibleColors and the palette count. |
| `backgroundImage` | Sprite resource path relative to `Assets/Resources`, without an extension; empty or null means black. |
| `possibleSoundtrackTracks` | Names from Gameplay Music Player's soundtrack registry, without extensions. An empty list permits the full soundtrack. |
| `fleetSpawns` | Ordered list of complete fleet patterns; must contain at least one nonempty fleet. |
| `lineSpawns` | Ordered list of incoming top-row patterns. An empty list adds no enemies on descent; existing enemies still descend normally. |

`schemaVersion` is currently 1. Unknown settings, missing required fields,
nonconsecutive level keys, duplicate keys, invalid colors, and impossible spawn
patterns are reported as errors. Invalid maps block Play from starting a run
and also block player builds; they do not silently select procedural defaults.

## Spawn characters and positions

| Character | Meaning |
| --- | --- |
| `r`, `b`, `g`, `p`, `y`, `o` | Red, Blue, Green, Purple, Yellow, Orange; uppercase also works. |
| `0` | Empty cell; does not count as an enemy or player color. |
| `1` through `6` | Random color identifiers; different digits bind to different colors. |
| `/` | Next row down, starting at the leftmost column; only valid in fleetSpawns. |

Each string describes grid positions from left to right. For example,
`"0r0r0r/b0b0b0"` places three Reds in the top row's second, fourth and sixth
positions and three Blues in the next row's first, third and fifth positions.
Blanks remain real gaps rather than shifting the remaining enemies together.

Rows may have 1–6 positions, and fleets may have 1–11 rows. All patterns within
one level must use the same width. Early levels can use six positions with only
three enemies; there is no level-number gate on authored widths. `/` controls
row count independently for each fleet. Completely blank incoming lines are
valid; completely blank fleets are not.

Patterns are literal layouts. They are not reordered or regrouped by the old
procedural row generator. To limit matching runs, write those limits into the
patterns, for example `11221`, `22112`, `21122`, `12211`, `12112`, `21221`.

## Round-wide randomness

A round starts when a fresh fleet is planned and ends when that fleet is cleared.
Its active palette and all numbered assignments are chosen once and captured in
an immutable reservation. Every numbered character in that fleet and every
incoming line uses those assignments, including lines that wrap to the start of
the list. A new round rolls fresh assignments, even if its fleet list loops.
A reroll can legitimately choose the same colors as the previous round.

For `312213`, if `1 = Red`, `2 = Blue`, and `3 = Green`, the resolved row is
`grbbrg`. A later `123123` row in that round resolves to `rbgrbg`.

The palette includes required colors and every literal color used by the
current fleet or its incoming-line list. Remaining slots are randomly selected
from possibleColors. Digits bind to distinct colors within that palette. A
literal can match a numbered group's color. A map is invalid if it asks for
more distinct numbered groups or mandatory literal colors than the palette
allows, or if required colors cannot appear in a fleet.

Orange remains limited to one live enemy. Validation chooses digit bindings
that respect that limit. Incoming Orange cells remain blank while an Orange is
already alive. A cleared color stays locked for the current clear streak:
later script cells for that color remain blank, including numbered cells.
Assignments never change to refill those gaps. Purple summons use the same
captured active palette and cleared-color locks.

## Two independent sequences

Each level has separate fleet and incoming-line cursors, both initially zero.

- A fresh fleet consumes one fleetSpawns entry.
- A successful descent consumes one lineSpawns entry when that list is nonempty.
- Each cursor wraps back to zero at the end of its own list.
- Clearing a fleet rerolls numbered colors but does not reset the line cursor.
- Entering a different level or starting/restarting a run resets both cursors.
- Pause and failed descents do not consume line entries.

The saved map includes introductory literal fleets followed by numbered
arrangements. Existing defeat targets and color pools were migrated from the
previous level assets. Each level has enough scripted fleet enemies to reach
its defeat target before needing to wrap in ordinary play; incoming lines and
summons can advance the level sooner.

## Progress, presentation and shared state

Defeat targets accumulate across the run. Targets of 20 and 40 mean level 3 is
earned after 60 total defeats. Excess defeats carry forward and can earn multiple
levels within one fleet. Score multipliers do not multiply defeated enemies.
Starting from a later level uses the sum of earlier targets as its baseline.

Background images must be imported as sprites under `Assets/Resources`, for
example `Assets/Resources/Backgrounds/Nebula.png` is referenced as
`"Backgrounds/Nebula"`. Images retain their proportions and crop to cover the
screen on desktop and phone layouts. Missing sprite paths are reported as
errors. The main menu stays black.

Soundtrack names refer to the existing registry, such as `AnotherJoe` or
`Contact`. Missing local audio is skipped; no unrelated track substitutes for a
restricted pool. Audio recordings remain local-only resources.

`EnemyGrid.Fleet` remains the shared source of live colors, counts, cleared
rewards and future reservations. The active plan also owns the captured palette,
number bindings, row dimensions and incoming scripts. Player shot weights ignore
gaps and use the actual occupied cells' row depths.

Bottom bars describe colors that have participated in the current fleet, not
all possible colors in the level. Cleared colors retain their slots through the
clear celebration. A color first introduced by a later line joins the current
fleet's history. Incoming reservations cannot change outgoing bar slots; they
switch together when the next fleet spawns.

Run **Candy Cruisers > Run Level Map Checks** for focused validation, or
**Candy Cruisers > Run Checks** for the full gameplay suite. These checks cover
parsing, invalid maps, required colors, sparse layouts, numbered bindings,
independent loops, level/reset behavior, clear locks, defeat targets and shared
fleet state. Runtime checks cover desktop and phone layouts.
