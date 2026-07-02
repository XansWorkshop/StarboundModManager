using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Godot;

using SBModManager.Attributes;
using SBModManager.GUI;
using SBModManager.Menus;
using SBModManager.Menus.Windows;
using SBModManager.ModInstances;
using SBModManager.SteamInterop;

namespace SBModManager {
	public sealed partial class Core : Panel {

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


		public override void _Ready() {
			ImportAttribute.ImportAll(this);

			NewOSBModpackButton.Pressed += OnNewOSBModpackButtonPressed;
			DuplicateModpackButton.Pressed += OnDuplicateModpackButtonPressed;
			ImportModpackButton.Pressed += OnImportModpackButtonPressed;
			EditModpackButton.Pressed += OnEditModpackButtonPressed;
			DeleteModpackButton.Pressed += OnDeleteModpackButtonPressed;
			ConfigButton.Pressed += OnConfigButtonPressed;

			AppSettings.VisibilityChanged += UpdateButtonUsability;

			string modpacks = Directories.GetPackDirectory();
			Directory.CreateDirectory(modpacks);
			PackedScene entry = GD.Load<PackedScene>("res://ui_elements/modpack_entry.tscn");

			foreach (string subdirectory in Directory.GetDirectories(modpacks)) {
				string nameOnly = Path.GetFileName(subdirectory);
				if (Guid.TryParse(nameOnly, out Guid modpackID)) {
					Modpack? modpack = Modpack.LoadFromDisk(modpackID);
					if (modpack != null) {
						CurrentModpacks.Add(modpack);
						ModpackEntry button = entry.Instantiate<ModpackEntry>();
						button.Name = modpackID.ToString("D");
						button.AssignOrUpdateModpack(modpack);
						button.OnModpackSelected += SetSelection;
						ModpacksList.AddChild(button);
					}
				}
			}
		}

		private void SetSelection(Modpack modpack, ModpackEntry clicked) {
			_currentSelectedEntryButton?.SetSelectedAppearance(false);
			clicked.SetSelectedAppearance(true);
			_currentSelectedEntryButton = clicked;
			_currentSelectedModpack = clicked.Modpack;
		}

		private void UpdateButtonUsability() {
			if (!IsFullySetUp()) {
				NewOSBModpackButton.Disabled = true;
				DuplicateModpackButton.Disabled = true;
				ImportModpackButton.Disabled = true;
				EditModpackButton.Disabled = true;
				DeleteModpackButton.Disabled = true;

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

			ModpackEntry button = entry.Instantiate<ModpackEntry>();
			button.Name = modpack.ID.ToString("D");
			button.AssignOrUpdateModpack(modpack);
			button.OnModpackSelected += SetSelection;
			ModpacksList.AddChild(button);
		}
		
		private void OnDuplicateModpackButtonPressed() {
			if (!IsFullySetUp()) {
				AppSettings.Show();
				return;
			}
			throw new NotImplementedException();
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
			EditModpack.AssignModpack(_currentSelectedModpack);
			EditModpack.Show();
		}

		private void OnDeleteModpackButtonPressed() {
			if (!IsFullySetUp()) {
				AppSettings.Show();
				return;
			}
			throw new NotImplementedException();
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