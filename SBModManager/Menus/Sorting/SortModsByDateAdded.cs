using System;
using System.Collections.Generic;
using System.Text;

using SBModManager.ModInstances;

namespace SBModManager.Menus.Sorting {

	/// <summary>
	/// Sorts <see cref="ModSource"/>s by their date added to a specific modpack.
	/// </summary>
	public static class SortModsByDateAdded {

		/// <summary>
		/// Returns an instance which sorts mods in the specific modpack, since the date added exists per modpack.
		/// </summary>
		/// <param name="modpack"></param>
		/// <returns></returns>
		public static SortModsByDateAddedImpl GetImplementation(Modpack modpack) => new SortModsByDateAddedImpl(modpack);

	}

	public readonly struct SortModsByDateAddedImpl : IComparer<ModSource> {

		public readonly Modpack inPack;

		public SortModsByDateAddedImpl(Modpack inPack) {
			this.inPack = inPack;
		}

		public int Compare(ModSource? x, ModSource? y) {
			DateTime left = default;
			DateTime right = default;
			if (x != null) {
				left = inPack.ModAddedOnDate.GetValueOrDefault(x, DateTime.Now);
			}
			if (y != null) {
				right = inPack.ModAddedOnDate.GetValueOrDefault(y, DateTime.Now);
			}
			return left.CompareTo(right);
		}
	}
}
