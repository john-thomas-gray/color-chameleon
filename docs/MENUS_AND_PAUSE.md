# Menus And Pause

The gameplay scene opens on the main menu over a solid black background. Play
or Enter starts the normal empty opening and phased-in fleet. No enemies,
missiles or player actions run behind the main menu. The menu also shows the
local top-five leaderboard when scores have been recorded.

Use P, Escape or the pause button in the upper-right playfield header to pause.
P, Escape or Resume continues the same run. Losing focus or backgrounding the
application pauses an active run; returning to it does not resume automatically.
The pause menu also offers Restart and Main Menu. Game over retains the final
score and Restart, shows the local leaderboard, and now also offers Main Menu.
R restarts a current run.

Buttons show their hotkeys in parentheses: Play (Enter), Resume (P / Enter),
Restart (R), and Main Menu (M). The game-over Restart button also accepts Enter.
Left and Right adjust the main menu's starting level, with shortcuts shown on
the minus and plus buttons. Typing one or two number keys directly enters the
starting level, capped at 99. Numeric-keypad Enter works wherever Enter does.
Main Menu and level-selection keys are active only in their corresponding menus;
Escape remains an alternate pause/resume shortcut.

Fatal fleet contact first enters a Dying state. Player controls, fleet,
abilities and missiles freeze, with no regular death animation triggered.
Music stops immediately at the fatal hit. The scene, rim, effects and score
fade to full black over two beats of the song's tempo captured at that hit;
the intact player stays visible above the blackout throughout the fade.
Only after the fade completes does the game-over death cue begin, replacing
the player with its detached flash and fragments. Its backdrop adds straight white radial
streaks, small curved shockwaves close to the player and a white impact flare.
At the fracture, colored shards from every enemy color unlocked at the current
run level disperse outward from the hit, even if that color is absent from the
fleet. The shards spin and fade beneath the player's death animation. The backdrop starts
after the two-beat fade, peaks 0.14 seconds into the death animation at the fracture,
and fades fully back to black by 0.62 seconds, before the options appear. Its origin follows the hit
position and its geometry adapts to portrait, landscape and the rim crunch;
it never changes gameplay time, camera framing or hitboxes.
A glass-shattering sound starts at the same 0.14-second marker as the player's
first fragments, with stereo echoes and a 2.1-second reverberant tail. The old descending
game-over cadence no longer plays. Ordinary recoverable deaths do not black
out the scene or stop the soundtrack.
Score submission waits for the fade and then the full death cue (another 0.9 seconds for the
placeholder). After a 0.2-second reveal delay, a centered game-over modal
fades in over 0.65 seconds against the black screen, showing the final score, level, leaderboard
and Restart/Main Menu options. Buttons, touch targets and keyboard actions stay
inactive until the modal is fully visible. The transition uses unscaled time;
pause cannot obscure or stall it. Compact screens show only the leaderboard rows
that fit above the options.

Pause is separate from the underlying Playing/Refilling state. It freezes
scaled time, locks player input, and suspends the fleet, spawning, abilities and
missiles. Tongue travel, death effects, cooldowns, recovery and refill delays
remain intact. Resuming restores the previous time scale and the correct
underlying suspension state. In particular, resuming a refill allows player
movement while keeping the fleet suspended until the new batch arrives.

Menu clicks are excluded from player gestures. Pause discards pointer movement;
starting or resuming also suppresses gameplay input through the following frame
to prevent menu activation from firing a shot.

Restart and Main Menu reload the gameplay scene, clearing the old run and its
effects. Restart bypasses the main menu and starts a new run. Returning to the
menu leaves the new scene idle until Play. Leaving or destroying a paused
session restores global time so the next scene cannot remain frozen.

`GameSession` owns menu state, navigation and drawing. `PlayerMovement` filters
menu gestures. `LocalLeaderboard` stores positive game-over scores in
`PlayerPrefs` and keeps the top five by score. `MenuChecks` covers pause state
and preserved timers; the full runtime suite also exercises menu startup,
returning from pause, real-frame tongue freezing, resume and restart from pause.
