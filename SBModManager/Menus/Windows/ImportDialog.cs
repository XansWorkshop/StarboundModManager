using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using SBModManager.Attributes;
using SBModManager.IO;
using SBModManager.ModInstances;

using SBModManager.Other;
using SBModManager.SteamInterop;

using static Godot.TextServer;

namespace SBModManager.Menus.Windows {
	public sealed partial class ImportDialog : Window {

		/// <summary>
		/// The path to a .pak file or unpacked mod directory.
		/// </summary>
		[Import, AllowNull]
		public LineEdit SingleModFileInput { get; }

		/// <summary>
		/// A button which can be clicked to import a packed mod.
		/// </summary>
		[Import, AllowNull]
		public Button ChooseModPakButton { get; }

		/// <summary>
		/// A button which can be clicked to import an unpacked mod as a folder.
		/// </summary>
		[Import, AllowNull]
		public Button ChooseModFolderButton { get; }

		/// <summary>
		/// A text area for placing a Steam Workshop URL or ID.
		/// </summary>
		[Import, AllowNull]
		public LineEdit WorkshopURLOrIDInput { get; }

		/// <summary>
		/// A way to enter the path of an SBMM file directly.
		/// </summary>
		[Import, AllowNull]
		public LineEdit SBMMListInput { get; }

		/// <summary>
		/// A button to choose an SBMM file instead of typing it in.
		/// </summary>
		[Import, AllowNull]
		public Button ChooseSBMMFileButton { get; }

		/// <summary>
		/// The file dialog to find mod lists.
		/// </summary>
		[Import, AllowNull]
		public FileDialog ImportModpackDialog { get; }

		/// <summary>
		/// The file dialog to find mod lists.
		/// </summary>
		[Import, AllowNull]
		public FileDialog ImportModFileDialog { get; }

		/// <summary>
		/// The file dialog to find mod lists.
		/// </summary>
		[Import, AllowNull]
		public FileDialog ImportModFolderDialog { get; }

		/// <summary>
		/// The OK button.
		/// </summary>
		[Import, AllowNull]
		public Button OKButton { get; }

		/// <summary>
		/// The Cancel button.
		/// </summary>
		[Import, AllowNull]
		public Button CancelButton { get; }

		/// <summary>
		/// The modpack that is currently being edited.
		/// </summary>
		public Modpack? EditingModpack { get; private set; }

		/// <summary>
		/// A reference to the <see cref="ViewModListPanel"/> that this belongs to.
		/// </summary>
		public ViewModListPanel? ViewModListPanel { get; private set; }

		private Task? _importTask;
		private int _mode;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			ChooseModPakButton.Pressed += OnChoosePakPressed;
			ChooseModFolderButton.Pressed += OnChooseFolderPressed;
			ChooseSBMMFileButton.Pressed += OnChooseSBMMFilePressed;

			ImportModFileDialog.FileSelected += OnImportPakOrFolderSelected;
			ImportModFolderDialog.DirSelected += OnImportPakOrFolderSelected;
			ImportModpackDialog.FileSelected += OnImportModpackFileSelected;

			SingleModFileInput.TextChanged += OnSingleModFileInputChanged;
			WorkshopURLOrIDInput.TextChanged += OnWorkshopURLInputChanged;
			SBMMListInput.TextChanged += OnSBMMListInputChanged;

			OKButton.Pressed += OnOKPressed;
			CancelButton.Pressed += OnCancelPressed;

			AboutToPopup += OnAboutToPopup;
			CloseRequested += OnCloseRequested;
		}

		private void OnAboutToPopup() {
			SingleModFileInput.Text = "";
			WorkshopURLOrIDInput.Text = "";
			SBMMListInput.Text = "";
			ChangeMode(-1);
		}
		private void OnCloseRequested() => OnCancelPressed();

		private void OnOKPressed() {
			if (_mode == -1) {
				Hide();
			} else if (_mode == 0) {
				PerformPakOrFolderImport(SingleModFileInput.Text);
			} else if (_mode == 1) {
				PerformSteamWorkshopImport(WorkshopURLOrIDInput.Text);
			} else if (_mode == 2) {
				PerformSBMMImport(SBMMListInput.Text);
			}
		}

		private void OnCancelPressed() {
			SingleModFileInput.Text = "";
			WorkshopURLOrIDInput.Text = "";
			SBMMListInput.Text = "";
			ChangeMode(-1);

			ImportModpackDialog.Hide();
			ImportModFileDialog.Hide();
			ImportModFolderDialog.Hide();

			_importTask?.Wait();
			_importTask = null;
			Hide();
		}

		private void ChangeMode(int newMode) {
			_mode = newMode;
			if (newMode == -1) {
				SingleModFileInput.Editable = true;
				WorkshopURLOrIDInput.Editable = true;
				SBMMListInput.Editable = true;

				ChooseModPakButton.Disabled = false;
				ChooseModFolderButton.Disabled = false;
				ChooseSBMMFileButton.Disabled = false;

			} else if (newMode == 0) {
				SingleModFileInput.Editable = true;
				WorkshopURLOrIDInput.Editable = false;
				SBMMListInput.Editable = false;

				ChooseModPakButton.Disabled = false;
				ChooseModFolderButton.Disabled = false;
				ChooseSBMMFileButton.Disabled = true;

			} else if (newMode == 1) {
				SingleModFileInput.Editable = false;
				WorkshopURLOrIDInput.Editable = true;
				SBMMListInput.Editable = false;

				ChooseModPakButton.Disabled = true;
				ChooseModFolderButton.Disabled = true;
				ChooseSBMMFileButton.Disabled = true;
			} else if (newMode == 2) {
				SingleModFileInput.Editable = false;
				WorkshopURLOrIDInput.Editable = false;
				SBMMListInput.Editable = true;

				ChooseModPakButton.Disabled = true;
				ChooseModFolderButton.Disabled = true;
				ChooseSBMMFileButton.Disabled = false;
			}
		}

		private void OnSingleModFileInputChanged(string newText) {
			if (newText.Length == 0) {
				ChangeMode(-1);
			} else {
				ChangeMode(0);
			}

		}

		private void OnWorkshopURLInputChanged(string newText) {
			if (newText.Length == 0) {
				ChangeMode(-1);
			} else {
				ChangeMode(1);
			}
		}

		private void OnSBMMListInputChanged(string newText) {
			if (newText.Length == 0) {
				ChangeMode(-1);
			} else {
				ChangeMode(2);
			}
		}

		private void OnChooseSBMMFilePressed() {
			if (_importTask != null) return;
			ImportModpackDialog.Show();
		}

		private void OnChooseFolderPressed() {
			if (_importTask != null) return;
			ImportModFolderDialog.Show();
		}

		private void OnChoosePakPressed() {
			if (_importTask != null) return;
			ImportModFileDialog.Show();
		}

		private void OnImportPakOrFolderSelected(string path) {
			if (_importTask != null) return;
			SingleModFileInput.Text = path;
			ChangeMode(0);
		}

		private void OnImportModpackFileSelected(string path) {
			if (_importTask != null) return;
			SBMMListInput.Text = path;
			ChangeMode(2);
		}

		#region Imports

		private void PerformSBMMImport(string sbmm) {
			if (EditingModpack == null) return;
			if (ViewModListPanel == null) return;
			if (_importTask != null) return;

			Modpack editing = EditingModpack;
			ViewModListPanel panel = ViewModListPanel;
			GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
			CancellationTokenSource cts = new CancellationTokenSource();
			progress.SetStatus("Importing mod list...", "Importing Mod");
			progress.SetProgress(float.NaN);
			AddChild(progress);
			progress.ShowWithCancellation(async delegate {
				try {
					using FileStream stream = File.OpenRead(sbmm);
					using GZipStream decompressor = new GZipStream(stream, CompressionMode.Decompress);
					Modpack modpack = await PackExportImport.ImportModpackAsync(decompressor, true, progress, cts.Token);
					modpack.IsCorruptedDeleteOnNextRead = true; // It's not actually corrupted, but it's a garbage pack that we don't want to save.

					foreach (KeyValuePair<ModSource, bool> binding in modpack.ModSources) {
						editing.ModSources.TryAdd(binding.Key, binding.Value);
						editing.ModAddedOnDate.TryAdd(binding.Key, modpack.ModAddedOnDate.GetValueOrDefault(binding.Key, DateTime.Now));
					}
				} catch (Exception exc) when (!exc.IsCancellation()) {
					OS.Alert(exc.Message, "Failed to import modpack!");
				}
			}, cts, true).ContinueWith(delegate {
				if (IsInstanceValid(panel)) {
					panel.RebuildList();
				}
				OnCloseRequested();
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void PerformSteamWorkshopImport(string workshopURLOrID) {
			if (EditingModpack == null) return;
			if (ViewModListPanel == null) return;
			if (_importTask != null) return;

			GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
			CancellationTokenSource cts = new CancellationTokenSource();
			Modpack editing = EditingModpack;
			ViewModListPanel panel = ViewModListPanel;
			progress.SetStatus("Downloading Workshop mod...", "Importing Mod");
			progress.SetProgress(float.NaN);
			AddChild(progress);
			progress.ShowWithCancellation(async delegate {
				if (!long.TryParse(workshopURLOrID, out long id)) {
					if (Uri.TryCreate(workshopURLOrID, default, out Uri? resultUri)) {
						// https://steamcommunity.com/sharedfiles/filedetails/?id=00000000000
						if (!long.TryParse(HttpUtility.ParseQueryString(resultUri.Query).Get("id"), out id)) {
							OS.Alert($"Failed to find id parameter in URL {workshopURLOrID}.", "Import failed!");
						}
					} else {
						OS.Alert($"Failed to parse {workshopURLOrID} as a number or a URL.", "Import failed!");
					}
				}
				long[] acquired = await SteamTools.DownloadWorkshopModOrCollectionAsync(id, true, progress, cts.Token);
				for (int i = 0; i < acquired.Length; i++) {
					editing.ModSources.TryAdd(ModSource.GetOrCreateSource(acquired[i]), true);
				}

			}, cts, true).ContinueWith(delegate {
				if (IsInstanceValid(ViewModListPanel)) {
					panel.RebuildList();
				}
				OnCloseRequested();
			}, TaskScheduler.FromCurrentSynchronizationContext());
			
		}

		private void PerformPakOrFolderImport(string pakOrFolderPath) {
			if (EditingModpack == null) return;
			if (ViewModListPanel == null) return;
			if (_importTask != null) return;

			_importTask = Task.CompletedTask;
			Importers.PerformPakOrFolderImport(EditingModpack, ViewModListPanel, pakOrFolderPath);
			OnCloseRequested();
		}

		#endregion

		public void AssignToModpack(Modpack modpack, ViewModListPanel panel) {
			EditingModpack = modpack;
			ViewModListPanel = panel;
		}
	}
}
