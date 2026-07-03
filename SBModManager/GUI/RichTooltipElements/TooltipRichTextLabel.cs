using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager.GUI.RichTooltipElements {
	public partial class TooltipRichTextLabel : RichTextLabel {

		public override GodotObject? _MakeCustomTooltip(string forText) => Assets.CreateTooltip(forText);
	}
}
