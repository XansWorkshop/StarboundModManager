using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager.GUI {
	public partial class TooltipCheckButton : CheckButton {
		public override GodotObject? _MakeCustomTooltip(string forText) => TooltipCommon.MakeCustomTooltip(forText);
	}
}
