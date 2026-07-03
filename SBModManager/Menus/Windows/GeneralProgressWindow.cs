using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;

using SBModManager.Attributes;

namespace SBModManager.Menus.Windows {

	/// <summary>
	/// The "Launching..." window that shows up when you click on a modpack.
	/// </summary>
	public partial class GeneralProgressWindow : Window {

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

		private string _nextStatus = "Working...";
		private CancellationTokenSource? _ongoingLaunch;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			ProgressBar.Indeterminate = true;
			CancelButton.Pressed += OnCancelled;
		}

		private void OnCancelled() {
			if (_ongoingLaunch != null) {
				CancelButton.Disabled = true;
				_nextStatus = "Cancelling...";
				StatusLabel.Text = "Cancelling...";
				_ongoingLaunch.Cancel();
			}
		}

		/// <summary>
		/// Sets the progress on the progress bar as a percent. <see cref="float.NaN"/> sets it to indeterminate.
		/// </summary>
		/// <returns></returns>
		public void SetProgress(float percent) {
			if (float.IsNaN(percent)) {
				ProgressBar.Indeterminate = true;
			} else {
				ProgressBar.Indeterminate = false;
				ProgressBar.Value = ((ProgressBar.MaxValue - ProgressBar.MinValue) * percent) + ProgressBar.MinValue;
			}
		}

		/// <summary>
		/// A thread-safe way to set the status.
		/// </summary>
		/// <param name="status"></param>
		public void SetStatus(string status) {
			_nextStatus = status ?? string.Empty;
		}

		public void ShowWithCancellation(CancellationTokenSource toCancel) {
			_ongoingLaunch = toCancel;
			Show();
		}

		public override void _Process(double delta) {
			if (StatusLabel.Text != _nextStatus) {
				StatusLabel.Text = _nextStatus;
				Title = _nextStatus;
			}
		}


	}
}
