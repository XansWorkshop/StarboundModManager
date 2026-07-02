using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Godot;

using SBModManager.ModInstances;

namespace SBModManager {

	/// <summary>
	/// Helper methods to get various directories for a modpack
	/// </summary>
	public static class Directories {

		/// <summary>
		/// Returns the location of the program's configuration file.
		/// </summary>
		/// <returns></returns>
		public static string GetAppConfigFile() {
			return ProjectSettings.GlobalizePath("user://configuration.json");
		}

		/// <summary>
		/// Returns the location of the launcher's install of Starbound.
		/// </summary>
		/// <returns></returns>
		public static string GetPrivateStarboundInstallDirectory() {
			return ProjectSettings.GlobalizePath("user://starbound");
		}

		/// <summary>
		/// Returns the base directory for all modpacks.
		/// </summary>
		/// <returns></returns>
		public static string GetPackDirectory() {
			return ProjectSettings.GlobalizePath($"user://profiles");
		}

		/// <summary>
		/// Returns the base directory for a modpack based on the provided ID.
		/// </summary>
		/// <param name="modpackID">The GUID of the modpack to get the directory for.</param>
		/// <returns></returns>
		public static string GetPackDirectory(Guid modpackID) {
			return ProjectSettings.GlobalizePath($"user://profiles/{modpackID:D}");
		}

		/// <summary>
		/// Returns the path of the information json file for the modpack with the provided ID.
		/// </summary>
		/// <param name="modpackID"></param>
		/// <returns></returns>
		public static string GetPackInfoFile(Guid modpackID) {
			return Path2.Combine(GetPackDirectory(modpackID), "info.json");
		}

		/// <summary>
		/// Returns the path to the workshop cache directory.
		/// </summary>
		/// <returns></returns>
		public static string GetLocalWorkshopCacheDirectory() {
			return ProjectSettings.GlobalizePath($"user://mod_catalog_workshop");
		}

		/// <summary>
		/// Returns the path to the manually installed mod cache directory. It's the same as the workshop cache but for
		/// mods that come from an explicit source like a .pak file being directly installed.
		/// </summary>
		/// <returns></returns>
		public static string GetLocalManualModCacheDirectory() {
			return ProjectSettings.GlobalizePath($"user://mod_catalog_manual");
		}

		/// <summary>
		/// Returns the path to the SteamCMD installation that is automatically set up on Windows when the user requests it.
		/// </summary>
		/// <returns></returns>
		public static string GetLocalSteamCMDInstallationDirectory() {
			return ProjectSettings.GlobalizePath("user://steamcmd");
		}

		/// <summary>
		/// Returns the path to a folder used to store temporary files for scripts.
		/// </summary>
		/// <returns></returns>
		public static string GetLocalSteamCMDTempScriptDir() {
			return ProjectSettings.GlobalizePath("user://steamcmd_tempscripts");
		}

		/// <summary>
		/// Ensures that the profile directory for the modpack with the given ID has been created.
		/// </summary>
		/// <param name="modpackID">The GUID of the modpack to initialize.</param>
		public static void InitializeModpackDirectory(Guid modpackID) {
			string directory = GetPackDirectory(modpackID);
			// CreateDirectory creates the entire tree.
			Directory.CreateDirectory(Path2.Combine(directory, "logs"));
			Directory.CreateDirectory(Path2.Combine(directory, "storage"));
			Directory.CreateDirectory(Path2.Combine(directory, "extra_mods"));
			Directory.CreateDirectory(Path2.Combine(directory, "extra_assets"));

			File.WriteAllText(
				Path2.Combine(directory, "NO_MODS_HERE.TXT"),
				"You might be wondering \"where is the mod folder?\"\n\nThere is none. If you want to install mods manually go to the shared mods directory instead.\nThen, you can install them from your catalog."
			);
		}

		/// <summary>
		/// Returns the logs directory for the modpack with the provided ID.
		/// </summary>
		/// <param name="modpackID"></param>
		public static string GetLogDirectory(Guid modpackID) {
			string directory = GetPackDirectory(modpackID);
			return Path2.Combine(directory, "logs");
		}

		/// <summary>
		/// Returns the storage directory for the modpack with the provided ID, where saves and the universe get stored.
		/// </summary>
		/// <param name="modpackID"></param>
		public static string GetStorageDirectory(Guid modpackID) {
			string directory = GetPackDirectory(modpackID);
			return Path2.Combine(directory, "storage");
		}

		/// <summary>
		/// Returns the extra assets directory for the modpack with the provided ID.
		/// </summary>
		/// <param name="modpackID"></param>
		public static string GetExtraAssetsDirectory(Guid modpackID) {
			string directory = GetPackDirectory(modpackID);
			return Path2.Combine(directory, "extra_assets");
		}

		/// <summary>
		/// Launches the game using the selected profile.
		/// </summary>
		/// <param name="profileName"></param>
		public static void LaunchGame(Guid modpackID) {
			string directory = GetPackDirectory(modpackID);
			string sbInit = Path2.Combine(directory, "sbinit.config");
		}

		/// <summary>
		/// Copies the <paramref name="source"/> directory to the <paramref name="target"/> location.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="target"></param>
		public static void CopyDirectory(string source, string target) {
			if (source.Equals(target, StringComparison.OrdinalIgnoreCase)) {
				return;
			}
			Directory.CreateDirectory(target);
			foreach (string file in Directory.GetFiles(source)) {
				File.Copy(file, Path2.Combine(target, Path.GetFileName(file)), true);
			}
			foreach (string subdirectory in Directory.GetDirectories(source)) {
				CopyDirectory(subdirectory, Path2.Combine(target, Path.GetFileName(subdirectory)));
			}
		}
	}
}
