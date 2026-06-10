using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Bindings;

public delegate void WindowFunction(int id);

public sealed class GUILayoutOption
{
}

public class GUISkin
{
	public GUIStyle label { get; set; }
	public GUIStyle button { get; set; }
	public GUIStyle textField { get; set; }
	public GUIStyle toggle { get; set; }
	public GUIStyle box { get; set; }
	public GUIStyle window { get; set; }
}

public class GUI
{
	public static GUISkin skin { get; set; }
	public static Color color { get; set; }
	public static Color backgroundColor { get; set; }
	public static int depth { get; set; }
	public static string tooltip { get; set; }

	public static extern void FocusControl(string name);

	public static Rect ModalWindow(int id, Rect clientRect, WindowFunction func, string title) => throw null;

	public static Rect Window(int id, Rect clientRect, WindowFunction func, GUIContent title, GUIStyle style) => throw null;
	public static Rect Window(int id, Rect clientRect, WindowFunction func, string title) => throw null;

	public static void DragWindow() => throw null;
	public static void DragWindow(Rect position) => throw null;

	public static void Label(Rect position, string text) => throw null;
	public static void Label(Rect position, GUIContent content, GUIStyle style) => throw null;
	public static void Label(Rect position, string text, GUIStyle style) => throw null;

	public static void Box(Rect position, string text) => throw null;
	public static void Box(Rect position, GUIContent content, GUIStyle style) => throw null;

}
public sealed class Event
{
	public static Event current { get; set; }

	public EventType type { get; set; }
	public KeyCode keyCode { get; set; }
	public Vector2 mousePosition { get; set; }
	public Vector2 delta { get; set; }

	public void Use() => throw null;

}
public class GUILayout
{
	public static void Label(string text, params GUILayoutOption[] options) => throw null;
	public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) => throw null;
	public static void Label(GUIContent content, params GUILayoutOption[] options) => throw null;
	public static void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options) => throw null;

	public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) => throw null;
	public static Vector2 BeginScrollView(Vector2 scrollPosition, bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar, params GUILayoutOption[] options) => throw null;
	public static Vector2 BeginScrollView(Vector2 scrollPosition, bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar, GUIStyle background, params GUILayoutOption[] options) => throw null;

	public static void EndScrollView() => throw null;

	public static GUILayoutOption Height(float height) => throw null;

	public static void BeginVertical(params GUILayoutOption[] options) => throw null;
	public static void BeginVertical(GUIStyle style, params GUILayoutOption[] options) => throw null;
	public static void BeginVertical(GUIContent content, GUIStyle style, params GUILayoutOption[] options) => throw null;

	public static void BeginHorizontal(params GUILayoutOption[] options) => throw null;
	public static void BeginHorizontal(GUIStyle style, params GUILayoutOption[] options) => throw null;
	public static void BeginHorizontal(GUIContent content, GUIStyle style, params GUILayoutOption[] options) => throw null;

	public static bool Button(string text, params GUILayoutOption[] options) => throw null;
	public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) => throw null;
	public static bool Button(GUIContent content, params GUILayoutOption[] options) => throw null;
	public static bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options) => throw null;

	public static GUILayoutOption Width(float width) => throw null;

	public static string TextField(string text, params GUILayoutOption[] options) => throw null;
	public static string TextField(string text, GUIStyle style, params GUILayoutOption[] options) => throw null;
	public static string TextField(string text, int maxLength, GUIStyle style, params GUILayoutOption[] options) => throw null;

	public static void EndHorizontal() => throw null;

	public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => throw null;
	public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options) => throw null;
	public static bool Toggle(bool value, GUIContent content, params GUILayoutOption[] options) => throw null;
	public static bool Toggle(bool value, GUIContent content, GUIStyle style, params GUILayoutOption[] options) => throw null;

	public static void EndVertical() => throw null;

	public static void FlexibleSpace() => throw null;

	public static GUILayoutOption ExpandWidth(bool expand) => throw null;

	public static void Space(float pixels) => throw null;

	public static float HorizontalSlider(float value, float leftValue, float rightValue, params GUILayoutOption[] options) => throw null;
}
public sealed class GUIStyle
{
	public TextAnchor alignment { get; set; }
	public bool richText { get; set; }
	public RectOffset padding { get; set; }
	public RectOffset margin { get; set; }
	public int fontSize { get; set; }
	public FontStyle fontStyle { get; set; }
	public bool wordWrap { get; set; }
	public GUIStyleState normal { get; set; }

	public static GUIStyle none { get; }

	public GUIStyle() { }
	public GUIStyle(GUIStyle other) => throw null;

	public Vector2 CalcSize(GUIContent content) => throw null;

}
public class GUIStyleState
{
	public Color textColor { get; set; }
}
public class GUILayoutUtility
{
	public static Rect GetLastRect() => throw null;

}
public class GUIContent
{
	public string text { get; set; }
	public string tooltip { get; set; }

	public GUIContent() { }
	public GUIContent(string text) => throw null;
	public GUIContent(string text, string tooltip) => throw null;
}
