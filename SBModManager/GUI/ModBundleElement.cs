using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using SBModManager.Attributes;
using SBModManager.ModInstances;

namespace SBModManager.GUI {

	/// <summary>
	/// Used for mod groups in the list.
	/// </summary>
	public partial class ModBundleElement : Control {

		[Import, AllowNull]
		public FoldableContainer Container { get; }

		[Import, AllowNull]
		public CheckButton CategoryEnabled { get; }

		[Import, AllowNull]
		public Label Header { get; }

		[Import, AllowNull]
		public VBoxContainer Children { get; }

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
		/// The list of pending children, if <see cref="AddModListEntry(ModListEntryElement)"/> is called before ready.
		/// </summary>
		private List<ModListEntryElement> _pendingChildren = [];

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			CategoryEnabled.Toggled += OnCategoryToggled;
			Container.ItemRectChanged += OnContainerResized;
			if (Pack != null && Source != null) {
				CategoryEnabled.SetPressedNoSignal(Source.IsEnabledIn(Pack));
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
		public void AssignModpack(Modpack modpack, ModSource source) {
			Pack = modpack;
			Source = source;
			if (IsNodeReady()) {
				AssignModpackImpl(modpack, source);
			}
		}

		private void AssignModpackImpl(Modpack modpack, ModSource source) {
			Pack = modpack;
			Source = source;
			CategoryEnabled.SetPressedNoSignal(source.IsEnabledIn(modpack));
		}
	}
}
