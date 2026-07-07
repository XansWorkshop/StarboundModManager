using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Attributes;
using SBModManager.Menus;
using SBModManager.Menus.Windows;
using SBModManager.ModInstances;
using SBModManager.SteamInterop;

namespace SBModManager.GUI {

	/// <summary>
	/// Used for mod groups in the list.
	/// </summary>
	public partial class ModBundleElement : Control {

		/// <summary>
		/// The container that stores the list of child mods.
		/// </summary>
		[Import, AllowNull]
		public FoldableContainer Container { get; }

		/// <summary>
		/// A checkbox to toggle the entire bundle.
		/// </summary>
		[Import, AllowNull]
		public CheckButton CategoryEnabled { get; }

		/// <summary>
		/// The header text to display.
		/// </summary>
		[Import, AllowNull]
		public Label Header { get; }

		/// <summary>
		/// The container where the <see cref="ModListEntryElement"/>s are actually stored.
		/// </summary>
		[Import, AllowNull]
		public VBoxContainer Children { get; }

		/// <summary>
		/// The X button used to uninstall mods.
		/// </summary>

		[Import, AllowNull]
		public Button UninstallModButton { get; }

		/// <summary>
		/// The button used to update Workshop mods.
		/// </summary>

		[Import, AllowNull]
		public Button UpdateModButton { get; }

		/// <summary>
		/// The <see cref="Modpack"/> that this represents.
		/// </summary>
		[AllowNull]
		public Modpack Pack { get; private set; }

		/// <summary>
		/// The <see cref="ModSource"/> that this represents.
		/// </summary>
		[AllowNull]
		public ModSource Source { get; private set; }

		/// <summary>
		/// The parent <see cref="ViewModListPanel"/>
		/// </summary>
		[AllowNull]
		private ViewModListPanel _viewModListPanel;

		/// <summary>
		/// The list of pending children, if <see cref="AddModListEntry(ModListEntryElement)"/> is called before ready.
		/// </summary>
		private List<ModListEntryElement> _pendingChildren = [];

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			CategoryEnabled.Toggled += OnCategoryToggled;
			Container.ItemRectChanged += OnContainerResized;
			UninstallModButton.Pressed += OnUninstallPressed;
			UpdateModButton.Pressed += OnUpdatePressed;
			if (Pack != null && Source != null) {
				CategoryEnabled.SetPressedNoSignal(Source.IsEnabledIn(Pack));
				AssignModpackImpl(Pack, Source);
			}
			foreach (ModListEntryElement element in _pendingChildren) {
				if (IsInstanceValid(element)) Children.AddChild(element);
			}
			_pendingChildren = null!;
			Children.SortChildren += OnInnerListSortingChildren;

		}

		/// <summary>
		/// Fires when the children change. This is used in tandem with the search feature to hide bundles where all of
		/// their inner mods have been filtered out.
		/// </summary>
		private void OnInnerListSortingChildren() {
			bool anyVisible = false;
			foreach (Node child in Children.GetChildren()) {
				if (child is Control control) {
					anyVisible |= control.Visible;
					if (anyVisible) break;
				}
			}
			Visible = anyVisible;
		}

		private void OnUninstallPressed() {
			ConfirmDeleteDialog dialog = Assets.CreateConfirmDeleteDialog();
			dialog.ShowAndGetResultCustomAsync("Are you sure you want to remove these mods from the list?").ContinueWith(delegate (Task<bool> result) {
				if (result.Result) {
					Pack.ModSources.Remove(Source);
					QueueFree();
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
			AddChild(dialog);
		}

		private void OnUpdatePressed() {
			if (!Source.IsWorkshopMod) return;
			if (!WorkshopUpdateInfo.IsUpdateAvailable(Source.WorkshopID)) return;

			GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
			AddChild(progress);
			CancellationTokenSource cts = new CancellationTokenSource();
			progress.ShowWithCancellation(
				async delegate {
					await SteamTools.DownloadWorkshopModsAsync([Source.WorkshopID], false, progress, cts.Token);
					WorkshopUpdateInfo.MarkAsUpdated(Source.WorkshopID);
					if (IsInstanceValid(_viewModListPanel)) {
						//_viewModListPanel.RebuildList(true);
						_viewModListPanel.CallDeferred(ViewModListPanel.MethodName.RebuildList, true);
					}
				},
				cts,
				true
			);
		}

		private void OnContainerResized() {
			CustomMinimumSize = Container.Size;
			CustomMaximumSize = new Vector2(-1, Container.Size.Y);
		}

		private void OnCategoryToggled(bool toggledOn) {
			foreach (Node child in Children.GetChildren()) {
				if (child is ModListEntryElement modListEntry) {
					modListEntry.EnableMod.SetPressedNoSignal(toggledOn);
				}
			}
			Pack.ModSources[Source] = toggledOn;
			Modulate = new Color(1, 1, 1, toggledOn ? 1 : 0.5f);
		}

		/// <summary>
		/// Adds a <see cref="ModListEntryElement"/> into this bundle. If the node is not yet ready, it is put into a queue
		/// to be added as soon as it is ready.
		/// </summary>
		/// <param name="entry"></param>
		public void AddModListEntry(ModListEntryElement entry) {
			if (IsNodeReady()) {
				Children.AddChild(entry);
			} else {
				_pendingChildren.Add(entry);
			}
		}

		/// <summary>
		/// Assigns a modpack and source to this element so that it displays the proper information.
		/// </summary>
		/// <param name="modpack"></param>
		/// <param name="source"></param>
		public void AssignModpack(ViewModListPanel from, Modpack modpack, ModSource source) {
			_viewModListPanel = from;
			Pack = modpack;
			Source = source;
			if (IsNodeReady()) {
				AssignModpackImpl(modpack, source);
			}
		}

		private void AssignModpackImpl(Modpack modpack, ModSource source) {
			UpdateModButton.Disabled = !source.IsWorkshopMod || !WorkshopUpdateInfo.IsUpdateAvailable(source.WorkshopID);
			Pack = modpack;
			Source = source;
			CategoryEnabled.SetPressedNoSignal(source.IsEnabledIn(modpack));
		}
	}
}
