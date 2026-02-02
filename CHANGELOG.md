# Changelog

## [4.13.12] - 2026-02-02

### Added

- fbw disabler for singleplayer

## [4.13.11] - 2026-01-31

### Fixed

- restore the override indicator

## [4.13.10] - 2026-01-31

### Added

- better GCAS visuals

### Fixed

- `GetSpeedofSound()` -> `GetSpeedOfSound()`

### Changed

- use codematcher for map zoom and pan
- improve changelog

## [4.13.9] - 2026-01-28

### Added

- undo waypoint button
- increase map pan limits
- increase map zoom limits
- center map buttons
- config for nav disable on AP disengage
- copy values from GUI readout on click
- changelog

### Fixed

- waypoint distance calculation
- waypoints being created when mouse is not on the map

### Changed

- nav disable on AP disengage defaults to false
- calculation for speed ETA and fuel time

## [4.13.8] - 2026-01-23

### Added

- Settings for making autothrottle disengage when AP disengages.
- Settings for keeping the previous target altitude on reengage after stick movement or engaging AP via the key.

### Fixed

- Roll target not changing in GUI when Nav/course enabled.

## [4.13.7] - 2026-01-22

### Changed

- gcas now only triggers on buildings and terrain

## [4.13.6] - 2026-01-21

### Added

- speed keys: shift, ctrl, home, end by default

### Fixed

- bracket keys now change bank limit in nav mode

### Changed

- keys now hopefully framerate independent
- gcas now works when upside down and when it has to pull more than 5g
- hud overlay improvements

## [4.13.5] - 2026-01-20

### Fixed

- autothrottle won't jump around as much anymore
- HUD overlay now scales with window/screen size
- waypoint creation now works whenever a yellow line is not created, before it was not possible to create waypoints with anything selected on the map.

### Changed

- HUD overlay now has configurable update interval?
- some keys are now more responsive (such as quote)
- default limits reduced for extra safety (configurable)
- waypoints now update when you get near them with autopilot off so you can use them as path planner
- autothrottle is now separate from autopilot, but no keybinds for increase/decrease speed yet
- the UI is more colourful now, and shows what the autopilot is controlling
- ap disengage with stick input is now more configurable
- `private void SyncMenuValues()` changed to `public static void SyncMenuValues()`

## [4.13.4] - 2026-01-17

## [4.13.3] - 2026-01-15

## [4.13.2] - 2026-01-14

## [4.13.1] - 2026-01-14

## [4.13.0] - 2026-01-13

## [4.12.2] - 2026-01-11

## [4.12.1] - 2026-01-11

## [4.12.0] - 2026-01-10

## [4.11.5] - 2026-01-09

## [4.11.4] - 2026-01-07

## [4.11.3] - 2026-01-07

## [4.11.2] - 2026-01-06

## [4.11.1] - 2026-01-05

## [4.10.0] - 2026-01-05

## [4.9.3] - 2026-01-05

## [4.9.2] - 2026-01-01

## [4.9.0] - 2025-12-31

## [4.8.5] - 2025-12-31

## [4.8.4] - 2025-12-29

[4.13.11]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.10...v4.13.11
[4.13.10]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.9...v4.13.10
[4.13.9]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.8...v4.13.9
[4.13.8]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.7...v4.13.8
[4.13.7]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.6...v4.13.7
[4.13.6]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.5...v4.13.6
[4.13.5]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.4...v4.13.5
[4.13.4]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.3...v4.13.4
[4.13.3]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.2...v4.13.3
[4.13.2]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.1...v4.13.2
[4.13.1]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.13.0...v4.13.1
[4.13.0]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.12.2...v4.13.0
[4.12.2]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.12.1...v4.12.2
[4.12.1]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.12.0...v4.12.1
[4.12.0]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.11.5...v4.12.0
[4.11.5]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.11.4...v4.11.5
[4.11.4]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.11.3...v4.11.4
[4.11.3]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.11.2...v4.11.3
[4.11.2]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.11.1...v4.11.2
[4.11.1]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.10.0...v4.11.1
[4.10.0]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.9.3...v4.10.0
[4.9.3]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.9.2...v4.9.3
[4.9.2]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.9.0...v4.9.2
[4.9.0]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.8.5...v4.9.0
[4.8.5]: https://github.com/qwerty1423/no-autopilot-mod/compare/v4.8.4...v4.8.5
[4.8.4]: https://github.com/qwerty1423/no-autopilot-mod/commits/v4.8.4
