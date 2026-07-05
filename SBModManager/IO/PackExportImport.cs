using System;
using System.Buffers;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Godot.NativeInterop;

using SBModManager.GUI;
using SBModManager.Menus.Windows;
using SBModManager.ModInstances;
using SBModManager.Other;
using SBModManager.SteamInterop;

using FileAccess = System.IO.FileAccess;

namespace SBModManager.IO {

	/// <summary>
	/// This class contains the code needed to import and export modpacks.
	/// </summary>
	public static class PackExportImport {

		/// <summary>
		/// Bundles and compresses the provided <paramref name="modpack"/> and then writes it to the provided <paramref name="stream"/>.
		/// </summary>
		/// <param name="modpack">The modpack to store.</param>
		/// <param name="stream">The stream to write the modpack to.</param>
		/// <param name="progressWindow">Can be used to display progress.</param>
		/// <param name="cancellationToken">A cancellation token which can be used to cancel the export.</param>
		/// <exception cref="OperationCanceledException"></exception>
		public static Task ExportModpackAsync(Modpack modpack, Stream stream, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			progressWindow?.SetStatus("Exporting modpack...", "Exporting Modpack");
			progressWindow?.SetProgress(float.NaN);

			return Task.Run(delegate {
				using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
				WriteModpack(writer, modpack, progressWindow, cancellationToken);
			}, cancellationToken);
		}

		/// <summary>
		/// Reads the data stored by <see cref="ExportModpackAsync"/> and turns it into a <see cref="Modpack"/>.
		/// </summary>
		/// <param name="stream">The stream to import the modpack from.</param>
		/// <param name="importAsNewModpack">If true, the GUID in the stream is ignored and a new one is generated. The new modpack has the date added to the name.</param>
		/// <param name="progressWindow">Can be used to display progress.</param>
		/// <param name="cancellationToken">A cancellation token which can be used to cancel the export.</param>
		/// <returns></returns>
		/// <exception cref="OperationCanceledException"></exception>
		public static Task<Modpack> ImportModpackAsync(Stream stream, bool importAsNewModpack, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			progressWindow?.SetStatus("Importing modpack...", "Importing MOdpack");
			progressWindow?.SetProgress(float.NaN);

			return Task.Run(delegate {
				using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
				return ReadModpack(reader, importAsNewModpack, progressWindow, cancellationToken);
			}, cancellationToken);
		}

		#region Writers

		/// <summary>
		/// Writes the metadata of the modpack.
		/// </summary>
		/// <remarks>
		/// This runs synchronously with the intent that it is run on a background thread. The division of work
		/// provided by the <see langword="async"/> keyword is not useful here.
		/// </remarks>
		/// <param name="writer"></param>
		/// <param name="modpack"></param>
		private static void WriteModpack(BinaryWriter writer, Modpack modpack, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();

			writer.Write(modpack.ID.ToByteArray());
			writer.Write(modpack.Name);
			writer.Write(modpack.Creator);
			writer.Write(modpack.Description);

			string directory = Directories.GetPackDirectory(modpack.ID);
			string icon = Path2.Combine(directory, "icon.png");
			try {
				using FileStream fs = File.OpenRead(icon);
				writer.Write(fs.Length);
				fs.CopyTo(writer.BaseStream);
			} catch (FileNotFoundException) {
				writer.Write(0L);
			}

			writer.Write(modpack.ModSources.Count);
			foreach (ModSource source in modpack.ModSources.Keys) {
				WriteModSource(writer, modpack, source, progressWindow, cancellationToken);
			}
		}

		/// <summary>
		/// Writes a <see cref="ModSource"/> to the stream.
		/// </summary>
		/// <param name="writer">The thing to write to the stream.</param>
		/// <param name="modpack">The modpack that this belongs to.</param>
		/// <param name="source">The source within the <paramref name="modpack"/> to write.</param>
		private static void WriteModSource(BinaryWriter writer, Modpack modpack, ModSource source, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();

			const byte BIT_IS_ENABLED        = 1 << 0;
			const byte BIT_IS_WORKSHOP_MOD   = 1 << 1;
			byte flags = 0;
			if (source.IsEnabledIn(modpack)) flags |= BIT_IS_ENABLED;
			if (source.IsWorkshopMod) flags |= BIT_IS_WORKSHOP_MOD;

			writer.Write(source.PersistentName);
			writer.Write(flags);
			if (source.IsWorkshopMod) {
				writer.Write7BitEncodedInt64(source.WorkshopID);
			} else {
				float maxProgress = source.Mods.Length;
				progressWindow?.SetStatus("Storing a mod file...", "Exporting Modpack");

				writer.Write7BitEncodedInt64(source.Mods.Length);
				for (int i = 0; i < source.Mods.Length; i++) {
					progressWindow?.SetProgress(i / maxProgress);
					cancellationToken.ThrowIfCancellationRequested();

					ModArchive archive = source.Mods[i];
					WriteModArchive(writer, modpack, source, archive, progressWindow, cancellationToken);
				}

				progressWindow?.SetStatus("Exporting modpack...", "Exporting Modpack");
				progressWindow?.SetProgress(float.NaN);
			}
		}

		/// <summary>
		/// Writes a <see cref="ModArchive"/> to the stream.
		/// </summary>
		/// <param name="writer">The thing to write to the stream.</param>
		/// <param name="modpack">The modpack that this belongs to.</param>
		/// <param name="source">The source within the <paramref name="modpack"/> to write.</param>
		/// <param name="archive">The actual mod itself, either a .pak file or a directory.</param>
		private static void WriteModArchive(BinaryWriter writer, Modpack modpack, ModSource source, ModArchive archive, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();

			writer.Write(archive.Name);
			writer.Write(archive.IsDirectory);

			if (archive.IsDirectory) {
				WriteDirectoryContents(writer, new DirectoryInfo(archive.AbsolutePath), string.Empty, progressWindow, cancellationToken);
			} else {
				FileInfo pak = new FileInfo(archive.AbsolutePath);
				using FileStream fs = pak.OpenRead();
				writer.Write(fs.Length);
				fs.CopyTo(writer.BaseStream);
			}
		}

		/// <summary>
		/// Writes the contents of a directory to the stream, with a relative path.
		/// </summary>
		/// <param name="writer">The thing to write to the stream.</param>
		/// <param name="dir">The directory whose files will be written.</param>
		/// <param name="pathSoFar">The relative path (to the <see cref="ModArchive"/>) that any files in this directory exist at. Must end with a trailing forward slash.</param>
		private static void WriteDirectoryContents(BinaryWriter writer, DirectoryInfo dir, string pathSoFar, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			FileInfo[] files = dir.GetFiles();
			DirectoryInfo[] dirs = dir.GetDirectories();

			writer.Write(pathSoFar);
			writer.Write(files.Length);
			writer.Write(dirs.Length);
			foreach (FileInfo file in files) {
				cancellationToken.ThrowIfCancellationRequested();

				writer.Write(pathSoFar + file.Name);

				using FileStream fs = file.OpenRead();
				writer.Write(fs.Length);
				fs.CopyTo(writer.BaseStream);
			}

			foreach (DirectoryInfo subDir in dirs) {
				cancellationToken.ThrowIfCancellationRequested();

				WriteDirectoryContents(writer, subDir, pathSoFar + subDir.Name + '/', progressWindow, cancellationToken);
			}
		}

		#endregion

		#region Readers

		/// <summary>
		/// Reads the metadata of the modpack and sets the values in a newly created instance.
		/// </summary>
		/// <remarks>
		/// This runs synchronously with the intent that it is run on a background thread. The division of work
		/// provided by the <see langword="async"/> keyword is not useful here.
		/// </remarks>
		/// <param name="reader">The reader to read the modpack from.</param>
		/// <param name="ignoreGuid">If true, the ID in the stream is ignored and the modpack gets a new one.</param>
		private static Modpack ReadModpack(BinaryReader reader, bool ignoreGuid, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();

			Modpack modpack;
			string appended = string.Empty;
			if (ignoreGuid) {
				reader.ReadBytes(16); // No seeking.
				modpack = new Modpack();
				appended = $" (Imported {DateTime.Now})";
			} else {
				modpack = new Modpack(new Guid(reader.ReadBytes(16)));
			}
			modpack.Name = reader.ReadString() + appended;
			modpack.Creator = reader.ReadString();
			modpack.Description = reader.ReadString();
			modpack.SaveAndUpdateInitAsync(cancellationToken).Wait(CancellationToken.None); // Save so the folders and stuff get created

			long iconLength = reader.ReadInt64();
			string directory = Directories.GetPackDirectory(modpack.ID);
			string icon = Path2.Combine(directory, "icon.png");
			if (iconLength > 0) {
				using FileStream fs = File.OpenWrite(icon);
				byte[] buffer = new byte[iconLength];
				reader.BaseStream.ReadExactly(buffer);
				fs.Write(buffer);
			}

			int sources = reader.ReadInt32();
			Dictionary<long, bool> workshopMods = [];
			for (int i = 0; i < sources; i++) {
				ModSource? source = ReadAndExtractModSource(reader, out long pendingWorkshopID, out bool isEnabled, progressWindow, cancellationToken);
				if (source == null) {
					workshopMods.Add(pendingWorkshopID, isEnabled);
				} else {
					modpack.ModSources[source] = isEnabled;
				}
			}

			progressWindow?.SetStatus("Importing Workshop Mods...\nThis might take a while.");
			progressWindow?.SetProgress(float.NaN);

			HashSet<long> failed = SteamTools.DownloadWorkshopModsAsync(workshopMods.Keys.ToArray(), true, cancellationToken).Result.ToHashSet();
			foreach ((long id, bool enabled) in workshopMods) {
				if (failed.Contains(id)) continue;
				ModSource source = new ModSource(id);
				modpack.ModSources[source] = enabled;
			}

			modpack.SaveAndUpdateInitAsync(cancellationToken).Wait(CancellationToken.None);

			return modpack;
		}

		/// <summary>
		/// Reads a <see cref="ModSource"/> from the stream. If the mod is a workshop mod, it instead
		/// returns <see langword="null"/> and sets <paramref name="pendingWorkshopID"/>.
		/// </summary>
		/// <param name="writer">The stream to read from.</param>
		/// <param name="modpack">The modpack that this belongs to.</param>
		/// <param name="source">The source within the <paramref name="modpack"/> to write.</param>
		private static ModSource? ReadAndExtractModSource(BinaryReader reader, out long pendingWorkshopID, out bool isEnabled, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();

			const byte BIT_IS_ENABLED        = 1 << 0;
			const byte BIT_IS_WORKSHOP_MOD   = 1 << 1;

			string persistentName = reader.ReadString();
			byte flags = reader.ReadByte();
			isEnabled = (flags & BIT_IS_ENABLED) != 0;
			bool isWorkshopMod = (flags & BIT_IS_WORKSHOP_MOD) != 0;

			// Validate the persistent name since it's part of a file path:
			if (persistentName.Contains('/')) throw new InvalidDataException("A directory separator was in the source's name.");
			StreamValidators.AssertModPathIsValid(persistentName);

			if (isWorkshopMod) {
				pendingWorkshopID = reader.Read7BitEncodedInt64();
				return null;
			} else {
				long modCount = reader.Read7BitEncodedInt64();
				float maxProgress = modCount;
				progressWindow?.SetStatus("Reading a mod file...", "Exporting Modpack");

				pendingWorkshopID = 0;
				for (long i = 0; i < modCount; i++) {
					progressWindow?.SetProgress(i / maxProgress);
					cancellationToken.ThrowIfCancellationRequested();
					ReadAndExtractModArchive(reader, persistentName, progressWindow, cancellationToken);
				}

				progressWindow?.SetStatus("Importing modpack...", "Exporting Modpack");
				progressWindow?.SetProgress(float.NaN);

				// Now I can create this, which will load the archives from disk.
				return new ModSource(persistentName);
			}
		}

		/// <summary>
		/// Reads one or more <see cref="ModArchive"/> from the stream, and extracts them onto the disk. This is used during the construction
		/// of a <see cref="ModSource"/>.
		/// </summary>
		/// <param name="reader">The stream to read from.</param>
		/// <param name="sourcePersistentName">The <see cref="ModSource.PersistentName"/> of the parent source. It is expected that the caller validates this.</param>
		private static void ReadAndExtractModArchive(BinaryReader reader, string sourcePersistentName, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();

			string name = reader.ReadString();
			bool isDirectory = reader.ReadBoolean();

			if (name.Contains('/')) throw new InvalidDataException("A directory separator was in the archive's name.");
			StreamValidators.AssertModPathIsValid(name);

			string archiveBasePath = Path2.Combine(Directories.GetLocalManualModCacheDirectory(), name, sourcePersistentName);
			if (isDirectory) {
				ReadAndExtractDirectoryContents(reader, archiveBasePath, progressWindow, cancellationToken);
			} else {
				Directory.CreateDirectory(Path.GetDirectoryName(archiveBasePath)!);
				long length = reader.ReadInt64();
				using FileStream fs = File.OpenWrite(archiveBasePath);
				byte[] buffer = new byte[length];
				reader.ReadExactly(buffer);
				fs.Write(buffer);
			}
		}

		/// <summary>
		/// Reads the contents of a directory from the stream, and extracts those files to disk.
		/// </summary>
		/// <param name="reader">The thing to write to the stream.</param>
		/// <param name="archiveBasePath">The path of the <see cref="ModArchive"/>. It is expected that the caller validates this.</param>
		private static void ReadAndExtractDirectoryContents(BinaryReader reader, string archiveBasePath, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			string containerDirPath = reader.ReadString();
			DirectoryInfo containerDir;
			if (containerDirPath.Length == 0) {
				containerDir = new DirectoryInfo(archiveBasePath);
				containerDir.Create();
			} else {
				// Here, I expect it to end in a slash. Once the slash is removed, I expect the path to be perfect.
				if (containerDirPath[^1] != '/') throw new InvalidDataException("Invalid container directory path.");
				StreamValidators.AssertModPathIsValid(containerDirPath.AsSpan()[0..^1]);
				containerDir = new DirectoryInfo(Path2.Combine(archiveBasePath, containerDirPath));
				containerDir.Create();
			}

			int fileCount = reader.ReadInt32();
			int dirCount = reader.ReadInt32();
			for (int i = 0; i < fileCount; i++) {
				cancellationToken.ThrowIfCancellationRequested();

				string fileDestination = reader.ReadString();
				long length = reader.ReadInt64();
				StreamValidators.AssertModPathIsValid(fileDestination);

				using FileStream fs = File.OpenWrite(Path2.Combine(archiveBasePath, fileDestination));
				byte[] buffer = new byte[length];
				reader.ReadExactly(buffer);
				fs.Write(buffer);
			}

			for (int i = 0; i < dirCount; i++) {
				cancellationToken.ThrowIfCancellationRequested();

				// Remember, do not append anything!
				// We write the path to the buffer. originalArchiveBasePath is for what it says on the tin.
				ReadAndExtractDirectoryContents(reader, archiveBasePath, progressWindow, cancellationToken);
			}
		}

		#endregion
	}
}
