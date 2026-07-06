using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using SBModManager.Attributes;
using SBModManager.IO;
using SBModManager.ModInstances;

namespace SBModManager.GUI {

	/// <summary>
	/// Represents a modpack that a user has created. Not to be confused with <see cref="ModListEntry"/>, which is shown when editing
	/// the mods that are included within the pack.
	/// </summary>
	public partial class ModpackEntryElement : ColorRect {

		[Import, AllowNull]
		public TextureButton ModpackIcon { get; }

		[Import, AllowNull]
		public Label ModpackName { get; }

		/// <summary>
		/// A callback invoked when the modpack is clicked.
		/// </summary>
		public Action<Modpack, ModpackEntryElement>? OnModpackSelected { get; set; }

		/// <summary>
		/// A callback invoked when the modpack is double-clicked.
		/// </summary>
		public Action<Modpack, ModpackEntryElement>? OnModpackDoubleClicked { get; set; }

		/// <summary>
		/// The modpack that this was set to.
		/// </summary>
		[AllowNull]
		public Modpack Modpack { get; private set; }

		private long _lastPressedThisPack = 0;
		private PopupMenu? _popup;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			ModpackIcon.Pressed += _OnModpackSelected;
			if (Modpack != null) {
				SetModpackRoutine(Modpack);
			}
		}

		public void SetSelectedAppearance(bool selected) {
			Color = selected ? new Color(0.1f, 0.2f, 0.4f, 0.5f) : Colors.Transparent;
		}

		private void _OnModpackSelected() {
			long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			if (Input.IsMouseButtonPressed(MouseButton.Right)) {
				OnModpackSelected?.Invoke(Modpack, this);
				OnRightClicked();
			} else {
				if (now - _lastPressedThisPack < 300) {
					OnModpackDoubleClicked?.Invoke(Modpack, this);
				} else {
					OnModpackSelected?.Invoke(Modpack, this);
				}
				_lastPressedThisPack = now;
			}

		}

		private void OnRightClicked() {
			if (IsInstanceValid(_popup)) {
				_popup?.QueueFree();
			}
			if (Core.Instance.specialHasPendingExport) {
				return;
			}
			PopupMenu menu = new PopupMenu();
			_popup = menu;
			menu.AddItem("Play this modpack");
			menu.AddItem("Start dedicated server");
			menu.AddSeparator();
			menu.AddItem("Edit this modpack");
			menu.AddItem("Open profile folder");
			menu.AddItem("Duplicate this modpack");
			menu.AddItem("Export this modpack");
			menu.AddSeparator();
			menu.AddItem("Delete this modpack");
			menu.IndexPressed += delegate (long index) {
				OnModpackSelected?.Invoke(Modpack, this);
				if (index == 0) {
					Core.Instance.OnRunPressed();
				} else if (index == 1) {
					Core.Instance.OnRunServerPressed();
					/* 2 is separator */
				} else if (index == 3) {
					Core.Instance.OnEditModpackButtonPressed();
				} else if (index == 4) {
					OS.ShellOpen(Directories.GetPackDirectory(Modpack.ID));
				} else if (index == 5) {
					Core.Instance.OnDuplicateModpackButtonPressed();
				} else if (index == 6) {
					Core.Instance.specialHasPendingExport = true;
					Core.Instance.ExportModpackDialog.Show();
					/* 7 is separator */
				} else if (index == 8) {
					Core.Instance.OnDeleteModpackButtonPressed();
				}
				menu.QueueFree();
			};
			GetWindow().AddChild(menu);

			Vector2 position = GetWindow().Position;
			position += GetWindow().GetMousePosition();
			menu.Popup(new Rect2I((Vector2I)position, Vector2I.Zero));

			GetWindow().SetInputAsHandled();
			AcceptEvent();
		}

		/// <summary>
		/// Sets the modpack that is displayed.
		/// </summary>
		/// <param name="pack"></param>
		public void AssignOrUpdateModpack(Modpack pack) {
			Modpack = pack;
			if (IsNodeReady()) {
				SetModpackRoutine(pack);
			}
		}

		private void SetModpackRoutine(Modpack pack) {
			ModpackIcon.TextureNormal = pack.GetIcon();
			ModpackName.Text = pack.Name;

			// Pack always has a name. It's forced.
			bool hasCreator = !string.IsNullOrWhiteSpace(pack.Creator);
			bool hasDescription = !string.IsNullOrWhiteSpace(pack.Description);
			string lastPlayed = pack.LastPlayed == default ? "Never" : pack.LastPlayed.ToString();
			if (hasCreator && hasDescription) {
				ModpackIcon.TooltipText = $"{pack.Name} by {pack.Creator}\nLast played: {lastPlayed}\n\n{pack.Description}";
			} else if (hasCreator) {
				ModpackIcon.TooltipText = $"{pack.Name} by {pack.Creator}\nLast played: {lastPlayed}";
			} else if (hasDescription) {
				ModpackIcon.TooltipText = $"{pack.Name}\nLast played: {lastPlayed}\n\n{pack.Description}";
			} else {
				ModpackIcon.TooltipText = pack.Name + $"\nLast played: {lastPlayed}";
			}
			ModpackName.TooltipText = ModpackIcon.TooltipText;
		}
	}
}
