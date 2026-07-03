using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Attributes;

using HttpClient = System.Net.Http.HttpClient;
using FileAccess = System.IO.FileAccess;
using SBModManager.SteamInterop;
using System.Formats.Tar;
using SBModManager.Other;
using SBModManager.Menus.Windows;

namespace SBModManager.Menus {
	public partial class ProgramSettingsWindow : Window {

		/// <summary>
		/// The note that describes where to install SteamCMD. It may also have a link to auto-install it.
		/// </summary>
		[Import, AllowNull]
		public RichTextLabel SteamCMDNote { get; }

		/// <summary>
		/// The text area where the location of SteamCMD can be manually input.
		/// </summary>
		[Import, AllowNull]
		public LineEdit SteamCMDLocationInput { get; }

		/// <summary>
		/// The text area where the location of Starbound can be manually input.
		/// </summary>
		[Import, AllowNull]
		public LineEdit StarboundLocationInput { get; }

		/// <summary>
		/// The button which opens a file dialog that can be used to choose the location of Starbound.
		/// </summary>
		[Import, AllowNull]
		public Button StarboundFileDialogButton { get; }

		/// <summary>
		/// The actual file dialog that is used to choose Starbound.
		/// </summary>
		[Import, AllowNull]
		public FileDialog StarboundFileDialog { get; }

		[Import, AllowNull]
		public LineEdit OpenStarboundLocationInput { get; }

		[Import, AllowNull]
		public Button OpenStarboundDownload { get; }

		[Import, AllowNull]
		public Button OpenStarboundFileDialogButton { get; }

		[Import, AllowNull]
		public FileDialog OpenStarboundFileDialog { get; }

		[Import, AllowNull]
		public Button OKButton { get; }

		[Import, AllowNull]
		public RichTextLabel NoStarboundRequiredNotice { get; }

		[Import, AllowNull]
		public Button PruneModCatalogButton { get; }

		private CancellationTokenSource? _downloadCancellation = null;
		private Task? _steamCmdInstallationTask = null;
		private GeneralProgressWindow? _steamCMDDownloadProgress;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);

			SetSteamCMDNoteText();
			SteamCMDNote.MetaClicked += OnSteamCMDMetaClicked;
			StarboundFileDialogButton.Pressed += OnStarboundFileDialogButtonPressed;
			StarboundFileDialog.DirSelected += OnStarboundDirSelected;
			OpenStarboundDownload.Pressed += OnOpenStarboundDownloadPressed;
			OpenStarboundFileDialogButton.Pressed += OnOpenStarboundFileDialogButtonPressed;
			OpenStarboundFileDialog.FileSelected += OnOpenStarboundFileSelected;
			PruneModCatalogButton.Pressed += OnPruneModCatalogPressed;
			OKButton.Pressed += OnOKPressed;

			CloseRequested += OnCloseRequested;
			VisibilityChanged += OnSelfVisibilityChanged;

			StarboundFileDialog.CurrentPath = SteamTools.GetStarboundDirectory() ?? string.Empty;

			OpenStarboundDownload.TooltipText = "Open a link to download the latest version of OpenStarbound.\n\n";
			string os = OS.GetName();
			if (os == "Windows") {
				OpenStarboundDownload.TooltipText += "As a Windows user, you want [color=#afa]OpenStarbound-Windows-Client.zip[/color] at the bottom of the page.";
			} else if (os == "macOS") {
				bool isIntel = OS.GetProcessorName().Contains("Intel"); // lmfao
				string chip = isIntel ? "Intel" : "Silicon";
				OpenStarboundDownload.TooltipText += $"As a Mac user, you want [color=#afa]OpenStarbound-macOS-{chip}-Client.zip[/color] at the bottom of the page.";
			} else if (os == "Linux") {
				string howToIdentifyALinuxUser = "Linux user";
				if (OS.GetDistributionName() == "Arch") {
					howToIdentifyALinuxUser = "Linux user (btw)";
					// I'm so sorry.
				}
				OpenStarboundDownload.TooltipText += $"As a {howToIdentifyALinuxUser}, you want [color=#afa]OpenStarbound-Linux-Clang-Client.zip[/color] at the bottom of the page.";
			} else {
				OpenStarboundDownload.TooltipText += $"[color=#f77]Your OS ({os}) does not have a native installation of OpenStarbound. You may need to use an emulator, or build it yourself.[/color]";
			}
		}

		private void OnOKPressed() => OnCloseRequested();

		private void OnSelfVisibilityChanged() {
			if (Visible) {
				string sbInstall = Directories.GetPrivateStarboundInstallDirectory();
				if (Directory.Exists(sbInstall)) {
					NoStarboundRequiredNotice.SelfModulate = Colors.White;
				} else {
					NoStarboundRequiredNotice.SelfModulate = Colors.Transparent;
				}
				SetSteamCMDNoteText();
				SteamCMDLocationInput.Text = ProgramSettings.SteamCMD?.FullName ?? string.Empty;
			}
		}

		private void OnOpenStarboundFileSelected(string path) {
			try {
				if (File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)) {
					OpenStarboundLocationInput.Text = path;
				} else {
					OpenStarboundLocationInput.Text = string.Empty;
				}
			} catch {
				OpenStarboundLocationInput.Text = string.Empty;
			}
		}

		private void OnStarboundDirSelected(string path) {
			try {
				if (Directory.Exists(path)) {
					StarboundLocationInput.Text = path;
				} else {
					StarboundLocationInput.Text = string.Empty;
				}
			} catch {
				StarboundLocationInput.Text = string.Empty;
			}
		}

		private void OnOpenStarboundDownloadPressed() {
			OS.ShellOpen("https://github.com/OpenStarbound/OpenStarbound/releases/latest");
		}

		private void OnStarboundFileDialogButtonPressed() {
			StarboundFileDialog.Show();
		}

		private void OnOpenStarboundFileDialogButtonPressed() {
			OpenStarboundFileDialog.Show();
		}


		private void OnPruneModCatalogPressed() {
			throw new NotImplementedException();
		}

		private void InstallStarboundIfNeeded() {
			string destination = Directories.GetPrivateStarboundInstallDirectory();
			string starboundFolderLocation = StarboundLocationInput.Text;
			string openStarboundZipLocation = OpenStarboundLocationInput.Text;
			if (string.IsNullOrWhiteSpace(starboundFolderLocation)) return;
			if (string.IsNullOrWhiteSpace(openStarboundZipLocation)) return;
			try {
				if (!Directory.Exists(starboundFolderLocation)) {
					OS.Alert("The provided folder path for Starbound does not exist.", "Incorrect Install");
					return;
				}
			} catch {
				OS.Alert("The provided folder path for Starbound is not a valid path.", "Incorrect Install");
				return;
			}
			try {
				if (!File.Exists(openStarboundZipLocation)) {
					OS.Alert("The provided zip file path for OpenStarbound does not exist.", "Incorrect Install");
					return;
				}
			} catch {
				OS.Alert("The provided zip file path for OpenStarbound is not a valid path.", "Incorrect Install");
				return;
			}

			string destinationAssets = Path2.Combine(destination, "assets");
			string destinationTiled = Path2.Combine(destination, "tiled");

			FileInfo packedpak = new FileInfo(Path2.Combine(starboundFolderLocation, "assets", "packed.pak"));
			if (!packedpak.Exists) {
				OS.Alert("Unable to find a file named \"packed.pak\"\ninside of an \"assets\" folder of your Starbound\ninstallation directory.", "Incorrect Install");
				return;
			}

			DirectoryInfo tiled = new DirectoryInfo(Path2.Combine(starboundFolderLocation, "tiled"));
			if (!tiled.Exists) {
				OS.Alert("Unable to find a folder named \"tiled\" in your\nStarbound installation directory.", "Incorrect Install");
				return;
			}

			try {
				using ZipArchive archive = new ZipArchive(File.OpenRead(openStarboundZipLocation), ZipArchiveMode.Read);
				if (OS.GetName() != "Windows") {
					// Mac and Linux have "client.tar"
					Stream clientTar = archive.GetEntry("client.tar")!.Open();
					TarFile.ExtractToDirectory(clientTar, destination, true);
					// Almost there. This now creates a client_distribution folder which we don't want.

					DirectoryInfo clientDistro = new DirectoryInfo(Path2.Combine(destination, "client_distribution"));
					foreach (DirectoryInfo child in clientDistro.GetDirectories()) {
						Directory.Move(child.FullName, Path2.Combine(destination, child.Name));
					}
					clientDistro.Delete(false);

					// Now we're good.
				} else {
					archive.ExtractToDirectory(destination);
				}
			} catch {
				OS.Alert("Failed to extract OpenStarbound's zip file.", "Error");
				return;
			}

			try {
				packedpak.CopyTo(Path2.Combine(destinationAssets, "packed.pak"));
				Directories.CopyDirectory(tiled.FullName, destinationTiled);
			} catch {
				OS.Alert("Failed to copy the required Starbound data.", "Error");
				return;
			}

			File.WriteAllText(
				Path2.Combine(destination, "DO NOT MODIFY, READ ME.TXT"), 
				"This is a template/shared installation of OpenStarbound.\nDO NOT INSTALL MODS HERE. It doesn't work (even if there is a mods folder)."
			);
		}

		private void OnCloseRequested() {
			_downloadCancellation?.Cancel();
			try {
				_steamCmdInstallationTask?.Wait();
			} catch (OperationCanceledException) { }

			_steamCmdInstallationTask = null;
			_downloadCancellation = null;
			_steamCMDDownloadProgress?.Free();
			_steamCMDDownloadProgress = null;

			try {
				if (File.Exists(SteamCMDLocationInput.Text)) {
					ProgramSettings.SteamCMD = new FileInfo(SteamCMDLocationInput.Text);
				}
			} catch { }
			StarboundFileDialog.Hide();
			OpenStarboundFileDialog.Hide();
			ProgramSettings.Save();
			InstallStarboundIfNeeded();

			StarboundLocationInput.Text = string.Empty;
			OpenStarboundLocationInput.Text = string.Empty;
			
			Hide();
		}

		#region SteamCMD

		private void OnSteamCMDMetaClicked(Variant meta) {
			if (meta.VariantType == Variant.Type.String) {
				string metaString = (string)meta;
				if (metaString == "AutoInstallHint") {
					if (_steamCmdInstallationTask != null && !_steamCmdInstallationTask.IsCompleted) {
						return;
					}
					PackedScene launchPromptScene = GD.Load<PackedScene>("res://popups/progress_window.tscn");
					GeneralProgressWindow launching = launchPromptScene.Instantiate<GeneralProgressWindow>();
					_downloadCancellation = new CancellationTokenSource();
					launching.ShowWithCancellation(_downloadCancellation);
					launching.SetStatus("Downloading SteamCMD...");
					AddChild(launching);
					_steamCMDDownloadProgress = launching;
					_steamCmdInstallationTask = InstallSteamCMDAsync(_downloadCancellation.Token);
				} else if (Uri.TryCreate(metaString, default, out _)) {
					OS.ShellOpen(metaString);
				}
			}
		}

		private async Task InstallSteamCMDAsync(CancellationToken cancellationToken) {

			string steamCMDDir = Directories.GetLocalSteamCMDInstallationDirectory();
			string steamCMDZip = Path2.Combine(steamCMDDir, "steamcmd.zip");
			string steamCMDExe = Path2.Combine(steamCMDDir, "steamcmd.exe");
			if (File.Exists(steamCMDExe)) return;

			_steamCMDDownloadProgress?.SetStatus("Downloading SteamCMD...");
			_steamCMDDownloadProgress?.SetProgress(float.NaN);
			{
				Directory.CreateDirectory(steamCMDDir);
				using HttpClient client = new HttpClient();

				using Stream download = await client.GetStreamAsync("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip", cancellationToken).ConfigureAwait(false);
				if (cancellationToken.IsCancellationRequested) return;

				using FileStream destinationFile = new FileStream(steamCMDZip, FileMode.Create);
				if (cancellationToken.IsCancellationRequested) return;
				download.CopyTo(destinationFile); // Must be synchronous.
			}

			if (cancellationToken.IsCancellationRequested) return;

			_steamCMDDownloadProgress?.SetStatus("Extracting SteamCMD...");
			_steamCMDDownloadProgress?.SetProgress(0.333f);
			{
				using FileStream reader = File.Open(
					steamCMDZip,
					FileMode.Open,
					FileAccess.Read,
					FileShare.Read
				);
				using ZipArchive archive = new ZipArchive(reader, ZipArchiveMode.Read);
				await archive.ExtractToDirectoryAsync(steamCMDDir, cancellationToken).ConfigureAwait(false);
				reader.Dispose();
				File.Delete(steamCMDZip);
			}

			if (cancellationToken.IsCancellationRequested) return;

			_steamCMDDownloadProgress?.SetStatus("Performing First Time Setup...");
			_steamCMDDownloadProgress?.SetProgress(0.666f);

			await Task.Delay(1000);
			// This is one of those "this fixes a bug but I don't know why" lines.
			// Intuitively I assume it has something to do with the fact that I'm interacting with the filesystem from a background thread,
			// but it's just one thread. I'm not doing any out of order access because I *can't* do any out of order access.
			// Without this line, SteamCMD will crash on startup.

			// I'll put it to use:
			_steamCMDDownloadProgress?.SetProgress(0.75f);

			// The illusion of progress. I am sinister.

			Process? steamCMDProcess = Process.Start(new ProcessStartInfo {
				FileName = steamCMDExe,
				Arguments = "+exit",
				UseShellExecute = false,
				CreateNoWindow = true
			});
			if (steamCMDProcess == null) throw new InvalidOperationException("Failed to perform first-time startup of SteamCMD");
			await steamCMDProcess.WaitForExitAsync(cancellationToken);
		}

		public override void _Process(double delta) {
			if (_steamCmdInstallationTask != null) {
				if (_steamCmdInstallationTask.IsCompleted) {
					if (_steamCmdInstallationTask.IsFaulted) {
						//SteamCMDNote.Text = $"[color=#ff7]ERROR: {_steamCmdInstallationTask.Exception.Message}[/color]";
						OS.Alert(_steamCmdInstallationTask.Exception.Message, "Failed to download SteamCMD");
					} else {
						string steamCMDDir = Directories.GetLocalSteamCMDInstallationDirectory();
						string steamCMDExe = Path2.Combine(steamCMDDir, "steamcmd.exe");
						SteamCMDLocationInput.Text = steamCMDExe;
					}
					_steamCmdInstallationTask = null;
					_downloadCancellation = null;
					_steamCMDDownloadProgress?.Free();
					_steamCMDDownloadProgress = null;
				}
			}
		}

		/// <summary>
		/// Sets the text of the SteamCMD install note based on the OS of the user.
		/// </summary>
		private void SetSteamCMDNoteText() {
			string baseTextFormat = "You can download it from the [color=#aff][url={0}]Valve Developer Community[/url][/color]";
			string os = OS.GetName();
			if (os == "Windows") {
				SteamCMDNote.Text = string.Format(baseTextFormat, "https://developer.valvesoftware.com/wiki/SteamCMD#_Windows") + ", or [color=#afa][url=AutoInstallHint]have SBMM install it for you.[/url][/color]";
			} else if (os == "Linux") {
				SteamCMDNote.Text = string.Format(baseTextFormat, "https://developer.valvesoftware.com/wiki/SteamCMD#_Linux") + ".";
			} else if (os == "macOS") {
				SteamCMDNote.Text = string.Format(baseTextFormat, "https://developer.valvesoftware.com/wiki/SteamCMD#_macOS") + ".";
			} else {
				SteamCMDNote.Text = $"SteamCMD may not work on {os}. Manual installation instructions for Unix are on the [color=#aff][url=https://developer.valvesoftware.com/wiki/SteamCMD#Manually]Valve Developer Community[/url][/color].";
			}
		}

		#endregion


	}
}
