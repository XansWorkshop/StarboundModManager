using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

using Godot.Collections;

using SBModManager.GUI;
using SBModManager.Other;

namespace SBModManager.ModInstances {

	/// <summary>
	/// Represents the metadata file of a mod. This has some different names for fields, in particular:
	/// <list type="table">
	/// 
	/// <item>
	/// <term><c>"name"</c></term>
	/// <description><see cref="ModID"/></description>
	/// </item>
	/// 
	/// <item>
	/// <term><c>"steamContentId"</c></term>
	/// <description><see cref="WorkshopID"/></description>
	/// </item>
	/// 
	/// <item>
	/// <term><c>"includes"</c></term>
	/// <description><see cref="OptionalDependencies"/></description>
	/// </item>
	/// 
	/// <item>
	/// <term><c>"requires"</c></term>
	/// <description><see cref="RequiredDependencies"/></description>
	/// </item>
	/// </list>
	/// </summary>
	public class ModMetadata {

		/// <summary>
		/// Corresponds to the "name" field.
		/// </summary>
		public string ModID { get; }

		/// <summary>
		/// Corresponds to the "friendlyName" field.
		/// </summary>
		public string FriendlyName { get; }

		/// <summary>
		/// Added by SBMM, this is the same as <see cref="FriendlyName"/> but without its markup.
		/// </summary>
		public string SBMMFriendlyNameNoMarkup { get; }

		/// <summary>
		/// Corresponds to the "description" field.
		/// </summary>
		public string Description { get; }

		/// <summary>
		/// A copy of the description that has been "fixed" by SBMM, which is where a lot of parsing is done to
		/// transform it from Starbound markup and Steam Workshop markup into Godot markup.
		/// </summary>
		public string? SBMMFixedDescription { get; set; }

		/// <summary>
		/// Every res:// path used by inline images in the <see cref="SBMMInlineImageHashes"/>. Specifically,
		/// this is just the md5 part which is how they are saved on disk.
		/// </summary>
		public List<string> SBMMInlineImageHashes { get; } = [];

		/// <summary>
		/// Corresponds to the "author" field.
		/// </summary>
		public string Author { get; }

		/// <summary>
		/// Added by SBMM, this is the same as <see cref="Author"/> but without its markup.
		/// </summary>
		public string SBMMAuthorNoMarkup { get; }

		/// <summary>
		/// Corresponds to the "version" field.
		/// </summary>
		public string Version { get; }

		/// <summary>
		/// Corresponds to the "link" field.
		/// </summary>
		public string Link { get; }

		/// <summary>
		/// Corresponds to the "steamContentId" field.
		/// </summary>
		public long WorkshopID { get; }

		/// <summary>
		/// Corresponds to the "tags" field.
		/// </summary>
		public ImmutableArray<string> Tags { get; }

		/// <summary>
		/// Corresponds to the "incldues" field.
		/// </summary>
		public ImmutableArray<string> OptionalDependencies { get; }

		/// <summary>
		/// Corresponds to the "requires" field.
		/// </summary>
		public ImmutableArray<string> RequiredDependencies { get; }

		/// <summary>
		/// Corresponds to the "priority" field.
		/// </summary>
		public int Priority { get; }

		/// <summary>
		/// Loaded alongside the metadata and bundled here for convenience, this is the preview image assigned to the mod.
		/// This image is normally only used on the Steam Workshop but it's there so I might as well use it, you know?
		/// </summary>
		public Texture2D? PreviewImage { get; }

		/// <summary>
		/// Create a <see cref="ModMetadata"/> wrapping the metadata found in the provided archive.
		/// </summary>
		/// <param name="modArchive">The archive that this belongs to.</param>
		/// <param name="fallbackWorkshopID">To be used by multi-mod sources from the Workshop.</param>
		public ModMetadata(ModArchive modArchive, long fallbackWorkshopID = 0) {
			GDDictionary? dictionary = MetadataReader.ReadMetadataFromDisk(modArchive.AbsolutePath);

			ModID = Path.GetFileName(modArchive.AbsolutePath);
			FriendlyName = ModID;
			Description = string.Empty;
			Author = string.Empty;
			Version = string.Empty;
			Link = string.Empty;
			WorkshopID = fallbackWorkshopID;
			Tags = [];
			OptionalDependencies = [];
			RequiredDependencies = [];
			Priority = 0;
			PreviewImage = null;
			if (dictionary != null) {
				ModID = dictionary.GetValueAsStringOrDefault("name", ModID);
				FriendlyName = dictionary.GetValueAsStringOrDefault("friendlyName", FriendlyName);
				Description = dictionary.GetValueAsStringOrDefault("description", Description);
				Author = dictionary.GetValueAsStringOrDefault("author", Author);
				Version = dictionary.GetValueAsStringOrDefault("version", Version);
				Link = dictionary.GetValueAsStringOrDefault("link", Link);
				WorkshopID = ReadWorkshopIDSpecial(dictionary, "steamContentId", fallbackWorkshopID);

				string tags = dictionary.GetValueAsStringOrDefault("tags", string.Empty);
				Tags = tags.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToImmutableArray();
				OptionalDependencies = GetValueAsStringArrayOrDefault(dictionary, "includes");
				RequiredDependencies = GetValueAsStringArrayOrDefault(dictionary, "requires");
				Priority = GetValueAsIntOrDefault(dictionary, "priority");
				PreviewImage = (Texture2D)dictionary.GetValueOrDefault("preview_image");
			}

			// This also really sucks.
			string bb = FormatTools.StarboundMarkupToBBCode(FriendlyName);
			using RichTextLabel dummy = new RichTextLabel {
				BbcodeEnabled = true,
				Text = bb
			};
			SBMMFriendlyNameNoMarkup = dummy.GetParsedText();

			bb = FormatTools.StarboundMarkupToBBCode(Author);
			dummy.Text = bb;
			SBMMAuthorNoMarkup = dummy.GetParsedText();
		}

		/// <summary>
		/// Create a <see cref="ModMetadata"/> directly from a dictionary loaded from <see cref="MetadataReader"/>
		/// </summary>
		/// <param name="fallbackModID">The name of the mod file archive being loaded. This is only used if <paramref name="json"/> is null.</param>
		/// <param name="json">The json containing the metadata, if it exists.</param>
		/// <param name="fallbackWorkshopID">In case the workshop ID can't be read from the json file, this is used</param>
		public ModMetadata(string fallbackModID, GDDictionary? json, long fallbackWorkshopID) {
			if (json != null) {
				ModID = json.GetValueAsStringOrDefault("name", string.Empty);
				FriendlyName = json.GetValueAsStringOrDefault("friendlyName", ModID);
				Description = json.GetValueAsStringOrDefault("description", string.Empty);
				Author = json.GetValueAsStringOrDefault("author", string.Empty);
				Version = json.GetValueAsStringOrDefault("version", string.Empty);
				Link = json.GetValueAsStringOrDefault("link", string.Empty);
				WorkshopID = ReadWorkshopIDSpecial(json, "steamContentId", fallbackWorkshopID);
				// TODO: Should a mismatch be an exception?

				string tags = json.GetValueAsStringOrDefault("tags", string.Empty);
				Tags = tags.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToImmutableArray();
				OptionalDependencies = GetValueAsStringArrayOrDefault(json, "includes");
				RequiredDependencies = GetValueAsStringArrayOrDefault(json, "requires");
				Priority = GetValueAsIntOrDefault(json, "priority");
				PreviewImage = (Texture2D)json.GetValueOrDefault("preview_image");
			} else {
				ModID = fallbackModID;
				FriendlyName = ModID;
				Description = string.Empty;
				Author = string.Empty;
				Version = string.Empty;
				Link = string.Empty;
				WorkshopID = fallbackWorkshopID;
				Tags = [];
				OptionalDependencies = [];
				RequiredDependencies = [];
				Priority = 0;
				PreviewImage = null;
			}

			// This also really sucks.
			string bb = FormatTools.StarboundMarkupToBBCode(FriendlyName);
			using RichTextLabel dummy = new RichTextLabel {
				BbcodeEnabled = true,
				Text = bb
			};
			SBMMFriendlyNameNoMarkup = dummy.GetParsedText();

			bb = FormatTools.StarboundMarkupToBBCode(Author);
			dummy.Text = bb;
			SBMMAuthorNoMarkup = dummy.GetParsedText();
		}

		private static int GetValueAsIntOrDefault(GDDictionary dictionary, string key, int @default = 0) {
			if (dictionary.TryGetValue(key, out Variant value)) {
				if (value.VariantType == Variant.Type.Int) {
					return (int)value;
				}
			}
			return @default;
		}

		public static ImmutableArray<string> GetValueAsStringArrayOrDefault(GDDictionary dictionary, string key) {
			if (dictionary.TryGetValue(key, out Variant value)) {
				if (value.VariantType >= Variant.Type.Array && value.VariantType < Variant.Type.Max) {
					try {
						return value.As<string[]>().ToImmutableArray();
					} catch (InvalidOperationException) { }
				}
			}
			return [];
		}

		private static long ReadWorkshopIDSpecial(GDDictionary dictionary, string key, long @default) {
			if (dictionary.TryGetValue(key, out Variant value)) {
				if (value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName) {
					if (long.TryParse((string)value, out long integerValue)) {
						return integerValue;
					}
				} else if (value.VariantType == Variant.Type.Int) {
					return (long)value;
				}
			}
			return @default;
		}
	}
}
