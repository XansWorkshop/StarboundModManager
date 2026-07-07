using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Godot.NativeInterop;

using SBModManager.Attributes;
using SBModManager.GUI;
using SBModManager.IO;
using SBModManager.Menus.Sorting;
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
		/// The dropdown for sort technique.
		/// </summary>
		[Import, AllowNull]
		public OptionButton SortTechnique { get; }

		/// <summary>
		/// The button to update all mods.
		/// </summary>
		[Import, AllowNull]
		public Button UpdateAll { get; }

		/// <summary>
		/// A sorted dictionary used to optimize searching for strings.
		/// </summary>
		private readonly Dictionary<string, ModListEntryElement> _elementsByDisplayName = [];

		/// <summary>
		/// Associates <see cref="Control"/> button/category instances and their <see cref="ModSource"/>s
		/// </summary>
		private readonly Dictionary<ModSource, Control> _buttonBindings = [];

		private string? _pendingSearchString;
		private string? _lastPendingSearchString;
		private double _pendingSearchCooldown = 0.2;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			ImportFromWorkshopButton.Pressed += OnImportFromWorkshopPressed;
			ImportOtherButton.Pressed += OnImportOtherPressed;
			UpdateAll.Pressed += OnUpdateAllPressed;
			UpdateAll.Disabled = true;

			SearchMods.TextChanged += OnSearchTextChanged;
			SortTechnique.ItemSelected += OnSortTechniqueSelected;
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
						} else if (Path.GetExtension(file).Equals(".sbmm", StringComparison.OrdinalIgnoreCase)) {
							Modpack editing = EditingModpack;
							GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
							CancellationTokenSource cts = new CancellationTokenSource();
							progress.SetStatus("Importing mod list...", "Importing Mod");
							progress.SetProgress(float.NaN);
							AddChild(progress);
							progress.ShowWithCancellation(async delegate {
								try {
									using FileStream stream = File.OpenRead(file);
									using GZipStream decompressor = new GZipStream(stream, CompressionMode.Decompress);
									Modpack modpack = await PackExportImport.ImportModpackAsync(decompressor, true, progress, cts.Token);
									modpack.IsCorruptedDeleteOnNextRead = true; // It's not actually corrupted, but it's a garbage pack that we don't want to save.

									foreach (KeyValuePair<ModSource, bool> binding in modpack.ModSources) {
										editing.ModSources.TryAdd(binding.Key, binding.Value);
										editing.ModAddedOnDate.TryAdd(binding.Key, modpack.ModAddedOnDate.GetValueOrDefault(binding.Key, DateTime.Now));
									}
								} catch (Exception exc) when (!exc.IsCancellation()) {
									OS.Alert(exc.Message, "Failed to import modpack!");
								}
							}, cts, true).ContinueWith(delegate {
								if (IsInstanceValid(this)) {
									RebuildList();
								}
							}, TaskScheduler.FromCurrentSynchronizationContext());
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
					_lastPendingSearchString = _pendingSearchString;
					_pendingSearchString = null;

					ModsList.SetBlockSignals(true); // Prevent a huge processor toll from the constant NOTIFICATION_SORT_CHILDREN invocation.
					try {
						while (enumerator.MoveNext()) {
							(ModListEntryElement element, string name, bool qualifies) = enumerator.Current;
							if (!IsInstanceValid(element)) {
								_elementsByDisplayName.Remove(name);
								continue;
							}
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

		private long[] GetIDsToUpdate() {
			if (EditingModpack == null) return [];
			HashSet<long> idsInThisPack = EditingModpack.ModSources.Keys
											.Where(src => src.IsWorkshopMod)
											.Select(src => src.WorkshopID)
											.ToHashSet();
			return WorkshopUpdateInfo.GetUpdateInformation()
					.Where(kvp => kvp.Value.IsUpdateAvailable)
					.Where(kvp => idsInThisPack.Contains(kvp.Key))
					.Select(kvp => kvp.Key)
					.ToArray();
		}

		private void OnUpdateAllPressed() {
			if (EditingModpack == null) return;
			long[] toUpdate = GetIDsToUpdate();
			GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
			AddChild(progress);
			CancellationTokenSource cts = new CancellationTokenSource();
			progress.ShowWithCancellation(async delegate {
				await SteamTools.DownloadWorkshopModsAsync(toUpdate, false, progress, cts.Token).ConfigureAwait(false);
			}, cts, true).ContinueWith(delegate {
				RebuildList(true);
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		/// <summary>
		/// Returns a <see cref="StringSearchEnumerator"/> which can be used to get all of the matching results of a search.
		/// The enumerator returns every <see cref="ModListEntryElement"/> coupled with a boolean indicating if it's a match.
		/// </summary>
		/// <param name="query">The string to search for.</param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"></exception>
		public StringSearchEnumerator EnumerateSearchResults(string query) {
			ArgumentNullException.ThrowIfNull(query);
			return new StringSearchEnumerator(query, _elementsByDisplayName);
		}

		#endregion

		internal void RebuildList(bool skipCheckingForWorkshopUpdate = false) {
			if (EditingModpack == null) {
				GD.PushError("Failed to rebuild the mod list because the dialog is open but no modpack is being edited.");
				return;
			}

			if (skipCheckingForWorkshopUpdate) {
				UpdateAll.Disabled = GetIDsToUpdate().Length == 0;
			} else {
				WorkshopUpdateInfo.CheckForUpdatesWithCooldownAsync().ContinueWith(delegate {
					if (EditingModpack != null) {
						//CallDeferred(MethodName.RebuildList, true); // Kind of cursed.
						RebuildList(true);
					}
				}, TaskScheduler.FromCurrentSynchronizationContext());
				return;
			}

			foreach (Node obj in ModsList.GetChildren()) {
				obj.Free();
			}

			// Some extra work to do here.
			// We need to load the image cache *before* loading anything in the list.
			HashSet<string> md5sOfImagesToLoad = [];
			foreach (ModSource source in EditingModpack.ModSources.Keys) {
				foreach (ModArchive archive in source.Mods) {
					md5sOfImagesToLoad.UnionWith(archive.Metadata.SBMMInlineImageHashes);
				}
			}

			InlineThumbnailImageHelper.PrepareForImageLoading();
			InlineThumbnailImageHelper.PreloadImagesFromDisk(md5sOfImagesToLoad);


			_elementsByDisplayName.Clear();
			_buttonBindings.Clear();
			foreach (KeyValuePair<ModSource, bool> srcBinding in EditingModpack.ModSources) {
				ModSource src = srcBinding.Key;
				bool enabled = srcBinding.Value;
				if (src.Mods.Length == 1) {
					ModListEntryElement mle = Assets.CreateModListEntryElementFor(this, EditingModpack, src.Mods[0]);
					ModsList.AddChild(mle);
					_buttonBindings[src] = mle;
					_elementsByDisplayName.Add(src.Mods[0].Metadata.SBMMFriendlyNameNoMarkup, mle);
				} else {
					ModBundleElement fmg = Assets.CreateModBundleElementFor(this, EditingModpack, src);
					ModsList.AddChild(fmg);
					_buttonBindings[src] = fmg;
					for (int i = 0; i < src.Mods.Length; i++) {
						ModListEntryElement mle = Assets.CreateModListEntryElementFor(this, EditingModpack, src.Mods[i]);
						fmg.AddModListEntry(mle);
						_elementsByDisplayName.Add(src.Mods[i].Metadata.SBMMFriendlyNameNoMarkup, mle);
					}
				}
			}

			// Retain searches once the list updates.
			_pendingSearchString ??= _lastPendingSearchString;
			OnSortTechniqueSelected(EditingModpack.PreferredSortTechnique);
		}

		#region Sorting

		private void OnSortTechniqueSelected(long index) {
			if (EditingModpack == null) return;
			EditingModpack.PreferredSortTechnique = index;

			if (index == 0) {
				SortModList(SortModsByName.Instance);
			} else if (index == 1) {
				SortModList(SortModsByAuthor.Instance);
			} else if (index == 2) {
				SortModList(SortModsByDateAdded.GetImplementation(EditingModpack));
			}
		}


		/// <summary>
		/// Sorts the contents of <see cref="ModList"/> based on the provided <paramref name="comparer"/>.
		/// </summary>
		/// <param name="comparer"></param>
		private void SortModList(IComparer<ModSource> comparer) {
			ModSource[] sources = _buttonBindings.Keys.ToArray();
			Array.Sort(sources, comparer);
			for (int i = 0; i < sources.Length; i++) {
				Control element = _buttonBindings[sources[i]];
				ModsList.MoveChild(element, i);
			}
		}

		#endregion

		private void OnImportFromWorkshopPressed() {
			if (EditingModpack == null) {
				GD.PushError("Failed to import from Workshop because the dialog is open but no modpack is being edited.");
				return;
			}
			long[] workshopIDs = SteamTools.CopyAllCurrentSubscriptionsToCache(true, default);
			HashSet<long> alreadyUsedIDs = EditingModpack.ModSources.Keys.Select(key => key.WorkshopID).Where(key => key != 0).ToHashSet();
			foreach (long id in workshopIDs) {
				if (!alreadyUsedIDs.Add(id)) continue;
				ModSource src = ModSource.GetOrCreateSource(id);
				EditingModpack.ModSources[src] = true;
				EditingModpack.ModAddedOnDate[src] = DateTime.Now;
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
			SearchMods.Text = string.Empty;
			OnSearchTextChanged(string.Empty);
			InlineThumbnailImageHelper.PurgeImages();
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


		public ref struct StringSearchEnumerator : IEnumerator<(ModListEntryElement, string, bool)> {
			public (ModListEntryElement, string, bool) Current { readonly get; private set; }
			readonly object IEnumerator.Current => Current;

			private readonly string _query;
			private readonly (string name, ModListEntryElement element)[] _candidates;
			private int _index = -1;

			internal StringSearchEnumerator(string query, Dictionary<string, ModListEntryElement> candidates) {
				ArgumentNullException.ThrowIfNull(query);
				ArgumentNullException.ThrowIfNull(candidates);
				_query = query.ToLower();
				_candidates = candidates.Select(kvp => (kvp.Key, kvp.Value)).ToArray();
			}

			public bool MoveNext() {
				int nextIndex = _index + 1;
				if (nextIndex >= _candidates.Length) return false;
				_index = nextIndex;
				(string name, ModListEntryElement element) = _candidates[_index];
				Current = (element, name, _query.Length == 0 || name.Contains(_query, StringComparison.OrdinalIgnoreCase));
				return true;
			}

			public readonly void Reset() => throw new NotSupportedException();

			public readonly void Dispose() { }
		}
	}
}
