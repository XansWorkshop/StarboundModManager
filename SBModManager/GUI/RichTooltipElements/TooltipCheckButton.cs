using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager.GUI.RichTooltipElements {
	public partial class TooltipCheckButton : CheckButton {
		public override GodotObject? _MakeCustomTooltip(string forText) => Assets.CreateTooltip(forText);
	}
}
