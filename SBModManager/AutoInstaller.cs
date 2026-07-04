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

using HttpClient = System.Net.Http.HttpClient;

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
		public static bool NeedsToInstallOpenStarbound() {
			string starboundDir = Directories.GetLocalStarboundInstallDirectory();
			string starboundApp = Directories.GetLocalStarboundProgram();
			if (!File.Exists(Path2.Combine(starboundDir, "assets", "opensb.pak"))) return true;
			return !File.Exists(starboundApp);
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

			using HttpClient client = new HttpClient();
			using Stream download = await client.GetStreamAsync(downloadLink).ConfigureAwait(false);
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
			using (ZipArchive archive = new ZipArchive(download, ZipArchiveMode.Read)) {
				cancellationToken.ThrowIfCancellationRequested();
				await archive.ExtractToDirectoryAsync(steamCMDDir, cancellationToken).ConfigureAwait(false);
			}

			// Start SteamCMD then tell it to run the "quit" command. This will start it, which makes it do its setup, and then close it.
			await Task.Delay(1000); // Delay is here because without it, it crashes on startup. I don't know why.
			cancellationToken.ThrowIfCancellationRequested();
			Process? steamCMDProcess = Process.Start(new ProcessStartInfo {
				FileName = steamCMDExe,
				Arguments = "+quit",
				UseShellExecute = false,
				CreateNoWindow = true
			});
			if (steamCMDProcess == null) throw new InvalidOperationException("Failed to perform first-time startup of SteamCMD.");
			await steamCMDProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

		}
		private static async Task InstallSteamCMDMacLinuxAsync(string steamCMDDir, Stream download, CancellationToken cancellationToken) {
			string steamCMDShell = Path2.Combine(steamCMDDir, "steamcmd.sh");

			// macOS and Linux have a different path.
			string steamCMDArchive = OS.GetName() switch {
				"macOS" => Path2.Combine(steamCMDDir, "steamcmd_osx.tar.gz"),
				_ => Path2.Combine(steamCMDDir, "steamcmd_linux.tar.gz")
			};

			// Unzip in memory and extract to the destination folder.
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
							await entry.ExtractToFileAsync(dst, true, cancellationToken).ConfigureAwait(false);
						} catch (NotSupportedException) { }
					}
				}
			}

			// Start SteamCMD then tell it to run the "quit" command. This will start it, which makes it do its setup, and then close it.
			await Task.Delay(1000); // Delay is here because without it, it crashes on startup. I don't know why.
									// Or, at least, it does on Windows. I can't test it here.
			cancellationToken.ThrowIfCancellationRequested();
			Process? steamCMDProcess = Process.Start(new ProcessStartInfo {
				FileName = steamCMDShell,
				Arguments = "+quit",
				UseShellExecute = false,
				CreateNoWindow = true
			});
			if (steamCMDProcess == null) throw new InvalidOperationException("Failed to perform first-time startup of SteamCMD.");
			await steamCMDProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

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
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> which can be used to stop the installation.</param>
		/// <returns></returns>
		/// <exception cref="NotSupportedException">The operating system is not supported.</exception>
		/// <exception cref="OperationCanceledException">The installation is cancelled.</exception>
		/// <exception cref="InvalidOperationException">OpenStarbound is already installed.</exception>
		/// <exception cref="HttpRequestException">Downloading OpenStarbound failed.</exception>
		public static async Task InstallOpenStarboundAsync(CancellationToken cancellationToken) {
			if (!NeedsToInstallOpenStarbound()) throw new InvalidOperationException("OpenStarbound already appears to be installed.");
			
			string localSBInstallDir = Directories.GetLocalStarboundInstallDirectory();

			// Get the name of the .zip archive to download off of GitHub.
			string os = OS.GetName();
			string cpu = OS.GetProcessorName();
			string installationName;
			if (os == "Windows") {
				installationName = "OpenStarbound-Windows-Client.zip";
			} else if (os == "Linux") {
				installationName = "OpenStarbound-Linux-Clang-Client.zip";
			} else if (os == "macOS") {
				if (cpu.Contains("Apple")) {
					installationName = "OpenStarbound-macOS-Silicon-Client.zip";
				} else {
					installationName = "OpenStarbound-macOS-Intel-Client.zip";
				}
			} else {
				throw new NotSupportedException($"OpenStarbound: Operating System {os} is not supported.");
			}

			// Try downloading it from GitHub. If the latest version fails, fall back.
			const string installationURLFormat = "https://github.com/OpenStarbound/OpenStarbound/releases/download/v{0}/{1}";
			string version = await GetCurrentInDevOSBVersionAsync();
			cancellationToken.ThrowIfCancellationRequested();

			using HttpClient client = new HttpClient();
			Stream download;
			try {
				download = await client.GetStreamAsync(string.Format(installationURLFormat, version, installationName), cancellationToken);
			} catch (HttpRequestException) {
				GD.PushError("Cannot get the latest version of OpenStarbound because it failed to download.");
				download = await client.GetStreamAsync(string.Format(installationURLFormat, OPENSB_VERSION_AS_OF_BUILD, installationName), cancellationToken);
			}

			// Extract the downloaded archive.
			using (ZipArchive archive = new ZipArchive(download, ZipArchiveMode.Read)) {
				if (os != "Windows") {
					Stream clientTar = archive.GetEntry("client.tar")!.Open();
					TarFile.ExtractToDirectory(clientTar, localSBInstallDir, true);
					// Almost there. This now creates a client_distribution folder which we don't want.

					// Move everything out, and then delete the old folder.
					DirectoryInfo clientDistro = new DirectoryInfo(Path2.Combine(localSBInstallDir, "client_distribution"));
					foreach (DirectoryInfo child in clientDistro.GetDirectories()) {
						Directory.Move(child.FullName, Path2.Combine(localSBInstallDir, child.Name));
					}
					clientDistro.Delete(false);
				} else {
					archive.ExtractToDirectory(localSBInstallDir);
				}
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
			if (steamSBInstall == null) throw new InvalidOperationException($"OpenStarbound installation is incomplete: Steam installation directory of Starbound was not found. Please install Starbound and relaunch the app.");
			
			string packedPak = Path2.Combine(steamSBInstall, "assets", "packed.pak");
			string tiledDir = Path2.Combine(steamSBInstall, "tiled");
			if (!File.Exists(packedPak)) {
				throw new InvalidOperationException("OpenStarbound installation is incomplete: Required file \"packed.pak\" (in the assets folder) does not exist in your Steam installation of Starbound.");
			}
			if (!Directory.Exists(tiledDir)) {
				throw new InvalidOperationException("OpenStarbound installation is incomplete: Required folder \"tiled\" does not exist in your Steam installation of Starbound.");
			}

			return Task.Run(() => {
				cancellationToken.ThrowIfCancellationRequested();
				File.Copy(packedPak, Path2.Combine(localSBInstallDir, "assets", "packed.pak"));
				Directories.CopyDirectory(tiledDir, Path2.Combine(localSBInstallDir, "tiled"), cancellationToken);
			}, cancellationToken);
		}

		/// <summary>
		/// Reads OpenStarbound's <c>_metadata</c> for its version.
		/// </summary>
		/// <returns></returns>
		private static async Task<string> GetCurrentInDevOSBVersionAsync() {
			using HttpClient client = new HttpClient();
			string json = await client.GetStringAsync("https://raw.githubusercontent.com/OpenStarbound/OpenStarbound/refs/heads/main/assets/opensb/_metadata");
			Variant parsed = StarboundJsonSanitizer.ParseString(json);
			if (parsed.VariantType == Variant.Type.Dictionary) {
				ModMetadata metadata = new ModMetadata((GDDictionary)parsed, 0);
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
			return NeedsToInstallSteamCMD() || NeedsToInstallOpenStarbound() || NeedsToInstallStarboundAssets();
		}

		/// <summary>
		/// Performs the first-time setup, completing only the steps that are necessary.
		/// </summary>
		/// <param name="progressWindow">A window to reflect upon the progress to the user. This is expected to have been newly created.</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop setup. Should be the same one as is used in <paramref name="progressWindow"/>.</param>
		public static async Task PerformSetupAsync(GeneralProgressWindow progressWindow, CancellationToken cancellationToken) {
			
			progressWindow.SetProgress(0.000f);
			if (NeedsToInstallSteamCMD()) {
				progressWindow.SetStatus("Installing SteamCMD...\n(This will take about 20 seconds)", "Performing first-time setup...");
				try {
					await InstallSteamCMDAsync(cancellationToken);
				} catch (OperationCanceledException) {
				} catch (Exception exc) {
					OS.Alert(exc.Message, "An error occurred!");
				}
			}

			progressWindow.SetProgress(0.333f);
			if (NeedsToInstallOpenStarbound()) {
				progressWindow.SetStatus("Installing OpenStarbound...", "Performing first-time setup...");
				try {
					await InstallOpenStarboundAsync(cancellationToken);
				} catch (OperationCanceledException) {
				} catch (Exception exc) {
					OS.Alert(exc.Message, "An error occurred!");
				}
			}

			progressWindow.SetProgress(0.666f);
			if (NeedsToInstallStarboundAssets()) {
				progressWindow.SetStatus("Importing Starbound Assets...", "Performing first-time setup...");
				try {
					await ImportGameAssetsAsync(cancellationToken);
				} catch (OperationCanceledException) {
				} catch (Exception exc) {
					OS.Alert(exc.Message, "An error occurred!");
				}
			}

			progressWindow.SetStatus("Done!");
			progressWindow.SetProgress(1.000f);
		}

		#endregion

	}
}
