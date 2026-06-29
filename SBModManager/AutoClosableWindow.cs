using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager {
	public sealed partial class AutoClosableWindow : Window {

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
