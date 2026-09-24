using System.Globalization;

using NOAutopilot.Core.Flight;

using UnityEngine;

namespace NOAutopilot.Core.Map;

// popup to type a waypoint's altitude in display units; Enter applies, Backspace closes
internal static class WaypointAltDialog
{
    private const int WindowId = 997;

    private static int s_index = -1;
    private static Vector2 s_anchor;
    private static string s_text = "";
    private static bool s_focusPending;
    private static Rect s_rect = new(0f, 0f, 280f, 110f);

    public static bool IsOpen => s_index >= 0;

    public static void Open(int index)
    {
        if (index < 0 || index >= APData.NavQueue.Count)
        {
            return;
        }

        s_index = index;
        Vector3 wp = APData.NavQueue[index];
        s_anchor = new Vector2(wp.x, wp.z);
        float shown = wp.y > 0f ? wp.y : MapInteractionPatch.SuggestAlt(wp.x, wp.z, index);
        s_text = ModUtils.ConvertAlt_ToDisplay(shown).ToString("F0", CultureInfo.InvariantCulture);
        s_focusPending = true;
    }

    public static void Close()
    {
        s_index = -1;
    }

    public static void Draw()
    {
        if (!IsOpen)
        {
            return;
        }

        int idx = ResolveIndex();
        if (idx < 0)
        {
            Close();
            return;
        }

        bool imperial = PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial;
        s_rect.x = (Screen.width - s_rect.width) * 0.5f;
        s_rect.y = Screen.height * 0.33f;
        s_rect = GUI.Window(WindowId, s_rect, DrawWindow,
            $"Waypoint {idx + 1}/{APData.NavQueue.Count} altitude ({(imperial ? "ft" : "m")})");
    }

    private static void DrawWindow(int id)
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                Apply();
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Backspace)
            {
                Close();
                e.Use();
                return;
            }
        }

        GUI.SetNextControlName("WpAltField");
        s_text = GUILayout.TextField(s_text, 12);
        if (s_focusPending && Event.current.type == EventType.Repaint)
        {
            GUI.FocusControl("WpAltField");
            TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            te?.SelectAll();
            s_focusPending = false;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("OK (Enter)"))
        {
            Apply();
        }

        if (GUILayout.Button("Close (Backspace)"))
        {
            Close();
        }

        GUILayout.EndHorizontal();
        GUILayout.Label("Type 0 for a 2D waypoint.");
        GUI.DragWindow();
    }

    private static void Apply()
    {
        int idx = ResolveIndex();
        if (idx >= 0)
        {
            Vector3 wp = APData.NavQueue[idx];
            wp.y = ParseAlt(s_text);
            APData.NavQueue[idx] = wp;
            Plugin.RefreshNavVisuals();
        }

        Close();
    }

    // empty or 0 -> 2D waypoint; anything else is the altitude in display units
    private static float ParseAlt(string s)
    {
        s = s.Trim();
        int end = 0;
        while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.' || s[end] == '-' || s[end] == '+'))
        {
            end++;
        }

        return float.TryParse(s.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
            ? ModUtils.ConvertAlt_FromDisplay(v) : 0f;
    }

    // the queue shifts as points pop: track the node by position
    private static int ResolveIndex()
    {
        if (s_index >= 0 && s_index < APData.NavQueue.Count &&
            Vector2.Distance(new Vector2(APData.NavQueue[s_index].x, APData.NavQueue[s_index].z), s_anchor) <= 5f)
        {
            return s_index;
        }

        int best = -1;
        float bestD = 50f;
        for (int i = 0; i < APData.NavQueue.Count; i++)
        {
            float d = Vector2.Distance(new Vector2(APData.NavQueue[i].x, APData.NavQueue[i].z), s_anchor);
            if (d <= bestD)
            {
                bestD = d;
                best = i;
            }
        }

        s_index = best;
        return best;
    }
}
