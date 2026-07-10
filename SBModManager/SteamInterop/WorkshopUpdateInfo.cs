using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.SteamInterop.Web;

namespace SBModManager.SteamInterop {

	/// <summary>
	/// Helps keep track of mod versions.
	/// </summary>
	public static class WorkshopUpdateInfo {

		/// <summary>
		/// A testing number which represents a workshop item ID that always has an update available.
		/// <see cref="SteamTools"/> also recognizes it and will simulate getting data on it accordingly.
		/// </summary>
		public const long MAGIC_WORKSHOP_ID = long.MinValue;

		/// <summary>
		/// Keys are workshop item IDs, values are the stored version information.
		/// </summary>
		private static Dictionary<long, VersionBinding> versionTracking = [];

		private static readonly Lock VERSION_TRACKING_LOCK = new Lock();

		private static long lastUpdateCheck = 0;

		private const long FIFTEEN_MINUTES_IN_SECONDS = 15 * 60;

		/// <summary>
		/// The same as <see cref="CheckForUpdatesIgnoreCooldownAsync(bool, bool)"/> but this keeps track of a 15 minute cooldown.
		/// If called too early, this does nothing and returns <see cref="Task.CompletedTask"/>.
		/// </summary>
		/// <param name="skipKnownOutOfDate"></param>
		/// <param name="autosave"></param>
		/// <returns></returns>
		public static Task CheckForUpdatesWithCooldownAsync(List<long>? appendIDs, bool skipKnownOutOfDate = true, bool autosave = true) {
			long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			if ((now - lastUpdateCheck) > FIFTEEN_MINUTES_IN_SECONDS) {
				return CheckForUpdatesIgnoreCooldownAsync(appendIDs, skipKnownOutOfDate, autosave);
			}
			return Task.CompletedTask;
		}

		/// <summary>
		/// Queries Steam to figure out which mods need an update. This still sets the cooldown.
		/// </summary>
		/// <param name="skipKnownOutOfDate">If stored information about update times has a mod which is known to be out of date already, 
		/// skip asking Steam for it. This does not affect processing time since Steam has the request contain every value in bulk already,
		/// but it can make the request smaller.</param>
		/// <param name="autosave">If <see langword="true"/>, save the results to disk as soon as they are ready.</param>
		/// <returns></returns>
		public static async Task CheckForUpdatesIgnoreCooldownAsync(List<long>? appendIDs, bool skipKnownOutOfDate = true, bool autosave = true) {
			lastUpdateCheck = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			string workshopCache = Directories.GetLocalWorkshopCacheDirectory();
			DirectoryInfo directory = new DirectoryInfo(workshopCache);
			if (!directory.Exists) return;

			List<long> workshopIDs = appendIDs != null ? new List<long>(appendIDs) : [];
			workshopIDs.Sort();
			Dictionary<long, VersionBinding> versionTrackingReplacement = [];
			foreach (DirectoryInfo child in directory.GetDirectories()) {
				if (long.TryParse(child.Name, out long workshopID)) {
					if (skipKnownOutOfDate && versionTracking.TryGetValue(workshopID, out VersionBinding existingBinding) && existingBinding.IsUpdateAvailable) {
						// Add it to the replacement directly.
						versionTrackingReplacement[workshopID] = existingBinding;
						continue;
					}
					// Otherwise, add it to the pending IDs to download, which once downloaded, will be added to the replacement.
					int index = workshopIDs.BinarySearch(workshopID);
					if (index < 0) {
						workshopIDs.Insert(~index, workshopID);
					}
				}
			}

			PublishedFileDetailsEntry[] details = await SteamTools.GetPublishedFileDetailsAsync(workshopIDs.ToArray(), CancellationToken.None);

			foreach (PublishedFileDetailsEntry entry in details) {
				VersionBinding binding = versionTracking.GetValueOrDefault(entry.publishedFileID);
				if (entry.timeUpdated > binding.lastUpdatedForCurrentInstall) {
					binding = new VersionBinding(entry.timeUpdated, entry.timeUpdated);
				}
				versionTrackingReplacement[entry.publishedFileID] = binding;
			}

			lock (VERSION_TRACKING_LOCK) {
				versionTracking = versionTrackingReplacement;
				if (autosave) SaveNoLock();
			}
		}

		/// <summary>
		/// Sets the item's last updated timestamp to be equal to the last known time from Steam.
		/// </summary>
		/// <param name="specificItemID"></param>
		/// <param name="autosave">If <see langword="true"/>, save the updated data to disk.</param>
		public static void MarkAsUpdated(long specificItemID, bool autosave = true) {
			lock (VERSION_TRACKING_LOCK) {
				VersionBinding binding = versionTracking.GetValueOrDefault(specificItemID);
				versionTracking[specificItemID] = new VersionBinding(binding.lastUpdatedSteam, binding.lastUpdatedSteam);
			}
			if (autosave) {
				Save();
			}
		}

		/// <summary>
		/// Returns every binding from a workshop ID to the two version timestamps, one for the update timestamp
		/// of the workshop mod when this app downloaded it, and one for the update timestamp of the workshop mod
		/// as Steam has last reported it.
		/// <para/>
		/// The accuracy of this information is up to the last time <see cref="CheckForUpdatesIgnoreCooldownAsync"/> was called.
		/// </summary>
		/// <returns></returns>
		public static IReadOnlyDictionary<long, VersionBinding> GetUpdateInformation() {
			lock (VERSION_TRACKING_LOCK) {
				return versionTracking.AsReadOnly();
			}
		}

		/// <summary>
		/// Returns the version information for the provided <paramref name="specificItemID"/>, if it exists.
		/// </summary>
		/// <param name="specificItemID"></param>
		/// <returns></returns>
		public static bool TryGetUpdateInformation(long specificItemID, out VersionBinding information) {
			lock (VERSION_TRACKING_LOCK) {
				return versionTracking.TryGetValue(specificItemID, out information);
			}
		}

		/// <summary>
		/// Returns <see langword="true"/> if version information is available for the provided workshop item ID, and
		/// if the version information for said ID indicates that an update is available.
		/// </summary>
		/// <param name="specificItemID"></param>
		/// <param name="defaultIfNoData">If there is no data available at all, return this value.</param>
		/// <returns></returns>
		public static bool IsUpdateAvailable(long specificItemID, bool defaultIfNoData = true) {
			if (TryGetUpdateInformation(specificItemID, out VersionBinding information)) {
				return information.IsUpdateAvailable;
			}
			return defaultIfNoData;
		}

		/// <summary>
		/// Saves the current information to disk.
		/// </summary>
		public static void Save() {
			GDDictionary data = [];
			lock (VERSION_TRACKING_LOCK) {
				foreach (KeyValuePair<long, VersionBinding> binding in versionTracking) {
					data[binding.Key.ToString()] = new GDArray { binding.Value.lastUpdatedForCurrentInstall.ToString(), binding.Value.lastUpdatedSteam.ToString() };
				}
			}
			Directory.CreateDirectory(Directories.GetLocalWorkshopCacheDirectory());
			File.WriteAllText(Directories.GetLocalWorkshopVersionCache(), Json.Stringify(data));
		}

		private static void SaveNoLock() {
			GDDictionary data = [];
			foreach (KeyValuePair<long, VersionBinding> binding in versionTracking) {
				data[binding.Key.ToString()] = new GDArray { binding.Value.lastUpdatedForCurrentInstall.ToString(), binding.Value.lastUpdatedSteam.ToString() };
			}
			Directory.CreateDirectory(Directories.GetLocalWorkshopCacheDirectory());
			File.WriteAllText(Directories.GetLocalWorkshopVersionCache(), Json.Stringify(data));
		}

		/// <summary>
		/// Loads the current information from disk.
		/// </summary>
		public static void Load() {
			if (File.Exists(Directories.GetLocalWorkshopVersionCache())) {
				try {
					GDDictionary bindings = (GDDictionary)Json.ParseString(File.ReadAllText(Directories.GetLocalWorkshopVersionCache()));
					if (bindings == null) return;
					lock (VERSION_TRACKING_LOCK) {
						versionTracking.Clear();
						foreach (KeyValuePair<Variant, Variant> binding in bindings) {
							long workshopID = long.Parse((string)binding.Key);
							GDArray values = (GDArray)binding.Value;
							long lastUpdatedForCurrentInstall = long.Parse((string)values[0]);
							long lastUpdatedSteam = long.Parse((string)values[0]);
							versionTracking[workshopID] = new VersionBinding(lastUpdatedForCurrentInstall, lastUpdatedSteam);
						}
					}
				} catch (FileNotFoundException) {
				} catch (DirectoryNotFoundException) { }
			}
		}

		public readonly struct VersionBinding {

			// Future Xan:
			// You tested it. last_updated does NOT change when the description or title changes.
			// You originally fell back to hcontent_file assuming that the last_updated timestamp would change with things like metadata edits,
			// but it does not. Which is awesome, because now I can actually show timestamps.

			/// <summary>
			/// Alias for checking if <see cref="lastUpdatedSteam"/> is after <see cref="lastUpdatedForCurrentInstall"/>.
			/// </summary>
			public readonly bool IsUpdateAvailable => lastUpdatedForCurrentInstall < lastUpdatedSteam;

			/// <summary>
			/// <see cref="lastUpdatedForCurrentInstall"/> as a local <see cref="DateTime"/>.
			/// </summary>
			public readonly DateTime CurrentInstalledUpdateDate => DateTimeOffset.FromUnixTimeSeconds(lastUpdatedForCurrentInstall).LocalDateTime;

			/// <summary>
			/// <see cref="lastUpdatedSteam"/> as a local <see cref="DateTime"/>.
			/// </summary>
			public readonly DateTime SteamLatestUpdateDate => DateTimeOffset.FromUnixTimeSeconds(lastUpdatedSteam).LocalDateTime;

			/// <summary>
			/// The last update timestamp that the mod had last time it was downloaded. Use this for displaying the release date of the installed version.
			/// </summary>
			public readonly long lastUpdatedForCurrentInstall;

			/// <summary>
			/// The last update timestamp as Steam reports it. Use this for displaying the release date of the update.
			/// </summary>
			public readonly long lastUpdatedSteam;

			public VersionBinding(long lastKnown, long fromSteam) {
				lastUpdatedForCurrentInstall = lastKnown;
				lastUpdatedSteam = fromSteam;
			}

		}
	}
}
