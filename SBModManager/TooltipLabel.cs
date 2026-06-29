using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager {
	public sealed partial class TooltipLabel : Label {

		public override GodotObject _MakeCustomTooltip(string forText) => TooltipCommon.MakeCustomTooltip(forText);

	}
}
