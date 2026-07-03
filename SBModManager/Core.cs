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
using SBModManager.SteamInterop;

namespace SBModManager {
	public sealed partial class Core : Panel {

		/// <summary>
		/// Allows access to the main window from outside.
		/// </summary>
		[AllowNull]
		public static Core Instance { get; private set; }

		[AllowNull, Import]
		public TextureButton RunButton { get; }

		[AllowNull, Import]
		public TextureButton NewOSBModpackButton { get; }

		[AllowNull, Import]
		public TextureButton DuplicateModpackButton { get; }

		[AllowNull, Import]
		public TextureButton ImportModpackButton { get; }

		[AllowNull, Import]
		public TextureButton EditModpackButton { get; }

		[AllowNull, Import]
		public TextureButton DeleteModpackButton { get; }

		[AllowNull, Import]
		public TextureButton ConfigButton { get; }

		[AllowNull, Import]
		public TextureButton HelpButton { get; }

		[AllowNull, Import]
		public HFlowContainer ModpacksList { get; }

		[AllowNull, Import]
		public ProgramSettingsWindow AppSettings { get; }

		[AllowNull, Import]
		public ModpackManagementWindow EditModpack { get; }

		/// <summary>
		/// Every current modpack that is known.
		/// </summary>
		private List<Modpack> CurrentModpacks { get; } = [];

		private ModpackEntry? _currentSelectedEntryButton;
		private Modpack? _currentSelectedModpack;

		/// <summary>
		/// The process of the current launch of Starbound. Used to know when to unlock the menu.
		/// </summary>
		private Process? _starbound;
		private CancellationTokenSource? _cancelPreppingLaunch;
		private Task? _starboundLaunchAndRunTask;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			Instance = this;
			ProgramSettings.Load();

			RunButton.Pressed += OnRunPressed;
			NewOSBModpackButton.Pressed += OnNewOSBModpackButtonPressed;
			DuplicateModpackButton.Pressed += OnDuplicateModpackButtonPressed;
			ImportModpackButton.Pressed += OnImportModpackButtonPressed;
			EditModpackButton.Pressed += OnEditModpackButtonPressed;
			DeleteModpackButton.Pressed += OnDeleteModpackButtonPressed;
			ConfigButton.Pressed += OnConfigButtonPressed;

			AppSettings.VisibilityChanged += UpdateButtonUsability;

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
		}

		/// <summary>
		/// Shared code that creates a <see cref="ModpackEntry"/> for the provided modpack.
		/// </summary>
		private ModpackEntry CreateButtonForModpack(Modpack modpack) {
			PackedScene entry = GD.Load<PackedScene>("res://ui_elements/modpack_entry.tscn"); // This is cached by Godot.
			ModpackEntry button = entry.Instantiate<ModpackEntry>();
			button.Name = modpack.ID.ToString("D");
			button.AssignOrUpdateModpack(modpack);
			button.OnModpackSelected += SetSelection;
			button.OnModpackDoubleClicked += Launch;
			ModpacksList.AddChild(button);
			return button;
		}

		private void OnRunPressed() {
			if (!IsFullySetUp()) {
				AppSettings.Show();
				return;
			}
			if (_currentSelectedModpack != null && _currentSelectedEntryButton != null) {
				Launch(_currentSelectedModpack, _currentSelectedEntryButton);
			}
		}

		public void RefreshModpackDisplay(Modpack pack) {
			ModpackEntry? button = null;
			if (_currentSelectedModpack == pack) {
				// Fast path
				button = _currentSelectedEntryButton;
			} else {
				// Slow path that also *should* be impossible?
				foreach (Node child in ModpacksList.GetChildren()) {
					if (child is ModpackEntry entry && entry.Modpack == pack) {
						button = entry;
						break;
					}
				}
			}
			button?.AssignOrUpdateModpack(pack);
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

		private void SetSelection(Modpack modpack, ModpackEntry clicked) {
			_currentSelectedEntryButton?.SetSelectedAppearance(false);
			clicked.SetSelectedAppearance(true);
			_currentSelectedEntryButton = clicked;
			_currentSelectedModpack = clicked.Modpack;
		}

		private void Launch(Modpack modpack, ModpackEntry clicked) {
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
					FileName = Directories.GetPrivateStarboundProgram()
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

		private void UpdateButtonUsability() {
			if (!IsFullySetUp() || (_starbound != null && !_starbound.HasExited) || _cancelPreppingLaunch != null) {
				NewOSBModpackButton.Disabled = true;
				DuplicateModpackButton.Disabled = true;
				ImportModpackButton.Disabled = true;
				EditModpackButton.Disabled = true;
				DeleteModpackButton.Disabled = true;
				if (EditModpack.Visible) EditModpack.Hide();
				

				// TODO: Alert icon for config.
			} else {
				NewOSBModpackButton.Disabled = false;
				DuplicateModpackButton.Disabled = false;
				ImportModpackButton.Disabled = false;
				EditModpackButton.Disabled = false;
				DeleteModpackButton.Disabled = false;

			}
		}

		private static bool IsFullySetUp() {
			if (!Directory.Exists(Directories.GetPrivateStarboundInstallDirectory())) return false;
			return true;
		}

		private void OnNewOSBModpackButtonPressed() {
			if (!IsFullySetUp()) {
				AppSettings.Show();
				return;
			}

			PackedScene entry = GD.Load<PackedScene>("res://ui_elements/modpack_entry.tscn");
			Modpack modpack = new Modpack();
			CurrentModpacks.Add(modpack);
			modpack.SaveAndUpdateInitAsync(CancellationToken.None).Wait();
			CreateButtonForModpack(modpack);
		}
		
		private void OnDuplicateModpackButtonPressed() {
			if (!IsFullySetUp()) {
				AppSettings.Show();
				return;
			}
			if (_currentSelectedModpack != null) {
				Modpack dupe = _currentSelectedModpack.Duplicate();
				CurrentModpacks.Add(dupe);
				ModpackEntry button = CreateButtonForModpack(dupe);
				SetSelection(dupe, button);
			}
		}

		private void OnImportModpackButtonPressed() {
			if (!IsFullySetUp()) {
				AppSettings.Show();
				return;
			}
			throw new NotImplementedException();
		}

		private void OnEditModpackButtonPressed() {
			if (!IsFullySetUp()) {
				AppSettings.Show();
				return;
			}
			if (_currentSelectedModpack == null) {
				return;
			}
			EditModpack.Show();
			EditModpack.AssignModpack(_currentSelectedModpack);
		}

		private void OnDeleteModpackButtonPressed() {
			if (!IsFullySetUp()) {
				AppSettings.Show();
				return;
			}
			if (_currentSelectedModpack == null) {
				return;
			}
			Modpack modpack = _currentSelectedModpack;
			PackedScene popupScene = GD.Load<PackedScene>("res://popups/confirm_delete.tscn");
			ConfirmDeletePopup popup = popupScene.Instantiate<ConfirmDeletePopup>();
			AddChild(popup);

			popup.ShowAsync(_currentSelectedModpack.Name).ContinueWith(task => {
				if (task.Result) {
					// Deselect if needed...
					if (_currentSelectedModpack == modpack) {
						_currentSelectedEntryButton?.SetSelectedAppearance(false);
						_currentSelectedModpack = null;
						_currentSelectedEntryButton = null;
					}
					foreach (Node node in ModpacksList.GetChildren()) {
						if (node is ModpackEntry entry && entry.Modpack == modpack) {
							entry.QueueFree();
							if (EditModpack.CurrentModpack == modpack) {
								EditModpack.Hide();
							}
							CurrentModpacks.Remove(modpack);
							modpack.Delete();
							break;
						}
					}
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void OnConfigButtonPressed() {
			AppSettings.Show();
		}

		/// <summary>
		/// Returns the icon for Starbound.
		/// </summary>
		/// <returns></returns>
		public static Texture2D GetStarboundIcon() {
			return (GD.Load("res://icons/starbound.png") as Texture2D)!;
		}

	}
}