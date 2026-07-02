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
	public partial class FoldableModGroup : Control {

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

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			CategoryEnabled.Toggled += OnCategoryToggled;
			Container.ItemRectChanged += OnContainerResized;
		}

		private void OnContainerResized() {
			CustomMinimumSize = Container.Size;
			CustomMaximumSize = Container.Size;
		}

		private void OnCategoryToggled(bool toggledOn) {
			foreach (Node child in Children.GetChildren()) {
				if (child is ModListEntry modListEntry) {
					modListEntry.EnableMod.SetPressedNoSignal(toggledOn);
				}
			}
			Pack.ModSources[Source] = toggledOn;
			Modulate = new Color(1, 1, 1, toggledOn ? 1 : 0.5f);
		}

		public void AssignModpack(Modpack modpack, ModSource source) {
			Pack = modpack;
			Source = source;
		}
	}
}
