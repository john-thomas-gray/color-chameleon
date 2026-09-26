# Candy Cruisers V2.0.0

Unity rebuild in the original `john-thomas-gray/color-chameleon` repository.
The previous version remains available at the `legacy-v1` tag and in the local
`/Users/johngray/swe/candy-cruisers-v1-archive` checkout. The active project is
`/Users/johngray/swe/candy-cruisers`. See [Changelog](CHANGELOG.md).

New five-enemy rows contain at most three distinct colors. Each color appears
in one adjacent run, with randomized run sizes and color order. This applies
to opening fleets, new top rows, and refill batches; Purple summons and Yellow
transformations keep their existing behavior.

Open this folder with Unity 6000.3.24f1, open `Assets/Scenes/Gameplay.unity`, and press Play.

For spawn testing, open **Candy Cruisers > Spawn Overrides**. Enable **Override level eligibility**, then check the enemy types you want. These selections replace level unlocks for the next opening fleet, future rows, refill batches, and Purple summons; they do not change existing enemies or Yellow imitation. At least one type must remain selected. Turn the override off to restore normal unlocks. Settings survive Play mode and scene restarts within the current editor session, and the control is excluded from player builds.

The gameplay scene starts empty against the original star background. After a one-second opening beat, ten enemies arrive in two rows of five, using Red/Blue unless a development override is active. Every arriving enemy fades in through a color-matched electric rift over 0.8 seconds, including new rows, refill batches, and Purple summons. Blue shields fade in with their owners. The formation reverses and descends only when its leading occupied column touches the playfield border. Empty edge columns allow more travel. Select **Enemy Grid** to adjust **Speed**; fixed Travel Distance is no longer used. The logical grid tracks fifty cells, unique enemy identities, occupied neighbors, and available colors.

Move with Left/Right arrows or A/D; fire with Space. On touch screens, drag horizontally to move and release a short tap to fire. Mouse dragging/clicking provides the same gesture controls for desktop testing. Movement wraps at the playfield edges; pointer movement takes the shortest wrapped route. The player's body shows the shot color. One tongue can be active at a time, and it follows the player while extending and retracting. The next color is chosen uniformly from colors still present; an empty grid disables firing.

The extending tongue passes through mismatched enemies. Its first matching hit clears that enemy and its connected same-color group through horizontal and vertical neighbors; diagonal neighbors do not connect. It then retracts from the hit position, without clearing more enemies on return. Hit detection checks the tip's full vertical travel each frame against current sprite bounds.

At each occupied-column edge contact, survivors descend one cell and five random level-eligible enemies spawn in the top row. With all five columns occupied, no Green bursts, and default settings, the first contact occurs after 3.75 seconds of fleet movement and subsequent contacts every 7.5 seconds; clearing outer columns delays contact. Columns and the grid origin are relabeled together when necessary so survivors do not jump sideways and a full new row stays inside the border. Attempting to descend with an occupied bottom row ends the game without overwriting enemies. Movement, retreat, player input, enemy abilities, and missiles stop, and a Game Over overlay offers Restart. Press R to restart at any time, or use the button after game over. Restart resets player state and timers, repeats the empty opening pause, then phases in the 10-enemy formation.

See [Occupied-column movement](docs/OCCUPIED_COLUMN_MOVEMENT.md) for the geometry, update sequence, row-placement strategy, and verification details.

Clearing the entire fleet pauses the fleet for one second, removes old missiles, resets the fleet's position and sweep timer, and spawns a fresh batch: 18 enemies at level 1, 24 at levels 2-3, 30 at levels 4-6, and 36 thereafter. Player movement and edge wrapping continue throughout this pause, including an ongoing touch or mouse drag. Firing waits for enemies to return. Select **Enemy Grid > Game Session** to adjust the refill delay.

The score and level display includes a level-progress bar and brief score/unlock feedback. Each defeated enemy awards `100 + 10 * (level - 1)` points; a cleared fleet adds `10000 * level`. Cumulative defeat thresholds are 18, 54, 108, 180, 270, and so on. Red/Blue are available from level 1, Green at 2, Purple at 4, and Yellow at 6. These gates apply to rows, refills, and summons. Game over shows final score; restart resets progression. Combo and magic multipliers remain pending.

Each Green flashes and gives the fleet an individual movement burst with green speed wakes. Purple preserves the original fleet-wide gap spawning, with a brilliant flash and electric rift around the incoming enemy. Ordinary Yellow permanently copies a random non-Yellow orthogonal neighbor; a tendril and gradual visual morph decorate the conversion without delaying or changing its gameplay. See [Progression and abilities](docs/PROGRESSION_AND_ABILITIES.md) for exact timing and replacement contracts.

A subtle border marks the playfield, with its side edges aligned to the player's horizontal wrap boundaries. It stays within the camera view on portrait and landscape screens.

Descent checks cover sparse formations, identity and color preservation, exactly one row per turnaround, long-frame boundary crossings, and full-grid blocking. The full suite also checks exactly-once fleet clears, batch sizing, fresh sweep timing, and a complete Play mode sequence through refill, game over, and scene restart.

Matching checks cover cyclic groups, isolated and diagonal enemies, full-fleet clears, nearest matching hits behind mismatched enemies, moving formations, and frame-rate independence. Play mode also verifies a matching chain clear and subsequent firing.

Red enemies fire straight downward missiles on independent 6-18-second starting cooldowns, with a bright flash in the final 0.6 seconds before firing. Missiles move independently of the formation and expire offscreen. A hit hides and disables the player for 1.5 seconds, cancels the tongue, then respawns the player with another 1.5 seconds of flashing protection. There is no lives limit yet.

Blue enemies start with a downward-facing shield arc. A mismatched tongue is absorbed by the nearest active shield and retracts; its starting recharge range is 35-75 seconds. A Blue tongue bypasses the shield and can clear the Blue enemy normally. While the shield is down, mismatched tongues pass through. New rows receive the same abilities, and defeated enemies stop using them. Special homing missiles and deflecting shields remain pending.

Purple cooldowns start at 18-50 seconds, Yellow at 18-40, and Green remains at 8-20 with 0.1-unit dashes. Every level adds 3% of the initial fleet speed (up to twice that speed), and reduces maximum ability cooldowns by 0.25 seconds, preserving a range of at least four seconds above each unchanged minimum. Existing countdowns are not shortened mid-cycle.

Clearing the last enemy of a color grants a magic shot, with up to two charges stored. The player flashes through all five colors when magic is ready or firing; the level-progress fill follows the player's displayed color. The next shot is a rainbow piercing tongue that hits any color, bypasses shields, and clears each hit enemy's same-color chain. It continues to full reach unless it clears the fleet, then visibly retracts. Misses spend the charge; blocked fire commands do not. Getting hit, clearing the fleet, or restarting resets magic, as in the original. Combo/magic multipliers and the color wheel remain pending.

Cleared colors cannot appear in new rows or Purple summons until the whole fleet is cleared and the next batch begins. Colored segments at the bottom of the playfield appear when you destroy a color; uncleared slots stay empty. Each slot occupies 1/N of the full progress-track width, where N is the number of distinct colors actually seen in the current fleet, including colors already cleared. Unlocks and override selections alone do not add slots. Seen history resets with each new batch. Override changes cannot bypass color locks. If only locked colors are selected while enemies remain, the fleet still descends but adds no new row.

Magic shots are twice as wide and extend/retract twice as fast as normal shots. On the final enemy hit, both kinds of tongue visibly retract instead of disappearing. The refill waits for the tongue to return. The player then switches directly to a color guaranteed to appear in the already-planned next batch, without a grey state. When multiple colors will spawn, a different color is preferred. Override edits during the pause affect later spawns, not that reserved batch.

Missile art is replaceable in `Assets/Prefabs/Red Missile.prefab`; shield art is assigned on **Enemy Grid**. The full checks cover shield hit ordering, recharge, matching bypass, projectile cleanup, swept player hits, cancelled shots, respawning, and protection expiry, including live Play mode behavior.

Disconnected enemies retreat upward as groups, one cell every 0.15 seconds, until they reconnect to the fleet anchored at the top row. Horizontal and vertical links count across all colors; diagonals do not. Groups keep their shape and are not destroyed by retreat. If the top row is empty, survivors rise to it. Adjust **Retreat Step Seconds** on **Enemy Grid** to change the pace. Logical cells and visuals move together, so matching and descent use the updated positions. Automated checks cover connectivity, shape preservation, color counts, timing, and reconnection after a connecting enemy is cleared in Play mode.

Art is reused from the original color-chameleon project. Replace Sprite Renderer sprites on the five color-named enemy prefabs in `Assets/Prefabs` to update enemy art independently of movement. Ability cues are isolated in `EnemyPresentation`. The background sprite is assigned on **Star Background**. Camera framing preserves the formation on portrait and landscape screens.

Run the editor checks from **Candy Cruisers > Run Checks** with Play mode stopped and scene changes saved. From the command line, launch Unity with `-batchmode -projectPath <project-folder> -executeMethod CandyCruisers.Editor.GameplayChecks.Run -quit -logFile <log-path>`. These checks cover grid occupancy, neighbors, color changes, wrapping, gesture classification, tongue locking/reset, movement timing, scene references, and camera framing. They render portrait, landscape, tall-phone, and tongue previews into `TestResults`.

The full suite also includes a separate Play mode check: launch Unity with `-batchmode -projectPath <project-folder> -executeMethod CandyCruisers.Editor.RuntimeGameplayChecks.Run -logFile <log-path>` (without `-quit`; it exits after testing). This verifies scene startup, enemy disable/enable registration, moving fleet, and tongue extension/return/refire during actual frame updates. Close the editor before running either batch command. Touch logic is covered by automated checks; physical-device feel still needs a phone playtest.
