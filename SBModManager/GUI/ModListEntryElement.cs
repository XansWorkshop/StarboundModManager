using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using SBModManager.Attributes;
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
		/// The mod that this represents.
		/// </summary>
		[AllowNull]
		public ModArchive Mod { get; private set; }

		/// <summary>
		/// The modpack that holds this mod.
		/// </summary>
		[AllowNull]
		public Modpack Pack { get; private set;  }

		public override void _Ready() {
			ImportAttribute.ImportAll(this);
			if (Pack != null && Mod != null) AssignModRoutine(Pack, Mod);

			EnableMod.Toggled += OnEnableModToggled;
			UninstallModButton.Pressed += OnUninstallPressed;


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
					QueueFree();
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
			AddChild(dialog);
		}

		public void AssignMod(Modpack modpack, ModArchive mod) {
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
				ModVersionAndSize.AppendText(FormatTools.StarboundMarkupToBBCode(version.Replace("\n", null).Replace("\r", null), true));
				ModVersionAndSize.PopContext();
				ModVersionAndSize.Pop();
				ModVersionAndSize.AppendText("\nSize: ");
				ModVersionAndSize.PushColor(Colors.Gray);
				ModVersionAndSize.AppendText(FormatTools.ToLargestSIUnitByteSize((ulong)mod.FileSizeBytes));
				ModVersionAndSize.Pop();
			} else {
				ModVersionAndSize.Clear();
				ModVersionAndSize.PushItalics();
				ModVersionAndSize.AppendText("No version information.");
				ModVersionAndSize.Pop();
				ModVersionAndSize.AppendText("\nSize: ");
				ModVersionAndSize.PushColor(Colors.Gray);
				ModVersionAndSize.AppendText(FormatTools.ToLargestSIUnitByteSize((ulong)mod.FileSizeBytes));
				ModVersionAndSize.Pop();
			}

			ModNameAndAuthor.TooltipText = $"[font_size=22]{formattedFriendlyName}[/font_size]\n";
			if (mod.IsDisabledByForce) {
				ModNameAndAuthor.TooltipText += $"[font_size=16][color=#f77]File name begins with an underscore; this is being forcibly disabled by Starbound itself.[/color][/font_size]\n";
			}
			ModNameAndAuthor.TooltipText += "[font_size=10][color=#aaa][i]Use Page Up and Page Down to scroll...[/i][/color]\n[/font_size]";
			if (mod.IsDirectory) {
				Color = new Color(0.23f, 0.08f, 0.02f);
				ModNameAndAuthor.TooltipText += "[color=#f77]Unpacked mod![/color] This mod may take longer to load.\n\n";
			}
			ModNameAndAuthor.TooltipText += "[hr]\n";

			string ttTextStored = ModNameAndAuthor.TooltipText;
			ModNameAndAuthor.TooltipText = "[i](This description is loading in the background to not freeze the menu. Try again in a bit.)[/i]\n\n" + ModNameAndAuthor.TooltipText;
			Task.Run(() => {
				string? description = mod.Metadata.SBMMFixedDescription;
				if (description == null) {
					if (!string.IsNullOrWhiteSpace(mod.Metadata.Description)) {
						description = FormatTools.ReparseStarboundIntoBBCode(mod.Metadata.Description);
					} else {
						description = "[i]No description was provided for this mod.[/i]";
					}
					mod.Metadata.SBMMFixedDescription = description;
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
