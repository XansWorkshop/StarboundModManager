using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager.Other {

	/// <summary>
	/// Contains some special cases for use in SBMM
	/// </summary>
	public static class SpecialCases {
		
		/// <summary>
		/// Returns messages for specific mods.
		/// </summary>
		/// <param name="modID"></param>
		/// <param name="specialCase"></param>
		/// <returns></returns>
		public static bool TryGetSpecialCaseFor(string modID, out string? specialCase) {
			// TO FUTURE ANYONE:
			// I'm actually really iffy about adding this feature.
			// SB modders are notoriously passive-aggressive and toxic to each other over incompatibilities. Adding them here seems like
			// a great way to use an otherwise harmless mod manager as a weapon for petty arguments over who's doing what wrong.

			// Basically, if you think you have something to PR into here, no, you probably don't.

			if (modID == "Futara's Dragon Pixel Full Bright Shader") {
				specialCase = $"OpenStarbound comes with [color=#E094FE]{modID}[/color] as a built-in feature. You can still have this installed, but it won't do anything.";
				return true;
			}
			specialCase = null;
			return false;
		}

	}
}
