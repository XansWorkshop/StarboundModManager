using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Attributes;

namespace SBModManager.Menus.Windows {

	/// <summary>
	/// A small window that shows a progress bar and a step. It can be used from outside of the main thread.
	/// </summary>
	public sealed partial class GeneralProgressWindow : Window {

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

		[AllowNull] private string _nextStatus;
		[AllowNull] private string _nextTitle;
		private float _nextProgress;
		private Task? _trackedTask;
		private CancellationTokenSource? _cancellationTokenSource;
		private bool _statusOrProgressDirty = false;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			CancelButton.Pressed += OnCancelled;
			CloseRequested += OnCancelled;
			_nextStatus = StatusLabel.Text;
			_nextTitle = Title;
		}

		/// <summary>
		/// Shows this window, and assigns its <see cref="CancellationTokenSource"/>.
		/// <para/>
		/// This can only be used once; if you need a different operation to cancel, open a new window.
		/// </summary>
		/// <param name="trackedTask">The task to track. This is required for <paramref name="freeWhenTaskCompleted"/>.</param>
		/// <param name="cancellationTokenSource">The <see cref="CancellationTokenSource"/> bound to the <paramref name="trackedTask"/> that is assigned to the Cancel button.</param>
		/// <param name="freeWhenTaskCompleted">If true, this will call <see cref="Node.QueueFree"/> on itself as soon as the task is completed.</param>
		public Task ShowWithCancellation(Func<Task> trackedTask, CancellationTokenSource cancellationTokenSource, bool freeWhenTaskCompleted = true) {
			Show();
			_trackedTask = Task.Run(trackedTask);
			_cancellationTokenSource = cancellationTokenSource;
			if (freeWhenTaskCompleted) {
				_trackedTask.ContinueWith(delegate (Task task) {
					if (IsInstanceValid(this)) {
						QueueFree();
					}
				}, TaskScheduler.FromCurrentSynchronizationContext());
			}
			return _trackedTask;
		}

		/// <summary>
		/// Shows this window, and assigns its <see cref="CancellationTokenSource"/>.
		/// <para/>
		/// This can only be used once; if you need a different operation to cancel, open a new window.
		/// </summary>
		/// <param name="trackedTask">The task to track. This is required for <paramref name="freeWhenTaskCompleted"/>.</param>
		/// <param name="cancellationTokenSource">The <see cref="CancellationTokenSource"/> bound to the <paramref name="trackedTask"/> that is assigned to the Cancel button.</param>
		/// <param name="freeWhenTaskCompleted">If true, this will call <see cref="Node.QueueFree"/> on itself as soon as the task is completed.</param>
		public Task<TResult> ShowWithCancellation<TResult>(Func<Task<TResult>> trackedTask, CancellationTokenSource cancellationTokenSource, bool freeWhenTaskCompleted = true) {
			Show();
			Task<TResult> trackedTaskRunning = Task.Run(trackedTask);
			_trackedTask = trackedTaskRunning;
			_cancellationTokenSource = cancellationTokenSource;
			if (freeWhenTaskCompleted) {
				trackedTaskRunning.ContinueWith(delegate (Task<TResult> task) {
					if (IsInstanceValid(this)) {
						QueueFree();
					}
				}, TaskScheduler.FromCurrentSynchronizationContext());
			}
			return trackedTaskRunning;
		}

		private void OnCancelled() {
			if (_cancellationTokenSource != null) {
				CancelButton.Disabled = true;
				_statusOrProgressDirty = false;
				_cancellationTokenSource.Cancel();

				StatusLabel.Text = "Cancelling...";
				ProgressBar.Indeterminate = true;
			}
		}

		/// <summary>
		/// Sets the progress on the progress bar as a percent. <see cref="float.NaN"/> sets it to indeterminate.
		/// <para/>
		/// This can be used outside of the main thread.
		/// </summary>
		/// <returns></returns>
		public void SetProgress(float percent) {
			if (_cancellationTokenSource == null) return;
			if (_cancellationTokenSource.IsCancellationRequested) return;
			_nextProgress = percent;
			_statusOrProgressDirty = true;
		}

		/// <summary>
		/// Sets the displayed status string and the title of the window. If this string has multiple lines, only the first
		/// line is used for the title of the window, but the popup will display all of the text.
		/// <para/>
		/// This can be used outside of the main thread.
		/// </summary>
		/// <param name="status">The text to display.</param>
		/// <param name="title">An optional title to use instead of the first line of the status.</param>
		public void SetStatus(string status, string? title = null) {
			if (_cancellationTokenSource == null) return;
			if (_cancellationTokenSource.IsCancellationRequested) return;
			_nextStatus = status ?? string.Empty;
			if (title != null) {
				_nextTitle = title;
			}
			_statusOrProgressDirty = true;
		}

		public override void _Process(double delta) {
			if (_statusOrProgressDirty) {
				StatusLabel.Text = _nextStatus;
				if (_nextTitle != null) {
					Title = _nextTitle;
					_nextTitle = null;
				} else {
					if (_nextStatus.Contains('\n')) {
						string[] parts = _nextStatus.Split('\n', 2);
						Title = parts[0];
					} else {
						Title = _nextStatus;
					}
				}

				if (float.IsNaN(_nextProgress)) {
					ProgressBar.Indeterminate = true;
				} else {
					ProgressBar.Indeterminate = false;
					ProgressBar.Value = ((ProgressBar.MaxValue - ProgressBar.MinValue) * _nextProgress) + ProgressBar.MinValue;
				}
			}
		}


	}
}
