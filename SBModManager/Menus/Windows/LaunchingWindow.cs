using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using SBModManager.Attributes;

namespace SBModManager.Menus.Windows {

	/// <summary>
	/// The "Launching..." window that shows up when you click on a modpack.
	/// </summary>
	public partial class LaunchingWindow : Window {

		/// <summary>
		/// A status label telling the user what step it's on.
		/// </summary>
		[Import, AllowNull]
		public Label StatusLabel { get; }

		/// <summary>
		/// The progress bar which displays how far along the startup is.
		/// </summary>
		[Import, AllowNull]
		public ProgressBar ProgressBar { get; }

		/// <summary>
		/// The cancel button which stops loading a modpack.
		/// </summary>
		[Import, AllowNull]
		public Button CancelButton { get; }


	}
}
