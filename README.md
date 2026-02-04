# Nuclear Option Autopilot Mod

Adds autopilot and some other features. Clientside and multiplayer compatible.

It would be best to make sure the host is ok with you using the mod, especially in PVP.

As of 2026-02-02, Talon Two allows the use of this mod on their servers, and GrayWar does not.

## [Changelog](https://github.com/qwerty1423/no-autopilot-mod/blob/main/CHANGELOG.md)

## Installation

1. Install BepInEx 5.
   - [their github has install instructions](https://github.com/BepInEx/BepInEx/)
   - if you are on linux, add override in steam launch options: WINEDLLOVERRIDES="winhttp=n,b" %command%
   - run the game, then quit
   - edit BepInEx.cfg in BepInEx/config

   - line that says:
   `HideManagerGameObject = false`
   - should be changed to:
   `HideManagerGameObject = true`

   (mod will not work with this setting off. the config manager will also not work with it off.)

2. (Recommended) Install BepInEx Configuration Manager. It is useful for editing settings ingame.
   - download [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)
   - place extracted folder in BepInEx/plugins. (make sure your folder structure is like the image below)
3. Download  `com.qwerty1423.NOAutopilot-{version}.zip` from releases (to the right if you are on pc) and extract the dll.
4. Place the dll into your `BepInEx/plugins/` folder.
5. Run the game again to generate the config file (for the mod). The mod should work at this point.

folder structure after install (the names are wrong now but the locations are the same):

![no-folder-structure](https://github.com/user-attachments/assets/ff161a5b-676d-48eb-9eac-944afdeedb6c)

If you're stuck you can try Primeva 2082 discord's mod install guide:
<https://docs.google.com/document/d/16aRWcrkt89YEn9_THwe9Fxo7AYPLE9SYSab_Y929dvE>

or ask for help on the nuclear option discord.

After updates, it is recommended to regenerate your config file at `BepInEx/config/com.qwerty1423.NOAutopilot.cfg` by either deleting it or moving it somewhere else and merging your changes with the newer version, because default config may change after an update.

## Default Controls

The autopilot can be configured using only the `F8` gui window and the map, so `F8` is probably the most important keybind to remember.
Leave blank the text boxes that you don't want to be controlled by the autopilot, then click set values (a few times just in case).
A more up to date version of the table below is included in one of the tooltips in the GUI, although without the table formatting. Currently only default controls included.

| Action                      | Key                    | Description                                  |
| :-------------------------- | :--------------------- | :------------------------------------------- |
| Toggle Autopilot            | `=` (Equals)           | Self-explanatory.                            |
| Toggle Auto-Jammer          | `/` (Slash)            | ^^^                                          |
| Target Alt Small Adjustment | `Up` / `Down` Arrow    | Small adjustments (0.1m default)             |
| Target Alt Large Adjustment | `Left` / `Right` Arrow | Large adjustments (100m default)             |
| Max Climb Rate +/-          | `PageUp` / `PageDown`  | Limit vertical speed                         |
| Bank Left/Right             | `[` and `]`            | Adjust roll angle                            |
| C Crs/R Roll/C Alt/C Roll   | `'` (Quote)            | clear/reset in that order, one per keystroke |
| Toggle GCAS                 | `\` (Backslash)        | added just in case                           |
| Toggle autothrottle         | `;`                    | will write current speed to ui/delete it     |
| **Toggle AP GUI**           | `F8`                   | **opens/closes the GUI**                     |

Large altitude adjustment key has minimum limit to reduce crashes when sea skimming if the key is accidentally pressed.

Keys and UI colours are configurable in the BepInEx config file.
The [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) is recommended for changing PID values and other settings in game.

## screenshot of an old version?

![imperialap](https://github.com/user-attachments/assets/23580e9d-2ea9-441d-8079-a181dab5c0cc)

## features and not features

Some of the not features may eventually be implemented.

### list of notable features in the mod

In order of implementation, earliest first

- wing leveller and altitude hold
- climb/descend to altitude at set rate
- set target roll angle
- HUD autopilot display
- fuel time and range display
- Medusa auto jam on full capacitor (won’t select targets or weapons)
- Auto-GCAS
- simple autopilot GUI
- course hold
- autothrottle
- waypoints
- minor map improvements
- singleplayer fbw disabler
  
### features not in the mod (yet?)

In approximate likelihood of implementation order descending.

- yaw control
- nap of the earth flying
- proper helicopter support
- auto take-off and landing

## Autopilot

<https://github.com/user-attachments/assets/3d2eaeaa-b810-4353-a4b6-90a1107e3cb9>

Autopilot controls roll and pitch. (helicopters somehow work, but probably require small ascent/descent rate limits).

Ascent/descent rate limits and target altitude can be configured with keyboard while flying. There is also GUI that opens with F8 key by default.

Target bank angle can be set so that plane turns in a circle. Should be useful for Medusa, for easier loitering.

Large stick inputs will disengage the autopilot.

Displays current settings on the HUD.

PID values can be tuned further if you like, but the defaults should be quite effective. They may not work in all situations or in all aircraft, however.

There is no limit to the minimum altitude or maximum altitude, but crashes and engine flameouts may result from flying too low or too high.

## Auto Jammer

There is also an auto jammer mode for Medusa, that will jam if capacitor is full. The auto jammer will hit the fire button if there is a target selected and the jammer pods are selected.

## Fuel time and range display

Also displays fuel time remaining and range.

Units use the game's metric/imperial setting.

## Auto-GCAS

<https://github.com/user-attachments/assets/aca3060d-a035-4750-953d-b189f833b2e2>

Will pull up if you are going to hit the ground and warns you a while before. Can be disabled with `\` key by default, and can be configured to start disabled. GCAS OFF warning can be disabled as well.

If you are making large inputs, it will not pull up.

## Waypoints

<https://github.com/user-attachments/assets/1ac109a4-bc84-49ed-8141-55bc9a217607>

Simple how to use guide:
Right click / shift + right click on the map to make a path, then press equals and f8. Hover mouse over the UI for tooltips.

Setting waypoints will work as long as you don't create any yellow unit path lines, so as long as the first unit you have selected is not commandable then you can make a waypoint.

## Building from source

prerequisites

- .NET SDK 10 something
- game files

steps

1. clone the repo, cd no-autopilot-mod, checkout a release version.
2. copy dependencies into /Libs folder from game files. (mainly in /Nuclear Option/NuclearOption_Data/Managed). the list is in the `.csproj`.
3. `dotnet build -c Release`

Latest release might be reproducible as long as no silent hotfix or game update has occurred. (no guarantees here, so this is rather useless)

Getting the correct versions of the dlls might be difficult since the game updates regularly and silently. You also have to clone the repo and checkout the specific version.
