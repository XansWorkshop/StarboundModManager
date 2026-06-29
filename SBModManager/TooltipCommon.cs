using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager {
	public static class TooltipCommon {
		public static GodotObject MakeCustomTooltip(string forText) {
			PackedScene tt = (PackedScene)GD.Load("res://tooltip.tscn");
			RichTextLabel text = tt.Instantiate<RichTextLabel>();
			GD.Print(text);
			text.Text = forText ?? string.Empty;
			return text;
		}

	}
}
