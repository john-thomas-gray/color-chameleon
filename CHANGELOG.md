# Changelog

## V2.0.0

Unity rebuild of Candy Cruisers, continuing the history of color-chameleon.

- Five-column fleet with occupied-column boundary detection, descent and refill.
- Empty opening and color-matched electric-rift arrival animations.
- New rows use at most three colors, each in a single adjacent run. Opening and
  refill rows follow the same rule. Individual Purple summons and Yellow
  transformations retain their existing behavior.
- Red missile cooldowns start at 6-18 seconds, with level-based scaling.
- Score, level progression, unlock gates, color-clear rewards and magic shots.
- Development spawn overrides, modular placeholder visuals and gameplay checks.

The original version is preserved by the `legacy-v1` Git tag and in the sibling
`candy-cruisers-v1-archive` checkout. V2.0.0 is a source release of the ongoing
rebuild, not an App Store or Steam binary release.
