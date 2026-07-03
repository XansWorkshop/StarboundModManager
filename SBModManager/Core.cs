using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Godot;

using SBModManager.Attributes;
using SBModManager.GUI;
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

		/*
		/// <summary>
		/// The topbar button to configure the program.
		/// </summary>
		[AllowNull, Import]
		public TextureButton ConfigureAppButton { get; }
		*/

		[AllowNull, Import]
		public HFlowContainer ModpacksList { get; }

		/*
		[AllowNull, Import]
		public ProgramSettingsWindow ProgramSettings { get; }
		*/

		[AllowNull, Import]
		public ModpackManagementWindow ModpackManagement { get; }

		/// <summary>
		/// Every current modpack that is known.
		/// </summary>
		private List<Modpack> CurrentModpacks { get; } = [];

		private ModpackEntryElement? _currentSelectedEntryButton;
		private Modpack? _currentSelectedModpack;
		private Task? _starbound;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			Instance = this;
			ProgramSettings.Load();

			// StarboundJsonSanitizer.ParseString(File.ReadAllText("F:\\Users\\Xan\\source\\godot\\sb_mod_manager\\fuckass_json_test.json"));

			RunButton.Pressed += OnRunPressed;
			NewModpackButton.Pressed += OnNewModpackButtonPressed;
			DuplicateModpackButton.Pressed += OnDuplicateModpackButtonPressed;
			ImportModpackButton.Pressed += OnImportModpackButtonPressed;
			EditModpackButton.Pressed += OnEditModpackButtonPressed;
			DeleteModpackButton.Pressed += OnDeleteModpackButtonPressed;
			//ConfigureAppButton.Pressed += OnConfigButtonPressed;

			// ProgramSettings.VisibilityChanged += UpdateButtonUsability;

			string modpacks = Directories.GetPackDirectory();
			Directory.CreateDirectory(modpacks);

			// Create buttons for all of the user's modpacks.
			foreach (string subdirectory in Directory.GetDirectories(modpacks)) {
				string nameOnly = Path.GetFileName(subdirectory);
				if (Guid.TryParse(nameOnly, out Guid modpackID)) {
					Modpack? modpack = Modpack.LoadFromDisk(modpackID);
					if (modpack != null) {
						CurrentModpacks.Add(modpack);
						CreateButtonForModpack(modpack);
					}
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

		public override void _UnhandledKeyInput(InputEvent @event) {
			if (@event is InputEventKey key && @event.IsPressed()) {
				if (key.Keycode == Key.Pageup) {
					MovingRichTextLabel.MostRecentTooltip?.ScrollUp();
				} else if (key.Keycode == Key.Pagedown) {
					MovingRichTextLabel.MostRecentTooltip?.ScrollDown();
				}
			}
		}

		private void OnRunPressed() {
			if (_starbound != null && !_starbound.IsCompleted) return;

			if (_currentSelectedModpack != null && _currentSelectedEntryButton != null) {
				Launch(_currentSelectedModpack, _currentSelectedEntryButton);
			}
		}

		/*
		private void Launch(Modpack modpack, ModpackEntryElement clicked) {
			PackedScene launchPromptScene = GD.Load<PackedScene>("res://popups/progress_window.tscn");
			GeneralProgressWindow launching = launchPromptScene.Instantiate<GeneralProgressWindow>();
			_cancelPreppingLaunch = new CancellationTokenSource();
			launching.ShowWithCancellation(_cancelPreppingLaunch);
			AddChild(launching);
			UpdateButtonUsability();
			_starboundLaunchAndRunTask = LaunchAsync(modpack, launching);
		}

		private async Task LaunchAsync(Modpack modpack, GeneralProgressWindow launching) {
			launching.SetStatus("Downloading mods...");
			try {
				await modpack.SaveAndUpdateInitAsync(_cancelPreppingLaunch!.Token);
				ProcessStartInfo game = new ProcessStartInfo {
					FileName = Directories.GetLocalStarboundProgram()
				};
				game.ArgumentList.Add("-bootconfig");
				game.ArgumentList.Add(modpack.SBInitPath);
				_starbound = Process.Start(game);
				if (_starbound == null) {
					throw new InvalidOperationException("Failed to launch Starbound.");
				} else {
					launching.SetStatus("Starbound is running!");
					launching.SetProgress(1.0f);
					launching.CancelButton.Text = "Exit Starbound";
					await _starbound.WaitForExitAsync(_cancelPreppingLaunch.Token);
					_starbound = null;
					if (IsInstanceValid(launching)) {
						launching.QueueFree();
					}
				}
			} catch (OperationCanceledException) {
				if (_starbound != null) {
					_starbound.Kill();
					_starbound = null;
				}
			} finally {
				if (IsInstanceValid(launching)) {
					launching.QueueFree();
				}
				_cancelPreppingLaunch = null;
				_starboundLaunchAndRunTask = null;
				UpdateButtonUsability();
			}
		}
		*/

		/*
		private void UpdateButtonUsability() {
			if (!IsFullySetUp() || (_starbound != null && !_starbound.HasExited) || _cancelPreppingLaunch != null) {
				NewModpackButton.Disabled = true;
				DuplicateModpackButton.Disabled = true;
				ImportModpackButton.Disabled = true;
				EditModpackButton.Disabled = true;
				DeleteModpackButton.Disabled = true;
				if (ModpackManagement.Visible) ModpackManagement.Hide();
				

				// TODO: Alert icon for config.
			} else {
				NewModpackButton.Disabled = false;
				DuplicateModpackButton.Disabled = false;
				ImportModpackButton.Disabled = false;
				EditModpackButton.Disabled = false;
				DeleteModpackButton.Disabled = false;

			}
		}
		*/

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

			throw new NotImplementedException();
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
			PackedScene popupScene = GD.Load<PackedScene>("res://popups/confirm_delete.tscn");
			ConfirmDeleteDialog popup = popupScene.Instantiate<ConfirmDeleteDialog>();
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

		/*
		private void OnConfigButtonPressed() {
			ProgramSettings.Show();
		}
		*/

		#region Helpers

		private void Launch(Modpack modpack, ModpackEntryElement clicked) {
			GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
			CancellationTokenSource cts = new CancellationTokenSource();
			HideButtons();
			AddChild(progress);
			_starbound = progress.ShowWithCancellation(() => Launcher.LaunchAsync(modpack, progress, cts.Token), cts, true)
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