using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager.GUI {
	public partial class TooltipTextureButton : TextureButton {

		public override GodotObject? _MakeCustomTooltip(string forText) => TooltipCommon.MakeCustomTooltip(forText);
	}
}
