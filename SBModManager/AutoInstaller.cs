using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Menus.Windows;
using SBModManager.ModInstances;
using SBModManager.Other;
using SBModManager.SteamInterop;

namespace SBModManager {

	/// <summary>
	/// Represents routines which download and extract necessary components.
	/// </summary>
	public static class AutoInstaller {

		/// <summary>
		/// Returns <see langword="true"/> if SteamCMD isn't installed in the local directory for it.
		/// </summary>
		/// <returns></returns>
		public static bool NeedsToInstallSteamCMD() {
			string directory = Directories.GetSteamCMDInstallationDirectory();
			if (!Directory.Exists(directory)) return true;

			string os = OS.GetName();
			if (os == "Windows") {
				return !File.Exists(Path2.Combine(directory, "steamcmd.exe"));
			} else {
				return !File.Exists(Path2.Combine(directory, "steamcmd.sh"));
			}
		}

		/// <summary>
		/// Returns <see langword="true"/> if the Starbound executable file cannot be found in the application data directory.
		/// </summary>
		/// <returns></returns>
		public static bool NeedsToInstallOpenStarboundClient() {
			string starboundDir = Directories.GetLocalStarboundInstallDirectory();
			string starboundApp = Directories.GetLocalStarboundProgram();
			if (!File.Exists(Path2.Combine(starboundDir, "assets", "opensb.pak"))) return true;
			return !File.Exists(starboundApp);
		}

		/// <summary>
		/// Returns <see langword="true"/> if the Starbound executable file cannot be found in the application data directory.
		/// </summary>
		/// <returns></returns>
		public static bool NeedsToInstallOpenStarboundServer() {
			if (OS.GetName() == "macOS") return false; // Mac doesn't have a server.

			string starboundDir = Directories.GetLocalStarboundInstallDirectory();
			string starboundServerApp = Directories.GetLocalStarboundServerProgram(true);
			if (!File.Exists(Path2.Combine(starboundDir, "assets", "opensb.pak"))) return true;
			return !File.Exists(starboundServerApp);
		}

		/// <summary>
		/// Returns <see langword="true"/> if <c>packed.pak</c> or <c>tiled</c> are missing from the Starbound installation in the application data directory.
		/// </summary>
		/// <returns></returns>
		public static bool NeedsToInstallStarboundAssets() {
			string starboundDir = Directories.GetLocalStarboundInstallDirectory();
			if (!File.Exists(Path2.Combine(starboundDir, "assets", "packed.pak"))) return true;
			if (!Directory.Exists(Path2.Combine(starboundDir, "tiled"))) return true;
			return false;
		}

		#region SteamCMD

		/// <summary>
		/// Asynchronously download and install steamcmd for the current operating system into SBMM's data folder.
		/// </summary>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> which can be used to stop the installation.</param>
		/// <returns></returns>
		/// <exception cref="NotSupportedException">The operating system is not supported.</exception>
		/// <exception cref="OperationCanceledException">The installation is cancelled.</exception>
		/// <exception cref="InvalidOperationException">The installation cannot be completed due to an incorrect state (i.e. already installed).</exception>
		public static async Task InstallSteamCMDAsync(CancellationToken cancellationToken) {
			if (!NeedsToInstallSteamCMD()) {
				throw new InvalidOperationException("SteamCMD already appears to be installed.");
			}

			string os = OS.GetName();
			string steamCMDDir = Directories.GetSteamCMDInstallationDirectory();
			string downloadLink = os switch {
				"Windows" => "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip",
				"macOS" => "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_osx.tar.gz",
				"Linux" => "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz",
				_ => throw new NotSupportedException($"SteamCMD: Operating System {os} is not supported.")
			};

			GD.Print($"Downloading from {downloadLink} ...");
			using Stream download = await SBModManagerGlobals.HTTP_CLIENT.GetStreamAsync(downloadLink, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();

			if (os == "Windows") {
				await InstallSteamCMDWindowsAsync(steamCMDDir, download, cancellationToken);
			} else {
				await InstallSteamCMDMacLinuxAsync(steamCMDDir, download, cancellationToken);
			}
		}

		private static async Task InstallSteamCMDWindowsAsync(string steamCMDDir, Stream download, CancellationToken cancellationToken) {
			string steamCMDExe = Path2.Combine(steamCMDDir, "steamcmd.exe");

			// Unzip in memory and extract to the destination folder.
			GD.Print($"Unzipping SteamCMD...");
			using (ZipArchive archive = new ZipArchive(download, ZipArchiveMode.Read)) {
				cancellationToken.ThrowIfCancellationRequested();
				await archive.ExtractToDirectoryAsync(steamCMDDir, cancellationToken).ConfigureAwait(false);
			}

			// Start SteamCMD then tell it to run the "quit" command. This will start it, which makes it do its setup, and then close it.
			await Task.Delay(1000); // Delay is here because without it, it crashes on startup. I don't know why.
			cancellationToken.ThrowIfCancellationRequested();

			GD.Print($"Running SteamCMD so it can do its setup routine...");
			Process? steamCMDProcess = Process.Start(new ProcessStartInfo {
				FileName = steamCMDExe,
				Arguments = "+quit",
				UseShellExecute = false,
				CreateNoWindow = true
			});
			if (steamCMDProcess == null) throw new InvalidOperationException("Failed to perform first-time startup of SteamCMD.");
			await steamCMDProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

			GD.Print($"Done, SteamCMD is now installed.");
		}
		private static async Task InstallSteamCMDMacLinuxAsync(string steamCMDDir, Stream download, CancellationToken cancellationToken) {
			string steamCMDShell = Path2.Combine(steamCMDDir, "steamcmd.sh");

			// macOS and Linux have a different path.
			string steamCMDArchive = OS.GetName() switch {
				"macOS" => Path2.Combine(steamCMDDir, "steamcmd_osx.tar.gz"),
				_ => Path2.Combine(steamCMDDir, "steamcmd_linux.tar.gz")
			};

			// Unzip in memory and extract to the destination folder.

			GD.Print($"Decompressing SteamCMD and extracting the tarball that's inside...");
			using (TarReader archive = new TarReader(new GZipStream(download, CompressionMode.Decompress))) {
				while (true) {
					cancellationToken.ThrowIfCancellationRequested();

					TarEntry? entry = await archive.GetNextEntryAsync(false, cancellationToken).ConfigureAwait(false);
					if (entry == null) break;

					string name = entry.Name;
					if (name[0] == '/') name = name[1..];
					if (entry.EntryType is not TarEntryType.GlobalExtendedAttributes) {
						// ^ This is how TarFile.ExtractToDir does it.
						string dst = Path2.Combine(steamCMDDir, name);
						Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
						try {
							GD.Print($"Extracting: {dst}");
							await entry.ExtractToFileAsync(dst, true, cancellationToken).ConfigureAwait(false);
						} catch (NotSupportedException) { }
					}
				}
			}

			// Start SteamCMD then tell it to run the "quit" command. This will start it, which makes it do its setup, and then close it.
			await Task.Delay(1000); // Delay is here because without it, it crashes on startup. I don't know why.
									// Or, at least, it does on Windows. I can't test it here.
			cancellationToken.ThrowIfCancellationRequested();

			GD.Print($"Running SteamCMD so it can do its setup routine...");
			Process? steamCMDProcess = Process.Start(new ProcessStartInfo {
				FileName = steamCMDShell,
				Arguments = "+quit",
				UseShellExecute = false,
				CreateNoWindow = true
			});
			if (steamCMDProcess == null) throw new InvalidOperationException("Failed to perform first-time startup of SteamCMD.");
			await steamCMDProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

			GD.Print($"Done, SteamCMD is now installed.");
		}

		#endregion
		
		#region OpenStarbound

		/// <summary>
		/// The OpenStarbound version in development as of the last build. This is my "safe fallback" option.
		/// </summary>
		private const string OPENSB_VERSION_AS_OF_BUILD = "0.1.14";

		/// <summary>
		/// Asynchronously download and install the latest version of OpenStarbound for the current operating system into SBMM's data folder.
		/// This is incomplete; <see cref="ImportGameAssetsAsync"/> needs to be called after this.
		/// </summary>
		/// <param name="server">If <see langword="false"/>, install the client. If <see langword="true"/>, install the server (not supported on Mac).</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> which can be used to stop the installation.</param>
		/// <returns></returns>
		/// <exception cref="NotSupportedException">The operating system is not supported.</exception>
		/// <exception cref="OperationCanceledException">The installation is cancelled.</exception>
		/// <exception cref="InvalidOperationException">OpenStarbound is already installed.</exception>
		/// <exception cref="HttpRequestException">Downloading OpenStarbound failed.</exception>
		public static async Task InstallOpenStarboundAsync(bool server, CancellationToken cancellationToken) {
			if (server) {
				if (!NeedsToInstallOpenStarboundServer()) throw new InvalidOperationException("OpenStarbound Server already appears to be installed. This is an application bug; this error should never be reached. Please report it.");
			} else {
				if (!NeedsToInstallOpenStarboundClient()) throw new InvalidOperationException("OpenStarbound already appears to be installed. This is an application bug; this error should never be reached. Please report it.");
			}
			
			string localSBInstallDir = Directories.GetLocalStarboundInstallDirectory();

			// Get the name of the .zip archive to download off of GitHub.
			string os = OS.GetName();
			string cpu = OS.GetProcessorName();
			string installationName;
			string distCapitalized = server ? "Server" : "Client";
			string distLowercase = server ? "server" : "client";

			if (os == "Windows") {
				installationName = $"OpenStarbound-Windows-{distCapitalized}.zip";
			} else if (os == "Linux") {
				installationName = $"OpenStarbound-Linux-Clang-{distCapitalized}.zip";
			} else if (os == "macOS") {
				if (server) {
					throw new NotSupportedException("OpenStarbound: There is no server application for Mac. If you are seeing this, this is a bug in the program (this error should have never been reached). Please report it.");
				}
				if (cpu.Contains("Apple")) {
					installationName = "OpenStarbound-macOS-Silicon-Client.zip";
				} else {
					installationName = "OpenStarbound-macOS-Intel-Client.zip";
				}
			} else {
				throw new NotSupportedException($"OpenStarbound: Operating System {os} is not supported.");
			}

			GD.Print($"The asset we need to install is {installationName}");

			// Try downloading it from GitHub. If the latest version fails, fall back.
			const string installationURLFormat = "https://github.com/OpenStarbound/OpenStarbound/releases/download/v{0}/{1}";
			string version = await GetCurrentInDevOSBVersionAsync();
			cancellationToken.ThrowIfCancellationRequested();

			Stream download;
			try {
				GD.Print($"Going to try downloading version {version}, but this is probably a dev build that isn't out yet...");
				download = await SBModManagerGlobals.HTTP_CLIENT.GetStreamAsync(string.Format(installationURLFormat, version, installationName), cancellationToken);
				
			} catch (HttpRequestException http) {
				if (http.StatusCode == System.Net.HttpStatusCode.NotFound) {
					GD.PushWarning($"OpenStarbound version {version} could not be found, but this is okay! It's probably just the in-development version and isn't out yet.");
					GD.Print($"Going to try the known good version ({OPENSB_VERSION_AS_OF_BUILD}) instead...");
					await Task.Delay(4000); // Possible mitigation for being rate limited by github.
				} else {
					GD.PushError($"Cannot get OpenStarbound version {version}: {http.Message}");
				}
				download = await SBModManagerGlobals.HTTP_CLIENT.GetStreamAsync(string.Format(installationURLFormat, OPENSB_VERSION_AS_OF_BUILD, installationName), cancellationToken);
			}

			// Extract the downloaded archive.
			using ZipArchive archive = new ZipArchive(download, ZipArchiveMode.Read);
			if (os != "Windows") {
				GD.Print("Decompressing and extracting the tarball that's inside...");
				Stream clientTar = archive.GetEntry($"{distLowercase}.tar")!.Open();
				Directory.CreateDirectory(localSBInstallDir);
				TarFile.ExtractToDirectory(clientTar, localSBInstallDir, true);
				// Almost there. This now creates a client_distribution or server_distribution folder which we don't want.

				// Move everything out, and then delete the old folder.
				DirectoryInfo clientDistro = new DirectoryInfo(Path2.Combine(localSBInstallDir, $"{distLowercase}_distribution"));
				foreach (DirectoryInfo child in clientDistro.GetDirectories()) {
					GD.Print($"Copied directory {child.Name}...");
					Directories.CopyDirectoryOverwrite(child.FullName, Path2.Combine(localSBInstallDir, child.Name), CancellationToken.None);
				}

				GD.Print($"Cleaning up...");
				clientDistro.Delete(true);
				GD.Print("Done.");
			} else {
				GD.Print($"Extracting the zip file...");
				archive.ExtractToDirectory(localSBInstallDir, true);
				GD.Print("Done.");
			}
		}

		/// <summary>
		/// The final step for OpenStarbound, this copies <c>packed.pak</c> and <c>tiled</c> into the OpenStarbound installation.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException">The steam installation was not found.</exception>
		/// <exception cref="OperationCanceledException">The installation is cancelled.</exception>
		public static Task ImportGameAssetsAsync(CancellationToken cancellationToken) {
			string localSBInstallDir = Directories.GetLocalStarboundInstallDirectory();
			string? steamSBInstall = SteamTools.GetStarboundDirectory();

			GD.Print("Porting over Starbound assets...");
			if (steamSBInstall == null) {
				throw new InvalidOperationException($"OpenStarbound installation is incomplete: Steam installation directory of Starbound was not found. Please install Starbound and relaunch the app.");
			}

			GD.Print("Finding assets/packed.pak and tiled...");
			string packedPak = Path2.Combine(steamSBInstall, "assets", "packed.pak");
			string tiledDir = Path2.Combine(steamSBInstall, "tiled");
			if (!File.Exists(packedPak)) {
				throw new InvalidOperationException("OpenStarbound installation is incomplete: Required file \"packed.pak\" (in the assets folder) does not exist in your Steam installation of Starbound.");
			}
			if (!Directory.Exists(tiledDir)) {
				throw new InvalidOperationException("OpenStarbound installation is incomplete: Required folder \"tiled\" does not exist in your Steam installation of Starbound.");
			}

			GD.Print("Copying packed.pak...");
			File.Copy(packedPak, Path2.Combine(localSBInstallDir, "assets", "packed.pak"), true);

			GD.Print("Copying tiled...");
			Directories.CopyDirectoryOverwrite(tiledDir, Path2.Combine(localSBInstallDir, "tiled"), cancellationToken);

			GD.Print("Done.");
			return Task.CompletedTask;
		}

		/// <summary>
		/// Reads OpenStarbound's <c>_metadata</c> for its version.
		/// </summary>
		/// <returns></returns>
		private static async Task<string> GetCurrentInDevOSBVersionAsync() {
			string json = await SBModManagerGlobals.HTTP_CLIENT.GetStringAsync("https://raw.githubusercontent.com/OpenStarbound/OpenStarbound/refs/heads/main/assets/opensb/_metadata");
			Variant parsed = StarboundJsonSanitizer.ParseString(json);
			if (parsed.VariantType == Variant.Type.Dictionary) {
				ModMetadata metadata = new ModMetadata("opensb", (GDDictionary)parsed, 0);
				return metadata.Version;
			} else {
				throw new InvalidOperationException("Failed to parse or download _metadata from OpenStarbound's repository.");
			}
		}

		#endregion

		#region Implementation

		/// <summary>
		/// Returns <see langword="true"/> if any setup tasks need to be done.
		/// </summary>
		/// <returns></returns>
		public static bool ShouldPerformSetup() {
			return NeedsToInstallSteamCMD() || NeedsToInstallOpenStarboundClient() || NeedsToInstallOpenStarboundServer() || NeedsToInstallStarboundAssets();
		}

		/// <summary>
		/// Performs the first-time setup, completing only the steps that are necessary.
		/// </summary>
		/// <param name="progressWindow">A window to reflect upon the progress to the user. This is expected to have been newly created.</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop setup. Should be the same one as is used in <paramref name="progressWindow"/>.</param>
		public static async Task PerformSetupAsync(GeneralProgressWindow progressWindow, CancellationToken cancellationToken) {
			
			progressWindow.SetProgress(0.00f);
			if (NeedsToInstallSteamCMD()) {
				GD.Print($"The user needs to install SteamCMD. Doing that now.");
				progressWindow.SetStatus("Installing SteamCMD...\n(This will take about 20 seconds)", "Performing first-time setup");
				try {
					await InstallSteamCMDAsync(cancellationToken);
				} catch (OperationCanceledException) {
				} catch (Exception exc) {
					OS.Alert(exc.Message, "An error occurred!");
				}
			}

			progressWindow.SetProgress(0.25f);
			if (NeedsToInstallOpenStarboundClient()) {
				GD.Print($"The user needs to install OpenStarbound's client. Doing that now.");
				progressWindow.SetStatus("Installing OpenStarbound Client...", "Performing first-time setup");
				try {
					await InstallOpenStarboundAsync(false, cancellationToken);
				} catch (OperationCanceledException) {
				} catch (Exception exc) {
					OS.Alert(exc.Message, "An error occurred!");
				}
			}

			progressWindow.SetProgress(0.50f);
			if (NeedsToInstallOpenStarboundServer()) {
				GD.Print($"The user needs to install OpenStarbound's server. Doing that now.");
				progressWindow.SetStatus("Installing OpenStarbound Server...", "Performing first-time setup");
				try {
					await InstallOpenStarboundAsync(true, cancellationToken);
				} catch (OperationCanceledException) {
				} catch (Exception exc) {
					OS.Alert(exc.Message, "An error occurred!");
				}
			}

			progressWindow.SetProgress(0.75f);
			if (NeedsToInstallStarboundAssets()) {
				GD.Print($"The user needs to import Starbound's game data into OpenStarbound. Doing that now.");
				progressWindow.SetStatus("Importing Starbound Assets...", "Performing first-time setup");
				try {
					await ImportGameAssetsAsync(cancellationToken);
				} catch (OperationCanceledException) {
				} catch (Exception exc) {
					OS.Alert(exc.Message, "An error occurred!");
				}
			}

			progressWindow.SetStatus("Done!", "Done!");
			progressWindow.SetProgress(1.000f);
		}

		#endregion

	}
}
