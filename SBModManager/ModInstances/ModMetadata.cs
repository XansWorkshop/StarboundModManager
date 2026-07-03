using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

using Godot.Collections;

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
		/// Corresponds to the "description" field.
		/// </summary>
		public string Description { get; }

		/// <summary>
		/// A copy of the description that has been "fixed" by SBMM, which is where a lot of parsing is done to
		/// transform it from Starbound markup and Steam Workshop markup into Godot markup.
		/// </summary>
		public string? SBMMFixedDescription { get; set; }

		/// <summary>
		/// Corresponds to the "author" field.
		/// </summary>
		public string Author { get; }

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
		public ulong WorkshopID { get; }

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
		public ModMetadata(ModArchive modArchive, ulong fallbackWorkshopID = 0) {
			GDDictionary? dictionary = MetadataReader.ReadMetadataFromDisk(modArchive.Path);

			ModID = Path.GetFileName(modArchive.Path);
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
		}

		/// <summary>
		/// Create a <see cref="ModMetadata"/> directly from a dictionary loaded from <see cref="MetadataReader"/>
		/// </summary>
		/// <param name="json"></param>
		public ModMetadata(GDDictionary json, ulong fallbackWorkshopID) {
			ArgumentNullException.ThrowIfNull(json);
			ModID = json.GetValueAsStringOrDefault("name", string.Empty);
			FriendlyName = json.GetValueAsStringOrDefault("friendlyName", ModID);
			Description = json.GetValueAsStringOrDefault("description", string.Empty);
			Author = json.GetValueAsStringOrDefault("author", string.Empty);
			Version = json.GetValueAsStringOrDefault("version", string.Empty);
			Link = json.GetValueAsStringOrDefault("link", string.Empty);
			WorkshopID = ReadWorkshopIDSpecial(json, "steamContentId", fallbackWorkshopID);

			string tags = json.GetValueAsStringOrDefault("tags", string.Empty);
			Tags = tags.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToImmutableArray();
			OptionalDependencies = GetValueAsStringArrayOrDefault(json, "includes");
			RequiredDependencies = GetValueAsStringArrayOrDefault(json, "requires");
			Priority = GetValueAsIntOrDefault(json, "priority");
			PreviewImage = (Texture2D)json.GetValueOrDefault("preview_image");
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

		private static ulong ReadWorkshopIDSpecial(GDDictionary dictionary, string key, ulong @default) {
			if (dictionary.TryGetValue(key, out Variant value)) {
				if (value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName) {
					if (ulong.TryParse((string)value, out ulong integerValue)) {
						return integerValue;
					}
				} else if (value.VariantType == Variant.Type.Int) {
					return (ulong)value;
				}
			}
			return @default;
		}
	}
}
