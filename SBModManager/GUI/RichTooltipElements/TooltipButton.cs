using System;
using System.Collections.Generic;
using System.Text;

using SBModManager.GUI;

namespace SBModManager.GUI.RichTooltipElements {
	public partial class TooltipButton : Button {

		public override GodotObject? _MakeCustomTooltip(string forText) => Assets.CreateTooltip(forText);
	}
}
