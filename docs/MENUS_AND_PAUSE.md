# Menus And Pause

The gameplay scene opens on the main menu over the drifting star field. Play
or Enter starts the normal empty opening and phased-in fleet. No enemies,
missiles or player actions run behind the main menu. The menu also shows the
local top-five leaderboard when scores have been recorded.

Use P, Escape or the pause button in the upper-right playfield header to pause.
P, Escape or Resume continues the same run. Losing focus or backgrounding the
application pauses an active run; returning to it does not resume automatically.
The pause menu also offers Restart and Main Menu. Game over retains the final
score and Restart, shows the local leaderboard, and now also offers Main Menu.
R restarts a current run.

Fatal fleet contact first enters a short Dying state. Player controls, fleet,
abilities and missiles freeze while the player flashes and bursts into fragments.
The game-over overlay and score submission wait for the death cue to finish
(0.9 seconds for the placeholder). Pause cannot obscure this transition.

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
