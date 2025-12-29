# Nuclear Option Autopilot Mod

Should be completely clientside.

## Autopilot

Autopilot controls roll and pitch. does not work on helicopters unless they fly like planes (tarantula probably works when propellers are horizontal. ibis not tested).

Ascent/descent rate limits and target altitude can be configured with keyboard.

Target bank angle can be set so that plane turns in a circle. Should be useful for Medusa.

The mod can also disable fly by wire, allowing you to perform cobra maneuvers and snap your wings instantly.

Displays current settings on the hud. The format is set altitude, set climbrate, set bankangle.

Has humanlike options that will help reduce its effectiveness. You can probably configure it so that it cannot seaskim at 2m ASL so easily.

PID values can be tuned further if you like, but the defaults should be quite effective.

## Auto Jammer

There is also an autojammer mode for Medusa, that will jam if capacitor is full. The autojammer will hit the fire button and there is no check for whether or not the jammer pods are selected, so you should make sure that they are. There is a check for the plane itself though, so you don't have to worry about accidentally enabling it on a different plane.

Also has humanlike options that can reduce its reaction time and randomise it.

## Fuel time and range display

Also displays fuel time remaining and range. Unfortunately for non metric system users, range is displayed in km. fuel time remaining is displayed HH:MM.

Oh forgot to mention that all the other units are also displayed in metric system, this cannot be changed.

## Example use on Medusa

After takeoff, throttle kept at 100% for climb. Autopilot set to 10000m ish target altitude, 50 m/s climb rate and 30 degrees bank angle. Autopilot displays `AP: 10000 50 30`.

After the plane levels out at around 10000m, throttle reduced to 30%. plane continues to fly in circles.

Weapon is switched to jammer, and autojammer enabled. when target is selected, it is auto jammed.

## Default Controls

| Action | Key | Description |
| :--- | :--- | :--- |
| **Toggle Autopilot** | `=` (Equals) | Self explanatory. |
| **Toggle Auto-Jammer** | `/` (Slash) | ^^^ |
| **Toggle FBW** | `Delete` | ^^^ |
| **Target Alt Small Adjustment** | `Up` / `Down` Arrow | Small adjustments (0.1m default) |
| **Target Alt Large Adjustment** | `Left` / `Right` Arrow | Large adjustments (100m default) |
| **Max Climb Rate +/-** | `PageUp` / `PageDown` | Limit vertical speed |
| **Bank Left/Right** | `[` and `]` | Adjust roll angle |
| **Reset Bank** | `'` (Quote) | Level the wings, sets roll to 0 |

Keys and colors are configurable in the BepInEx config file.
The [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) is recommended for changing pid values and settings ingame.

## Installation

1. Install BepInEx.
2. (Recommended) Install BepInEx Configuration Manager.
3. Download `autopilotmod.zip` and extract the `autopilotmod` folder.
4. Place the `autopilotmod` folder into your `Nuclear Option/BepInEx/plugins/` folder.
5. Run the game once to generate the config file.
