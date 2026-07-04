using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

namespace SBModManager {

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
		public static async Task ExportModpackAsync(Modpack modpack, Stream stream, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			// Use a file instead of a MemoryStream just in case the modpack exceeds 2GB (int.MaxValue)
			// Also, because the output stream is likely a compressor, we need a temporary buffer to do the staging work.
			using FileStream temporaryBuffer = new FileStream(
				Path.GetTempFileName(),
				FileMode.OpenOrCreate,
				FileAccess.ReadWrite,
				FileShare.None,
				4096,
				FileOptions.RandomAccess | FileOptions.DeleteOnClose
			);

			// Shared string for the progress window.
			const string EXPORTING_MODPACK = "Exporting Modpack...";

			progressWindow?.SetStatus("Initializing...", EXPORTING_MODPACK);
			progressWindow?.SetProgress(float.NaN);

			await Task.Yield();

			using BinaryWriter writer = new BinaryWriter(temporaryBuffer, Encoding.UTF8, true);
			writer.Write(modpack.ID.ToByteArray());
			writer.Write(0); // Version.
			writer.Write(modpack.Name);
			writer.Write(modpack.Creator);
			writer.Write(modpack.Description);

			await Task.Yield();

			string directory = Directories.GetPackDirectory(modpack.ID);
			string icon = Path2.Combine(directory, "icon.png");
			if (File.Exists(icon)) {
				byte[] iconBuffer = File.ReadAllBytes(icon);
				writer.Write(iconBuffer.Length);
				writer.Write(iconBuffer);
			} else {
				writer.Write(0);
			}

			writer.Write(modpack.ModSources.Count);

			// Binds ModSources to the empty 64 bit integer value set aside in the stream where the
			// data address is stored.
			Dictionary<ModSource, long> emptySpaceForAddressValues = [];

			// Write metadata for the mods. 
			cancellationToken.ThrowIfCancellationRequested();
			float maxStep = modpack.ModSources.Count * 2;
			float currentStep = 0;

			if (modpack.ModSources.Count > 0) {

				progressWindow?.SetStatus("Writing Index...", EXPORTING_MODPACK);
				progressWindow?.SetProgress(0.00f);
				foreach (KeyValuePair<ModSource, bool> binding in modpack.ModSources) {
					cancellationToken.ThrowIfCancellationRequested();
					await Task.Yield();

					ModSource source = binding.Key;
					bool isEnabled = binding.Value;
					bool isWorkshop = source.IsWorkshopMod;

					const byte IS_ENABLED = 1;
					const byte IS_WORKSHOP = 2;

					byte packedBits = 0;
					if (isEnabled) packedBits |= IS_ENABLED;
					if (isWorkshop) packedBits |= IS_WORKSHOP;

					writer.Write(packedBits);
					if (isWorkshop) {
						writer.Write(source.WorkshopID);
						writer.Write(0); // Mods length isn't used for workshop.
					} else {
						emptySpaceForAddressValues.Add(source, temporaryBuffer.Position);
						writer.Write(0L); // Temporary space for an address.
						writer.Write(source.Mods.Length);
					}

					progressWindow?.SetProgress(++currentStep / maxStep);
				}

				// Now write the mods themselves.
				progressWindow?.SetStatus("Writing Mods...", EXPORTING_MODPACK);
				foreach (KeyValuePair<ModSource, bool> binding in modpack.ModSources) {
					cancellationToken.ThrowIfCancellationRequested();
					await Task.Yield();

					ModSource source = binding.Key;
					long currentAddress = temporaryBuffer.Position;
					if (emptySpaceForAddressValues.TryGetValue(source, out long locationOfAddress)) {
						// An address was allocated, not a workshop ID.
						temporaryBuffer.Seek(locationOfAddress, SeekOrigin.Begin);
						writer.Write(currentAddress);
						temporaryBuffer.Seek(currentAddress, SeekOrigin.Begin);

						writer.Write(source.PersistentName);
						foreach (ModArchive mod in source.Mods) {
							string name = Path.GetFileName(mod.Path)!;
							writer.Write(mod.IsDirectory);
							writer.Write(name);
							if (mod.IsDirectory) {
								writer.Write(0L); // Size.
								long bookmark = temporaryBuffer.Position;
								TarFile.CreateFromDirectory(mod.Path, temporaryBuffer, false);
								long now = temporaryBuffer.Position;
								long size = now - bookmark;
								temporaryBuffer.Seek(bookmark, SeekOrigin.Begin);
								writer.Write(size);
								temporaryBuffer.Seek(now, SeekOrigin.Begin);
								// :|
							} else {
								byte[] pakFileBytes = File.ReadAllBytes(mod.Path);
								writer.Write(pakFileBytes.LongLength);
								writer.Write(pakFileBytes);
							}
						}
					}

					progressWindow?.SetProgress(++currentStep / maxStep);
				}
			}
			temporaryBuffer.Seek(0, SeekOrigin.Begin);
			temporaryBuffer.CopyTo(stream);
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
		public static async Task<Modpack> ImportModpackAsync(Stream stream, bool importAsNewModpack, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
			Guid guid = new Guid(reader.ReadBytes(16));
			uint version = reader.ReadUInt32();
			if (version != 0) throw new NotSupportedException("Unsupported version.");
			if (importAsNewModpack) guid = Guid.NewGuid();

			const string IMPORTING_MODPACK = "Importing Modpack...";
			progressWindow?.SetStatus("Initializing...", IMPORTING_MODPACK);
			progressWindow?.SetProgress(float.NaN);

			await Task.Yield();

			Modpack? result = Core.Instance.CurrentModpacks.Find(modpack => modpack.ID == guid);
			result?.ModSources.Clear();

			string name = reader.ReadString();
			string creator = reader.ReadString();
			string description = reader.ReadString();

			if (importAsNewModpack) {
				name += $" (Imported {DateTime.Now})";
			}

			result ??= new Modpack(guid) {
				Name = name,
				Creator = creator,
				Description = description
			};
			await result.SaveAndUpdateInitAsync(cancellationToken);

			int imageBufferSize = reader.ReadInt32();
			if (imageBufferSize > 0) {
				byte[] pngData = reader.ReadBytes(imageBufferSize);
				string directory = Directories.GetPackDirectory(guid);
				string destination = Path2.Combine(directory, "icon.png");
				File.WriteAllBytes(destination, pngData);
			}

			int modSourceCount = reader.ReadInt32();

			string manualModsDirectory = Directories.GetLocalManualModCacheDirectory();
			string workshopModsDirectory = Directories.GetLocalWorkshopCacheDirectory();


			progressWindow?.SetStatus("Reading Index...", IMPORTING_MODPACK);
			progressWindow?.SetProgress(0.00f);
			List<(ulong, bool)> pendingWorkshopMods = [];
			for (int i = 0; i < modSourceCount; i++) {
				cancellationToken.ThrowIfCancellationRequested();
				await Task.Yield();

				const byte IS_ENABLED = 1;
				const byte IS_WORKSHOP = 2;

				byte packedBits = reader.ReadByte();
				bool isEnabled = (packedBits & IS_ENABLED) != 0;
				bool isWorkshop = (packedBits & IS_WORKSHOP) != 0;
				if (isEnabled) packedBits |= IS_ENABLED;
				if (isWorkshop) packedBits |= IS_WORKSHOP;

				if (isWorkshop) {
					ulong workshopID = reader.ReadUInt64();
					pendingWorkshopMods.Add((workshopID, isEnabled));
					_ = reader.ReadInt32();
				} else {
					long address = reader.ReadInt64();
					int modCount = reader.ReadInt32();

					long here = stream.Position;
					stream.Seek(address, SeekOrigin.Begin);

					string sourceName = reader.ReadString();
					string sourceDirectory = Path2.Combine(manualModsDirectory, sourceName);
					Directory.CreateDirectory(sourceDirectory);

					for (int modIndex = 0; modIndex < modCount; modIndex++) {
						bool isDirectory = reader.ReadBoolean();
						string archiveName = reader.ReadString();

						int size = reader.ReadInt32();
						byte[] buffer = reader.ReadBytes(size);

						string archiveDirectoryOrFile = Path2.Combine(manualModsDirectory, archiveName);
						if (isDirectory) {
							using MemoryStream temporaryRestrictedBuffer = new MemoryStream(buffer);
							if (Directory.Exists(archiveDirectoryOrFile)) {
								try {
									Directory.Delete(archiveDirectoryOrFile, true);
								} catch (DirectoryNotFoundException) { }
							}
							Directory.CreateDirectory(archiveDirectoryOrFile);
							TarFile.ExtractToDirectory(temporaryRestrictedBuffer, archiveDirectoryOrFile, true);
						} else {
							File.WriteAllBytes(archiveDirectoryOrFile, buffer);
						}
					}

					result.ModSources[new ModSource(sourceName)] = isEnabled;
					stream.Seek(here, SeekOrigin.Begin);
				}

				progressWindow?.SetProgress((float)i / modSourceCount);
			}


			progressWindow?.SetStatus("Installing Workshop Mods...", IMPORTING_MODPACK);
			progressWindow?.SetProgress(float.NaN);

			ulong[] ids = pendingWorkshopMods.Select(static data => data.Item1).ToArray();
			await SteamTools.DownloadWorkshopModsAsync(ids, true, cancellationToken);
			foreach ((ulong id, bool enabled) in pendingWorkshopMods) {
				cancellationToken.ThrowIfCancellationRequested();
				result.ModSources[new ModSource(id)] = enabled;
			}

			await result.SaveAndUpdateInitAsync(cancellationToken);
			return result;
		}
	}
}
