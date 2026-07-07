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
		/// The name of the archive only.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// The absolute path to the archive.
		/// </summary>
		public string AbsolutePath { get; }

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

		/*
		/// <summary>
		/// Create a new <see cref="ModArchive"/> within the provided <see cref="ModSource"/> and with the provided archive or directory name.
		/// <para/>
		/// <strong>You probably shouldn't be creating this.</strong> When a <see cref="ModSource"/> is instantiated, it enumerates the directory,
		/// and does not accept manually created archives.
		/// </summary>
		/// <param name="owner"></param>
		/// <param name="path"></param>
		public ModArchive(ModSource owner, string name) {
			Name = name;
			Owner = owner;
			IsDisabledByForce = name.StartsWith('_');
			AbsolutePath = Path2.Combine(owner.AbsolutePath, name);
			IsDirectory = File.GetAttributes(AbsolutePath).HasFlag(FileAttributes.Directory); // Let this throw.
			Metadata = new ModMetadata(this);

			if (!IsDirectory) {
				FileSizeBytes = new FileInfo(AbsolutePath).Length;
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
				GetDirectorySize(new DirectoryInfo(AbsolutePath), ref fsb);
				FileSizeBytes = fsb;
			}
		}
		*/

		/// <summary>
		/// Create a <see cref="ModArchive"/> as a <see cref="ModSource"/>.
		/// <para/>
		/// <strong>You probably shouldn't be creating this.</strong> When a <see cref="ModSource"/> is instantiated, it enumerates the directory,
		/// and does not accept manually created archives. This means the file has to exist <em>before</em> you create this.
		/// </summary>
		/// <param name="owner"></param>
		/// <param name="fullPath"></param>
		/// <param name="metadata"></param>
		internal ModArchive(ModSource owner, string fullPath, ModMetadata metadata) {
			Name = Path.GetFileName(fullPath);
			Owner = owner;
			IsDisabledByForce = Name.StartsWith('_');
			AbsolutePath = fullPath;
			IsDirectory = File.GetAttributes(fullPath).HasFlag(FileAttributes.Directory);
			Metadata = metadata;

			if (!IsDirectory) {
				FileSizeBytes = new FileInfo(AbsolutePath).Length;
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
				GetDirectorySize(new DirectoryInfo(AbsolutePath), ref fsb);
				FileSizeBytes = fsb;
			}
		}

	}
}
