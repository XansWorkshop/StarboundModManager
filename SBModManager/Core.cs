using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
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
		/// The file dialog to export a modpack.
		/// </summary>
		[Import, AllowNull]
		public FileDialog ImportModpackDialog { get; }

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

		private ModpackEntryElement? _currentSelectedEntryButton;
		private Modpack? _currentSelectedModpack;
		private Task? _starbound;

		private List<Guid> _pendingPacksToLoad = [];

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			Instance = this;
			ProgramSettings.Load();
			ResourceLoader.SetAbortOnMissingResources(false);

			RunButton.Pressed += OnRunPressed;
			NewModpackButton.Pressed += OnNewModpackButtonPressed;
			DuplicateModpackButton.Pressed += OnDuplicateModpackButtonPressed;
			ImportModpackButton.Pressed += OnImportModpackButtonPressed;
			EditModpackButton.Pressed += OnEditModpackButtonPressed;
			DeleteModpackButton.Pressed += OnDeleteModpackButtonPressed;
			HelpButton.Pressed += OnHelpButtonPressed;
			ImportModpackDialog.FileSelected += OnModpackImportSelected;
			Status.MetaClicked += OnStatusMetaClicked;

			string modpacks = Directories.GetPackDirectory();
			Directory.CreateDirectory(modpacks);

			// Create buttons for all of the user's modpacks.
			foreach (string subdirectory in Directory.GetDirectories(modpacks)) {
				string nameOnly = Path.GetFileName(subdirectory);
				if (Guid.TryParse(nameOnly, out Guid modpackID)) {
					_pendingPacksToLoad.Add(modpackID);
					/*
					Modpack? modpack = Modpack.LoadFromDisk(modpackID);
					if (modpack != null) {
						CurrentModpacks.Add(modpack);
						CreateButtonForModpack(modpack);
					}
					*/
				}
			}

			if (AutoInstaller.ShouldPerformSetup()) {
				GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
				CancellationTokenSource cts = new CancellationTokenSource();
				HideButtons();
				AddChild(progress);
				_starbound = progress.ShowWithCancellation(() => AutoInstaller.PerformSetupAsync(progress, cts.Token), cts, true)
							.ContinueWith(delegate {
								_starbound = null;
								ShowButtons();
							}, TaskScheduler.FromCurrentSynchronizationContext());
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

				GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
				CancellationTokenSource cts = new CancellationTokenSource();
				AddChild(progress);
				progress.ShowWithCancellation(async delegate {
					try {
						Modpack modpack = await PackExportImport.ImportModpackAsync(decompressor, mode == 0, progress, cts.Token);
						CurrentModpacks.Add(modpack);
						return modpack;
					} catch (Exception exc) {
						OS.Alert(exc.Message, "Failed to import modpack!");
						return null;
					}
				}, cts, true).ContinueWith(delegate (Task<Modpack?> task) {
					stream.Dispose();
					if (task.IsCompletedSuccessfully) {
						CreateButtonForModpack(task.Result!);
					}
				}, TaskScheduler.FromCurrentSynchronizationContext());
			} catch (Exception exc) {
				OS.Alert(exc.Message, "Failed to import modpack!");
			}
		}

		private void OnRunPressed() {
			if (_starbound != null && !_starbound.IsCompleted) return;

			if (_currentSelectedModpack != null && _currentSelectedEntryButton != null) {
				Launch(_currentSelectedModpack, _currentSelectedEntryButton);
			}
		}

		private void OnNewModpackButtonPressed() {
			if (_starbound != null && !_starbound.IsCompleted) return;

			Modpack modpack = new Modpack();
			CurrentModpacks.Add(modpack);
			modpack.SaveAndUpdateInitAsync(CancellationToken.None).Wait();
			CreateButtonForModpack(modpack);
		}
		
		private void OnDuplicateModpackButtonPressed() {
			if (_starbound != null && !_starbound.IsCompleted) return;

			if (_currentSelectedModpack != null) {
				Modpack dupe = _currentSelectedModpack.Duplicate();
				CurrentModpacks.Add(dupe);
				ModpackEntryElement button = CreateButtonForModpack(dupe);
				SetSelection(dupe, button);
			}
		}

		private void OnImportModpackButtonPressed() {
			if (_starbound != null && !_starbound.IsCompleted) return;

			ImportModpackDialog.Show();
		}

		private void OnEditModpackButtonPressed() {
			if (_starbound != null && !_starbound.IsCompleted) return;

			if (_currentSelectedModpack == null) return;
			ModpackManagement.Show();
			ModpackManagement.AssignModpack(_currentSelectedModpack);
		}

		private void OnDeleteModpackButtonPressed() {
			if (_starbound != null && !_starbound.IsCompleted) return;

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

		private void Launch(Modpack modpack, ModpackEntryElement clicked) {
			GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
			CancellationTokenSource cts = new CancellationTokenSource();
			HideButtons();
			AddChild(progress);
			_starbound = progress.ShowWithCancellation(() => Launcher.LaunchAsync(modpack, progress, false, cts.Token), cts, true)
						.ContinueWith(delegate {
							_starbound = null;
							ShowButtons();
						}, TaskScheduler.FromCurrentSynchronizationContext());
		}

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
			if (_starbound != null && !_starbound.IsCompleted) return;

			_currentSelectedEntryButton?.SetSelectedAppearance(false);
			clicked.SetSelectedAppearance(true);
			_currentSelectedEntryButton = clicked;
			_currentSelectedModpack = clicked.Modpack;
		}

		/// <summary>
		/// Shared code that creates a <see cref="ModpackEntry"/> for the provided modpack.
		/// </summary>
		private ModpackEntryElement CreateButtonForModpack(Modpack modpack) {
			ModpackEntryElement button = Assets.CreateModpackEntryElementFor(modpack);
			button.OnModpackSelected += SetSelection;
			button.OnModpackDoubleClicked += Launch;
			ModpacksList.AddChild(button);
			return button;
		}

		/// <summary>
		/// Disables all the buttons.
		/// </summary>
		private void HideButtons() {
			RunButton.Disabled = true;
			NewModpackButton.Disabled = true;
			DuplicateModpackButton.Disabled = true;
			ImportModpackButton.Disabled = true;
			EditModpackButton.Disabled = true;
			DeleteModpackButton.Disabled = true;
			if (ModpackManagement.Visible) ModpackManagement.Hide();
		}

		/// <summary>
		/// Enables all the buttons.
		/// </summary>
		private void ShowButtons() {
			RunButton.Disabled = false;
			NewModpackButton.Disabled = false;
			DuplicateModpackButton.Disabled = false;
			ImportModpackButton.Disabled = false;
			EditModpackButton.Disabled = false;
			DeleteModpackButton.Disabled = false;
		}
		#endregion

	}
}