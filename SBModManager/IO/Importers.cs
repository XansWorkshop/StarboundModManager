using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Menus;
using SBModManager.Menus.Windows;
using SBModManager.ModInstances;
using SBModManager.Other;

namespace SBModManager.IO {
	public static class Importers {

		private static readonly string[] SB_GENERAL_FOLDERS = [
			"achievements",
			"ai",
			"animations",
			"behaviors",
			"biomes",
			"celestial",
			"cinematics",
			"codex",
			"collections",
			"cursors",
			"damage",
			"dialog",
			"dungeons",
			"effects",
			"humanoid",
			"interface",
			"items",
			"leveling",
			"liquids",
			"monsters",
			"music",
			"names",
			"npcs",
			"objects",
			"parallax",
			"particles",
			"plants",
			"player",
			"projectiles",
			"quests",
			"radiomessages",
			"recipes",
			"rendering",
			"scripts",
			"sfx",
			"ships",
			"sky",
			"spawntypes",
			"species",
			"stagehands",
			"stats",
			"tech",
			"tenants",
			"terrain",
			"tiles",
			"tilesets",
			"treasure",
			"vehicles",
			"weather"
		];


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

				bool isDirectoryAModReal = isDirectoryAMod.GetValueOrDefault();
				bool ifNotModWhyModShaped = false;
				if (isDirectory && isDirectoryAMod == null) {
					// Let's try to guess.
					bool stop = false;
					isDirectoryAModReal = true; // Assume it is, unless we can find child mods:
					foreach (string subdirectory in Directory.GetDirectories(pakOrFolderPath)) {
						string metadataPossiblyUnderscore = Path2.Combine(subdirectory, "_metadata");
						string metadataPossiblyPeriod = Path2.Combine(subdirectory, ".metadata");
						if (File.Exists(metadataPossiblyUnderscore) || File.Exists(metadataPossiblyPeriod)) {
							isDirectoryAModReal = false;
							stop = true;
							break;
						}
						if (Directory.GetFiles(subdirectory, "*.patch").Length > 0) {
							ifNotModWhyModShaped = true;
						} else {
							int hits = 0;
							foreach (string sbKnownFolderName in SB_GENERAL_FOLDERS) {
								if (Directory.Exists(Path2.Combine(subdirectory, sbKnownFolderName))) {
									hits++;
									if (hits >= 3) {
										ifNotModWhyModShaped = true;
										break;
									}
								}
							}
						}
					}
					if (!stop) {
						foreach (string file in Directory.GetFiles(pakOrFolderPath)) {
							if (Path.GetExtension(file).Equals(".pak", StringComparison.OrdinalIgnoreCase)) {
								isDirectoryAModReal = false;
								break;
							}
						}
					}
				}

				if (!isDirectoryAMod.HasValue && isDirectoryAModReal && ifNotModWhyModShaped) {
					// ^ Confusing naming
					// isDirectoryAModReal = true means that our assumption that the directory being dragged is a mod held true because no
					// _metadata was found in any subdirectory, and no .pak files were found within.
					OS.Alert(
$@"None of the folders inside of ""{name}"" have a metadata file, yet the contents of at least one of them still looks like a mod.

SBMM is going to assume that this ""{name}"" folder contains a list of individual mods, but if this guess is incorrect, the import will be broken.

If you created the mod that caused this hiccup, please add metadata to avoid this ambiguity in the future, or pack your mod into a .pak file. Thank you.", 
						////////////////////////
						"If not mod, why mod shaped?"
					);
					GD.PushWarning($"User dragged folder \"{name}\" to import it, but one of its subfolders was ambiguous. It didn't have metadata, but it has a file structire that looks very similar to how a mod looks. SBMM is assuming that \"{name}\" is a collection of many mods, rather than a mod itself.");
					isDirectoryAModReal = false;
				}

				string manualModsDir = Directories.GetLocalManualModCacheDirectory();
				string? modSourceDirectory = null;
				// ^ As in ModSource the class.
				string? actualDestinationFile = null;

				if (isDirectoryAModReal || !isDirectory) {
					modSourceDirectory = Path2.Combine(manualModsDir, name);
					actualDestinationFile = Path2.Combine(modSourceDirectory, name);
				} else {
					modSourceDirectory = null;
					actualDestinationFile = null;
				}

				if (pakOrFolderPath == actualDestinationFile) {
					// Imported directly from the local cache.
					ModSource source = ModSource.GetOrCreateSource(name);
					editingModpack.ModSources.TryAdd(source, true);
					editingModpack.ModAddedOnDate.TryAdd(source, DateTime.Now);
				} else {

					try {
						if (File.Exists(actualDestinationFile)) {
							File.Delete(actualDestinationFile);
						} else if (Directory.Exists(actualDestinationFile) && isDirectoryAModReal) {
							Directory.Delete(actualDestinationFile, true);
						}
					} catch (FileNotFoundException) {
					} catch (DirectoryNotFoundException) {
					}

					if (isDirectory) {
						if (!isDirectoryAModReal) {
							foreach (string subdirectory in Directory.GetDirectories(pakOrFolderPath)) {
								GD.Print($"Importing folder \"{subdirectory}\" as a mod...");
								PerformPakOrFolderImport(editingModpack, viewModListPanel, subdirectory, true);
							}

							foreach (string subFile in Directory.GetFiles(pakOrFolderPath)) {
								GD.Print($"Importing {subFile}...");
								PerformPakOrFolderImport(editingModpack, viewModListPanel, subFile, true);
							}
						} else {
							Directories.CopyDirectoryOverwrite(pakOrFolderPath, actualDestinationFile!, CancellationToken.None);
							ModSource source = ModSource.GetOrCreateSource(name);
							editingModpack.ModSources.TryAdd(source, true);
							editingModpack.ModAddedOnDate.TryAdd(source, DateTime.Now);
						}
					} else {
						Directory.CreateDirectory(Path.GetDirectoryName(actualDestinationFile)!);
						File.Copy(pakOrFolderPath, actualDestinationFile!, true);
						ModSource source = ModSource.GetOrCreateSource(name);
						editingModpack.ModSources.TryAdd(source, true);
						editingModpack.ModAddedOnDate.TryAdd(source, DateTime.Now);
					}
				}
				viewModListPanel?.RebuildList();
			} catch (Exception exc) {
				OS.Alert(exc.Message, "Failed to import mod!");
			}
		}

	}
}
