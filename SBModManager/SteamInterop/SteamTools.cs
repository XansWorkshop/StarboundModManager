using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Other;

using FileAccess = System.IO.FileAccess;

namespace SBModManager.SteamInterop {

	/// <summary>
	/// Helps to manage the mods from the Steam Workshop.
	/// </summary>
	public static class SteamTools {

		public static string? GetStarboundDirectory() {
			string? steamapps = GetSteamappsContainingStarbound();
			if (steamapps == null) return null;
			return Path2.Combine(steamapps, "common", "Starbound");
		}

		/// <summary>
		/// Reads libraryfolders.vdf, which is a file stored in Steam's default installation location. This file contains
		/// a list of every library folder and every game within it, which this method uses to try to find the install location
		/// of Starbound.
		/// <para/>
		/// This method then returns the steamapps directory that contains Starbound, not the Starbound directory itself. To get that,
		/// use <see cref="GetStarboundDirectory"/>
		/// </summary>
		/// <returns></returns>
		public static string? GetSteamappsContainingStarbound() {
			try {
				string os = OS.GetName();
				VDFObject? vdf;
				if (os == "Windows") {
					vdf = VDFReader.TryReadVDF(@"C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf");
				} else if (os == "macOS") {
					vdf = VDFReader.TryReadVDF("~/Library/Application Support/Steam/steamapps/libraryfolders.vdf");
				} else if (os == "Linux") {
					vdf = VDFReader.TryReadVDF("~/.steam/steam/steamapps/libraryfolders.vdf");
				} else {
					throw new NotSupportedException($"No known steam directory on OS: {os}");
				}
				if (vdf == null) {
					throw new InvalidOperationException("Failed to read vdf file to get library directories.");
				}
				VDFObject libraryFolders = vdf.GetChild("libraryfolders");
				foreach (KeyValuePair<string, object> kvp in libraryFolders.Values) {
					VDFObject libraryFolder = (VDFObject)kvp.Value;
					string path = libraryFolder.GetValue("path");
					VDFObject apps = libraryFolder.GetChild("apps");
					if (apps.Values.ContainsKey("211820")) {
						return Path2.Combine(path, "steamapps");
					}
				}
			} catch { }
			return null;
		}

		/// <summary>
		/// Creates a series of commands which are executed by SteamCMD to download one or more workshop mods to disk.
		/// </summary>
		/// <param name="ids">An array of workshop item IDs to download.</param>
		/// <param name="skipIfInstalled">If true, workshop items that appear to be already installed are skipped.</param>
		/// <param name="cancellationToken">Can be used to cancel the process.</param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="OperationCanceledException"></exception>
		public static async Task DownloadWorkshopModsAsync(long[] ids, bool skipIfInstalled, CancellationToken cancellationToken) {
			if (ids.Length == 0) return;

			string workshopDir = Directories.GetLocalWorkshopCacheDirectory();
			string scriptDir = Directories.GetSteamCMDTempScriptDirectory();
			string scriptFile = Path2.Combine(scriptDir, Path.GetRandomFileName() + ".txt");
			Directory.CreateDirectory(workshopDir);
			Directory.CreateDirectory(scriptDir);

			StringBuilder script = new StringBuilder();
			script.AppendLine("@ShutdownOnFailedCommand 0");
			script.AppendLine("login anonymous");
			bool hadAny = false;
			for (int i = 0; i < ids.Length; i++) {
				cancellationToken.ThrowIfCancellationRequested();

				if (skipIfInstalled) {
					string itemPath = Path2.Combine(workshopDir, ids[i].ToString());
					if (Directory.Exists(itemPath)) continue;
				}
				script.AppendLine($"download_item 211820 {ids[i]}");
				hadAny = true;
			}
			if (!hadAny) return;

			File.WriteAllText(scriptFile, script.ToString());
			try {
				await SteamCMD.RunSteamCMDScriptAsync(scriptFile, cancellationToken);
				string baseInstallPath = Path2.Combine(Directories.GetSteamCMDInstallationDirectory(), "steamapps", "content", "app_211820");

				for (int i = 0; i < ids.Length; i++) {
					cancellationToken.ThrowIfCancellationRequested();

					string itemPath = Path2.Combine(baseInstallPath, $"item_{ids[i]}");
					string destination = Path2.Combine(workshopDir, ids[i].ToString());
					if (Directory.Exists(destination)) {
						try {
							Directory.Delete(destination, true);
						} catch (DirectoryNotFoundException) { }
					}
					Directory.Move(itemPath, destination);
				}
			} finally {
				File.Delete(scriptFile);
			}
		}

		/// <summary>
		/// Efficiency-promoting method that copies every current workshop subscription into the cache.
		/// Returns a list of every installed mod ID.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="OperationCanceledException"></exception>
		public static long[] CopyAllCurrentSubscriptionsToCache(bool skipDuplicates, CancellationToken cancellationToken) {
			List<long> installed = [];

			string? sbPath = GetSteamappsContainingStarbound();
			string workshopCacheDir = Directories.GetLocalWorkshopCacheDirectory();
			if (sbPath != null) {
				string workshopContent = Path2.Combine(sbPath, "workshop", "content", "211820");
				string[] subdirectories = Directory.GetDirectories(workshopContent);
				for (int i = 0; i < subdirectories.Length; i++) {
					cancellationToken.ThrowIfCancellationRequested();

					string workshopSubdirectory = subdirectories[i];
					string? name = Path.GetFileName(workshopSubdirectory);

					if (name != null && long.TryParse(name, out long workshopID)) {
						string destination = Path2.Combine(workshopCacheDir, workshopID.ToString());
						if (Directory.Exists(destination)) {
							if (skipDuplicates) {
								installed.Add(workshopID);
								continue;
							}
							// ^ For performance, not to prevent the error.
							try {
								Directory.Delete(destination, true);
							} catch (DirectoryNotFoundException) { }
						}

						Directories.CopyDirectory(workshopSubdirectory, destination, cancellationToken);
						installed.Add(workshopID);
					}
				}
			} else {
				throw new DirectoryNotFoundException("Unable to find Starbound install directory.");
			}

			return installed.ToArray();
		}

	}
}
