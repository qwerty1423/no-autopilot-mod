using System.Text.RegularExpressions;

using UnityEngine;

namespace NOAutopilot.Core;

public static class ModUtils
{
    private static readonly Regex RxSpaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex RxDecimals = new(@"[\.]\d+", RegexOptions.Compiled);
    private static readonly Regex RxNumber = new(@"-?\d+", RegexOptions.Compiled);

    public static Color GetColor(string hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return fallback;
        }

        if (!hex.StartsWith("#"))
        {
            hex = "#" + hex;
        }

        return ColorUtility.TryParseHtmlString(hex, out Color c)
            ? c : fallback;
    }

    public static float ConvertAlt_ToDisplay(float meters)
    {
        return PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial
            ? meters * 3.28084f : meters;
    }

    public static float ConvertAlt_FromDisplay(float displayVal)
    {
        return PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial
            ? displayVal / 3.28084f : displayVal;
    }

    public static float ConvertVS_ToDisplay(float metersPerSec)
    {
        return PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial
            ? metersPerSec * 196.850394f : metersPerSec;
    }

    public static float ConvertVS_FromDisplay(float displayVal)
    {
        return PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial
            ? displayVal / 196.850394f : displayVal;
    }

    public static float ConvertSpeed_ToDisplay(float ms)
    {
        return PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial
            ? ms * 1.94384f : ms * 3.6f;
    }

    public static float ConvertSpeed_FromDisplay(float val)
    {
        return PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial
            ? val / 1.94384f : val / 3.6f;
    }

    public static string ProcessGameString(string raw, bool keepUnit)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "";
        }

        // remove spaces
        string clean = RxSpaces.Replace(raw, "");

        clean = clean.Replace("+", "");

        // remove decimals
        clean = RxDecimals.Replace(clean, "");

        if (keepUnit)
        {
            return clean;
        }

        // remove units
        Match match = RxNumber.Match(clean);
        return match.Success ? match.Value : clean;
    }

    public static Vector3 ParseGridToPos(string grid)
    {
        try
        {
            grid = grid.Trim();
            float x = 0, z = 0;
            const float offX = 80000f; // offsetX from GridLabels class
            const float offY = 80000f; // offsetY also from GL class

            if (grid.Length == 2)
            {
                // 1 letter, 1 number
                int majY = char.ToUpper(grid[0]) - 'A';
                int majX = int.Parse(grid[1].ToString());
                x = (majX * 10000f) + 5000f - offX;
                z = offY - ((majY * 10000f) + 5000f);
            }
            else if (grid.Length == 4)
            {
                // 2 letter, 2 num
                int majY = char.ToUpper(grid[0]) - 'A';
                int minY = char.ToLower(grid[1]) - 'a';
                int majX = int.Parse(grid[2].ToString());
                int minX = int.Parse(grid[3].ToString());
                x = (majX * 10000f) + (minX * 1000f) + 500f - offX;
                z = offY - ((majY * 10000f) + (minY * 1000f) + 500f);
            }

            return new Vector3(x, 0, z);
        }
        catch { return Vector3.zero; }
    }
}
