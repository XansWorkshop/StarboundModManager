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
		public required string Name {
			get;
			set {
				if (string.IsNullOrWhiteSpace(value)) value = "No Name";
				field = value?.Trim() ?? throw new ArgumentNullException(nameof(Name));
			}
		} = "No Name";

		/// <summary>
		/// The person or entity who created this modpack. Cannot be null.
		/// </summary>
		public required string Creator {
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
		/// The GUID of this modpack. This does not change.
		/// </summary>
		public Guid ID { get; }

		/// <summary>
		/// An alias to using <see cref="Directories.GetPackSBInitFile(Guid)"/>
		/// </summary>
		public string SBInitPath => Directories.GetPackSBInitFile(ID);

		/// <summary>
		/// The mods that are part of this pack, then their state (enabled or disabled).
		/// </summary>
		public SortedDictionary<ModSource, bool> ModSources { get; } = [];

		/// <summary>
		/// Create a new, blank modpack.
		/// </summary>
		[SetsRequiredMembers]
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
			Modpack pack = new Modpack(id) {
				Name = data.GetValueAsStringOrDefault("name", "No name"),
				Creator = data.GetValueAsStringOrDefault("creator", ""),
				Description = data.GetValueAsStringOrDefault("description", "")
			};

			GDArray modSources = (GDArray)data["mod_sources"];
			HashSet<ulong> alreadyGotWorkshop = [];
			HashSet<string> alreadyGotNamed = [];
			foreach (Variant innerArrayVar in modSources) {
				GDArray innerArray = innerArrayVar.As<GDArray>();
				string key = innerArray[0].As<string>();
				int flags = innerArray[1].As<int>();
				bool isWorkshop = (flags & 2) != 0;
				bool isEnabled = (flags & 1) != 0;

				if (isWorkshop) {
					if (ulong.TryParse(key, out ulong workshopID)) {
						if (!alreadyGotWorkshop.Add(workshopID)) {
							GD.PushError($"Workshop mod {workshopID} has already been added to this pack, but its mod_sources lookup included it more than once.");
							continue;
						}
						try {
							pack.ModSources[new ModSource(workshopID)] = isEnabled;
						} catch (Exception ex) {
							GD.PushError($"Workshop mod {workshopID} failed to load: {ex}");
							continue;
						}
					} else {
						GD.PushError($"Failed to load a mod with key {key} as a workshop mod because {key} is not a positive integer number.");
						continue;
					}
				} else {
					string lowerKey = key.ToLower();
					if (!alreadyGotNamed.Add(lowerKey)) {
						GD.PushError($"Named mod {key} has already been added to this pack, but its mod_sources lookup included it more than once.");
						continue;
					}
					try {
						pack.ModSources[new ModSource(lowerKey)] = isEnabled;
					} catch (Exception ex) {
						GD.PushError($"Named mod {key} failed to load: {ex}");
						continue;
					}
				}
			}

			return pack;
		}

		/// <summary>
		/// Asynchronously updates and saves <c>sbinit.config</c>. For convenience, the path to the file is 
		/// returned so that the game can be launched.
		/// </summary>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> which can be used to terminate the launch.</param>
		/// <returns></returns>
		public async Task<string> SaveAndUpdateInitAsync(CancellationToken cancellationToken) {
			GDDictionary data = [];
			data["name"] = Name;
			data["creator"] = Creator;
			data["description"] = Description;

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
			string sbInitJson = Directories.GetPackSBInitFile(ID);
			Directory.CreateDirectory(Path.GetDirectoryName(packJson)!);
			File.WriteAllText(packJson, Json.Stringify(data));

			GDDictionary sbInit = await MakeSBInitAsync(cancellationToken).ConfigureAwait(false);
			File.WriteAllText(sbInitJson, Json.Stringify(sbInit));
			return sbInitJson;
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
				Description = Description
			};
			foreach (KeyValuePair<ModSource, bool> kvp in ModSources) {
				dupe.ModSources[kvp.Key] = kvp.Value;
			}
			dupe.SaveAndUpdateInitAsync(CancellationToken.None).Wait();

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
		/// Sets the modpack icon based on an image file.
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
		/// for things like Workshop mods.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="OperationCanceledException"></exception>
		private async Task<GDDictionary> MakeSBInitAsync(CancellationToken cancellationToken) {
			GDArray assetDirectories = [];
			assetDirectories.Add(Path2.Combine(Directories.GetLocalStarboundInstallDirectory(), "assets"));
			assetDirectories.Add(Directories.GetExtraAssetsDirectory(ID));

			// This might prevent a lot of downloading.
			SteamTools.CopyAllCurrentSubscriptionsToCache(true, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			ulong[] workshopMods = ModSources.Keys.Select(src => src.WorkshopID).Where(id => id != 0).ToArray();
			await SteamTools.DownloadWorkshopModsAsync(workshopMods, true, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			foreach (KeyValuePair<ModSource, bool> binding in ModSources) {
				if (!binding.Value) continue;
				assetDirectories.Add(binding.Key.Path);
				cancellationToken.ThrowIfCancellationRequested();
			}

			GDDictionary sbInit = [ ];
			sbInit["assetDirectories"] = assetDirectories;
			sbInit["logDirectory"] = Directories.GetLogDirectory(ID);
			sbInit["storageDirectory"] = Directories.GetStorageDirectory(ID);
			sbInit["includeUGC"] = false;

			return sbInit;
		}
	}
}
