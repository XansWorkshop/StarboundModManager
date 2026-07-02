using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using SBModManager.Attributes;
using SBModManager.ModInstances;

namespace SBModManager.GUI {

	/// <summary>
	/// Represents an entry in the mod list. Not to be confused with <see cref="ModPackEntry"/>, which is on the main screen.
	/// </summary>
	public partial class ModListEntry : ColorRect {

		[Import, AllowNull]
		public CheckButton EnableMod { get; }

		[Import, AllowNull]
		public TextureRect ModIcon { get; }

		[Import, AllowNull]
		public RichTextLabel ModNameAndAuthor { get; }

		[Import, AllowNull]
		public RichTextLabel ModVersionAndSize { get; }

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
			if (Pack != null && Mod != null) AssignModpackRoutine(Pack, Mod);

			EnableMod.Toggled += OnEnableModToggled;
		}

		private void OnEnableModToggled(bool toggledOn) {
			Pack.ModSources[Mod.Owner] = toggledOn;
			Modulate = new Color(1, 1, 1, toggledOn ? 1 : 0.5f);
		}

		public void AssignMod(Modpack pack, ModArchive mod) {
			Pack = pack;
			Mod = mod;
			if (!IsNodeReady()) return;
			AssignModpackRoutine(pack, mod);
		}

		private void AssignModpackRoutine(Modpack pack, ModArchive mod) {
			Pack = pack;
			Mod = mod;
			EnableMod.Disabled = !mod.IsExclusive;
			//EnableMod.SetPressedNoSignal()
			ModIcon.Texture = mod.Metadata.PreviewImage;
			
			if (EnableMod.Disabled) {
				EnableMod.TooltipText = "This mod is part of a folder which contains multiple mods at once.\n\nTypically, modders do this if the groups mods [i]must[/i] be together. To turn off this mod, you must disable the entire category.";
			} else {
				EnableMod.TooltipText = string.Empty;
			}

			string friendlyName = mod.Metadata.FriendlyName ?? string.Empty;
			string author = mod.Metadata.Author ?? string.Empty;
			string version = mod.Metadata.Version ?? string.Empty;

			if (!string.IsNullOrWhiteSpace(author)) {
				ModNameAndAuthor.Clear();
				ModNameAndAuthor.PushFontSize(16);
				ModNameAndAuthor.AppendText(FormatTools.ShittyStarboundMarkupToBBCode(friendlyName.Replace("\n", null).Replace("\r", null)));
				ModNameAndAuthor.Pop();
				ModNameAndAuthor.PushFontSize(10);
				ModNameAndAuthor.AppendText("\nby ");
				ModNameAndAuthor.PushColor(Colors.MediumSeaGreen);
				ModNameAndAuthor.AppendText(FormatTools.ShittyStarboundMarkupToBBCode(author.Replace("\n", null).Replace("\r", null)));
				ModNameAndAuthor.Pop();
				ModNameAndAuthor.AppendText(" - Hover for more information.");
				ModNameAndAuthor.Pop();
			} else {
				ModNameAndAuthor.Clear();
				ModNameAndAuthor.AppendText(FormatTools.ShittyStarboundMarkupToBBCode(friendlyName.Replace("\n", null).Replace("\r", null)));
				ModNameAndAuthor.PushFontSize(10);
				ModNameAndAuthor.AppendText("\nHover for more information.");
				ModNameAndAuthor.Pop();
			}
			if (!string.IsNullOrWhiteSpace(version)) {
				ModVersionAndSize.Clear();
				ModVersionAndSize.AppendText("Version ");
				ModVersionAndSize.PushColor(Colors.MediumSeaGreen);
				ModVersionAndSize.AppendText(FormatTools.ShittyStarboundMarkupToBBCode(version.Replace("\n", null).Replace("\r", null)));
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

			ModNameAndAuthor.TooltipText = string.Empty;
			if (mod.IsDirectory) {
				Color = Colors.Wheat;
				ModNameAndAuthor.TooltipText = "[color=wheat]Unpacked mod![/color] This mod may take longer to load.\n\n";
			}

			string description = mod.Metadata.Description;
			if (string.IsNullOrWhiteSpace(description)) {
				ModNameAndAuthor.TooltipText += "[i]No description was provided for this mod.[/i]";
			} else {
				ModNameAndAuthor.TooltipText += FormatTools.ShittyStarboundMarkupToBBCode(description);
			}
		}

	}
}
