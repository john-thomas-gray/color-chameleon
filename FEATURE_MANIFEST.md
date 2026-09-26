# Candy Cruisers Feature Manifest

This document records what is present in the original project and what has been
discussed for the rebuild. It is an inventory for choosing implementation
order, not a claim that every original feature is complete or balanced.

## Rebuild Progress

This copy is the active rebuild checklist. Check items off as they are implemented
and verified. The original-game inventory below describes the old game, not the
completion status of the rebuild.

### Completed

- [x] Create the Unity 6000.3.24f1 rebuild project.
- [x] Create the standalone gameplay scene and make it the build entry scene.
- [x] Display the original star background (static for this first slice).
- [x] Spawn ten opening enemies in two rows of five after an empty opening pause.
- [x] Move the formation horizontally at adjustable speed; turn and descend
  only when the leading occupied column reaches a playfield edge. Empty edge
  columns extend travel. This replaces the original fixed sweep limits.
- [x] Reindex columns when needed before a new row spawns, preserving survivors'
  horizontal positions, identities, and connectivity while fitting the full row
  inside the playfield.
- [x] Store Red/Blue enemy visuals in replaceable sprite prefabs.
- [x] Keep the formation visible in portrait, tall-phone, and landscape views.
- [x] Add a subtle playfield border aligned with the player's horizontal wrap
  boundaries and visible in portrait and landscape views.
- [x] Keep player movement, wrapping, and ongoing pointer movement active
  between cleared fleets; only game over locks player controls.
- [x] Run the full initial gameplay check suite: movement bounds, frame-rate
  independence, scene references, framing, and rendered previews.

The opening formation uses Red and Blue unless a development override is active. Level-based spawning now gates
all rows, batches, and Purple summons. Five sprite prefabs and a separate
ability-presentation component support replaceable art; full animation coverage
is still pending. The scene now uses a fifty-cell
occupancy model, player controls, and a single active extend/retract tongue.
Matching shots now clear orthogonally connected same-color enemies and retract
at the first matching hit. Mismatched enemies allow the tongue to pass through.
Cleared cells, scene objects, and available-color counts update together.

- [x] Disconnected groups back up one row every 0.15 seconds until reconnected
  to the top-anchored fleet. Connections use horizontal/vertical neighbors of
  any color. Groups retain their shape, identities, colors, and enemy counts.
  If no top-row enemies remain, groups rise until they reach the top row.
  This replaces the original game's removal of disconnected groups.
- [x] Verify reconnection in the full editor suite and in Play mode after
  clearing a connecting enemy.

### Recommended Next Steps

This order is a recommendation, pending the user's implementation choices.

1. [x] Implement the five-column, ten-row logical grid with reliable cell
   occupancy, enemy identity, color counts, and neighbor queries.
2. [x] Add player movement and edge wrapping, with keyboard and touch controls.
3. [x] Add the extend/retract tongue, one active shot at a time, and shot colors
   selected from enemies currently present.
4. [x] Implement matching hits and orthogonally connected same-color chains,
   including reliable removal from the grid and color counts.
5. [ ] Establish separate visual children and animation event hooks; add
   replaceable placeholder cues for firing, matching, and enemy defeat.
6. [x] Implement ordinary Red missiles and Blue shields, with warning cues,
   player hits, respawning, and temporary invulnerability.
7. [x] Complete the basic wave loop: descent, new rows, game over, fleet refill,
   position reset, and restart.
8. [x] Add score and level progression with visible feedback, plus the exact
   unlock schedule: Red/Blue at level 1, Green at 2, Purple at 4, Yellow at 6.
   Implement the ordinary abilities of newly unlocked types with warning cues.
9. [ ] Author reusable five-enemy line configurations with level eligibility
   and a simple way to preview each formation.
10. [ ] Add field-aware spawn selection using live enemy counts and active
    threats, then playtest and tune limits on overwhelming combinations.

### Step 7 Progress

Prioritized ahead of steps 5 and 6 at the user's request.

- [x] Shift surviving enemies down one cell at each occupied-column edge contact.
- [x] Add five randomly selected Red/Blue enemies in the new top row.
- [x] Preserve enemy identities, gaps, colors, and logical/visual alignment.
- [x] Block descent and spawning when the bottom row is occupied, without
  overwriting enemies, and end the run at that attempted descent.
- [x] Verify both turnarounds, long frames, full-grid blocking, and spawning
  with the complete editor and Play mode check suites.
- [x] Turn the bottom-boundary condition into game over; stop movement,
  retreat, firing, enemy abilities, and missiles, and show a Restart button.
- [x] Refill cleared fleets after a one-second pause with a separate 15-enemy
  Red/Blue batch; reset fleet position and sweep timing and clear old missiles.
- [x] Add restart through the game-over button or R key. Reload the scene to
  restore the opening 10 enemies, player state, abilities, and timers.
- [x] Verify the complete refill, game-over, and restart sequence in Play mode.

Step 8 now scales refills to 15/20/25/30 enemies by level and gates their colors.
Authored lines and field-aware fairness remain pending.

### Step 8 Progress

- [x] Award points once per defeated enemy, plus the level-scaled fleet bonus.
- [x] Advance through cumulative defeat thresholds, including multiple levels
  in one clear; show score, level, progress, score feedback, and unlock notices.
- [x] Gate new rows, refill batches, and Purple summons: Red/Blue at 1, Green
  at 2, Purple at 4, Yellow at 6. Restart resets score and level.
- [x] Give each Green its own warning flash and physical fleet step with green
  speed wakes; bursts obey occupied-column contacts, descent, and game over.
- [x] Rebalance Green: shorten each dash to 0.1 world units (one third of its
  initial length) and independently resample 8-20-second cooldowns at spawn
  and each activation for fewer, more widely staggered bursts.
- [x] Preserve ordinary Purple's fleet-wide gap spawning, with a brilliant
  flash and electric phase-in. Cooldowns are rebalanced below.
- [x] Preserve ordinary Yellow's random neighboring-type adoption.
  Add a growing tendril and gradual visual morph
  without delaying the gameplay conversion or making it reversible.
- [x] Keep presentation separate from gameplay timers and state changes so
  hand-drawn effects can replace the placeholders.
- [x] Verify the full editor and Play mode suites, including scoring, unlocks,
  Green edge contacts, Yellow permanence, and Purple capacity/eligibility.

See `docs/PROGRESSION_AND_ABILITIES.md` for exact rules, visual timing, tests,
and the presentation replacement boundary. Combo/magic multipliers and special
enemy variants are not included in this milestone.

### Development Controls

- [x] Add an editor-only spawn override window with individual enemy-type
  toggles. Override level eligibility for future rows, refill batches, and
  Purple summons; require at least one type. Preserve selections through Play
  mode and scene restarts, without changing existing enemies or shipping the
  control in player builds.

### Combat Balance and Magic

- [x] Replace full-ring shields with lower arcs facing the player, retaining
  ordinary shield blocking and matching-color bypass.
- [x] Spread out abilities with independent cooldown ranges: Red 6-18 seconds,
  Blue recharge 35-75, Green 8-20, Purple 18-50, Yellow 18-40.
- [x] Increase base fleet speed by 3% of its starting speed per level, capped
  at twice that speed. Reduce maximum cooldowns by 0.25 seconds per level,
  preserving unchanged minimums and at least four seconds of variation.
- [x] Grant a magic charge when the last enemy of a color is cleared; cap at
  two, show readiness, and make the next shot pierce all colors and shields.
  Preserve same-color chain clearing; reset magic on hit, fleet clear, and restart.
- [x] Verify full editor and Play mode suites plus rendered shield/magic previews.

### Color-Cycle Feedback

- [x] Match the level-progress fill to the player's displayed color; flash the
  player through all five colors while magic is ready or a magic shot is active.
- [x] Exclude cleared colors from rows and Purple summons until the entire
  fleet is cleared and a new batch begins. Preserve locks through level changes,
  refill delay, and override edits; reset them for a new batch or restart.
- [x] Show earned colored segments at the bottom of the playfield, each using 1/N of the
  full track where N is the distinct color count actually seen in the current fleet,
  including cleared colors. Unlocks and override selections alone do not add slots.
  Draw a segment only after its color is destroyed, without changing the denominator.
- [x] Make magic twice as wide and twice as fast in both directions.
- [x] Retract the tongue visibly after the last enemy dies; wait for its return
  before refilling. Switch straight to a color guaranteed in the planned batch,
  preferring a different color when available, with no grey state.
- [x] Verify editor and Play mode regressions and a rendered feedback preview.

### Spawn Presentation

- [x] Start and restart with an empty field, then spawn the planned two-row
  opening fleet after a one-second pause; keep player movement available.
- [x] Use the shared electric-rift phase-in for opening fleets, new rows,
  refill batches, and Purple summons, tinted to the arriving enemy's color.
- [x] Begin sprites fully transparent and fade Blue shields with their owners.
- [x] Verify full editor and Play mode suites plus empty-opening and spawn-effect renders.

### Five-Column Fleet And Red Timing

- [x] Reduce Red's level-one cooldown to 6-18 seconds (12 seconds on average,
  down from 16), retaining independent random timing and level scaling.
- [x] Use five centered columns with unchanged cell spacing and playfield width:
  ten opening enemies, five enemies per new row, and fifty-cell capacity.
- [x] Update refill sizes, sparse-column boundary checks, summon fixtures, and
  documentation; verify full editor and Play mode suites and rendered layouts.

### V2.0.0 Row Composition And Migration

- [x] Cap each newly generated row at three distinct colors, with each color
  occupying a single contiguous run. Apply to openings, refills, and new top rows.
- [x] Preserve unlocks, development overrides, and cleared-color exclusions.
  Individual Purple summons and Yellow transformations retain existing rules.
- [x] Verify full editor and Play mode suites for the grouped formations.
- [x] Move the Unity project to `/Users/johngray/swe/candy-cruisers`, preserve
  the old checkout and history, and publish the V2.0.0 source release.

### Later Features Still Pending

- [ ] Special enemy states and abilities, including clear activation cues.
- [ ] Combo/magic multipliers and color-wheel behavior (magic shot implemented).
- [ ] Main menu and pause/resume (game over, final score, and restart are implemented).
- [ ] Background motion and level-driven transitions.
- [ ] Full animation coverage and documented final-art replacement contract.
- [ ] Leaderboard design and implementation, if retained.
- [ ] App Store and web release preparation; Steam afterward.

Items above summarize milestones. The inventory below remains the detailed
reference for individual rules and original limitations.

## Status Key

- **Original: implemented** — behavior is present in the original Unity project.
- **Original: partial** — assets or code exist, but the feature is incomplete,
  inconsistent, or not fully connected to the interface.
- **Original: development-only** — test scene, cheat, or debugging behavior.
- **Original: not found / not included** — a capability or build artifact
  searched for but absent from the inspected repository.
- **Rebuild: confirmed** — a direction stated by the user in this conversation.
- **Rebuild: considered** — an idea raised for consideration, not yet a final
  requirement.
- **Rebuild: proposed structure** — an implementation approach discussed here;
  details remain open.

## Original Game

### Launch, Menu, and Scenes

- **Original: implemented** — main menu with a title, Play, and Quit. The menu
  title is **Color Chameleon**, while the repository and README use
  **Candy Cruisers**.
- **Original: partial** — a Leaderboard button is displayed, but it has no
  click behavior. The gameplay game-over leaderboard control is also unwired.
- **Original: implemented** — Play opens the gameplay scene.
- **Original: implemented** — gameplay scene contains the playfield, HUD,
  pause overlay, and game-over overlay.
- **Original: implemented** — pause and game-over flows offer resume/restart
  and return-to-menu actions.
- **Original: development-only** — RayCastTest and TestAnimation scenes are
  present but excluded from the normal build scene list.

### Player Controls and Survival

- **Original: implemented** — left and right arrow keys move the player.
- **Original: implemented** — horizontal movement wraps across the left and
  right edges of the playfield.
- **Original: implemented** — Space fires a single extend-and-retract tongue;
  the next shot waits for the previous tongue to reset.
- **Original: implemented** — touch input moves the player toward a touch
  target and supports a touch gesture for firing.
- **Original: partial** — touch movement attempts to wrap by moving through
  the nearer screen edge; touch bounds include a hard-coded screen-coordinate
  cutoff.
- **Original: implemented** — enemy missiles can hit the player.
- **Original: implemented** — a hit temporarily disables the player, then
  respawns them after about 1.5 seconds with flashing/invulnerability ending
  after about 3 seconds.
- **Original: implemented** — the player can repeatedly respawn; no finite
  lives counter was found.
- **Original: partial** — the README says P pauses, while the game uses Q for
  pause. P is a developer shortcut for forcing a purple enemy row.

### Color Matching and Enemy Chain Rules

- **Original: implemented** — five enemy colors are used: Red, Blue, Green,
  Purple, and Yellow.
- **Original: implemented** — the player's next shot color is selected from
  colors currently represented among enemies; if none are counted, a random
  color is used. Selection is uniform across available color types, not
  weighted by the number of enemies of each type (despite a source comment
  describing weighting).
- **Original: implemented** — a normal tongue kills enemies matching its
  current color. A mismatched shot can continue through the formation.
- **Original: implemented** — killing an enemy propagates to connected
  orthogonal neighbors of the same color.
- **Original: implemented** — an enemy with at least two adjacent enemies of
  its color becomes Special. The special visual uses a shared special sprite.
- **Original: implemented** — clearing the last enemy of a color grants a
  temporary magic/multicolor tongue and marks that color on the wheel.
- **Original: implemented** — magic can hit enemies regardless of their
  color; the stored magic value is capped at two.
- **Original: implemented** — successive successful shots increase a combo
  multiplier; a shot without an enemy hit resets it.
- **Original: implemented** — using magic contributes to a separate magic
  multiplier. At a threshold of five, the color wheel performs a spin.
- **Original: partial** — the wheel spins through three rotations and removes
  or deactivates wedges as magic builds; inspected code showed presentation
  feedback, but no gameplay reward or effect from the spin.
- **Original: partial** — color counts drive shot selection and some enemy
  abilities, but comments in the code acknowledge count drift during
  imitation and warping.

### Enemy Types and Abilities

- **Original: implemented** — Red enemies periodically fire straight missiles;
  Special Red enemies fire homing missiles.
- **Original: implemented** — Blue enemies generate a shield. A Special Blue
  enemy's shield can deflect a mismatched tongue; ordinary shields absorb a
  mismatched tongue and later reactivate.
- **Original: implemented** — Green enemies increase fleet movement speed;
  Special Green enemies add an additional speed contribution.
- **Original: implemented** — Purple enemies periodically warp another enemy
  into a nearby empty cell.
- **Original: partial** — Yellow enemies inspect nearby enemies and can adopt
  a neighbor's color. Special Yellow enemies attempt a disguise, but the
  comments describe copying abilities that are not fully reflected by the
  current ability dispatch.
- **Original: implemented** — ability cooldowns generally shorten as the
  global level rises.
- **Original: implemented** — approximate base cooldown ranges are Red
  missiles 4-8 seconds, Blue shields 25-45 seconds, Purple warps 10-30
  seconds, and Yellow imitation 10-18 seconds; level scaling modifies these
  timings.

### Formation, Waves, and Difficulty

- **Original: implemented** — the playfield is represented by 60 cells in a
  six-column by ten-row grid.
- **Original: implemented** — the opening formation contains 12 enemies.
- **Original: implemented** — the formation moves horizontally in a sweep and
  descends one row at each end of the sweep.
- **Original: implemented** — each descent adds six newly randomized enemies
  to the top row.
- **Original: implemented** — if enemies reach the bottom row when the fleet
  descends, the game ends.
- **Original: implemented** — clearing the formation awards a fleet-wipe
  bonus, resets the fleet position, and starts another batch. Batch size
  increases with level: 18 at level 1, 24 at levels 2-3, 30 at levels 4-6,
  and 36 from level 7 onward.
- **Original: implemented** — global level rises as cumulative enemy
  destruction reaches progressively larger thresholds: 18 for level 1, 54
  for level 2, 108 for level 3, 180 for level 4, and so on.
- **Original: implemented** — disconnected orthogonal groups that no longer
  connect to the top row are removed by the retreat check.
- **Original: partial** — any color/type can currently be selected for a new
  enemy; there is no level-based type unlock schedule or field-aware fairness
  director.
- **Original: partial** — new enemies are chosen individually at random; there
  are no authored six-enemy line patterns or constraints on combinations.

### Scoring and On-Screen Feedback

- **Original: implemented** — the apparent score award per enemy is
  `(100 + 10 * (level - 1)) * chainMultiplier * comboMultiplier *
  magicMultiplier`, with chain depth contributing to its multiplier.
- **Original: implemented** — a full fleet clear awards an additional
  level-scaled bonus.
- **Original: implemented** — small score/multiplier text appears near
  defeated enemies.
- **Original: implemented** — the gameplay HUD displays score, level, combo,
  and magic multiplier.
- **Original: partial** — the connected-match scoring traversal appears to
  increment score and enemy-destruction progression more than once for
  non-root enemies in a chain: once when discovered and again when dequeued.

### Menus and Persistence

- **Original: implemented** — pause menu stops game time and can resume.
- **Original: implemented** — restart resets score, level, multipliers, and
  reloads gameplay.
- **Original: implemented** — game-over menu shows the final score and offers
  restart/menu navigation.
- **Original: partial** — a Node.js/Express service stores scores in SQLite
  and exposes sorted score retrieval and score submission routes.
- **Original: partial** — Unity submits a score at game over to a hard-coded
  local address and always uses the player name “Suerte.”
- **Original: partial** — no in-game score retrieval/display, player-name
  entry, or working leaderboard-button action was found.

### Visuals, Animation, and Audio

- **Original: implemented** — a rotating galaxy backdrop is used in the menu
  and gameplay; gameplay also moves it vertically.
- **Original: implemented** — background art can change and fade in by level.
- **Original: implemented** — basic squash/stretch animation is used for
  selected actions, and the color wheel has a spin animation.
- **Original: implemented** — the player blinks during respawn protection;
  score popups and shield growth/shrink effects provide additional feedback.
- **Original: partial** — special states mostly swap sprites rather than
  giving each enemy a full animation language.
- **Original: partial** — color wedges and HUD text communicate some state,
  but there is no complete set of visual telegraphs for enemy abilities and
  matching rules.
- **Original: not found** — no in-game sound playback code or sound assets
  were found during inspection.

### Developer and Test Behavior

- **Original: development-only** — C logs enemy color counts.
- **Original: development-only** — D forces a descent; R, Y, B, G, and P force
  a descent with a particular enemy color; L forces a level increase.
- **Original: development-only** — Q pauses and also triggers a Red missile
  test when a Red enemy is present; X spins the wheel; T logs magic value;
  Z advances the background image.
- **Original: development-only** — a standalone raycast test script and two
  non-build test scenes are included.
- **Original: development-only** — `LaserController` is fully commented out
  and is not active gameplay.
- **Original: not found** — no automated project tests were found. The server
  test command is a placeholder that exits with an error.
- **Original: not included** — no playable desktop build was found in the
  repository.

## Rebuild Goals Discussed in This Chat

### Engine and Release Targets

- **Rebuild: confirmed** — continue with Unity.
- **Rebuild: confirmed** — release on the Apple App Store and the web, with
  Steam as a later target.
- **Rebuild: confirmed** — give the game a substantially better visual design.

### Enemy Unlocks and Controlled Spawning

- **Rebuild: confirmed** — Level 1 can spawn only Red and Blue enemies.
- **Rebuild: confirmed** — Green enemies become available starting at Level 2.
- **Rebuild: confirmed** — Purple enemies become available starting at Level 4.
- **Rebuild: confirmed** — Yellow enemies become available starting at Level 6.
- **Rebuild: confirmed** — the spawn system should track enemies currently on
  screen and make fairer choices about what enters next.
- **Rebuild: considered** — use authored five-enemy “line” configurations so
  rows look intentional and less cluttered than five independent random picks.
- **Rebuild: proposed structure** — select a whole five-slot line from
  level-eligible patterns, then evaluate it against the live screen composition
  and threat mix before spawning it. Newly unlocked types need not appear in
  every line unless the design later calls for a guaranteed introduction.

### Modular Animation Placeholders and Rule Telegraphs

- **Rebuild: confirmed** — build animations and visuals that explain game rules
  and enemy behavior to players.
- **Rebuild: confirmed** — use temporary generated placeholders so the game
  communicates clearly before final hand-drawn art is ready.
- **Rebuild: confirmed** — allow a human artist to replace placeholder art
  without rewriting gameplay systems.
- **Rebuild: proposed structure** — separate gameplay/collision objects from
  replaceable visual children; drive animation from semantic game events such
  as spawn, ability warning/action, match, chain, Special activation, and
  defeat.
- **Rebuild: proposed structure** — keep animation clips, states, parameters,
  sprite bounds, pivots, timing, and effect attachment points under a stable
  art handoff contract. Do not let animation frames determine gameplay results.
- **Rebuild: proposed structure** — use shared animator logic with replaceable
  clips where appropriate, allowing new placeholder or hand-drawn clips to
  occupy the same animation slots.
- **Rebuild: open design work** — enumerate the exact cues and visual language
  for each rule and enemy ability. Color can support a cue, but shape and
  motion should also make it readable.

## Source Map

- Main menu: Candy Cruisers/Assets/Scenes/MainMenu.unity and
  Candy Cruisers/Assets/Scripts/MainMenu.cs
- Gameplay scene and HUD: Candy Cruisers/Assets/Scenes/Gameplay.unity and
  Candy Cruisers/Assets/Scripts/InGameMenues.cs
- Player and tongue: Candy Cruisers/Assets/Scripts/PlayerController.cs and
  Candy Cruisers/Assets/Scripts/Tongue.cs
- Grid, movement, waves, retreats, and score traversal:
  Candy Cruisers/Assets/Scripts/GridManager.cs
- Enemy behavior and color abilities: Candy Cruisers/Assets/Scripts/Enemy.cs
- Colors and score/progression: Candy Cruisers/Assets/Scripts/ColorManager.cs,
  Candy Cruisers/Assets/Scripts/ScoreManagerSO.cs, and
  Candy Cruisers/Assets/Scripts/LevelManagerSO.cs
- Leaderboard client and service:
  Candy Cruisers/Assets/Scripts/LeaderboardManager.cs and GameServer/index.js
- Project overview and documented controls:
  Candy Cruisers/README.md
