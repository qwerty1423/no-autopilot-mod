# NO Autopilot Mod

Currently the autopilot is rather basic. There are some additional features, such as Medusa auto jam (currently just an auto fire mode, it won't select targets automatically or anything, it will only save your finger).

## features and not features

Some of the not features may eventually be implemented. the ones toward the end, probably won't be.

### list of notable features in the mod

In order of implementation, earliest first

- wing leveller
- altitude hold
- climb/descend to altitude
- set target roll angle
- vertical speed limiter
- HUD autopilot display
- fuel time and range display
- Medusa auto jam
- FBW manual override

### features not in the mod (yet?)

In approximate likelihood of implementation order descending.

- heading hold
- autothrottle
- auto-GCAS
- waypoints and flying to them
- separate PID values for every aircraft
- nap of the earth flying
- helicopter support
- auto take-off and landing
- auto fighting
- superhuman combat ai

## Autopilot

Autopilot controls roll and pitch. Does not work on helicopters unless they fly like planes (tarantula probably works when propellers are horizontal. ibis not tested).

Ascent/descent rate limits and target altitude can be configured with keyboard while flying.

Target bank angle can be set so that plane turns in a circle. Should be useful for Medusa, for easier loitering.

Large stick inputs will disengage the autopilot.

The mod can also override fly by wire, allowing you to perform better cobra manoeuvres or snap your wings.

Displays current settings on the HUD. The format is `AP: [Target Altitude] [Max Climb Rate] [Target Bank Angle]`. For example, autopilot set to 3m altitude, at 40m/s climb rate, at 0 degrees bank angle will show up as `AP: 3 40 0` on the HUD.

Has humanlike options that will help reduce its effectiveness. You can probably configure it so that it cannot sea skim at 2m ASL so easily.

PID values can be tuned further if you like, but the defaults should be quite effective. They may not work in all situations or in all aircraft, however.

There is no limit to the minimum altitude or maximum altitude, but crashes and engine flameouts may result from flying too low or too high.

Use this mod at your own risk.

## Auto Jammer

There is also an auto jammer mode for Medusa, that will jam if capacitor is full. The auto jammer will hit the fire button and there is no check for whether the jammer pods are selected, so you should make sure that they are. There is a check for the plane itself though, so you don't have to worry about accidentally enabling it on a different plane.

Also has humanlike options that can reduce its reaction time and randomise it.

## Fuel time and range display

Also displays fuel time remaining and range. Unfortunately for non-metric system users, range is displayed in km. fuel time remaining is displayed HH: MM.

Oh, forgot to mention that all the other units are also displayed in metric system, this cannot be changed.

## Usage example with Medusa

After take-off, throttle kept at 100% for climb. Autopilot set to 10000m target altitude, 50 m/s climb rate and 30 degrees bank angle. Autopilot displays `AP: 10000 50 30`.

After the plane levels out at around 10000m, throttle reduced to 30%. Plane continues to fly in circles.

Weapon is switched to jammer, and auto jammer enabled. when target is selected, it is auto jammed.

## Default Controls

| Action | Key | Description |
| :--- | :--- | :--- |
| **Toggle Autopilot** | `=` (Equals) | Self-explanatory. |
| **Toggle Auto-Jammer** | `/` (Slash) | ^^^ |
| **Toggle FBW** | `Delete` | ^^^ |
| **Target Alt Small Adjustment** | `Up` / `Down` Arrow | Small adjustments (0.1m default) |
| **Target Alt Large Adjustment** | `Left` / `Right` Arrow | Large adjustments (100m default) |
| **Max Climb Rate +/-** | `PageUp` / `PageDown` | Limit vertical speed |
| **Bank Left/Right** | `[` and `]` | Adjust roll angle |
| **Reset Bank** | `'` (Quote) | Level the wings, sets roll to 0 |

Large altitude adjustment key has minimum limit to reduce crashes when sea skimming if the key is accidentally pressed.

Keys and UI colours are configurable in the BepInEx config file.
The [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) is recommended for changing PID values and other settings in game.

## Installation

1. Install BepInEx.
2. (Recommended) Install BepInEx Configuration Manager.
3. Download  `noautopilotmod.zip` and extract the `noautopilotmod` folder.
4. Place the `noautopilotmod` folder into your `Nuclear Option/BepInEx/plugins/` folder.
6. Run the game once to generate the config file.
