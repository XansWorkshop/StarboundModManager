using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading;

using SBModManager.Menus.Sorting;
using SBModManager.Other;

namespace SBModManager.ModInstances {

	/// <summary>
	/// Reprensets a set of multiple mods. It is possible for Workshop uploaders to store several mods in one upload, which this addresses.
	/// <para/>
	/// Note that conceptually, a <see cref="ModSource"/> is just a set of mods. It has no context to any specific <see cref="Modpack"/>, and
	/// indeed one instance of this type can be shared across many modpacks. This just represents the mods, and that's it.
	/// </remarks>
	public class ModSource : IEquatable<ModSource>, IComparable<ModSource> {

		private static readonly Dictionary<string, ModSource> INTERNED_MOD_SOURCES = [];

		private static readonly Lock INTERN_LOCK = new Lock();

		/// <summary>
		/// If <see langword="true"/>, this is a workshop mod (and will thus use the workshop catalog folder).
		/// Otherwise, it's a standalone mod and will use the standard catalog folder.
		/// </summary>
		/// <remarks>
		/// Even if the mod itself has a workshop ID, this does not care. This is used to indicate if it was installed
		/// by the workshop cache. It's totally possible that someone manually downloaded a pak or directory that has
		/// a workshop ID, but the fact that they downloaded it manually means this needs to be <see langword="false"/>.
		/// </remarks>
		public bool IsWorkshopMod { get; }

		/// <summary>
		/// The mods that are included in this source. In the vast majority of cases, this will be just one mod.
		/// <para/>
		/// This array is sorted based on the alphabetical name of the mod, not their load order.
		/// </summary>
		public ImmutableArray<ModArchive> Mods { get; } = [];

		/// <summary>
		/// The workshop ID of this mod, or 0 if it is not a workshop mod.
		/// </summary>
		public long WorkshopID { get; }

		/// <summary>
		/// The absolute directory path to this mod source.
		/// </summary>
		public string AbsolutePath { get; }

		/// <summary>
		/// The persistent name of this source is what gets saved in the mod_sources dictionary of a pack.
		/// </summary>
		public string PersistentName { get; }

		/// <summary>
		/// Alias method to get the first mod, or null if there are no mods.
		/// </summary>
		/// <returns></returns>
		public ModArchive? GetFirstModOrDefault() => Mods.Length > 0 ? Mods[0] : null;

		/// <summary>
		/// Returns a <see cref="ModSource"/> for the provided workshop ID. The instance is shared globally.
		/// </summary>
		/// <param name="workshopID"></param>
		/// <returns></returns>
		public static ModSource GetOrCreateSource(long workshopID) {
			string absPath = Path2.Combine(Directories.GetLocalWorkshopCacheDirectory(), workshopID.ToString());
			lock (INTERN_LOCK) {
				if (!INTERNED_MOD_SOURCES.TryGetValue(absPath, out ModSource? existing)) {
					existing = new ModSource(workshopID);
					INTERNED_MOD_SOURCES[absPath] = existing;
				}
				return existing;
			}
		}

		/// <summary>
		/// Returns a <see cref="ModSource"/> for the provided workshop ID. The instance is shared globally.
		/// </summary>
		/// <param name="workshopID"></param>
		/// <returns></returns>
		public static ModSource GetOrCreateSource(string name) {
			string absPath = Path2.Combine(Directories.GetLocalManualModCacheDirectory(), name);
			lock (INTERN_LOCK) {
				if (!INTERNED_MOD_SOURCES.TryGetValue(absPath, out ModSource? existing)) {
					existing = new ModSource(name);
					INTERNED_MOD_SOURCES[absPath] = existing;
				}
				return existing;
			}
		}

		/// <summary>
		/// Create a mod source from a workshop mod. This loads from the workshop catalog.
		/// </summary>
		/// <param name="workshopID"></param>
		internal ModSource(long workshopID) {
			AbsolutePath = Path2.Combine(Directories.GetLocalWorkshopCacheDirectory(), workshopID.ToString());
			if (!Directory.Exists(AbsolutePath)) throw new DirectoryNotFoundException($"No directory exists at {AbsolutePath}");

			IsWorkshopMod = true;
			WorkshopID = workshopID;
			Mods = CreateModList(AbsolutePath, workshopID);
			PersistentName = workshopID.ToString();
		}

		/// <summary>
		/// Create a mod source from a name. This loads from the standard catalog.
		/// </summary>
		/// <param name="name"></param>
		private ModSource(string name) {
			if (name.ContainsAny(Path.GetInvalidFileNameChars())) throw new InvalidOperationException("The provided name is not a valid file name.");

			AbsolutePath = Path2.Combine(Directories.GetLocalManualModCacheDirectory(), name);
			if (!Directory.Exists(AbsolutePath)) throw new DirectoryNotFoundException($"No directory exists at {AbsolutePath}");

			IsWorkshopMod = false;
			WorkshopID = 0;
			Mods = CreateModList(AbsolutePath, 0);
			PersistentName = name;
		}

		/// <summary>
		/// Determines if this source of (a) mod(s) is enabled in the provided <paramref name="modpack"/>.
		/// </summary>
		/// <param name="modpack"></param>
		/// <returns></returns>
		public bool IsEnabledIn(Modpack modpack) {
			if (modpack.ModSources.TryGetValue(this, out bool enabled)) {
				return enabled;
			}
			return false;
		}

		private ImmutableArray<ModArchive> CreateModList(string path, long workshopID) {
			List<ModArchive> result = [];
			foreach (string file in Directory.GetFiles(path)) {
				GDDictionary? data = MetadataReader.GetMetadataFromPak(new FileInfo(file), out bool hadMalformedHeader);
				if (hadMalformedHeader) continue;

				string fileName = Path.GetFileName(file);
				if (data != null && !data.ContainsKey("name")) {
					data["name"] = fileName;
				}
				result.Add(new ModArchive(this, file, new ModMetadata(fileName, data, workshopID)));
			}
			foreach (string directory in Directory.GetDirectories(path)) {
				GDDictionary? data = MetadataReader.GetMetadataFromDirectory(new DirectoryInfo(directory));

				string fileName = Path.GetFileName(directory);
				if (data != null && !data.ContainsKey("name")) {
					data["name"] = fileName;
				}
				result.Add(new ModArchive(this, directory, new ModMetadata(fileName, data, workshopID)));
			}
			return result.ToImmutableArray();
		}

		/// <inheritdoc/>
		public bool Equals(ModSource? other) {
			if (other is null) return false;
			return AbsolutePath == other.AbsolutePath;
		}

		/// <inheritdoc/>
		public override bool Equals(object? obj) => Equals(obj as ModSource);

		public override int GetHashCode() => AbsolutePath.GetHashCode();

		public static bool operator ==(ModSource? left, ModSource? right) {
			if (left is null && right is null) return true;
			if (left is null || right is null) return false;
			return left.Equals(right);
		}

		public static bool operator !=(ModSource? left, ModSource? right) => !(left == right);

		/// <summary>
		/// Sorts mods by the name of the first mod included in the source.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public int CompareTo(ModSource? other) {
			return SortModsByName.Instance.Compare(this, other);
		}
	}
}
