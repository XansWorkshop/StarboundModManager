using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SBModManager.GUI;
using SBModManager.Menus.Windows;
using SBModManager.ModInstances;

namespace SBModManager {

	/// <summary>
	/// Provides access to scene assets.
	/// </summary>
	public static class Assets {

		#region Cache

		/// <summary>
		/// The <see cref="Texture2D"/> for the game's icon.
		/// </summary>
		public static Texture2D DefaultStarboundIcon => field ??= GD.Load<Texture2D>("res://icons/starbound.png");

		/// <summary>
		/// The <see cref="Texture2D"/> for the loading placeholder for Workshop description images that are displayed inline.
		/// </summary>
		public static Image PlaceholderWorkshopImageLoading => field ??= GD.Load<Image>("res://icons/loading.png");

		/// <summary>
		/// The <see cref="Texture2D"/> for the error placeholder for Workshop description images that are displayed inline.
		/// </summary>
		public static Image PlaceholderWorkshopImageError => field ??= GD.Load<Image>("res://icons/error.png");

		/// <summary>
		/// The scene file for <see cref="CreateGeneralProgressWindow"/>
		/// </summary>
		private static PackedScene GeneralProgressWindowPrefab => field ??= GD.Load<PackedScene>("res://ui_elements/popups/general_progress_window.tscn");

		/// <summary>
		/// The scene file for <see cref="CreateGeneralProgressWindow"/>
		/// </summary>
		private static PackedScene TooltipPrefab => field ??= GD.Load<PackedScene>("res://ui_elements/tooltip.tscn");

		/// <summary>
		/// The scene file for <see cref="CreateModpackEntryElement"/>
		/// </summary>
		private static PackedScene ModpackEntryPrefab => field ??= GD.Load<PackedScene>("res://ui_elements/modpack_entry.tscn");

		/// <summary>
		/// The scene file for <see cref="CreateModListEntryElement"/>
		/// </summary>
		private static PackedScene ModListEntryPrefab => field ??= GD.Load<PackedScene>("res://ui_elements/mod_list_entry.tscn");

		/// <summary>
		/// The scene file for <see cref="CreateModBundleElement"/>
		/// </summary>
		private static PackedScene ModBundleElementPrefab => field ??= GD.Load<PackedScene>("res://ui_elements/mod_bundle.tscn");

		/// <summary>
		/// The scene file for <see cref="CreateConfirmDeleteDialog"/>
		/// </summary>
		private static PackedScene ConfirmDeleteDialogPrefab => field ??= GD.Load<PackedScene>("res://ui_elements/popups/confirm_delete.tscn");

		#endregion

		#region Simple Creators

		/// <summary>
		/// Instantiates a new <see cref="GeneralProgressWindow"/> from the internal prefab.
		/// </summary>
		/// <returns></returns>
		public static GeneralProgressWindow CreateGeneralProgressWindow() => GeneralProgressWindowPrefab.Instantiate<GeneralProgressWindow>();

		/// <summary>
		/// Instantiates a new <see cref="ModpackEntryElement"/> from the internal prefab.
		/// </summary>
		/// <returns></returns>
		public static ModpackEntryElement CreateModpackEntryElement() => ModpackEntryPrefab.Instantiate<ModpackEntryElement>();

		/// <summary>
		/// Instantiates a new <see cref="ModListEntryElement"/> from the internal prefab.
		/// </summary>
		/// <returns></returns>
		public static ModListEntryElement CreateModListEntryElement() => ModListEntryPrefab.Instantiate<ModListEntryElement>();

		/// <summary>
		/// Instantiates a new <see cref="ModBundleElement"/> from the internal prefab.
		/// </summary>
		/// <returns></returns>
		public static ModBundleElement CreateModBundleElement() => ModBundleElementPrefab.Instantiate<ModBundleElement>();

		/// <summary>
		/// Instantiates a new <see cref="ConfirmDeleteDialog"/> from the internal prefab.
		/// </summary>
		/// <returns></returns>
		public static ConfirmDeleteDialog CreateConfirmDeleteDialog() => ConfirmDeleteDialogPrefab.Instantiate<ConfirmDeleteDialog>();

		/// <summary>
		/// A 1x1 texture.
		/// </summary>
		public static ImageTexture Dummy1x1 => field ??= ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));

		/// <summary>
		/// Create a new tooltip using the custom rich-text version. Returns <see langword="null"/> if the text is null.
		/// </summary>
		/// <param name="forText">The bbcode text to display.</param>
		/// <returns></returns>
		public static GodotObject? CreateTooltip(string forText) {
			if (string.IsNullOrWhiteSpace(forText)) return null;
			MovingRichTextLabel label = TooltipPrefab.Instantiate<MovingRichTextLabel>();
			label.Text = forText;
			return label;
		}

		#endregion

		#region Automation

		/// <summary>
		/// A more complete alternative to <see cref="CreateModListEntryElement"/> which sets the object's name
		/// for you, as well as calling <see cref="ModListEntryElement.AssignMod(Modpack, ModArchive)"/>.
		/// </summary>
		/// <param name="modpack">The modpack that the <see cref="ModArchive"/> exists in the context of.</param>
		/// <param name="mod">The mod itself.</param>
		/// <returns></returns>
		public static ModListEntryElement CreateModListEntryElementFor(Modpack modpack, ModArchive mod) {
			ModListEntryElement element = CreateModListEntryElement();
			element.Name = mod.Metadata.ModID;
			element.AssignMod(modpack, mod);
			return element;
		}

		/// <summary>
		/// A more complete alternative to <see cref="CreateModBundleElement"/> which sets the object's name
		/// for you, as well as calling <see cref="ModBundleElement.AssignModpack(Modpack, ModSource)"/>.
		/// </summary>
		/// <param name="modpack">The modpack that the <see cref="ModSource"/> exists in the context of.</param>
		/// <param name="source">The container of one or more mods.</param>
		/// <returns></returns>
		public static ModBundleElement CreateModBundleElementFor(Modpack modpack, ModSource source) {
			ModBundleElement element = CreateModBundleElement();
			element.Name = string.Join(',', source.Mods.Select(mod => mod.Metadata.ModID));
			element.AssignModpack(modpack, source);
			return element;
		}


		/// <summary>
		/// A more complete alternative to <see cref="CreateModBundleElement"/> which sets the object's name
		/// for you, as well as calling <see cref="ModBundleElement.AssignModpack(Modpack, ModSource)"/>.
		/// </summary>
		/// <param name="modpack">The modpack that it represents.</param>
		public static ModpackEntryElement CreateModpackEntryElementFor(Modpack modpack) {
			ModpackEntryElement button = CreateModpackEntryElement();
			button.Name = modpack.ID.ToString("D");
			button.AssignOrUpdateModpack(modpack);
			return button;
		}


		#endregion



	}
}
