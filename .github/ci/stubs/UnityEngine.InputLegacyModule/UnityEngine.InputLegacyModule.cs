using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Bindings;

public class Input
{
	public static extern bool GetMouseButtonDown(int button);

	public static bool GetKey(string name) => throw null;

	public static bool GetKey(KeyCode key) => throw null;

}
