using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Attributes;
using SBModManager.Menus;
using SBModManager.Menus.Windows;
using SBModManager.ModInstances;
using SBModManager.SteamInterop;

namespace SBModManager.GUI {

	/// <summary>
	/// Represents an entry in the mod list. Not to be confused with <see cref="ModPackEntry"/>, which is on the main screen.
	/// </summary>
	public partial class ModListEntryElement : ColorRect {

		/// <summary>
		/// A checkbox which is used to enable or disable the mod.
		/// </summary>
		[Import, AllowNull]
		public CheckButton EnableMod { get; }

		/// <summary>
		/// The thumbnail image of the mod.
		/// </summary>
		[Import, AllowNull]
		public TextureRect ModIcon { get; }

		/// <summary>
		/// The name of the mod and its author.
		/// </summary>
		[Import, AllowNull]
		public RichTextLabel ModNameAndAuthor { get; }

		/// <summary>
		/// Information about the mod's version and size on disk.
		/// </summary>
		[Import, AllowNull]
		public RichTextLabel ModVersionAndSize { get; }

		/// <summary>
		/// The X button used to remove the mod from the list.
		/// </summary>
		[Import, AllowNull]
		public Button UninstallModButton { get; }

		/// <summary>
		/// The button used to download a mod update from the Workshop.
		/// </summary>
		[Import, AllowNull]
		public Button UpdateModButton { get; }

		/// <summary>
		/// The mod that this represents.
		/// </summary>
		[AllowNull]
		public ModArchive Mod { get; private set; }

		/// <summary>
		/// The modpack that holds this mod.
		/// </summary>
		[AllowNull]
		public Modpack Pack { get; private set; }

		/// <summary>
		/// The parent <see cref="ViewModListPanel"/>
		/// </summary>
		[AllowNull]
		private ViewModListPanel _viewModListPanel;


		private PopupMenu? _popupRightClickMenu;

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			UpdateModButton.Disabled = true;
			if (Pack != null && Mod != null) {
				AssignModRoutine(Pack, Mod);
			} else {
				;
			}

			EnableMod.Toggled += OnEnableModToggled;
			UninstallModButton.Pressed += OnUninstallPressed;
			UpdateModButton.Pressed += OnUpdatePressed;
		}

		public override void _GuiInput(InputEvent @event) {
			if (@event is InputEventMouseButton button) {
				if (button.ButtonIndex == MouseButton.Right) {
					if (IsInstanceValid(_popupRightClickMenu)) {
						_popupRightClickMenu?.QueueFree();
					}
					PopupMenu menu = new PopupMenu();
					_popupRightClickMenu = menu;

					if (Mod.Owner.IsEnabledIn(Pack) && !Mod.IsDisabledByForce) {
						menu.AddItem("Disable");
					} else {
						menu.AddItem("Enable");
					}
					menu.AddItem("Update from Steam Workshop");
					menu.AddItem("Open installation folder");
					menu.AddSeparator();
					menu.AddItem("View on Steam Workshop (Browser)");
					menu.AddItem("View on Steam Workshop (Steam Client)");
					menu.AddSeparator();
					menu.AddItem("Remove from this pack");

					if (Mod.IsDisabledByForce || !Mod.IsExclusive) {
						menu.SetItemDisabled(0, true);
					}

					if (!Mod.Owner.IsWorkshopMod) {
						menu.SetItemDisabled(1, true);
						menu.SetItemDisabled(4, true);
						menu.SetItemDisabled(5, true);
					} else {
						menu.SetItemDisabled(1, !Mod.IsExclusive || !WorkshopUpdateInfo.IsUpdateAvailable(Mod.Owner.WorkshopID));
					}
					if (!Mod.IsExclusive) {
						menu.SetItemDisabled(7, true);
					}

					menu.IndexPressed += delegate (long index) {
						if (index == 0) {
							EnableMod.ButtonPressed = !EnableMod.ButtonPressed;
						} else if (index == 1) {
							OnUpdatePressed();
						} else if (index == 2) {
							OS.ShellOpen(Mod.Owner.AbsolutePath);
							/* 3 is separator */
						} else if (index == 4) {
							OS.ShellOpen($"https://steamcommunity.com/sharedfiles/filedetails/?id={Mod.Owner.WorkshopID}");
						} else if (index == 5) {
							OS.ShellOpen($"steam://url/communityfilepage/{Mod.Owner.WorkshopID}");
							/* 5 is separator */
						} else if (index == 7) {
							OnUninstallPressed();
						}
						menu.QueueFree();
					};
					GetWindow().AddChild(menu);

					Vector2 position = GetWindow().Position;
					position += GetWindow().GetMousePosition();
					menu.Popup(new Rect2I((Vector2I)position, Vector2I.Zero));

					GetWindow().SetInputAsHandled();
				} else if (button.ButtonIndex == MouseButton.WheelDown || button.ButtonIndex == MouseButton.WheelUp) {
					_popupRightClickMenu?.QueueFree();
					_popupRightClickMenu = null;
				}
			}
		}

		private void OnEnableModToggled(bool toggledOn) {
			Pack.ModSources[Mod.Owner] = toggledOn;
			Modulate = new Color(1, 1, 1, toggledOn ? 1 : 0.5f);
		}

		private void OnUninstallPressed() {
			if (Mod.Owner.Mods.Length > 1) return;
			ConfirmDeleteDialog dialog = Assets.CreateConfirmDeleteDialog();
			dialog.ShowAndGetResultCustomAsync("Are you sure you want to remove this mod from the list?").ContinueWith(delegate (Task<bool> result) {
				if (result.Result) {
					Pack.ModSources.Remove(Mod.Owner);
					Pack.ModAddedOnDate.Remove(Mod.Owner);
					if (IsInstanceValid(_viewModListPanel)) {
						_viewModListPanel.RebuildList();
					}
					QueueFree();
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
			AddChild(dialog);
		}

		private void OnUpdatePressed() {
			if (!Mod.Owner.IsWorkshopMod) return;
			if (!WorkshopUpdateInfo.IsUpdateAvailable(Mod.Owner.WorkshopID)) return;

			GeneralProgressWindow progress = Assets.CreateGeneralProgressWindow();
			AddChild(progress);
			CancellationTokenSource cts = new CancellationTokenSource();
			progress.ShowWithCancellation(
				async delegate {
					await SteamTools.DownloadWorkshopModsAsync([Mod.Owner.WorkshopID], false, progress, cts.Token);
					WorkshopUpdateInfo.MarkAsUpdated(Mod.Owner.WorkshopID); // Refresh update status.
					if (IsInstanceValid(_viewModListPanel)) {
						_viewModListPanel.CallDeferred(ViewModListPanel.MethodName.RebuildList, true);
					}
				},
				cts,
				true
			);
		}

		public void AssignMod(ViewModListPanel from, Modpack modpack, ModArchive mod) {
			_viewModListPanel = from;
			Pack = modpack;
			Mod = mod;
			if (!IsNodeReady()) return;
			AssignModRoutine(modpack, mod);
		}

		private void AssignModRoutine(Modpack modpack, ModArchive mod) {
			Pack = modpack;
			Mod = mod;
			EnableMod.Disabled = !mod.IsExclusive || mod.IsDisabledByForce;
			UninstallModButton.Visible = mod.IsExclusive; // Flat out hide it.
			UpdateModButton.Disabled = !mod.Owner.IsWorkshopMod || !WorkshopUpdateInfo.IsUpdateAvailable(mod.Owner.WorkshopID);
			UpdateModButton.Visible = UninstallModButton.Visible;
			EnableMod.SetPressedNoSignal(mod.Owner.IsEnabledIn(modpack) && !mod.IsDisabledByForce);
			ModIcon.Texture = mod.Metadata.PreviewImage;

			if (mod.IsDisabledByForce) {
				EnableMod.TooltipText = "This mod's archive name begins with an underscore.\nStarbound itself actually uses this to forcibly skip loading a mod.";
				Modulate = new Color(1, 1, 1, 0.5f);
			} else if (!mod.IsExclusive) {
				EnableMod.TooltipText = "You can't disable this mod because it's part of a bundle.";
				Modulate = Colors.White;
			} else {
				EnableMod.TooltipText = string.Empty;
				Modulate = Colors.White;
			}

			string friendlyName = mod.Metadata.FriendlyName ?? string.Empty;
			string author = mod.Metadata.Author ?? string.Empty;
			string version = mod.Metadata.Version ?? string.Empty;

			string formattedFriendlyName = FormatTools.StarboundMarkupToBBCode(friendlyName.Replace("\n", null).Replace("\r", null), true);
			string formattedAuthor = string.IsNullOrWhiteSpace(author) ? "[color=#faa][i]<no author>[/i][/color]" : FormatTools.StarboundMarkupToBBCode(author.Replace("\n", null).Replace("\r", null), true);
			string formattedVersion = string.IsNullOrWhiteSpace(version) ? "[color=#faa][i]<no version info>[/i][/color]" : FormatTools.StarboundMarkupToBBCode(version.Replace("\n", null).Replace("\r", null), true);
			
#pragma warning disable format // Preserve my strange indenting
			ModNameAndAuthor.Clear();
			ModNameAndAuthor.PushFontSize(16);
				ModNameAndAuthor.PushContext();
					ModNameAndAuthor.AppendText(formattedFriendlyName);
				ModNameAndAuthor.PopContext();
			ModNameAndAuthor.Pop();
			ModNameAndAuthor.PushFontSize(10);
				ModNameAndAuthor.AppendText("\nby ");
				ModNameAndAuthor.PushColor(Colors.MediumSeaGreen);
					ModNameAndAuthor.PushContext();
					ModNameAndAuthor.AppendText(formattedAuthor);
					ModNameAndAuthor.PopContext();
				ModNameAndAuthor.Pop();
				// ModNameAndAuthor.AppendText(" - Hover for more information.");
			ModNameAndAuthor.Pop();

			string lastPublicationDatePrefix; ;
			string lastPublicationDateString;// = "[lb]??? Information Outdated]";
			if (mod.Owner.IsWorkshopMod && WorkshopUpdateInfo.TryGetUpdateInformation(mod.Owner.WorkshopID, out WorkshopUpdateInfo.VersionBinding updateInfo) && updateInfo.lastUpdatedForCurrentInstall != 0) {
				lastPublicationDatePrefix = "Published ";
				lastPublicationDateString = updateInfo.CurrentInstalledUpdateDate.ToString();
			} else {
				if (mod.Owner.IsWorkshopMod) {
					lastPublicationDatePrefix = "Published ";
					lastPublicationDateString = "[lb]??? Info out of date; Update to fix]";
				} else {
					lastPublicationDatePrefix = "File created ";
					try {
						DateTime creation = File.GetCreationTime(mod.AbsolutePath);
						lastPublicationDateString = creation.ToString();
					} catch (Exception exc) when (exc is FileNotFoundException or DirectoryNotFoundException) {
						lastPublicationDateString = " <error reading file>";
					}
				}
			}

			ModVersionAndSize.Clear();
			ModVersionAndSize.AppendText("Version ");
			ModVersionAndSize.PushColor(Colors.MediumSeaGreen);
				ModVersionAndSize.PushContext();
					ModVersionAndSize.AppendText(formattedVersion);
				ModVersionAndSize.PopContext();
			ModVersionAndSize.Pop();
			ModVersionAndSize.PushColor(Colors.LightGray);
				ModVersionAndSize.PushFontSize(10);
					ModVersionAndSize.AppendText("\n");
					ModVersionAndSize.PushColor(Colors.Thistle);
						ModVersionAndSize.AppendText(FormatTools.ToLargestSIUnitByteSize((ulong)mod.FileSizeBytes));
					ModVersionAndSize.Pop();
					ModVersionAndSize.AppendText(" (shared)");
				ModVersionAndSize.Pop();
			ModVersionAndSize.Pop();
			//ModVersionAndSize.AppendText("\n");
			ModVersionAndSize.AppendText(" | ");
			ModVersionAndSize.AppendText(lastPublicationDatePrefix);
			ModVersionAndSize.PushColor(Colors.LightSteelBlue);
				ModVersionAndSize.AppendText(lastPublicationDateString);
			ModVersionAndSize.Pop();
#pragma warning restore format


			/*
			if (!string.IsNullOrWhiteSpace(author)) {
				ModNameAndAuthor.Clear();
				ModNameAndAuthor.PushFontSize(16);
				ModNameAndAuthor.PushContext();
				ModNameAndAuthor.AppendText(formattedFriendlyName);
				ModNameAndAuthor.PopContext();
				ModNameAndAuthor.Pop();
				ModNameAndAuthor.PushFontSize(10);
				ModNameAndAuthor.AppendText("\nby ");
				ModNameAndAuthor.PushColor(Colors.MediumSeaGreen);
				ModNameAndAuthor.PushContext();
				ModNameAndAuthor.AppendText(FormatTools.StarboundMarkupToBBCode(author.Replace("\n", null).Replace("\r", null), true));
				ModNameAndAuthor.PopContext();
				ModNameAndAuthor.Pop();
				ModNameAndAuthor.AppendText(" - Hover for more information.");
				ModNameAndAuthor.Pop();
			} else {
				ModNameAndAuthor.Clear();
				ModNameAndAuthor.PushContext();
				ModNameAndAuthor.AppendText(formattedFriendlyName);
				ModNameAndAuthor.PopContext();
				ModNameAndAuthor.PushFontSize(10);
				ModNameAndAuthor.AppendText("\nHover for more information.");
				ModNameAndAuthor.Pop();
			}
			if (!string.IsNullOrWhiteSpace(version)) {
				ModVersionAndSize.Clear();
				ModVersionAndSize.AppendText("Version ");
				ModVersionAndSize.PushColor(Colors.MediumSeaGreen);
				ModVersionAndSize.PushContext();
				ModVersionAndSize.AppendText();
				ModVersionAndSize.PopContext();
				ModVersionAndSize.Pop();

				ModVersionAndSize.PushColor(Colors.LightGray);
				if (mod.Owner.IsWorkshopMod && WorkshopUpdateInfo.TryGetUpdateInformation(mod.Owner.WorkshopID, out WorkshopUpdateInfo.VersionBinding updateInfo) && updateInfo.lastUpdatedForCurrentInstall != 0) {
					ModVersionAndSize.AppendText($" | Published on {updateInfo.CurrentInstalledUpdateDate}");
				} else {
					if (mod.Owner.IsWorkshopMod) {
						ModVersionAndSize.AppendText(" | Published on [lb]??? Information Outdated]");
					} else {
						try {
							DateTime creation = File.GetCreationTime(mod.AbsolutePath);
							ModVersionAndSize.AppendText($" | File made on {creation}");
						} catch (FileNotFoundException) {
						} catch (DirectoryNotFoundException) { }
					}
				}
				ModVersionAndSize.Pop();

				ModVersionAndSize.AppendText("\nSize: ");
				ModVersionAndSize.PushColor(Colors.Gray);
				ModVersionAndSize.AppendText(FormatTools.ToLargestSIUnitByteSize((ulong)mod.FileSizeBytes));
				ModVersionAndSize.Pop();
			} else {
				ModVersionAndSize.Clear();
				ModVersionAndSize.PushItalics();
				ModVersionAndSize.AppendText("N/A");
				ModVersionAndSize.Pop();

				ModVersionAndSize.PushColor(Colors.LightGray);
				if (mod.Owner.IsWorkshopMod && WorkshopUpdateInfo.TryGetUpdateInformation(mod.Owner.WorkshopID, out WorkshopUpdateInfo.VersionBinding updateInfo) && updateInfo.lastUpdatedForCurrentInstall != 0) {
					ModVersionAndSize.AppendText($" | Published on {updateInfo.CurrentInstalledUpdateDate}");
				} else {
					if (mod.Owner.IsWorkshopMod) {
						ModVersionAndSize.AppendText(" | Published on [lb]??? Information Outdated]");
					} else {
						try {
							DateTime creation = File.GetCreationTime(mod.AbsolutePath);
							ModVersionAndSize.AppendText($" | File made on {creation}");
						} catch (FileNotFoundException) {
						} catch (DirectoryNotFoundException) { }
					}
				}
				ModVersionAndSize.Pop();

				ModVersionAndSize.AppendText("\nSize: ");
				ModVersionAndSize.PushColor(Colors.Gray);
				ModVersionAndSize.AppendText(FormatTools.ToLargestSIUnitByteSize((ulong)mod.FileSizeBytes));
				ModVersionAndSize.Pop();
			}
			*/

			ModNameAndAuthor.TooltipText = $"[font_size=22]{formattedFriendlyName}[/font_size]\n";
			if (mod.IsDisabledByForce) {
				ModNameAndAuthor.TooltipText += $"[font_size=16][color=#f77]File name begins with an underscore; this is being forcibly disabled by Starbound itself.[/color][/font_size]\n";
			}
			ModNameAndAuthor.TooltipText += "[font_size=10][color=#aaa][i]Use Page Up and Page Down to scroll...[/i][/color]\n[/font_size]";
			if (mod.IsDirectory) {
				Color = new Color(0.23f, 0.08f, 0.02f);
				ModNameAndAuthor.TooltipText += "[color=#f77]Unpacked mod![/color] This mod may take longer to load.\n";
			}
			ModNameAndAuthor.TooltipText += "[hr]\n";

			string ttTextStored = ModNameAndAuthor.TooltipText;
			ModNameAndAuthor.TooltipText = "[i](This description is loading in the background to not freeze the menu. Try again in a bit.)[/i]\n\n" + ModNameAndAuthor.TooltipText;

			Task.Run(() => {
				string? description = mod.Metadata.SBMMFixedDescription;
				if (description == null) {
					if (!string.IsNullOrWhiteSpace(mod.Metadata.Description)) {
						description = FormatTools.ReparseStarboundIntoBBCode(mod.Metadata.Description, mod.Metadata.SBMMInlineImageHashes);
					} else {
						description = "[i]No description was provided for this mod.[/i]";
					}
					mod.Metadata.SBMMFixedDescription = description;
				} else {

				}
				return description;
			}).ContinueWith(delegate (Task<string> task) {
				if (IsInstanceValid(ModNameAndAuthor)) {
					ModNameAndAuthor.TooltipText = ttTextStored + task.Result;
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}

	}
}
