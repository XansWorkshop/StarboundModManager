using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

using SBModManager.Attributes;

namespace SBModManager.Menus.Windows {

	/// <summary>
	/// This dialog opens when the user is about to delete a modpack, to prompt them to confirm.
	/// </summary>
	public partial class ConfirmDeleteDialog : ConfirmationDialog {

		[Import, AllowNull]
		public RichTextLabel RichTextLabel { get; }

		private const string FORMAT = @"Are you sure you want to delete [b]{0}[/b]? [color=#f77]All characters and worlds in this modpack will be [b]permanently deleted[/b]. You cannot undo this action![/color]";

		private TaskCompletionSource<bool>? _tcs;
		private string? _pendingText;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			if (_pendingText != null) {
				RichTextLabel.Text = _pendingText;
				Show();
			}
		}

		/// <summary>
		/// Shows this popup, and then waits until an option is selected, returning <see langword="true"/>
		/// if accept was clicked, and <see langword="false"/> if cancel was clicked or the dialog was exited.
		/// </summary>
		/// <param name="modpackNameToDelete"></param>
		/// <returns></returns>
		public Task<bool> ShowAndGetResultAsync(string modpackNameToDelete) {
			if (_tcs != null) throw new InvalidOperationException("Make a new dialog, don't reuse an old one.");
			_tcs = new TaskCompletionSource<bool>();
			_pendingText = string.Format(FORMAT, modpackNameToDelete);
			if (IsNodeReady()) {
				RichTextLabel.Text = _pendingText;
				Show();
			}
			Confirmed += delegate {
				_tcs.SetResult(true);
				QueueFree();
			};
			Canceled += delegate {
				_tcs.SetResult(false);
				QueueFree();
			};
			CloseRequested += delegate {
				_tcs.TrySetResult(false); // TrySet because confirm/cancel take precedence.
				QueueFree();
			};
			return _tcs.Task;
		}

		/// <summary>
		/// Shows this popup, and then waits until an option is selected, returning <see langword="true"/>
		/// if accept was clicked, and <see langword="false"/> if cancel was clicked or the dialog was exited.
		/// </summary>
		/// <param name="customText">Fully custom text to display. Can have bbcode.</param>
		/// <returns></returns>
		public Task<bool> ShowAndGetResultCustomAsync(string customText) {
			if (_tcs != null) throw new InvalidOperationException("Make a new dialog, don't reuse an old one.");
			_tcs = new TaskCompletionSource<bool>();
			_pendingText = customText;
			if (IsNodeReady()) {
				RichTextLabel.Text = _pendingText;
				Show();
			}
			Confirmed += delegate {
				_tcs.SetResult(true);
				QueueFree();
			};
			Canceled += delegate {
				_tcs.SetResult(false);
				QueueFree();
			};
			CloseRequested += delegate {
				_tcs.TrySetResult(false); // TrySet because confirm/cancel take precedence.
				QueueFree();
			};
			return _tcs.Task;
		}
	}
}
