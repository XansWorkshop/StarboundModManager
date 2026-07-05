using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Menus;
using SBModManager.Menus.Windows;
using SBModManager.ModInstances;
using SBModManager.Other;

namespace SBModManager.IO {
	public static class Importers {


		/// <summary>
		/// Helper method to import a pak or folder as a mod.
		/// </summary>
		/// <param name="editingModpack"></param>
		/// <param name="viewModListPanel"></param>
		/// <param name="pakOrFolderPath"></param>
		/// <param name="isDirectoryAMod">If true or false, a directory import is treated as a mod or as a container. Otherwise it will try to guess hueristically.</param>
		public static void PerformPakOrFolderImport(Modpack editingModpack, ViewModListPanel? viewModListPanel, string pakOrFolderPath, bool? isDirectoryAMod = null) {
			try {
				bool isDirectory = File.GetAttributes(pakOrFolderPath).HasFlag(FileAttributes.Directory);
				GDDictionary? metadata = MetadataReader.ReadMetadataFromDisk(pakOrFolderPath);
				string name = Path.GetFileName(pakOrFolderPath);
				if (metadata != null) {
					name = metadata.GetValueAsStringOrDefault("name", name);
				}
				if (!isDirectory && !name.EndsWith(".pak")) {
					name += ".pak";
				}

				bool directoryIsAModReal = isDirectoryAMod.GetValueOrDefault();
				if (isDirectory && isDirectoryAMod == null) {
					// Let's try to guess.
					bool stop = false;
					directoryIsAModReal = true; // Assume it is, unless we can find child mods:
					foreach (string subdirectory in Directory.GetDirectories(pakOrFolderPath)) {
						string metadataPossiblyUnderscore = Path2.Combine(subdirectory, "_metadata");
						string metadataPossiblyPeriod = Path2.Combine(subdirectory, ".metadata");
						if (File.Exists(metadataPossiblyUnderscore) || File.Exists(metadataPossiblyPeriod)) {
							directoryIsAModReal = false;
							stop = true;
							break;
						}
					}
					if (!stop) {
						foreach (string file in Directory.GetFiles(pakOrFolderPath)) {
							if (Path.GetExtension(file).Equals(".pak", StringComparison.OrdinalIgnoreCase)) {
								directoryIsAModReal = false;
								break;
							}
						}
					}
				}

				string manualModsDir = Directories.GetLocalManualModCacheDirectory();
				string? modSourceDirectory = null;
				// ^ As in ModSource the class.
				string? actualDestinationFile = null;

				if (directoryIsAModReal || !isDirectory) {
					modSourceDirectory = Path2.Combine(manualModsDir, name);
					actualDestinationFile = Path2.Combine(modSourceDirectory, name);
				} else {
					modSourceDirectory = null;
					actualDestinationFile = null;
				}

				if (pakOrFolderPath == actualDestinationFile) {
					// Imported directly from the local cache.
					editingModpack.ModSources.TryAdd(new ModSource(name), true);
				} else {

					try {
						if (File.Exists(actualDestinationFile)) {
							File.Delete(actualDestinationFile);
						} else if (Directory.Exists(actualDestinationFile) && directoryIsAModReal) {
							Directory.Delete(actualDestinationFile, true);
						}
					} catch (FileNotFoundException) {
					} catch (DirectoryNotFoundException) {
					}

					if (isDirectory) {
						if (!directoryIsAModReal) {
							foreach (string subdirectory in Directory.GetDirectories(pakOrFolderPath)) {
								PerformPakOrFolderImport(editingModpack, viewModListPanel, subdirectory, true);
							}

							foreach (string subFile in Directory.GetFiles(pakOrFolderPath)) {
								PerformPakOrFolderImport(editingModpack, viewModListPanel, subFile, true);
							}
						} else {
							Directories.CopyDirectory(pakOrFolderPath, actualDestinationFile!, CancellationToken.None);
							editingModpack.ModSources.TryAdd(new ModSource(name), true);
						}
					} else {
						Directory.CreateDirectory(Path.GetDirectoryName(actualDestinationFile)!);
						File.Copy(pakOrFolderPath, actualDestinationFile!, true);
						editingModpack.ModSources.TryAdd(new ModSource(name), true);
					}
				}
				viewModListPanel?.RebuildList();
			} catch (Exception exc) {
				OS.Alert(exc.Message, "Failed to import mod!");
			}
		}

	}
}
