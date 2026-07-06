using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;

using SBModManager.Attributes;
using SBModManager.ModInstances;
using SBModManager.SteamInterop;

namespace SBModManager.Menus {

	/// <summary>
	/// Controls the Edit Modpack Details window.
	/// </summary>
	public sealed partial class EditModpackDetailsPanel : MarginContainer {

		/// <summary>
		/// The modpack that is currently being edited.
		/// </summary>
		public Modpack? EditingModpack { get; private set; }

		/// <summary>
		/// Make visible to open a file dialog to find a new modpack icon
		/// </summary>
		[Import, AllowNull]
		public FileDialog FindIconDialog { get; }

		/// <summary>
		/// The area where the user enters the name of their modpack.
		/// </summary>
		[Import, AllowNull]
		public LineEdit ModpackNameEntry { get; }

		/// <summary>
		/// The area where the user enters the name of the person who made the modpack.
		/// </summary>
		[Import, AllowNull]
		public LineEdit ModpackCreatorEntry { get; }

		/// <summary>
		/// The area where a user can add a custom description to their modpack.
		/// </summary>
		[Import, AllowNull]
		public TextEdit DescriptionEntry { get; }

		/// <summary>
		/// The icon of the modpack as set by the user.
		/// </summary>
		[Import, AllowNull]
		public TextureRect ModpackIcon { get; }

		/// <summary>
		/// A button which can be used to change the modpack icon. Uses <see cref="CanvasItem.SelfModulate"/> to
		/// become visible.
		/// </summary>
		[Import, AllowNull]
		public Button ChangeModpackIconButton { get; }

		/// <summary>
		/// A label which displays the location of the profile.
		/// </summary>
		[Import, AllowNull]
		public RichTextLabel ProfileLocation { get; }

		/// <inheritdoc/>
		public override void _Ready() {
			ImportAttribute.ImportAll(this);

			ChangeModpackIconButton.MouseEntered += OnChangeIconMouseEntered;
			ChangeModpackIconButton.MouseExited += OnChangeIconMouseExited; ;
			ChangeModpackIconButton.Pressed += OnChangeIconPressed;

			FindIconDialog.FileSelected += OnIconSelected;
		}

		private void OnIconSelected(string path) {
			if (EditingModpack == null) return;
			Texture2D? texture = EditingModpack.TrySetIcon(path);
			ModpackIcon.Texture = texture ?? EditingModpack.GetIcon(); // Keep old icon if it fails.
		}

		private void OnChangeIconPressed() {
			FindIconDialog.Show();
		}

		private void OnChangeIconMouseExited() {
			ChangeModpackIconButton.SelfModulate = Colors.Transparent;
		}

		private void OnChangeIconMouseEntered() {
			ChangeModpackIconButton.SelfModulate = Colors.White;
		}

		internal void OnClosing() {
			if (EditingModpack != null) {
				FindIconDialog.Hide();
				string newName = ModpackNameEntry.Text;
				if (string.IsNullOrWhiteSpace(newName)) newName = "No Name";
				EditingModpack.Name = newName;
				EditingModpack.Creator = ModpackCreatorEntry.Text;
				EditingModpack.Description = DescriptionEntry.Text;
				EditingModpack.SaveAndUpdateInitsAsync(CancellationToken.None).Wait();
			}
		}

		/// <summary>
		/// Sets <see cref="EditingModpack"/>, and updates every element in this menu to display its customization.
		/// </summary>
		/// <param name="modpack"></param>
		internal void SetModpack(Modpack modpack) {
			ArgumentNullException.ThrowIfNull(modpack);
			FindIconDialog.Hide();
			EditingModpack = modpack;

			ModpackNameEntry.Text = modpack.Name;
			ModpackCreatorEntry.Text = modpack.Creator;
			DescriptionEntry.Text = modpack.Description;
			ModpackIcon.Texture = modpack.GetIcon();

			ProfileLocation.Text = $"ID: {EditingModpack.ID:D}";
			GetWindow().Title = $"Edit Modpack Details: {EditingModpack.Name}";
		}

	}
}
