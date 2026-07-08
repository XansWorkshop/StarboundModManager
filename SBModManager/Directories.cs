using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using Godot;

using SBModManager.ModInstances;
using SBModManager.Other;
using SBModManager.SteamInterop;

namespace SBModManager {

	/// <summary>
	/// Helper methods to get various directories for a modpack
	/// </summary>
	public static class Directories {

		/// <summary>
		/// Returns the path to the SBMM directory.
		/// </summary>
		/// <returns></returns>
		public static string GetSBMMDirectory() {
			if (OS.HasFeature("standalone")) {
				return Path.GetDirectoryName(OS.GetExecutablePath())!;
			} else {
#if DEBUG
				return ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug");
#else
				OS.Alert("If you are seeing this message, please file a bug report: Invalid branch taken when getting program directory", "This is one of those impossible errors.");
#endif
			}
		}

		/// <summary>
		/// Returns the location of the Starbound executable.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException">This is run on an unsupported operating system.</exception>
		public static string GetLocalStarboundProgram() {
			string baseDir = GetLocalStarboundInstallDirectory();
			string os = OS.GetName();
			if (os == "Windows") {
				return Path2.Combine(baseDir, "win", "starbound.exe");
			} else if (os == "macOS") {
				return Path2.Combine(baseDir, "osx", "Starbound.app");
			} else if (os == "Linux") {
				return Path2.Combine(baseDir, "linux", "starbound");
			} else {
				throw new InvalidOperationException($"Cannot get Starbond installation on OS: {os}");
			}
		}

		/// <summary>
		/// Returns the location of the Starbound server executable.
		/// </summary>
		/// <returns></returns>
		/// <param name="isCheckingForInstall">If true, this is <see cref="AutoInstaller"/> and it should find <c>starbound_server</c>. Otherwise, it should find <c>run-server.sh</c> to run it.</param>
		/// <exception cref="InvalidOperationException">This is run on an unsupported operating system.</exception>
		public static string GetLocalStarboundServerProgram(bool isCheckingForInstall) {
			string baseDir = GetLocalStarboundInstallDirectory();
			string os = OS.GetName();
			if (os == "Windows") {
				return Path2.Combine(baseDir, "win", "starbound_server.exe");
			} else if (os == "macOS") {
				throw new NotSupportedException("MacOS does not support running a Starbound Server.");
			} else if (os == "Linux") {
				return Path2.Combine(baseDir, "linux", isCheckingForInstall ? "starbound_server" : "run-server.sh");
			} else {
				throw new InvalidOperationException($"Cannot get Starbond installation on OS: {os}");
			}
		}

		/// <summary>
		/// Returns the path to the executable program or script for SteamCMD
		/// </summary>
		/// <returns></returns>
		public static string GetSteamCMDProgram() {
			string baseDir = GetSteamCMDInstallationDirectory();
			string os = OS.GetName();
			if (os == "Windows") {
				return Path2.Combine(baseDir, "steamcmd.exe");
			} else {
				return Path2.Combine(baseDir, "steamcmd.sh");
			}
		}

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
		public static string GetLocalStarboundInstallDirectory() {
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
		/// Returns the path of the information json file for the modpack with the provided ID.
		/// </summary>
		/// <param name="modpackID"></param>
		/// <param name="forServer">If true, get the log directory for the server. Get it for the client otherwise.</param>
		/// <returns></returns>
		public static string GetPackSBInitFile(Guid modpackID, bool forServer) {
			if (forServer) {
				return Path2.Combine(GetPackDirectory(modpackID), "sbinit_server.config");
			} else {
				return Path2.Combine(GetPackDirectory(modpackID), "sbinit.config");
			}
		}

		/// <summary>
		/// Returns the path to the workshop cache directory.
		/// </summary>
		/// <returns></returns>
		public static string GetLocalWorkshopCacheDirectory() {
			return ProjectSettings.GlobalizePath($"user://mod_catalog_workshop");
		}

		/// <summary>
		/// Returns a path to a special json file in the workshop directory which stores update timestamps.
		/// This is used for checking updates to mods.
		/// </summary>
		/// <returns></returns>
		public static string GetLocalWorkshopVersionCache() {
			return Path2.Combine(GetLocalWorkshopCacheDirectory(), "versions.json");
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
		public static string GetSteamCMDInstallationDirectory() {
			return ProjectSettings.GlobalizePath("user://steamcmd");
		}

		/// <summary>
		/// Returns the path to a folder used to store temporary files for scripts.
		/// </summary>
		/// <returns></returns>
		public static string GetSteamCMDTempScriptDirectory() {
			return ProjectSettings.GlobalizePath("user://steamcmd_tempscripts");
		}

		/// <summary>
		/// Returns the path a folder used to store pak files being downloaded from the internet.
		/// </summary>
		/// <returns></returns>
		public static string GetOnlinePakFileStagingDirectory() {
			return ProjectSettings.GlobalizePath("user://downloading_paks");
		}

		/// <summary>
		/// Returns the path to a folder used to download inline images used in Steam Workshop descriptions.
		/// </summary>
		/// <returns></returns>
		public static string GetSteamImageCacheDirectory() {
			return ProjectSettings.GlobalizePath("user://workshop_description_image_cache");
		}

		/// <summary>
		/// Returns the logs directory for the modpack with the provided ID.
		/// </summary>
		/// <param name="modpackID"></param>
		/// <param name="forServer">If true, get the log directory for the server. Get it for the client otherwise.</param>
		public static string GetLogDirectory(Guid modpackID, bool forServer) {
			string directory = GetPackDirectory(modpackID);
			if (forServer) {
				return Path2.Combine(directory, "logs_server");
			} else {
				return Path2.Combine(directory, "logs");
			}
		}

		/// <summary>
		/// Returns the storage directory for the modpack with the provided ID, where saves and the universe get stored.
		/// </summary>
		/// <param name="modpackID"></param>
		/// <param name="forServer">If true, get the storage directory for the server. Get it for the client otherwise.</param>
		public static string GetStorageDirectory(Guid modpackID, bool forServer) {
			string directory = GetPackDirectory(modpackID);
			if (forServer) {
				return Path2.Combine(directory, "storage_server");
			} else {
				return Path2.Combine(directory, "storage");
			}
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
		/// Copies the <paramref name="source"/> directory to the <paramref name="target"/> location, overwriting duplicate files.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="target"></param>
		public static void CopyDirectoryOverwrite(string source, string target, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();
			if (source.Equals(target, StringComparison.OrdinalIgnoreCase)) {
				return;
			}

			Directory.CreateDirectory(target);
			foreach (string file in Directory.GetFiles(source)) {
				cancellationToken.ThrowIfCancellationRequested();
				File.Copy(file, Path2.Combine(target, Path.GetFileName(file)), true);
			}
			foreach (string subdirectory in Directory.GetDirectories(source)) {
				CopyDirectoryOverwrite(subdirectory, Path2.Combine(target, Path.GetFileName(subdirectory)), cancellationToken);
			}
		}
	}
}
