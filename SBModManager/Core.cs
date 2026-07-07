using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Godot;

using SBModManager.Attributes;
using SBModManager.GUI;
using SBModManager.IO;
using SBModManager.Menus;
using SBModManager.Menus.Windows;
using SBModManager.ModInstances;
using SBModManager.Other;
using SBModManager.SteamInterop;

namespace SBModManager {
	public sealed partial class Core : Panel {

		/// <summary>
		/// Allows access to the main window from outside.
		/// </summary>
		[AllowNull]
		public static Core Instance { get; private set; }

		/// <summary>
		/// The topbar button to run the current pack.
		/// </summary>
		[AllowNull, Import]
		public TextureButton RunButton { get; }

		/// <summary>
		/// The topbar button to run the current pack as a server.
		/// </summary>
		[AllowNull, Import]
		public TextureButton RunServerButton { get; }

		/// <summary>
		/// The topbar button to create a new modpack.
		/// </summary>
		[AllowNull, Import]
		public TextureButton NewModpackButton { get; }

		/// <summary>
		/// The topbar button to duplicate the selected modpack.
		/// </summary>
		[AllowNull, Import]
		public TextureButton DuplicateModpackButton { get; }

		/// <summary>
		/// The topbar button to import a modpack from a list.
		/// </summary>
		[AllowNull, Import]
		public TextureButton ImportModpackButton { get; }

		/// <summary>
		/// The topbar button to configure a modpack's settings and mods.
		/// </summary>
		[AllowNull, Import]
		public TextureButton EditModpackButton { get; }

		/// <summary>
		/// The topbar button to delete a modpack.
		/// </summary>
		[AllowNull, Import]
		public TextureButton DeleteModpackButton { get; }

		/// <summary>
		/// The button to open help.md
		/// </summary>
		[Import, AllowNull]
		public TextureButton HelpButton { get; }

		/// <summary>
		/// The list of modpacks.
		/// </summary>
		[AllowNull, Import]
		public HFlowContainer ModpacksList { get; }

		/// <summary>
		/// The entire window for managing modpacks.
		/// </summary>
		[AllowNull, Import]
		public ModpackManagementWindow ModpackManagement { get; }

		/// <summary>
		/// The file dialog to import a modpack.
		/// </summary>
		[Import, AllowNull]
		public FileDialog ImportModpackDialog { get; }

		/// <summary>
		/// The file dialog to export a modpack.
		/// </summary>
		/// <remarks>
		/// FIXME: This is a duplicate of the same dialog on <see cref="ModpackManagementWindow"/>.
		/// </remarks>
		[Import, AllowNull]
		public FileDialog ExportModpackDialog { get; }

		/// <summary>
		/// The status label at the bottom of the window.
		/// </summary>
		[Import, AllowNull]
		public RichTextLabel Status { get; }

		/// <summary>
		/// A label shown before loading is done to tell people that not all the modpacks are there.
		/// </summary>
		[Import, AllowNull]
		public Label StillLoadingLabel { get; }

		/// <summary>
		/// Every current modpack that is known.
		/// </summary>
		internal List<Modpack> CurrentModpacks { get; } = [];

		/// <summary>
		/// The button associated with <see cref="_currentSelectedModpack"/>.
		/// </summary>
		private ModpackEntryElement? _currentSelectedEntryButton;

		/// <summary>
		/// A reference to the modpack that has been clicked on.
		/// </summary>
		private Modpack? _currentSelectedModpack;

		/// <summary>
		/// This task is the auto-installer setup, or the running task for Starbound (but currently that feature is disabled).
		/// </summary>
		private Task? _autoInstallerSetupOrStarbound;

		/// <summary>
		/// The GUIDs of packs that need to be loaded from disk on the next <see cref="_Process(double)"/> call.
		/// </summary>
		private List<Guid> _pendingPacksToLoad = [];

		/// <summary>
		/// FIXME: Spaghetti bullshit :(
		/// </summary>
		public bool specialHasPendingExport = false;

		/// <summary>
		/// Keeps track of which modpacks are running which components.
		/// </summary>
		private readonly Dictionary<Modpack, (Process? client, Process? server)> _runningModpacks = [];

		private readonly Lock _runningModpacksLock = new Lock();

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			Instance = this;
			WorkshopUpdateInfo.Load();
			_ = WorkshopUpdateInfo.CheckForUpdatesIgnoreCooldownAsync(null);

			RunButton.Pressed += OnRunPressed;
			NewModpackButton.Pressed += OnNewModpackButtonPressed;
			DuplicateModpackButton.Pressed += OnDuplicateModpackButtonPressed;
			ImportModpackButton.Pressed += OnImportModpackButtonPressed;
			EditModpackButton.Pressed += OnEditModpackButtonPressed;
			DeleteModpackButton.Pressed += OnDeleteModpackButtonPressed;
			HelpButton.Pressed += OnHelpButtonPressed;
			ImportModpackDialog.FileSelected += OnModpackImportSelected;
			ExportModpackDialog.FileSelected += OnModpackExportSelected;
			ExportModpackDialog.Canceled += delegate {
				specialHasPendingExport = false;
			};
			Status.MetaClicked += OnStatusMetaClicked;

			if (OS.GetName() == "macOS") {
				RunServerButton.Disabled = true;
				RunServerButton.TooltipText = "[color=#f77]Unavailable.[/color] MacOS does not currently support running game servers. Sorry!";
			} else {
				RunServerButton.Pressed += OnRunServerPressed;
				RunServerButton.TooltipText = "Run a dedicated server for the selected modpack.";
			}

			// Also disable the buttons except the create button.
			HideButtons();
			NewModpackButton.Disabled = false;
			ImportModpackButton.Disabled = false;

			string modpacks = Directories.GetPackDirectory();
			Directory.CreateDirectory(modpacks);

			// Create buttons for all of the user's modpacks.
			foreach (string subdirectory in Directory.GetDirectories(modpacks)) {
				string nameOnly = Path.GetFileName(subdirectory);
				if (Guid.TryParse(nameOnly, out Guid modpackID)) {
					_pendingPacksToLoad.Add(modpackID);
				}
			}

			GD.Print($"Checking if auto-setup is needed...");
			if (AutoInstaller.ShouldPerformSetup()) {
				GD.Print($"Yes, yes it is.");

				GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
				CancellationTokenSource cts = new CancellationTokenSource();
				HideButtons();
				AddChild(progress);
				_autoInstallerSetupOrStarbound = progress.ShowWithCancellation(() => AutoInstaller.PerformSetupAsync(progress, cts.Token), cts, true)
							.ContinueWith(delegate {
								GD.Print($"Auto-setup is complete.");
								_autoInstallerSetupOrStarbound = null;
								ShowButtonsExceptMacServer();
							}, TaskScheduler.FromCurrentSynchronizationContext());
			}

			GetWindow().FilesDropped += OnFilesDropped;

			string projectVersion = ProjectSettings.GetSetting("application/config/version").AsString();
			Version projectVersionInstance = new Version(projectVersion);

			Status.Text = @$"[font_size=14]Starbound Mod Manager
Installed Version: [color=#5f5]{projectVersion}[/color][/font_size]
To report bugs or request features, visit [color=#aff][url]https://github.com/XansWorkshop/StarboundModManager[/url][/color]";

			SBModManagerGlobals.HTTP_CLIENT.GetAsync("https://raw.githubusercontent.com/XansWorkshop/StarboundModManager/refs/heads/master/VERSION").ContinueWith(task => {
				if (task.IsCompletedSuccessfully) {
					try {
						string versionText = task.Result.Content.ReadAsStringAsync().Result;
						Version updateVersion = new Version(versionText.Trim());
						if (updateVersion > projectVersionInstance) {
							Status.Text = @$"[font_size=14]Starbound Mod Manager
Installed Version: [color=#f55]{projectVersion} (Version {updateVersion} is now available! [url=https://github.com/XansWorkshop/StarboundModManager/releases/latest]Click here to get it.[/url])[/color][/font_size]
To report bugs or request features, visit [color=#aff][url]https://github.com/XansWorkshop/StarboundModManager[/url][/color]";
						}
					} catch { }
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}


		private void OnFilesDropped(string[] files) {
			string path = files.First();
			if (Path.GetExtension(path).Equals(".sbmm", StringComparison.OrdinalIgnoreCase)) {
				try {
					FileStream fs = File.OpenRead(path);
					GZipStream decompressor = new GZipStream(fs, CompressionMode.Decompress);
					OnModpackImportSelected(decompressor, path, true); // This closes the streams.
				} catch (Exception exc) {
					OS.Alert(exc.Message, "Failed to import.");
				}
			}
		}

		public override void _Process(double delta) {
			int count = _pendingPacksToLoad.Count;
			if (count > 0) {
				Guid modpackID = _pendingPacksToLoad[^1];
				_pendingPacksToLoad.RemoveAt(count - 1);
				Modpack? modpack = Modpack.LoadFromDisk(modpackID);
				if (modpack != null) {
					CurrentModpacks.Add(modpack);
					CreateButtonForModpack(modpack);
					ModpacksList.MoveChild(StillLoadingLabel, ModpacksList.GetChildCount());
				}
			} else {
				StillLoadingLabel.Visible = false;
				SetProcess(false);
			}
		}

		public override void _UnhandledKeyInput(InputEvent @event) {
			if (@event is InputEventKey key && @event.IsPressed()) {
				if (key.Keycode == Key.Pageup) {
					MovingRichTextLabel.MostRecentTooltip?.ScrollUp();
				} else if (key.Keycode == Key.Pagedown) {
					MovingRichTextLabel.MostRecentTooltip?.ScrollDown();
				}
			}
		}

		private void OnHelpButtonPressed() {
			OS.ShellOpen("https://github.com/EtiTheSpirit/StarboundModManager/blob/master/HELP.md");
		}

		private void OnStatusMetaClicked(Variant meta) {
			if (meta.VariantType == Variant.Type.String && Uri.TryCreate((string)meta, default, out _)) {
				OS.ShellOpen((string)meta);
			}
		}

		private void OnModpackImportSelected(string path) {
			try {
				FileStream stream = File.OpenRead(path);
				GZipStream decompressor = new GZipStream(stream, CompressionMode.Decompress);
				GDDictionary options = ImportModpackDialog.GetSelectedOptions();
				int mode = (int)options["Duplicate Modpack Behavior"];
				OnModpackImportSelected(decompressor, path, mode == 0);
			} catch (Exception exc) when (!exc.IsCancellation()) {
				OS.Alert(exc.Message, "Failed to import modpack!");
			}
		}

		private void OnModpackExportSelected(string path) {
			if (_currentSelectedModpack == null) {
				specialHasPendingExport = false;
				return;
			}

			try {
				FileStream writer = File.Open(path, FileMode.Create, System.IO.FileAccess.Write, FileShare.None);
				GZipStream compressor = new GZipStream(writer, CompressionLevel.SmallestSize);
				GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
				CancellationTokenSource cts = new CancellationTokenSource();
				Modpack currentModpack = _currentSelectedModpack;
				AddChild(progress);
				progress.ShowWithCancellation(async delegate {
					try {
						await PackExportImport.ExportModpackAsync(currentModpack, compressor, progress, cts.Token);
					} catch (Exception exc) {
						OS.Alert(exc.Message, "Failed to export modpack!");
					}
				}, cts, true).ContinueWith(delegate {
					writer.Close();
				}, TaskScheduler.FromCurrentSynchronizationContext());
			} catch (Exception exc) {
				OS.Alert(exc.Message, "Failed to export modpack!");
			} finally {
				specialHasPendingExport = false;
			}
		}

		private Task OnModpackImportSelected(Stream stream, string path, bool importAsNewModpack) {
			GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
			CancellationTokenSource cts = new CancellationTokenSource();
			AddChild(progress);
			return progress.ShowWithCancellation(async delegate {
				try {
					Modpack modpack = await PackExportImport.ImportModpackAsync(stream, importAsNewModpack, progress, cts.Token);
					CurrentModpacks.Add(modpack);
					return modpack;
				} catch (Exception exc) when (!exc.IsCancellation()) {
					OS.Alert(exc.Message, "Failed to import modpack!");
					return null;
				}
			}, cts, true).ContinueWith(delegate (Task<Modpack?> task) {
				stream.Dispose();
				if (task.IsCompletedSuccessfully) {
					task.Result!.IsCorruptedDeleteOnNextRead = false;
					task.Result.SaveAndUpdateInitsAsync(CancellationToken.None).Wait();
					CreateButtonForModpack(task.Result!);
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());

		}

		private void OnNewModpackButtonPressed() {
			if (_autoInstallerSetupOrStarbound != null && !_autoInstallerSetupOrStarbound.IsCompleted) return;

			Modpack modpack = new Modpack();
			CurrentModpacks.Add(modpack);
#if DEBUG
			if (Directory.Exists(Path2.Combine(Directories.GetLocalWorkshopCacheDirectory(), long.MinValue.ToString()))) {
				ModSource dummy = new ModSource(-9223372036854775808);
				modpack.ModSources[dummy] = true;
				modpack.ModAddedOnDate[dummy] = DateTime.Now;
			}
#endif
			modpack.SaveAndUpdateInitsAsync(CancellationToken.None).Wait();
			CreateButtonForModpack(modpack);
		}

		public void OnDuplicateModpackButtonPressed() {
			if (_autoInstallerSetupOrStarbound != null && !_autoInstallerSetupOrStarbound.IsCompleted) return;

			if (_currentSelectedModpack != null) {
				Modpack dupe = _currentSelectedModpack.Duplicate();
				dupe.Name = dupe.Name + " (Copy)";

				CurrentModpacks.Add(dupe);
				ModpackEntryElement button = CreateButtonForModpack(dupe);
				SetSelection(dupe, button);
			}
		}

		private void OnImportModpackButtonPressed() {
			if (_autoInstallerSetupOrStarbound != null && !_autoInstallerSetupOrStarbound.IsCompleted) return;

			ImportModpackDialog.Show();
		}

		public void OnEditModpackButtonPressed() {
			if (_autoInstallerSetupOrStarbound != null && !_autoInstallerSetupOrStarbound.IsCompleted) return;
			if (_currentSelectedModpack == null) return;
			ModpackManagement.Show();
			ModpackManagement.AssignModpack(_currentSelectedModpack, false);
		}

		public void OnDeleteModpackButtonPressed() {
			if (_autoInstallerSetupOrStarbound != null && !_autoInstallerSetupOrStarbound.IsCompleted) return;

			if (_currentSelectedModpack == null) return;
			Modpack modpack = _currentSelectedModpack;
			ConfirmDeleteDialog popup = Assets.CreateConfirmDeleteDialog();
			AddChild(popup);

			popup.ShowAndGetResultAsync(_currentSelectedModpack.Name).ContinueWith(task => {
				if (task.Result) {
					// Deselect if needed...
					if (_currentSelectedModpack == modpack) {
						_currentSelectedEntryButton?.SetSelectedAppearance(false);
						_currentSelectedModpack = null;
						_currentSelectedEntryButton = null;

						// Also disable the buttons except the create button.
						HideButtons();
						NewModpackButton.Disabled = false;
						ImportModpackButton.Disabled = false;
					}
					foreach (Node node in ModpacksList.GetChildren()) {
						if (node is ModpackEntryElement entry && entry.Modpack == modpack) {
							entry.QueueFree();
							if (ModpackManagement.CurrentModpack == modpack) {
								ModpackManagement.Hide();
							}
							CurrentModpacks.Remove(modpack);
							modpack.Delete();
							break;
						}
					}
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		#region Helpers

		#region Launching

		public void OnRunPressed() {
			if (_autoInstallerSetupOrStarbound != null && !_autoInstallerSetupOrStarbound.IsCompleted) return;
			if (_currentSelectedModpack == null || _currentSelectedEntryButton == null) return;
			if (IsRunningClient(_currentSelectedModpack)) {
				OS.Alert("Cannot run this pack's Starbound client; Starbound client is already running.");
				return;
			}

			TryLaunch(_currentSelectedModpack, _currentSelectedEntryButton, false);
		}

		public void OnRunServerPressed() {
			if (_autoInstallerSetupOrStarbound != null && !_autoInstallerSetupOrStarbound.IsCompleted) return;
			if (_currentSelectedModpack == null || _currentSelectedEntryButton == null) return;
			if (IsRunningServer(_currentSelectedModpack)) {
				OS.Alert("Cannot run this pack's Starbound server; Starbound server is already running.");
				return;
			}

			TryLaunch(_currentSelectedModpack, _currentSelectedEntryButton, true);
		}

		/// <summary>
		/// Returns <see langword="true"/> if the Starbound game client is running for the provided <paramref name="modpack"/>.
		/// </summary>
		/// <param name="modpack"></param>
		/// <returns></returns>
		public bool IsRunningClient(Modpack modpack) {
			lock (_runningModpacksLock) {
				if (_runningModpacks.TryGetValue(modpack, out (Process? client, Process? server) data)) {
					return data.client != null && !data.client.HasExited;
				}
			}
			return false;
		}

		/// <summary>
		/// Returns <see langword="true"/> if the Starbound game server is running for the provided <paramref name="modpack"/>.
		/// </summary>
		/// <param name="modpack"></param>
		/// <returns></returns>
		public bool IsRunningServer(Modpack modpack) {
			lock (_runningModpacksLock) {
				if (_runningModpacks.TryGetValue(modpack, out (Process? client, Process? server) data)) {
					return data.server != null && !data.server.HasExited;
				}
			}
			return false;
		}

		/// <summary>
		/// Assigns a modpack to a process, either for the client or server. This is used to lock out
		/// </summary>
		/// <param name="modpack"></param>
		/// <param name="process"></param>
		/// <param name="isServer"></param>
		public void UpdateStarboundProcess(Modpack modpack, Process? process, bool isServer) {
			lock (_runningModpacksLock) {
				_ = _runningModpacks.TryGetValue(modpack, out (Process? client, Process? server) data);

				if (isServer) {
					data.server = process;
				} else {
					data.client = process;
				}
				if (data.server == null && data.client == null) {
					_runningModpacks.Remove(modpack);

					if (_currentSelectedModpack == modpack) {
						CallDeferred(MethodName.ShowButtonsExceptMacServer);
					}
				} else {
					_runningModpacks[modpack] = data;
					if (_currentSelectedModpack == modpack) {
						CallDeferred(MethodName.HideButtons);
						if (data.client == null || data.client.HasExited) {
							RunButton.SetDeferred(BaseButton.PropertyName.Disabled, false);
						}
						if (data.server == null || data.server.HasExited) {
							RunServerButton.SetDeferred(BaseButton.PropertyName.Disabled, OS.GetName() == "macOS");
						}
					}
				}
			}
		}

		private void TryLaunch(Modpack modpack, ModpackEntryElement clicked, bool asServer) {
			if (asServer && IsRunningServer(modpack)) {
				OS.Alert("The Starbound server is already running. You cannot start it again.", "Cannot start Starbound server");
				return;
			} else if (!asServer && IsRunningClient(modpack)) {
				OS.Alert("The Starbound client is already running. You cannot start it again.", "Cannot start Starbound server");
				return;
			}

			// Special edge case:
			string modpackPath = Directories.GetPackDirectory(modpack.ID);
			string modsDirectory = Path2.Combine(modpackPath, "mods");
			if (Directory.Exists(modsDirectory)) {
				bool hasSubdirectories = Directory.GetDirectories(modsDirectory).Length > 0;
				bool hasPaks = Directory.GetFiles(modsDirectory, "*.pak").Length > 0;
				if (hasSubdirectories || hasPaks) {
					OS.Alert(
						// I hate this formatting...
@"Your modpack has a ""mods"" folder in it, but SBMM doesn't use the default mods directory, so these mods will never be loaded.

You need to add these mods to your pack instead. SBMM will open the folder and the pack editor for you.

Drag and drop your entire ""mods"" folder onto the list in SBMM to install them, then rename or delete your mods folder to silence this warning.", 
						////////////////////////	
						"Invalid usage detected!"
					);
					string os = OS.GetName();
					if (os == "Windows" || os == "macOS") {
						OS.ShellShowInFileManager(modsDirectory, false);
					} else {
						OS.ShellOpen(modpackPath);
					}
					ModpackManagement.Show();
					ModpackManagement.AssignModpack(modpack, true);
					return;
				}
			}

			GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
			CancellationTokenSource cts = new CancellationTokenSource();
			AddChild(progress);
			HideButtons(); // During the startup sequence...
			_autoInstallerSetupOrStarbound = progress.ShowWithCancellation(() => Launcher.LaunchAsync(modpack, progress, false, asServer, cts.Token), cts, true)
						.ContinueWith(delegate {
							_autoInstallerSetupOrStarbound = null;
							RefreshModpackDisplay(modpack); // For the last played date

							// Unlock the buttons, where applicable, if needed.
							if (_currentSelectedModpack != modpack) {
								ShowButtonsExceptMacServer();
							} else {
								RunButton.Disabled = IsRunningClient(modpack);
								RunServerButton.Disabled = OS.GetName() == "macOS" || IsRunningServer(modpack);
							}
						}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		#endregion


		/// <summary>
		/// Updates the displayed information on the button for the provided <paramref name="pack"/>.
		/// </summary>
		/// <param name="pack"></param>
		public void RefreshModpackDisplay(Modpack pack) {
			ModpackEntryElement? button = null;
			if (_currentSelectedModpack == pack) {
				// Fast path
				button = _currentSelectedEntryButton;
			} else {
				// Slow path that also *should* be impossible?
				foreach (Node child in ModpacksList.GetChildren()) {
					if (child is ModpackEntryElement entry && entry.Modpack == pack) {
						button = entry;
						break;
					}
				}
			}
			button?.AssignOrUpdateModpack(pack);
		}

		/// <summary>
		/// Changes the selected mod, unless a background task is running.
		/// </summary>
		/// <param name="modpack"></param>
		/// <param name="clicked"></param>
		private void SetSelection(Modpack modpack, ModpackEntryElement clicked) {
			if (_autoInstallerSetupOrStarbound != null && !_autoInstallerSetupOrStarbound.IsCompleted) return;
			if (specialHasPendingExport) return; // no.

			_currentSelectedEntryButton?.SetSelectedAppearance(false);
			clicked.SetSelectedAppearance(true);
			_currentSelectedEntryButton = clicked;
			_currentSelectedModpack = clicked.Modpack;

			bool isRunningClient = IsRunningClient(modpack);
			bool isRunningServer = IsRunningServer(modpack);
			if (isRunningClient || isRunningServer) {
				HideButtons();
				if (!isRunningClient) {
					RunButton.Disabled = false;
				} else if (!isRunningServer) {
					RunServerButton.Disabled = OS.GetName() == "macOS";
				}
			} else {
				ShowButtonsExceptMacServer();
			}
		}

		/// <summary>
		/// Shared code that creates a <see cref="ModpackEntry"/> for the provided modpack.
		/// </summary>
		private ModpackEntryElement CreateButtonForModpack(Modpack modpack) {
			ModpackEntryElement button = Assets.CreateModpackEntryElementFor(modpack);
			button.OnModpackSelected += SetSelection;
			button.OnModpackDoubleClicked += (modpack, clicked) => TryLaunch(modpack, clicked, false);
			ModpacksList.AddChild(button);
			return button;
		}

		/// <summary>
		/// Disables all the buttons.
		/// </summary>
		private void HideButtons() {
			RunButton.Disabled = true;
			RunServerButton.Disabled = true;
			NewModpackButton.Disabled = true;
			DuplicateModpackButton.Disabled = true;
			ImportModpackButton.Disabled = true;
			EditModpackButton.Disabled = true;
			DeleteModpackButton.Disabled = true;
		}

		/// <summary>
		/// Enables all the buttons.
		/// </summary>
		private void ShowButtonsExceptMacServer() {
			RunButton.Disabled = false;
			RunServerButton.Disabled = OS.GetName() == "macOS";
			NewModpackButton.Disabled = false;
			DuplicateModpackButton.Disabled = false;
			ImportModpackButton.Disabled = false;
			EditModpackButton.Disabled = false;
			DeleteModpackButton.Disabled = false;
		}
		#endregion

	}
}