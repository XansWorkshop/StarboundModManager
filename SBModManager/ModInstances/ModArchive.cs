using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

using SBModManager.Other;

namespace SBModManager.ModInstances {

	/// <summary>
	/// Represents a mod archive in the cache. This will usually be a .pak file, but may also be a directory.
	/// <para/>
	/// See the documentation of <see cref="ModSource"/> for how to conceptualize this.
	/// </summary>
	public class ModArchive {

		/// <summary>
		/// The source (workshop installation, manual installation) that this belongs to.
		/// </summary>
		public ModSource Owner { get; }

		/// <summary>
		/// If the name of the mod archive begins with an underscore <c>_</c>, Starbound will skip loading it.
		/// </summary>
		public bool IsDisabledByForce { get; }

		/// <summary>
		/// If <see langword="true"/>, this archive is a directory. Otherwise, it is a .pak file.
		/// </summary>
		public bool IsDirectory { get; }

		/// <summary>
		/// The absolute path to is archive.
		/// </summary>
		public string Path { get; }

		/// <summary>
		/// The metadata of this mod.
		/// </summary>
		public ModMetadata Metadata { get; }

		/// <summary>
		/// If <see langword="true"/>, this is the only mod in its parent <see cref="ModSource"/>.
		/// </summary>
		public bool IsExclusive => Owner.Mods.Length == 1;

		/// <summary>
		/// The size of the file, in bytes.
		/// </summary>
		public long FileSizeBytes { get; }

		/// <summary>
		/// Create a new <see cref="ModArchive"/> within the provided <see cref="ModSource"/> and with the provided archive or directory name.
		/// </summary>
		/// <param name="owner"></param>
		/// <param name="path"></param>
		public ModArchive(ModSource owner, string name) {
			Owner = owner;
			IsDisabledByForce = name.StartsWith('_');
			Path = Path2.Combine(owner.Path, name);
			IsDirectory = File.GetAttributes(Path).HasFlag(FileAttributes.Directory); // Let this throw.
			Metadata = new ModMetadata(this);

			if (!IsDirectory) {
				FileSizeBytes = new FileInfo(Path).Length;
			} else {
				static void GetDirectorySize(DirectoryInfo dir, ref long fileSizeBytes) {
					foreach (FileInfo file in dir.GetFiles()) {
						fileSizeBytes += file.Length;
					}
					foreach (DirectoryInfo subDir in dir.GetDirectories()) {
						GetDirectorySize(subDir, ref fileSizeBytes);
					}
				}
				long fsb = 0;
				GetDirectorySize(new DirectoryInfo(Path), ref fsb);
				FileSizeBytes = fsb;
			}
		}

		internal ModArchive(ModSource owner, string fullPath, ModMetadata metadata) {
			Owner = owner;
			IsDisabledByForce = System.IO.Path.GetFileName(fullPath)!.StartsWith('_');
			Path = fullPath;
			IsDirectory = File.GetAttributes(fullPath).HasFlag(FileAttributes.Directory);
			Metadata = metadata;

			if (!IsDirectory) {
				FileSizeBytes = new FileInfo(Path).Length;
			} else {
				static void GetDirectorySize(DirectoryInfo dir, ref long fileSizeBytes) {
					foreach (FileInfo file in dir.GetFiles()) {
						fileSizeBytes += file.Length;
					}
					foreach (DirectoryInfo subDir in dir.GetDirectories()) {
						GetDirectorySize(subDir, ref fileSizeBytes);
					}
				}
				long fsb = 0;
				GetDirectorySize(new DirectoryInfo(Path), ref fsb);
				FileSizeBytes = fsb;
			}
		}

	}
}
