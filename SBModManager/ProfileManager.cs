using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Godot;

namespace SBModManager {
	public static class ProfileManager {

		/// <summary>
		/// Launches the game using the selected profile.
		/// </summary>
		/// <param name="profileName"></param>
		public static void LaunchGame(string profileName) {
			string bootCfgDir = ProjectSettings.GlobalizePath($"user://profiles/{profileName}/sbinit.config");
		}

		

		/// <summary>
		/// Shows the user to their profiles directory.
		/// </summary>
		public static void OpenProfilesDirectory() {
			OS.ShellOpen(ProjectSettings.GlobalizePath("user://profiles"));
		}

	}
}
