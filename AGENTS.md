# AGENTS.md

## Candy Cruisers platform expectations

For every Unity implementation, treat desktop and iOS (iPhone Operating System) as supported targets.

When changing animations, layouts, camera behavior, input, timing, particles, shaders, or visual effects:
- Verify the result on both desktop and iOS.
- Account for aspect ratio, safe area, resolution, frame rate, touch input, and platform-specific performance.
- Prefer shared animation logic with platform-specific tuning isolated in configuration.
- Do not mark the task complete unless desktop and iOS behavior has been tested, or the reason it could not be tested is stated clearly.