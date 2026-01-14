# Nuclear Option Autopilot Mod

Currently the autopilot is rather basic. There are some additional features, such as EW auto jam (currently just an auto fire mode, it won't select targets automatically or anything, it will only save your finger).

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
  
### features not in the mod (yet?)

In approximate likelihood of implementation order descending.

- nap of the earth flying
- proper helicopter support
- auto take-off and landing

## Autopilot

<https://github.com/user-attachments/assets/3d2eaeaa-b810-4353-a4b6-90a1107e3cb9>

Autopilot controls roll and pitch. Any aircraft works if it flies like a plane (helicopters somehow work, but probably require small ascent/descent rate limits).

Ascent/descent rate limits and target altitude can be configured with keyboard while flying. There is also GUI that opens with F8 key by default.

Target bank angle can be set so that plane turns in a circle. Should be useful for Medusa, for easier loitering.

Large stick inputs will disengage the autopilot.

Displays current settings on the HUD. The format is `[Target Altitude] [Max Climb Rate]\n[Target speed] [Target Bank Angle/course] [distance to next wp]`. For example, autopilot set to 3m altitude, at 40m/s climb rate, at 0 degrees bank angle will show up as `3 40 S- R0` on the HUD.

PID values can be tuned further if you like, but the defaults should be quite effective. They may not work in all situations or in all aircraft, however.

There is no limit to the minimum altitude or maximum altitude, but crashes and engine flameouts may result from flying too low or too high.

Use this mod at your own risk.

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

simple how to use guide: 
right click / shift + right click on the map to make a path, then press equals and f8. hover mouse over the UI for tooltips.

## Default Controls

| Action | Key | Description |
| :--- | :--- | :--- |
| **Toggle Autopilot** | `=` (Equals) | Self-explanatory. |
| **Toggle Auto-Jammer** | `/` (Slash) | ^^^ |
| **Target Alt Small Adjustment** | `Up` / `Down` Arrow | Small adjustments (0.1m default) |
| **Target Alt Large Adjustment** | `Left` / `Right` Arrow | Large adjustments (100m default) |
| **Max Climb Rate +/-** | `PageUp` / `PageDown` | Limit vertical speed |
| **Bank Left/Right** | `[` and `]` | Adjust roll angle |
| **Reset Bank** | `'` (Quote) | Level the wings, sets roll to 0 |
| **Toggle GCAS** | `\` (Backslash) | added just in case |
| **Toggle AP GUI** | `F8` | opens/closes the GUI |

Large altitude adjustment key has minimum limit to reduce crashes when sea skimming if the key is accidentally pressed.

Keys and UI colours are configurable in the BepInEx config file.
The [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) is recommended for changing PID values and other settings in game.

## Installation

1. Install BepInEx.
2. (Recommended) Install BepInEx Configuration Manager.
3. Download  `NOAutopilotMod.zip` and extract the dll.
4. Place the dll into your `BepInEx/plugins/` folder.
5. Run the game once to generate the config file.

## Building from source

prerequisites

- .NET SDK 10 something
- game files

steps

1. clone the repo, cd no-autopilot-mod
2. create /Libs folder next to source code
3. copy dependencies into /Libs folder from game files. the list is in the `.csproj`.
4. `dotnet build -c Release`
