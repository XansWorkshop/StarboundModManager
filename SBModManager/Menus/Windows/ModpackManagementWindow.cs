using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Attributes;
using SBModManager.GUI;
using SBModManager.IO;
using SBModManager.Menus;
using SBModManager.ModInstances;
using SBModManager.SteamInterop;

namespace SBModManager.Menus.Windows {

	/// <summary>
	/// The entire window for managing a specific modpack.
	/// </summary>
	public partial class ModpackManagementWindow : AutoClosableWindow {

		/// <summary>
		/// The tab window for the editor.
		/// </summary>
		[Import, AllowNull]
		public TabContainer Tabs { get; }

		/// <summary>
		/// The panel where the modpack's name, author, description, and icon are set.
		/// </summary>
		[Import, AllowNull]
		public EditModpackDetailsPanel EditModpackDetails { get; }
		
		/// <summary>
		/// The panel where the list of mods is shown.
		/// </summary>
		[Import, AllowNull]
		public ViewModListPanel ViewModList { get; }

		/// <summary>
		/// This button opens a prompt to export the modpack.
		/// </summary>
		[Import, AllowNull]
		public Button ExportButton { get; }

		/// <summary>
		/// This button opens a prompt to export the modpack.
		/// </summary>
		[Import, AllowNull]
		public Button ApplyButton { get; }

		/// <summary>
		/// The file dialog to export a modpack.
		/// </summary>
		[Import, AllowNull]
		public FileDialog ExportModpackDialog { get; }

		/// <summary>
		/// The modpack being managed.
		/// </summary>
		public Modpack? CurrentModpack { get; private set; }

		public override void _Ready() {
			ImportAttribute.ImportAll(this);

			CloseRequested += OnCloseRequested;
			VisibilityChanged += OnVisibilityChanged;
			ApplyButton.Pressed += EmitSignalCloseRequested;
			ExportButton.Pressed += OnExportModPressed;
			ExportModpackDialog.FileSelected += OnExportFileConfirmed;
			if (CurrentModpack != null) {
				AssignModpackImpl(CurrentModpack);
			}
			Tabs.CurrentTab = 0;
		}

		private void OnExportFileConfirmed(string path) {
			if (CurrentModpack == null) return;

			try {
				FileStream writer = File.Open(path, FileMode.Create, System.IO.FileAccess.Write, FileShare.None);
				GZipStream compressor = new GZipStream(writer, CompressionLevel.SmallestSize);
				GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
				CancellationTokenSource cts = new CancellationTokenSource();
				Modpack currentModpack = CurrentModpack;
				AddChild(progress);
				progress.ShowWithCancellation(async delegate {
					try {
						await PackExportImport.ExportModpackAsync(currentModpack, compressor, progress, cts.Token);
					} catch (Exception exc) {
						OS.Alert(exc.Message, "Failed to export modpack!");
					}
				}, cts, true).ContinueWith(delegate {
					writer.Close();
				}, TaskScheduler.FromCurrentSynchronizationContext());
			} catch (Exception exc) {
				OS.Alert(exc.Message, "Failed to export modpack!");
			}
		}

		private void OnExportModPressed() {
			ExportModpackDialog.Show();
		}

		public override void _UnhandledKeyInput(InputEvent @event) {
			if (@event is InputEventKey key && @event.IsPressed()) {
				if (key.Keycode == Key.Pageup) {
					MovingRichTextLabel.MostRecentTooltip?.ScrollUp();
				} else if (key.Keycode == Key.Pagedown) {
					MovingRichTextLabel.MostRecentTooltip?.ScrollDown();
				}
			}
		}

		private void OnVisibilityChanged() {
			if (Visible) {
				Tabs.CurrentTab = 0;
			}
		}

		private void OnCloseRequested() {
			EditModpackDetails.OnClosing();
			ViewModList.OnClosing();
			if (CurrentModpack != null) {
				Core.Instance.RefreshModpackDisplay(CurrentModpack);
			}
			Hide();
		}

		/// <summary>
		/// Sets <see cref="EditingModpack"/>, and updates every element in this menu to display its customization.
		/// </summary>
		/// <param name="modpack"></param>
		public void AssignModpack(Modpack modpack) {
			ArgumentNullException.ThrowIfNull(modpack);
			CurrentModpack = modpack;
			if (IsNodeReady()) {
				AssignModpackImpl(modpack);
			}
		}

		private void AssignModpackImpl(Modpack modpack) {
			CurrentModpack = modpack;
			EditModpackDetails.SetModpack(modpack);
			ViewModList.SetModpack(modpack);
		}
	}
}
