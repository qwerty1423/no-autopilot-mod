using UnityEngine;

namespace UnityEngine.UI
{
	public class Graphic : MonoBehaviour
	{
		public Color color { get; set; }
	}

	public class MaskableGraphic : Graphic
	{
	}

	public class Text : MaskableGraphic
	{
		public string text { get; set; }
		public int fontSize { get; set; }
		public FontStyle fontStyle { get; set; }
		public bool supportRichText { get; set; }
		public bool resizeTextForBestFit { get; set; }
		public TextAnchor alignment { get; set; }
		public HorizontalWrapMode horizontalOverflow { get; set; }
		public VerticalWrapMode verticalOverflow { get; set; }
		public bool richText { get; set; }
	}

	public class Image : MaskableGraphic
	{
	}

	public class RawImage : MaskableGraphic
	{
	}

	public class Mask : MonoBehaviour
	{
	}

	public class RectMask2D : MonoBehaviour
	{
	}
}
