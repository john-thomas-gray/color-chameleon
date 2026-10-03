# AGENTS.md

## Candy Cruisers platform expectations

For every Unity implementation, treat desktop and iOS (iPhone Operating System) as supported targets.

## Unity editor checks

- Run Unity Editor and Hub commands with escalated host access. Unity writes licensing state outside the repository, including under `~/Library/Application Support/Unity`; the restricted workspace sandbox can make that database read-only and leave a detached Licensing Client holding the editor interprocess communication (IPC) channel.
- If an editor command is interrupted, inspect the Licensing Client process and channel before retrying. Do not launch another Unity command while a stranded client owns the channel.
- Codex is permitted to close currently running Unity Editor, Hub, or Licensing Client sessions when they are blocking Codex from completing a requested task. Identify the blocking process first and close only the minimum Unity processes needed to unblock the work.

When changing animations, layouts, camera behavior, input, timing, particles, shaders, or visual effects:
- Verify the result on both desktop and iOS.
- Account for aspect ratio, safe area, resolution, frame rate, touch input, and platform-specific performance.
- Prefer shared animation logic with platform-specific tuning isolated in configuration.
- Do not mark the task complete unless desktop and iOS behavior has been tested, or the reason it could not be tested is stated clearly.
