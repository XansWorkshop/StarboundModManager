using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

namespace SBModManager.ModInstances {

	/// <summary>
	/// While this may seem useless at first, this exists to address an edge case where it is possible for a modder to include several .pak files
	/// or directories within their Workshop upload. Only like 4 modders actually do this, but it's a thing and can be useful, so it has to be accounted for.
	/// </summary>
	public class ModSource : IEquatable<ModSource> {

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
		public ulong WorkshopID { get; }

		/// <summary>
		/// The path to this mod source.
		/// </summary>
		public string Path { get; }

		/// <summary>
		/// The persistent name of this source is what gets saved in the mod_sources dictionary of a pack.
		/// </summary>
		public string PersistentName { get; }

		/// <summary>
		/// Create a mod source from a workshop mod. This loads from the workshop catalog.
		/// </summary>
		/// <param name="workshopID"></param>
		public ModSource(ulong workshopID) {
			Path = Path2.Combine(Directories.GetLocalWorkshopCacheDirectory(), workshopID.ToString());
			if (!Directory.Exists(Path)) throw new DirectoryNotFoundException($"No directory exists at {Path}");

			IsWorkshopMod = true;
			WorkshopID = workshopID;
			Mods = CreateModList(Path, workshopID);
			PersistentName = workshopID.ToString();
		}

		/// <summary>
		/// Create a mod source from a name. This loads from the standard catalog.
		/// </summary>
		/// <param name="name"></param>
		public ModSource(string name) {
			if (name.ContainsAny(System.IO.Path.GetInvalidFileNameChars())) throw new InvalidOperationException("The provided name is not a valid file name.");

			Path = Path2.Combine(Directories.GetLocalManualModCacheDirectory(), name);
			if (!Directory.Exists(Path)) throw new DirectoryNotFoundException($"No directory exists at {Path}");

			IsWorkshopMod = false;
			WorkshopID = 0;
			Mods = CreateModList(Path, 0);
			PersistentName = name;
		}

		private ImmutableArray<ModArchive> CreateModList(string path, ulong workshopID) {
			List<ModArchive> result = [];
			foreach (string file in Directory.GetFiles(path)) {
				GDDictionary? data = MetadataReader.GetMetadataFromPak(new FileInfo(file), out bool hadMalformedHeader);
				if (hadMalformedHeader) continue;
				if (data == null) continue;
				if (!data.ContainsKey("name")) {
					data["name"] = System.IO.Path.GetFileNameWithoutExtension(file);
				}
				result.Add(new ModArchive(this, file, new ModMetadata(data, workshopID)));
			}
			return result.ToImmutableArray();
		}

		/// <inheritdoc/>
		public bool Equals(ModSource? other) {
			if (other is null) return false;
			return Path == other.Path;
		}

		/// <inheritdoc/>
		public override bool Equals(object? obj) => Equals(obj as ModSource);

		public override int GetHashCode() => Path.GetHashCode();

		public static bool operator ==(ModSource? left, ModSource? right) {
			if (left is null && right is null) return true;
			if (left is null || right is null) return false;
			return left.Equals(right);
		}

		public static bool operator !=(ModSource? left, ModSource? right) => !(left == right);
	}
}
