using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Menus.Windows;
using SBModManager.Other;
using SBModManager.SteamInterop;

namespace SBModManager.ModInstances {

	/// <summary>
	/// Represents an entire modpack.
	/// </summary>
	public sealed class Modpack {

		/// <summary>
		/// The name of this modpack, in a user-friendly format. Cannot be null or whitespace.
		/// </summary>
		public string Name {
			get;
			set {
				value = value ?? throw new ArgumentNullException(nameof(Name));
				value = value.Trim();
				if (string.IsNullOrWhiteSpace(value)) value = "No Name";
				field = value;
			}
		} = "No Name";

		/// <summary>
		/// The person or entity who created this modpack. Cannot be null.
		/// </summary>
		public string Creator {
			get;
			set => field = value?.Trim() ?? throw new ArgumentNullException(nameof(Creator));
		} = "";

		/// <summary>
		/// The user-defined description of this modpack. Cannot be null.
		/// </summary>
		public string Description {
			get;
			set => field = value?.Trim() ?? throw new ArgumentNullException(nameof(Description));
		} = "";

		/// <summary>
		/// The last time this modpack was launched.
		/// </summary>
		public DateTime LastPlayed { get; set; }

		/// <summary>
		/// The GUID of this modpack. This does not change.
		/// </summary>
		public Guid ID { get; }

		/// <summary>
		/// The mods that are part of this pack, then their state (enabled or disabled).
		/// </summary>
		public SortedDictionary<ModSource, bool> ModSources { get; } = [];

		/// <summary>
		/// When importing a modpack, this is set to true. The intent is that if the modpack is stored on disk,
		/// but then the import is cancelled or fails, this will have been saved as true. The next time the game
		/// reads this modpack, it will delete it instead of loading a corrupted pack.
		/// </summary>
		public bool IsCorruptedDeleteOnNextRead { get; set; }

		/// <summary>
		/// Create a new, blank modpack.
		/// </summary>
		public Modpack() {
			ID = Guid.NewGuid();
		}

		internal Modpack(Guid id) {
			ID = id;
		}

		/// <summary>
		/// Loads an existing modpack from disk.
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static Modpack? LoadFromDisk(Guid id) {
			string packJson = Directories.GetPackInfoFile(id);
			if (!File.Exists(packJson)) {
				GD.PushError($"Failed to load {packJson} because it doesn't exist.");
				return null;
			}

			GDDictionary data = (GDDictionary)StarboundJsonSanitizer.ParseString(File.ReadAllText(packJson));
			if (data.TryGetValue("is_corrupted_delete_next_time", out Variant isCorrupted)) {
				if (isCorrupted.AsBool()) {
					GD.Print("Deleted a corrupted modpack.");
					Directory.Delete(Directories.GetPackDirectory(id), true);
					return null;
				}
			}

			Modpack pack = new Modpack(id) {
				Name = data.GetValueAsStringOrDefault("name", "No name"),
				Creator = data.GetValueAsStringOrDefault("creator", ""),
				Description = data.GetValueAsStringOrDefault("description", "")
			};
			if (data.TryGetValue("last_played", out Variant lastPlayedVar) && (lastPlayedVar.VariantType == Variant.Type.Int || lastPlayedVar.VariantType == Variant.Type.Float)) {
				pack.LastPlayed = DateTime.FromBinary((long)lastPlayedVar);
			}

			GDArray modSources = (GDArray)data["mod_sources"];
			HashSet<long> alreadyGotWorkshop = [];
			HashSet<string> alreadyGotNamed = [];
			List<string> missing = [];
			foreach (Variant innerArrayVar in modSources) {
				GDArray innerArray = innerArrayVar.As<GDArray>();
				string key = innerArray[0].As<string>();
				int flags = innerArray[1].As<int>();
				bool isWorkshop = (flags & 2) != 0;
				bool isEnabled = (flags & 1) != 0;

				if (isWorkshop) {
					if (long.TryParse(key, out long workshopID)) {
						if (!alreadyGotWorkshop.Add(workshopID)) {
							GD.PushError($"Workshop mod {workshopID} has already been added to this pack, but its mod_sources lookup included it more than once.");
							continue;
						}
						try {
							pack.ModSources[ModSource.GetOrCreateSource(workshopID)] = isEnabled;
						} catch (Exception ex) {
							GD.PushError($"Workshop mod {workshopID} failed to load: {ex}");
							missing.Add($"Workshop: {workshopID}");
							continue;
						}
					} else {
						GD.PushError($"Failed to load a mod with key {key} as a workshop mod because {key} is not a positive integer number.");
						continue;
					}
				} else {
					string lowerKey = key.ToLower();
					if (!alreadyGotNamed.Add(lowerKey)) {
						GD.PushError($"Named mod {key} has already been added to this pack, but its mod_sources lookup included it more than once. Note that mods are not case sensitive to retain support on Windows!");
						continue;
					}
					try {
						pack.ModSources[ModSource.GetOrCreateSource(key)] = isEnabled;
					} catch (Exception ex) {
						GD.PushError($"Named mod {key} failed to load: {ex}");
						missing.Add(key);
						continue;
					}
				}
			}

			if (missing.Count > 0) {
				File.WriteAllText(Path.Combine(Directories.GetPackDirectory(id), "missingmods.txt"), string.Join('\n', missing));
				OS.Alert("One or more mods have been deleted from disk and will not load. The list of missing mods has been written to missingmods.txt in this pack's profile folder.");
			}

			return pack;
		}

		/// <summary>
		/// Asynchronously updates and saves <c>sbinit.config</c>. For convenience, the path to the file is 
		/// returned so that the game can be launched.
		/// </summary>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> which can be used to terminate the launch.</param>
		/// <returns></returns>
		public async Task<(string client, string server)> SaveAndUpdateInitsAsync(CancellationToken cancellationToken) {
			GDDictionary data = [];
			data["name"] = Name;
			data["creator"] = Creator;
			data["description"] = Description;
			data["last_played"] = LastPlayed.ToBinary();
			data["is_corrupted_delete_next_time"] = IsCorruptedDeleteOnNextRead;

			// This is an array because of a possible edge case where a mod's name is just numbers.
			GDArray modSources = [];
			foreach (KeyValuePair<ModSource, bool> binding in ModSources) {
				ModSource source = binding.Key;
				bool enabled = binding.Value;
				int flags = (source.IsWorkshopMod ? 2 : 0) | (enabled ? 1 : 0);
				modSources.Add(new GDArray { source.PersistentName, flags });
			}
			data["mod_sources"] = modSources;

			string packJson = Directories.GetPackInfoFile(ID);
			string sbInitClientJson = Directories.GetPackSBInitFile(ID, false);
			string sbInitServerJson = Directories.GetPackSBInitFile(ID, true);
			Directory.CreateDirectory(Path.GetDirectoryName(packJson)!);
			File.WriteAllText(packJson, Json.Stringify(data));

			(GDDictionary sbInitClient, GDDictionary sbInitServer) = await MakeSBInitsAsync(cancellationToken).ConfigureAwait(false);
			File.WriteAllText(sbInitClientJson, Json.Stringify(sbInitClient));
			File.WriteAllText(sbInitServerJson, Json.Stringify(sbInitServer));
			return (sbInitClientJson, sbInitServerJson);
		}

		public void Delete() {
			string packDirectory = Directories.GetPackDirectory(ID);
			Directory.Delete(packDirectory, true);
		}

		/// <summary>
		/// Copies everything from this modpack into a new modpack.
		/// </summary>
		/// <returns></returns>
		public Modpack Duplicate() {
			Modpack dupe = new Modpack {
				Name = Name,
				Creator = Creator,
				Description = Description,
				// Do not copy LastPlayed, since I want the default state of a duplicate pack to be "never played" (I mean, you haven't).
			};
			foreach (KeyValuePair<ModSource, bool> kvp in ModSources) {
				dupe.ModSources[kvp.Key] = kvp.Value;
			}
			dupe.SaveAndUpdateInitsAsync(CancellationToken.None).Wait();

			try {
				Directories.CopyDirectoryOverwrite(
					Directories.GetStorageDirectory(ID, false),
					Directories.GetStorageDirectory(dupe.ID, false),
					CancellationToken.None
				);
			} catch (DirectoryNotFoundException) { }
			try {
				Directories.CopyDirectoryOverwrite(
					Directories.GetStorageDirectory(ID, true),
					Directories.GetStorageDirectory(dupe.ID, true),
					CancellationToken.None
				);
			} catch (DirectoryNotFoundException) { }

			string iconSrc = Path2.Combine(Directories.GetPackDirectory(ID), "icon.png");
			string iconDst = Path2.Combine(Directories.GetPackDirectory(dupe.ID), "icon.png");
			try {
				File.Copy(iconSrc, iconDst, true);
			} catch (FileNotFoundException) { }

			return dupe;
		}

		/// <summary>
		/// Returns a <see cref="Texture2D"/> representing the icon of this modpack.
		/// If no icon is found, this returns <see cref="Assets.DefaultStarboundIcon"/>.
		/// </summary>
		/// <returns></returns>
		public Texture2D GetIcon() {
			string directory = Directories.GetPackDirectory(ID);
			string icon = Path2.Combine(directory, "icon.png");
			try {
				byte[] buffer = File.ReadAllBytes(icon);
				Image result = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
				Error error = result.LoadPngFromBuffer(buffer);
				if (error != Error.Ok) {
					error = result.LoadJpgFromBuffer(buffer);
				}
				if (error == Error.Ok) {
					return ImageTexture.CreateFromImage(result);
				}
			} catch (FileNotFoundException) { }
			return Assets.DefaultStarboundIcon;
		}

		/// <summary>
		/// Sets the modpack icon based on an image file. This just writes the file to disk.
		/// </summary>
		/// <param name="imageFile"></param>
		public Texture2D? TrySetIcon(string imageFile) {
			string directory = Directories.GetPackDirectory(ID);
			string destination = Path2.Combine(directory, "icon.png");
			try {
				// "Why not just Image.LoadFromFile?"
				// Because while giggling to myself like a fucking idiot at 3 AM over setting the thumbnail icon to various memes,
				// I realized that sometimes people upload their jpegs with the png extension. Godot uses the file extension to
				// load the images instead of the data, which causes LoadFromFile to break.

				// also lol
				byte[] buffer = File.ReadAllBytes(imageFile);
				Image result = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
				Error error = result.LoadPngFromBuffer(buffer);
				if (error != Error.Ok) {
					error = result.LoadJpgFromBuffer(buffer);
				}
				if (error != Error.Ok) {
					error = result.LoadGifFirstFrameFromBuffer(buffer);
				}
				if (error == Error.Ok) {
					result.Resize(256, 256, Image.Interpolation.Lanczos);
					result.SavePng(destination);
					return ImageTexture.CreateFromImage(result);
				}
			} catch { }
			return null;
		}

		/// <summary>
		/// Returns the JSON string which represents the contents of sbinit.config. This may need to do some downloading ahead of time
		/// for things like Workshop mods. This also makes sbinit_server.config, which is used by the server installation.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="OperationCanceledException"></exception>
		private async Task<(GDDictionary client, GDDictionary server)> MakeSBInitsAsync(CancellationToken cancellationToken) {
			GDArray assetDirectories = [];
			assetDirectories.Add(Path2.Combine(Directories.GetLocalStarboundInstallDirectory(), "assets"));
			assetDirectories.Add(Directories.GetExtraAssetsDirectory(ID));

			// This might prevent a lot of downloading.
			SteamTools.CopyAllCurrentSubscriptionsToCache(true, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			long[] workshopMods = ModSources.Keys.Select(src => src.WorkshopID).Where(id => id != 0).ToArray();
			HashSet<long> failed = (await SteamTools.DownloadWorkshopModsAsync(workshopMods, true, null, cancellationToken)).ToHashSet();
			cancellationToken.ThrowIfCancellationRequested();

			foreach (KeyValuePair<ModSource, bool> binding in ModSources) {
				if (!binding.Value) continue;
				if (binding.Key.IsWorkshopMod && failed.Contains(binding.Key.WorkshopID)) continue;
				assetDirectories.Add(binding.Key.AbsolutePath);
				cancellationToken.ThrowIfCancellationRequested();
			}

			GDDictionary sbInitClient = [];
			sbInitClient["assetDirectories"] = assetDirectories;
			sbInitClient["logDirectory"] = Directories.GetLogDirectory(ID, false);
			sbInitClient["storageDirectory"] = Directories.GetStorageDirectory(ID, false);
			sbInitClient["includeUGC"] = false;

			GDDictionary sbInitServer = sbInitClient.Duplicate(false);
			sbInitServer["logDirectory"] = Directories.GetLogDirectory(ID, true);
			sbInitServer["storageDirectory"] = Directories.GetStorageDirectory(ID, true);

			return (sbInitClient, sbInitServer);
		}
	}
}
