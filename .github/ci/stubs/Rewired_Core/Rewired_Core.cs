using System.Collections.Generic;

namespace Rewired
{
	public abstract class Controller
	{
		public string name { get; }
		public ControllerType type { get; }
		public int buttonCount { get; }
		public Button[] Buttons { get; }

		public virtual Element GetElementById(int elementIdentifierId) => throw null;

		public int GetButtonIndexById(int elementIdentifierId) => throw null;

		public virtual bool GetButtonDown(int index) => throw null;

		public virtual bool GetButton(int index) => throw null;

		public abstract class Element
		{
			public ControllerElementIdentifier elementIdentifier { get; }
		}

		public class Button : Element
		{
		}
	}

	public class ControllerElementIdentifier
	{
		public string name { get; }
		public int id { get; }
	}

	public enum ControllerType
	{
		Keyboard,
		Mouse,
		Joystick,
		Custom
	}

	public class Joystick : Controller
	{
	}

	public class Mouse : Controller
	{
	}

	public class Keyboard : Controller
	{
	}

	public static class ReInput
	{
		public static ControllerHelper controllers { get; }

		public sealed class ControllerHelper
		{
			public IList<Joystick> Joysticks { get; }
			public Mouse Mouse { get; }
			public Keyboard Keyboard { get; }
		}
	}
}
