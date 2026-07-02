using System;
using System.Collections.Generic;
using System.Text;

using SBModManager.GUI;

namespace SBModManager {
	public partial class TooltipLabel : Label {

		public override GodotObject? _MakeCustomTooltip(string forText) => TooltipCommon.MakeCustomTooltip(forText);

	}
}
