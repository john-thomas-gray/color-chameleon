# Occupied-column movement

## Rule

Only an occupied column can trigger a turn and descent. Movement to the right
uses the rightmost occupied column; movement to the left uses the leftmost.
One enemy anywhere in a column is sufficient. Color, special state, and row do
not affect whether that column is occupied. Gaps between occupied columns do
not matter for the horizontal boundary calculation.

Contact uses the outer edge of the logical cell, not sprite pixels or shields.
This keeps movement stable when art changes or shield/animation visuals grow.
The current scene uses an unrotated grid with positive horizontal scale.

## Geometry

The playfield edges are x = -3 and x = +3, shared with player wrapping and the
visible border. Five columns are spaced 0.75 world units apart. Their local
centers are:

    localCenter(column) = (column - 2) * 0.75
    occupiedLeft  = gridX + localCenter(firstOccupied) - 0.375
    occupiedRight = gridX + localCenter(lastOccupied)  + 0.375

The implementation converts cell edges through the grid transform, rather than
assuming its origin remains fixed. At unit scale, a complete row occupies 3.75
units and reaches an edge when gridX is +1.125 or -1.125. At speed 0.3 units per
second, that means 3.75 seconds to the first right contact from center and 7.5
seconds between opposite contacts if both outer columns remain occupied.

If column 4 is empty, the rightmost possible occupied column becomes 3. It must
travel another 0.75 units before touching the right border. At the default
speed, this adds 2.5 seconds. Clearing the last enemy in column 3 adds another
column's worth of travel. These are geometric consequences, not timers.

## Movement update

`GridModel.OccupiedColumns` scans live enemy records to find the first and last
occupied columns. `EnemyGrid.OccupiedHorizontalBounds` converts their outer
edges into world coordinates. Neither operation caches occupancy, so removals
are reflected on the next movement update.

`EnemyGridMovement.Tick` spends a frame's travel distance, speed multiplied by
elapsed time, against the distance to the relevant border:

1. If the grid is empty, do not move, turn, or request descent.
2. If the frame cannot reach the border, move normally in the current direction.
3. Otherwise, move exactly to contact, reverse direction, and emit `SweepEnded`.
4. The existing spawner handles descent, new rows, or bottom-boundary game over.
5. Recompute bounds and spend any leftover travel against the new state.

Recomputing after the callback matters: spawning can expand the occupied range
and relabel columns. A single slow frame can cross multiple boundaries, but each
event corresponds to an actual occupied-edge contact. Disabling movement during
game over stops the loop immediately. Resetting the sweep increments a version
counter, preventing unused travel from leaking into a reset fleet. A stationary
fleet or a formation occupying the entire field width remains still.

This replaces both the old reflected-offset formula and its time-based turn
counter. Direction is now explicit state, and there is no Travel Distance
setting.

## Fitting a new row

A sparse fleet can travel beyond the position where a full five-column row fits.
Simply spawning columns 0-4 at that origin would put new enemies offscreen.
Snapping the origin back alone would make survivors jump sideways.

`EnemyGrid.AlignForNewRow` instead shifts column labels and the origin by equal
and opposite amounts. `GridModel.TryShiftColumns` validates every destination
before rebuilding its cell lookup. All surviving enemies receive the same
column shift, so their adjacency, identity, color, and relative shape survive.

Example: only columns 0-3 remain occupied when the fleet reaches the right wall.
The grid origin is x = 1.875. Before spawning:

- Shift all survivor column indices right by one.
- Move the grid origin left by 0.75 to x = 1.125.
- Rebuild each survivor's local position from its new column.

A survivor formerly in column 3 was at 1.875 + 0.75 = 2.625. After relabeling it
is in column 4 at 1.125 + 1.5 = 2.625. Its horizontal screen position is unchanged.
It then descends one row normally. The new row fills columns 0-4 entirely inside
the border. Left-edge contacts use the mirrored operation.

The spawner checks the bottom boundary before relabeling or descending, so game
over does not mutate the formation. Batch refill still restores the original
grid position and starts moving right.

## Verification

The editor suite covers clearing a leading column immediately before contact,
empty columns on both sides, occupancy in a lower row, empty fleets, zero speed,
30/60/120-frame-per-second simulation versus a long frame, callback-driven stop
and reset, and no-room-to-move protection. Spawn tests cover both directions and
verify survivor positions, identities, occupancy, and full-row bounds.

The Play mode suite additionally removes all but one occupied column, verifies
that the previous full-row boundary no longer causes a descent, and continues
to real contact. It checks exactly one new row, preserved survivor positions,
and all spawned cells inside the border. Existing matching, retreat, abilities,
refill, game-over, and restart checks also pass.
