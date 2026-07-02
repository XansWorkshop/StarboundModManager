using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

using SBModManager.Attributes;
using SBModManager.GUI;
using SBModManager.ModInstances;
using SBModManager.SteamInterop;

namespace SBModManager.Menus {
	public partial class ViewModListPanel : MarginContainer {

		/// <summary>
		/// The modpack that is currently being edited.
		/// </summary>
		public Modpack? EditingModpack { get; private set; }

		/// <summary>
		/// The list of mods to display.
		/// </summary>
		[Import, AllowNull]
		public VBoxContainer ModsList { get; }

		/// <summary>
		/// The button to import mods from the Steam Workshop.
		/// </summary>
		[Import, AllowNull]
		public Button ImportFromWorkshopButton { get; }

		/// <summary>
		/// The button to import mods from a list.
		/// </summary>
		[Import, AllowNull]
		public Button ImportFromListButton { get; }

		/// <summary>
		/// The button to import a mod from the catalog.
		/// </summary>
		[Import, AllowNull]
		public Button ImportFromCatalogButton { get; }

		/// <summary>
		/// The button to import a mod from a downloaded file or directory.
		/// </summary>
		[Import, AllowNull]
		public Button ImportFromFileButton { get; }

		/// <summary>
		/// The file dialog to find mod lists.
		/// </summary>
		[Import, AllowNull]
		public FileDialog FindModListDialog { get; }



		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			ImportFromWorkshopButton.Pressed += OnImportFromWorkshopPressed;
			ImportFromListButton.Pressed += OnImportFromListPressed;
			ImportFromCatalogButton.Pressed += OnImportFromCatalogPressed;
			ImportFromFileButton.Pressed += OnImportFromFilePressed;
			FindModListDialog.FileSelected += OnModlistFileSelected;
		}

		private void RebuildList() {
			if (EditingModpack == null) {
				GD.PushError("Failed to rebuild the mod list because the dialog is open but no modpack is being edited.");
				return;
			}
			foreach (Node obj in ModsList.GetChildren()) {
				obj.Free();
			}

			PackedScene foldableModGroup = GD.Load<PackedScene>("res://ui_elements/mod_bundle.tscn");
			PackedScene modListEntry = GD.Load<PackedScene>("res://ui_elements/mod_list_entry.tscn");

			foreach (KeyValuePair<ModSource, bool> srcBinding in EditingModpack.ModSources) {
				ModSource src = srcBinding.Key;
				bool enabled = srcBinding.Value;
				if (src.Mods.Length == 1) {
					ModListEntry mle = modListEntry.Instantiate<ModListEntry>();
					mle.AssignMod(EditingModpack, src.Mods[0]);
					mle.Name = src.Mods[0].Metadata.ModID;
					ModsList.AddChild(mle);
				} else {
					FoldableModGroup fmg = foldableModGroup.Instantiate<FoldableModGroup>();
					fmg.AssignModpack(EditingModpack, src);
					fmg.Name = src.PersistentName;
					ModsList.AddChild(fmg);
					for (int i = 0; i < src.Mods.Length; i++) {
						ModListEntry mle = modListEntry.Instantiate<ModListEntry>();
						mle.AssignMod(EditingModpack, src.Mods[i]);
						mle.Name = src.Mods[i].Metadata.ModID;
						fmg.Children.AddChild(mle);
					}
				}
			}
		}

		private void OnImportFromWorkshopPressed() {
			if (EditingModpack == null) {
				GD.PushError("Failed to import from Workshop because the dialog is open but no modpack is being edited.");
				return;
			}
			ulong[] workshopIDs = SteamTools.CopyAllCurrentSubscriptionsToCache(true, default);
			HashSet<ulong> alreadyUsedIDs = EditingModpack.ModSources.Keys.Select(key => key.WorkshopID).Where(key => key != 0).ToHashSet();
			foreach (ulong id in workshopIDs) {
				if (!alreadyUsedIDs.Add(id)) continue;
				EditingModpack.ModSources[new ModSource(id)] = true;
			}
			RebuildList();
		}

		private void OnImportFromListPressed() {
			throw new NotImplementedException();
		}

		private void OnImportFromCatalogPressed() {
			throw new NotImplementedException();
		}

		private void OnImportFromFilePressed() {
			throw new NotImplementedException();
		}

		private void OnModlistFileSelected(string path) {
		}

		public void OnClosing() {
			FindModListDialog.Hide();
		}

		/// <summary>
		/// Sets <see cref="EditingModpack"/>, and updates every element in this menu to display its customization.
		/// </summary>
		/// <param name="modpack"></param>
		internal void SetModpack(Modpack modpack) {
			ArgumentNullException.ThrowIfNull(modpack);
			EditingModpack = modpack;
			FindModListDialog.Hide();
			RebuildList();
		}
	}
}
