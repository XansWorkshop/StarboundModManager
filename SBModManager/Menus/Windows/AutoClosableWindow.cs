using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager.Menus.Windows {
	public partial class AutoClosableWindow : Window {

		/// <summary>
		/// Closing will free the window if this is true, otherwise it will hide it.
		/// </summary>
		[Export]
		public bool FreeOnClose { get; set; }

		public AutoClosableWindow() {
			CloseRequested += OnCloseRequested;
		}

		private void OnCloseRequested() {
			if (FreeOnClose) {
				Free();
			} else {
				Hide();
			}
		}
	}
}
