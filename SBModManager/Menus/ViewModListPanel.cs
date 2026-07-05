using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Godot.NativeInterop;

using SBModManager.Attributes;
using SBModManager.GUI;
using SBModManager.IO;
using SBModManager.Menus.Windows;
using SBModManager.ModInstances;
using SBModManager.Other;
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
		/// The button to import mods from somewhere else.
		/// </summary>
		[Import, AllowNull]
		public Button ImportOtherButton { get; }

		/// <summary>
		/// The import dialog to show.
		/// </summary>
		[Import, AllowNull]
		public ImportDialog ImportDialog { get; }

		/// <summary>
		/// The search bar.
		/// </summary>
		[Import, AllowNull]
		public LineEdit SearchMods { get; }

		/// <summary>
		/// A sorted dictionary used to optimize searching for strings.
		/// </summary>
		private readonly SortedDictionary<string, ModListEntryElement> _sortedElementsByDisplayName = [];
		private string? _pendingSearchString;
		private double _pendingSearchCooldown = 0.2;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			ImportFromWorkshopButton.Pressed += OnImportFromWorkshopPressed;
			ImportOtherButton.Pressed += OnImportOtherPressed;

			SearchMods.TextChanged += OnSearchTextChanged;
			GetWindow().FilesDropped += OnFilesDropped;
		}

		private void OnFilesDropped(string[] files) {
			if (Visible && EditingModpack != null) {
				foreach (string file in files) {
					if (File.GetAttributes(file).HasFlag(FileAttributes.Directory)) {
						Importers.PerformPakOrFolderImport(EditingModpack, this, file);
					} else {
						if (Path.GetExtension(file).Equals(".pak", StringComparison.OrdinalIgnoreCase)) {
							Importers.PerformPakOrFolderImport(EditingModpack, this, file);
						}
					}
				}
			}
		}

		#region Search

		public override void _Process(double delta) {
			if (_pendingSearchString != null) {
				// ^ Yes, even for empty strings (that just shows everything).

				_pendingSearchCooldown -= delta;
				if (_pendingSearchCooldown <= 0) {
					scoped StringSearchEnumerator enumerator = EnumerateSearchResults(_pendingSearchString);
					_pendingSearchString = null;

					ModsList.SetBlockSignals(true); // Prevent a huge processor toll from the constant NOTIFICATION_SORT_CHILDREN invocation.
					try {
						while (enumerator.MoveNext()) {
							(ModListEntryElement element, bool qualifies) = enumerator.Current;
							element.Visible = qualifies;
						}
					} finally {
						ModsList.SetBlockSignals(false);
					}
				}
			}
		}

		private void OnSearchTextChanged(string newText) {
			_pendingSearchCooldown = 0.2;
			_pendingSearchString = newText;
			
			// Give it a border when there is text.
			if (SearchMods.GetThemeStylebox(NORMAL) is StyleBoxFlat flat) {
				if (newText.Length == 0) {
					flat.SetBorderWidthAll(0);
				} else {
					flat.SetBorderWidthAll(4);
				}
			}
		}
		private static readonly StringName NORMAL = "normal";

		/// <summary>
		/// Returns a <see cref="StringSearchEnumerator"/> which can be used to get all of the matching results of a search.
		/// The enumerator returns every <see cref="ModListEntryElement"/> coupled with a boolean indicating if it's a match.
		/// </summary>
		/// <param name="query">The string to search for.</param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"></exception>
		public StringSearchEnumerator EnumerateSearchResults(string query) {
			ArgumentNullException.ThrowIfNull(query);
			return new StringSearchEnumerator(query, _sortedElementsByDisplayName);
		}

		#endregion

		internal void RebuildList() {
			if (EditingModpack == null) {
				GD.PushError("Failed to rebuild the mod list because the dialog is open but no modpack is being edited.");
				return;
			}
			foreach (Node obj in ModsList.GetChildren()) {
				obj.Free();
			}

			_sortedElementsByDisplayName.Clear();
			foreach (KeyValuePair<ModSource, bool> srcBinding in EditingModpack.ModSources) {
				ModSource src = srcBinding.Key;
				bool enabled = srcBinding.Value;
				if (src.Mods.Length == 1) {
					ModListEntryElement mle = Assets.CreateModListEntryElementFor(EditingModpack, src.Mods[0]);
					ModsList.AddChild(mle);
					_sortedElementsByDisplayName.Add(src.Mods[0].Metadata.FriendlyName, mle);
				} else {
					ModBundleElement fmg = Assets.CreateModBundleElementFor(EditingModpack, src);
					ModsList.AddChild(fmg);
					for (int i = 0; i < src.Mods.Length; i++) {
						ModListEntryElement mle = Assets.CreateModListEntryElementFor(EditingModpack, src.Mods[i]);
						fmg.AddModListEntry(mle);
						_sortedElementsByDisplayName.Add(src.Mods[i].Metadata.FriendlyName, mle);
					}
				}
			}
		}

		private void OnImportFromWorkshopPressed() {
			if (EditingModpack == null) {
				GD.PushError("Failed to import from Workshop because the dialog is open but no modpack is being edited.");
				return;
			}
			long[] workshopIDs = SteamTools.CopyAllCurrentSubscriptionsToCache(true, default);
			HashSet<long> alreadyUsedIDs = EditingModpack.ModSources.Keys.Select(key => key.WorkshopID).Where(key => key != 0).ToHashSet();
			foreach (long id in workshopIDs) {
				if (!alreadyUsedIDs.Add(id)) continue;
				EditingModpack.ModSources[new ModSource(id)] = true;
			}
			RebuildList();
		}

		private void OnImportOtherPressed() {
			if (EditingModpack == null) return;

			ImportDialog.AssignToModpack(EditingModpack, this);
			ImportDialog.Show();

		}

		public void OnClosing() {
			ImportDialog.Hide();
		}

		/// <summary>
		/// Sets <see cref="EditingModpack"/>, and updates every element in this menu to display its customization.
		/// </summary>
		/// <param name="modpack"></param>
		internal void SetModpack(Modpack modpack) {
			ArgumentNullException.ThrowIfNull(modpack);
			EditingModpack = modpack;
			ImportDialog?.Hide();
			RebuildList();
		}


		public ref struct StringSearchEnumerator : IEnumerator<(ModListEntryElement, bool)> {
			public (ModListEntryElement, bool) Current { readonly get; private set; }
			readonly object IEnumerator.Current => Current;

			private readonly string _query;
			private readonly (string name, ModListEntryElement element)[] _candidates;
			private int _index = -1;

			internal StringSearchEnumerator(string query, SortedDictionary<string, ModListEntryElement> candidates) {
				ArgumentNullException.ThrowIfNull(query);
				ArgumentNullException.ThrowIfNull(candidates);
				_query = query.ToLower();
				_candidates = candidates.Select(kvp => (kvp.Key.ToLower(), kvp.Value)).ToArray();
			}

			public bool MoveNext() {
				int nextIndex = _index + 1;
				if (nextIndex >= _candidates.Length) return false;
				_index = nextIndex;
				(string name, ModListEntryElement element) = _candidates[_index];
				Current = (element, _query.Length == 0 || name.Contains(_query, StringComparison.Ordinal));
				return true;
			}

			public readonly void Reset() => throw new NotSupportedException();

			public readonly void Dispose() { }
		}
	}
}
