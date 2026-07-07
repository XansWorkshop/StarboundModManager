using System;
using System.Collections.Generic;
using System.Text;

using SBModManager.ModInstances;

namespace SBModManager.Menus.Sorting {

	/// <summary>
	/// Sorts <see cref="ModSource"/>s by their author's name.
	/// </summary>
	public sealed class SortModsByAuthor : IComparer<ModSource> {

		/// <summary>
		/// The shared instance of this type.
		/// </summary>
		public static SortModsByAuthor Instance { get; } = new SortModsByAuthor();

		private SortModsByAuthor() { }

		public int Compare(ModSource? x, ModSource? y) {
			return StringComparer.OrdinalIgnoreCase.Compare(x?.GetFirstModOrDefault()?.Metadata?.SBMMAuthorNoMarkup, y?.GetFirstModOrDefault()?.Metadata?.SBMMAuthorNoMarkup);
		}

	}
}
