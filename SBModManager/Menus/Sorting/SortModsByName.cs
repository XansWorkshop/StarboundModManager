using System;
using System.Collections.Generic;
using System.Text;

using SBModManager.ModInstances;

namespace SBModManager.Menus.Sorting {

	/// <summary>
	/// Sorts <see cref="ModSource"/>s by their display name.
	/// </summary>
	public sealed class SortModsByName : IComparer<ModSource> {

		/// <summary>
		/// The shared instance of this type.
		/// </summary>
		public static SortModsByName Instance { get; } = new SortModsByName();

		private SortModsByName() { }

		public int Compare(ModSource? x, ModSource? y) {
			return StringComparer.CurrentCulture.Compare(x?.GetFirstModOrDefault()?.Metadata?.SBMMFriendlyNameNoMarkup, y?.GetFirstModOrDefault()?.Metadata?.SBMMFriendlyNameNoMarkup);	
		}
	}
}
