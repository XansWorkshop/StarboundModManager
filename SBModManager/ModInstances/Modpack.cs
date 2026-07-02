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
		/// The mods that are part of this pack, then their state (enabled or disabled).
		/// </summary>
		public Dictionary<ModSource, bool> ModSources { get; } = [];

		/// <summary>
		/// Create a new, blank modpack.
		/// </summary>
		[SetsRequiredMembers]
		public Modpack() {
			ID = Guid.NewGuid();
		}

		private Modpack(Guid id) {
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

			GDDictionary data = (GDDictionary)Json.ParseString(File.ReadAllText(packJson));
			Modpack pack = new Modpack(id) {
				Name = data.GetValueAsStringOrDefault("name", "No name"),
				Creator = data.GetValueAsStringOrDefault("creator", ""),
				Description = data.GetValueAsStringOrDefault("description", "")
			};

			GDDictionary modSources = (GDDictionary)data["mod_sources"];
			HashSet<ulong> alreadyGotWorkshop = [];
			HashSet<string> alreadyGotNamed = [];
			foreach (KeyValuePair<Variant, Variant> binding in modSources) {
				string key = binding.Key.As<string>();
				int flags = binding.Value.As<int>();
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

		public void Save() {
			GDDictionary data = [];
			data["name"] = Name;
			data["creator"] = Creator;
			data["description"] = Description;
			GDDictionary modSources = [];
			foreach (KeyValuePair<ModSource, bool> binding in ModSources) {
				ModSource source = binding.Key;
				bool enabled = binding.Value;
				int flags = (source.IsWorkshopMod ? 2 : 0) | (enabled ? 1 : 0);
				modSources[source.PersistentName] = flags;
			}
			data["mod_sources"] = modSources;

			string packJson = Directories.GetPackInfoFile(ID);
			Directory.CreateDirectory(Path.GetDirectoryName(packJson)!);
			File.WriteAllText(packJson, Json.Stringify(data));
		}

		/// <summary>
		/// Returns a <see cref="Texture2D"/> representing the icon of this modpack.
		/// </summary>
		/// <returns></returns>
		public Texture2D GetIcon() {
			string directory = Directories.GetPackDirectory(ID);
			string icon = Path2.Combine(directory, "icon.png");
			try {
				Image? result = Image.LoadFromFile(icon);
				if (result != null) {
					return ImageTexture.CreateFromImage(result);
				}
			} catch { }
			return Core.GetStarboundIcon();
		}

		/// <summary>
		/// Sets the modpack icon based on an image file.
		/// </summary>
		/// <param name="imageFile"></param>
		public Texture2D? TrySetIcon(string imageFile) {
			string directory = Directories.GetPackDirectory(ID);
			string icon = Path2.Combine(directory, "icon.png");
			try {
				Image? result = Image.LoadFromFile(imageFile);
				if (result != null) {
					result.Resize(256, 256, Image.Interpolation.Lanczos);
					result.SavePng(icon);
					return ImageTexture.CreateFromImage(result);
				}
			} catch { }
			return null;
		}

		public void Launch() {
			CancellationTokenSource canceller = new CancellationTokenSource();

		}

		/// <summary>
		/// Returns the JSON string which represents the contents of sbinit.config. This may need to do some downloading ahead of time
		/// for things like Workshop mods.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="OperationCanceledException"></exception>
		private async Task<string> PrepareForLaunchAsync(CancellationToken cancellationToken) {
			GDArray assetDirectories = [];
			assetDirectories.Add(Path2.Combine(Directories.GetPrivateStarboundInstallDirectory(), "assets"));
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

			return Json.Stringify(sbInit, "\t", false, false);
		}

	}
}
