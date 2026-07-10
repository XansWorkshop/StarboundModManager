using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32;

using SBModManager.Menus.Windows;
using SBModManager.Other;
using SBModManager.SteamInterop.Web;

using Environment = System.Environment;

namespace SBModManager.SteamInterop {

	/// <summary>
	/// Helps to manage the mods from the Steam Workshop.
	/// </summary>
	public static class SteamTools {

		/// <summary>
		/// Sent via HTTP POST. Form-Body Parameters:
		/// <list type="table">
		/// <item>
		/// <term><c>collectioncount</c></term>
		/// <description>Integer as string. Amount of collections being requested.</description>
		/// </item>
		/// 
		/// <item>
		/// <term><c>publishedfileids[<em>n</em>]</c></term>
		/// <description>Integer as string. ID of a collection.</description>
		/// </item>
		/// 
		/// </list>
		/// <para/>
		/// Response JSON example:
		/// <code>
		/// {
		///		"response" : {
		///			"result" : (see <see cref="EResult"/>)
		///			"resultcount" : n,
		///			"collectiondetails" : [
		///				{
		///					"publishedfileid" : "123456789",
		///					"sortorder" : "(integer as string)",
		///					"filetype" : (see <see cref="EWorkshopFileType"/>)
		///				},
		///				...
		///			]
		///		}
		/// }
		/// </code>
		/// </summary>
		const string STEAM_API_GET_COLLECTION_DETAILS = "https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/";

		/// <summary>
		/// Sent via HTTP POST. Form-Body Parameters:
		/// <list type="table">
		/// <item>
		/// <term><c>itemcount</c></term>
		/// <description>Integer as string. Amount of items being requested.</description>
		/// </item>
		/// 
		/// <item>
		/// <term><c>publishedfileids[<em>n</em>]</c></term>
		/// <description>Integer as string. ID of a workshop item.</description>
		/// </item>
		/// 
		/// </list>
		/// <para/>
		/// Response JSON example:
		/// <code>
		/// {
		///		"response" : {
		///			"result" : (see <see cref="EResult"/>)
		///			"resultcount" : n,
		///			"publishedfiledetails" : [
		///				{
		///					"publishedfileid" : "123456789",
		///					"result" : (see <see cref="EResult"/>),
		///					// A bunch of other shit. title, description, subscriptions, favorited (int), lifetime_subscriptions, lifetime_favorited, views, tags [{"tag":""},...]
		///					"time_updated" : "(integer as string)" // This one is important to me
		///				},
		///				...
		///			]
		///		}
		/// }
		/// </code>
		/// </summary>
		const string STEAM_API_GET_PUBLISHED_FILE_DETAILS = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

		#region Starbound Directories

		/// <summary>
		/// Uses <see cref="GetSteamappsContainingStarbound"/> and adds <c>common/Starbound</c> to the path.
		/// </summary>
		/// <returns></returns>
		public static string? GetStarboundDirectory() {
			string? steamapps = GetSteamappsContainingStarbound();
			if (steamapps == null) return null;
			return Path2.Combine(steamapps, "common", "Starbound");
		}

		/// <summary>
		/// Reads libraryfolders.vdf, which is a file stored in Steam's default installation location. This file contains
		/// a list of every library folder and every game within it, which this method uses to try to find the install location
		/// of Starbound.
		/// <para/>
		/// This method then returns the steamapps directory that contains Starbound, not the Starbound directory itself. To get that,
		/// use <see cref="GetStarboundDirectory"/>
		/// </summary>
		/// <returns></returns>
		public static string? GetSteamappsContainingStarbound() {
			try {
				string os = OS.GetName();
				VDFObject? vdf;
				if (os == "Windows") {
					#pragma warning disable CA1416 // We checked for Windows just above!
					string steamInstallLocation = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string ?? @"C:\Program Files (x86)\Steam";
					vdf = VDFReader.TryReadVDF(Path.Combine(steamInstallLocation, "steamapps", "libraryfolders.vdf"));
				} else if (os == "macOS") {
					string userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
					vdf = VDFReader.TryReadVDF(Path2.Combine(userDirectory, "Library/Application Support/Steam/steamapps/libraryfolders.vdf"));
				} else if (os == "Linux") {
					string userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
					vdf = VDFReader.TryReadVDF(Path2.Combine(userDirectory, ".steam/steam/steamapps/libraryfolders.vdf"));
				} else {
					throw new NotSupportedException($"No known steam directory on OS: {os}");
				}
				if (vdf == null) {
					throw new InvalidOperationException("Failed to read vdf file to get library directories.");
				}
				VDFObject libraryFolders = vdf.GetChild("libraryfolders");
				foreach (KeyValuePair<string, object> kvp in libraryFolders.Values) {
					VDFObject libraryFolder = (VDFObject)kvp.Value;
					string path = libraryFolder.GetValue("path");
					VDFObject apps = libraryFolder.GetChild("apps");
					if (apps.Values.ContainsKey("211820")) {
						if (!Directory.Exists(path)) {
							throw new InvalidOperationException($"Your Steam library folders are corrupted (Steam says you have one at {path} but that folder doesn't exist).");
						}

						return Path2.Combine(path, "steamapps");
					}
				}
			} catch { }
			return null;
		}

		#endregion

		#region Download Agnostic Workshop

		/// <summary>
		/// Downloads a single mod, or an entire collection recursively. Returns the list of successful mod IDs.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="skipIfInstalled"></param>
		/// <param name="progressWindow"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<long[]> DownloadWorkshopModOrCollectionAsync(long id, bool skipIfInstalled, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			long[] mods;
			try {
				mods = await GetAllModsInCollectionAsync(id, true, cancellationToken);
				if (mods.Length == 0) {
					mods = [id];
				}
			} catch (KeyNotFoundException) {
				mods = [id];
			}

			cancellationToken.ThrowIfCancellationRequested();
			long[] failed = await DownloadWorkshopModsAsync(mods, skipIfInstalled, progressWindow, cancellationToken);

			HashSet<long> modsAsSet = mods.ToHashSet();
			for (int i = 0; i < failed.Length; i++) {
				modsAsSet.Remove(failed[i]);
			}

			return modsAsSet.ToArray();
		}

		#endregion

		#region Download From File ID

		/// <summary>
		/// Creates a series of commands which are executed by SteamCMD to download one or more workshop mods to disk.
		/// Returns a list of IDs that failed to load. This does not support collection IDs; use <see cref="DownloadEntireCollectionAsync(long, bool, bool, GeneralProgressWindow?, CancellationToken)"/>
		/// or use 
		/// </summary>
		/// <param name="ids">An array of workshop item IDs to download.</param>
		/// <param name="onlyMissing">If true, workshop items that appear to be already installed are skipped.</param>
		/// <param name="progressWindow">A progress window to display progress to the user.</param>
		/// <param name="cancellationToken">Can be used to cancel the process.</param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="OperationCanceledException"></exception>
		public static async Task<long[]> DownloadWorkshopModsAsync(long[] ids, bool onlyMissing, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			if (ids.Length == 0) return [];

			string sbmmWorkshopStorageDirectory = Directories.GetLocalWorkshopCacheDirectory();
			string sbmmSteamCMDScriptDirectory = Directories.GetSteamCMDTempScriptDirectory();
			string steamCmdStagingDirectory;
			if (OS.GetName() == "Linux") {
				steamCmdStagingDirectory = Path2.Combine(Directories.GetSteamCMDInstallationDirectory(), "linux32", "steamapps", "content", "app_211820");
			} else {
				steamCmdStagingDirectory = Path2.Combine(Directories.GetSteamCMDInstallationDirectory(), "steamapps", "content", "app_211820");
			}
			Directory.CreateDirectory(sbmmWorkshopStorageDirectory);
			Directory.CreateDirectory(sbmmSteamCMDScriptDirectory);
			Directory.CreateDirectory(steamCmdStagingDirectory); // We make this so the watcher doesn't shit the bed.
			// Not the slugcat I mean FileSystemWatcher.

			// To begin, we need to generate a script.
			// FIXME: Figure out why I can't use stdin to send commands to SteamCMD.
			// No matter how hard I try, it just will not accept commands.
			// I contemplated on reverse engineering SteamCMD to extract just the download_item function, to reimplement it myself,
			// but I'm not sure I want to spin up IDA and do that sort of thing for such a simple case. It would remove a dependency
			// though. So maybe.

			List<long> idsList = ids.ToList();
			bool hasMagicID = false;
			idsList.Sort();
			if (onlyMissing) {
				GD.Print("Trimming workshop mods from the download that we already have on this PC...");
				PreprocessWorkshopDownloadList(idsList);
			}
			string? installScriptPath = AssembleWorkshopDownloadList(idsList);
			if (installScriptPath == null) return [];

			hasMagicID = idsList[0] == long.MinValue; // [0] because it's sorted.
			if (hasMagicID) {
				idsList.RemoveAt(0);
				if (idsList.Count == 0) {
					await Task.Delay(1000); // Simulate download time.
					return [];
				}
			}

			GD.Print($"Collecting version information for {idsList.Count} workshop mods, if we need to: {string.Join(',', idsList)}");
			await WorkshopUpdateInfo.CheckForUpdatesIgnoreCooldownAsync(idsList, false); // We need this to update the version info right away.

			GD.Print($"Downloading {idsList.Count} workshop mods: {string.Join(',', idsList)}");
			try {
				float totalToDownload = idsList.Count;
				float downloaded = 0;
				progressWindow?.SetStatus("Downloading...", "Downloading Workshop Mod(s)");
				progressWindow?.SetProgress(1 / (totalToDownload + 1)); 
				// ^ This is kind of a psychological hack. It feels worse when it's stuck at 0%.
				// By lying to people and giving it some small percentage, it looks like it's doing something.

				using FileSystemWatcher steamCmdInstallationWatcher = new FileSystemWatcher {
					Path = steamCmdStagingDirectory,
					NotifyFilter = NotifyFilters.DirectoryName,
					EnableRaisingEvents = true
				};
				steamCmdInstallationWatcher.Created += delegate (object sender, FileSystemEventArgs e) {
					if (e.Name != null && e.Name.StartsWith("item_") && long.TryParse(e.Name[5..], out long workshopID) && idsList.BinarySearch(workshopID) >= 0) {
						downloaded++;
						if (downloaded < totalToDownload) {
							progressWindow?.SetProgress(downloaded / (totalToDownload + 1));
						} else {
							progressWindow?.SetProgress(float.NaN); 
							// Change to indeterminate to indicate the last copying step which might take a sec.
						}
					}
				};

				await SteamCMD.RunSteamCMDScriptAsync(installScriptPath, cancellationToken);
				if (hasMagicID) {
					await Task.Delay(1000); // Just some dummy time.
				}

				GD.Print($"Copying folders from SteamCMD's installation directory into the SBMM cache...");
				List<long> seeminglyMissing = [];
				for (int i = 0; i < idsList.Count; i++) {
					cancellationToken.ThrowIfCancellationRequested();
					string itemPath = Path2.Combine(steamCmdStagingDirectory, $"item_{idsList[i]}");
					string destination = Path2.Combine(sbmmWorkshopStorageDirectory, idsList[i].ToString());
					if (Directory.Exists(destination)) {
						try {
							Directory.Delete(destination, true);
						} catch (DirectoryNotFoundException) { }
					}
					if (!Directory.Exists(itemPath)) {
						GD.Print($"Mod with ID {idsList[i]} wasn't in the SteamCMD installation directory. It might be unlisted/private or just outright invalid.");
						seeminglyMissing.Add(idsList[i]);
					} else {
						WorkshopUpdateInfo.MarkAsUpdated(idsList[i], false); // We save later.
						Directory.Move(itemPath, destination);
					}
				}
				WorkshopUpdateInfo.Save();

				seeminglyMissing.Remove(long.MinValue);

				// Reuse IDs
				if (seeminglyMissing.Count == 0) {
					ids = [];
				} else {
					ids = seeminglyMissing.ToArray();
				}
			} finally {
				File.Delete(installScriptPath);
			}

			if (ids.Length > 0) {
				StringBuilder failList = new StringBuilder();
				foreach (long id in ids) {
					failList.Append("https://steamcommunity.com/sharedfiles/filedetails/?id=");
					failList.AppendLine(id.ToString());
				}
				File.WriteAllText(Path2.Combine(sbmmWorkshopStorageDirectory, "failedworkshop.txt"), failList.ToString());
				OS.Alert($"{ids.Length} {(ids.Length == 1 ? "mod" : "mods")} failed to download (they are probably unlisted or private). The mods have been written to \"failedworkshop.txt\" in the mod_catalog_workshop directory.");
				return ids;
			}
			return [];
		}

		/// <summary>
		/// Preprocesses a list of workshop IDs to install. This modifies the input <paramref name="ids"/>.
		/// If an ID is not installed, but is found in the staging directory of SteamCMD, it will be copied to the 
		/// SBMM cache directory.
		/// <para/>
		/// This should only be used if the caller intends to skip IDs which are already downloaded.
		/// </summary>
		/// <param name="ids">The IDs to process.</param>
		private static void PreprocessWorkshopDownloadList(List<long> ids) {
			if (ids.Count == 0) return;
			string sbmmWorkshopStorageDirectory = Directories.GetLocalWorkshopCacheDirectory();
			string steamCmdStagingDirectory = Path2.Combine(Directories.GetSteamCMDInstallationDirectory(), "steamapps", "content", "app_211820");

			for (int i = ids.Count - 1; i >= 0; i--) {
				string idString = ids[i].ToString();
				string itemPath = Path2.Combine(sbmmWorkshopStorageDirectory, idString);
				if (Directory.Exists(itemPath)) {
					ids.RemoveAt(i);
					continue;
				}

				string existingStagedDirectory = Path2.Combine(steamCmdStagingDirectory, $"item_{idString}");
				if (Directory.Exists(existingStagedDirectory)) {
					// If we make it here, there's another problem: The installation failed and it wasn't copied.
					// Copy it over then add it to the ignore list.
					string destination = Path2.Combine(sbmmWorkshopStorageDirectory, idString);
					if (Directory.Exists(destination)) {
						try {
							Directory.Delete(destination, true);
						} catch (DirectoryNotFoundException) { }
					}
					Directory.Move(existingStagedDirectory, destination);
					ids.RemoveAt(i);
					continue;
				}
			}
		}

		/// <summary>
		/// A subroutine which takes a list of IDs, which are assumed to have been filtered already, and generates an installation script.
		/// This returns the file path to the script, or null if no IDs were added.
		/// </summary>
		/// <param name="ids">The list of workshop item IDs to download.</param>
		private static string? AssembleWorkshopDownloadList(List<long> ids) {
			if (ids.Count == 0) return null;

			string sbmmWorkshopStorageDirectory = Directories.GetLocalWorkshopCacheDirectory();
			string sbmmSteamCMDScriptDirectory = Directories.GetSteamCMDTempScriptDirectory();
			string thisSteamCMDScript = Path2.Combine(sbmmSteamCMDScriptDirectory, Path.GetRandomFileName() + ".txt");
			Directory.CreateDirectory(sbmmWorkshopStorageDirectory);
			Directory.CreateDirectory(sbmmSteamCMDScriptDirectory);

			StringBuilder script = new StringBuilder();
			script.AppendLine("@ShutdownOnFailedCommand 0");
			script.AppendLine("login anonymous");
			foreach (long id in ids) {
				if (id == long.MinValue) continue; // Magic ID
				script.AppendLine($"download_item 211820 {id}");
			}
			File.WriteAllText(thisSteamCMDScript, script.ToString());
			return thisSteamCMDScript;
		}

		#endregion

		#region Download from Collection ID

		/// <summary>
		/// Downloads every mod that is in a Steam Workshop Collection.
		/// </summary>
		/// <param name="collectionID">The collection to download from.</param>
		/// <param name="skipIfInstalled">If true, workshop mods that are already installed are skipped.</param>
		/// <param name="recursive">If <see langword="true"/>, any collections that are within this collection are also enumerated and downloaded.</param>
		/// <param name="progressWindow">A progress window to display progress to the user.</param>
		/// <param name="cancellationToken">Can be used to cancel the download.</param>
		/// <returns></returns>
		public static async Task DownloadEntireCollectionAsync(long collectionID, bool skipIfInstalled, bool recursive, GeneralProgressWindow? progressWindow, CancellationToken cancellationToken) {
			progressWindow?.SetStatus("Gathering collection information...", "Downloading Workshop Mod(s)");
			progressWindow?.SetProgress(float.NaN);

			ConcurrentHashSet<long> items = [];
			long[] tempArray = [collectionID];
			await GetAllModsInCollectionAsync(tempArray, [], items, null, recursive, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			await DownloadWorkshopModsAsync(items.ToArray(), skipIfInstalled, progressWindow, cancellationToken);
		}

		#endregion

		#region Copy Subscriptions

		/// <summary>
		/// Efficiency-promoting method that copies every current workshop subscription into the cache.
		/// Returns a list of every installed mod ID.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="OperationCanceledException"></exception>
		public static long[] CopyAllCurrentSubscriptionsToCache(bool skipDuplicates, CancellationToken cancellationToken) {
			List<long> installed = [];

			string? sbPath = GetSteamappsContainingStarbound();
			string workshopCacheDir = Directories.GetLocalWorkshopCacheDirectory();
			if (sbPath != null) {
				string workshopContent = Path2.Combine(sbPath, "workshop", "content", "211820");
				if (!Directory.Exists(workshopContent)) {
					return []; // This can happen if the game is uninstalled and reinstalled.
				}
				string[] subdirectories = Directory.GetDirectories(workshopContent);
				for (int i = 0; i < subdirectories.Length; i++) {
					cancellationToken.ThrowIfCancellationRequested();

					string workshopSubdirectory = subdirectories[i];
					string? name = Path.GetFileName(workshopSubdirectory);

					if (name != null && long.TryParse(name, out long workshopID)) {
						string destination = Path2.Combine(workshopCacheDir, workshopID.ToString());
						if (Directory.Exists(destination)) {
							if (skipDuplicates) {
								installed.Add(workshopID);
								continue;
							}
							// ^ For performance, not to prevent the error.
							try {
								Directory.Delete(destination, true);
							} catch (DirectoryNotFoundException) { }
						}

						Directories.CopyDirectoryOverwrite(workshopSubdirectory, destination, cancellationToken);
						installed.Add(workshopID);
					}
				}
			} else {
				throw new DirectoryNotFoundException("Unable to find Starbound install directory.");
			}

			return installed.ToArray();
		}

		#endregion

		#region Get data of Collection IDs

		/// <summary>
		/// Returns a list of the workshop IDs of every mod in the provided <paramref name="collection"/>.
		/// </summary>
		/// <param name="collection">The collection to query.</param>
		/// <param name="recursive">If true, collections within this collection will also be queried, and collections in those will be, so on.</param>
		/// <param name="cancellationToken">Can be used to cancel the task.</param>
		/// <returns></returns>
		/// <exception cref="OperationCanceledException"></exception>
		public static async Task<long[]> GetAllModsInCollectionAsync(long collection, bool recursive, CancellationToken cancellationToken) {
			ConcurrentHashSet<long> itemIDs = [];
			long[] buffer = [collection];
			await GetAllModsInCollectionAsync(buffer, [], itemIDs, null, recursive, cancellationToken);
			return itemIDs.ToArray();
		}

		/// <summary>
		/// Requests a manifest of everything inside of the provided collection(s), and optionally enumerates any child collections recursively.
		/// </summary>
		/// <param name="collectionsToRead">Every collection to get the data of.</param>
		/// <param name="ignoreCollectionIDs">A list of collection IDs to skip.</param>
		/// <param name="itemIDsOut">Every mod ID that is in this collection. Null to ignore.</param>
		/// <param name="collectionIDsOut">Every child collection within this one. <strong>Only filled if <paramref name="recursive"/> is <see langword="false"/>.</strong> Null to ignore.</param>
		/// <param name="recursive">If <see langword="true"/>, child collections are also enumerated.</param>
		/// <param name="cancellationToken">Can be used to cancel the task.</param>
		/// <returns></returns>
		/// <exception cref="OperationCanceledException"></exception>
		/// <exception cref="KeyNotFoundException">The file was not found, which also might mean it's not a collection.</exception>
		/// <exception cref="InvalidOperationException">An error occurred that wasn't the file being missing.</exception>
		private static async Task GetAllModsInCollectionAsync(ReadOnlyMemory<long> collectionsToRead, ConcurrentHashSet<long> ignoreCollectionIDs, ConcurrentHashSet<long>? itemIDsOut, ConcurrentHashSet<long>? collectionIDsOut, bool recursive, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();

			/*
			MultipartFormDataContent requestBody = new MultipartFormDataContent();

			ReadOnlySpan<long> entries = collectionsToRead.Span;
			int index = 0;
			for (int i = 0; i < collectionsToRead.Length; i++) {
				long collection = entries[i];
				if (!ignoreCollectionIDs.Contains(collection)) {
					requestBody.Add(new StringContent(collection.ToString()), $"publishedfileids[{index++}]");
				}
			}
			requestBody.Add(new StringContent(index.ToString()), "collectioncount");
			*/

			// TODO: Why does ^ brick the request?

			Dictionary<string, string> kvp = [];
			ReadOnlySpan<long> entries = collectionsToRead.Span;
			int index = 0;
			for (int i = 0; i < collectionsToRead.Length; i++) {
				long collection = entries[i];
				if (!ignoreCollectionIDs.Contains(collection)) {
					kvp[$"publishedfileids[{index++}]"] = collection.ToString();
				}
			}
			kvp["collectioncount"] = index.ToString();
			FormUrlEncodedContent requestBody = new FormUrlEncodedContent(kvp);
			
			if (index == 0) return; // Nothing to request!

			int retries = 3;
			while (retries-- > 0) {
				try {
					GD.Print("Asking Steam for collection information...");
					HttpRequestMessage request = new HttpRequestMessage {
						Method = HttpMethod.Post,
						RequestUri = new Uri(STEAM_API_GET_COLLECTION_DETAILS),
						Content = requestBody
					};
					HttpResponseMessage message = await SBModManagerGlobals.HTTP_CLIENT.SendAsync(request, cancellationToken).ConfigureAwait(false);
					CollectionDetails details = await GetCollectionDetailsFromResponseAsync(message, cancellationToken).ConfigureAwait(false);
					if (details.result == EResult.RateLimitExceeded) {
						throw new HttpRequestException($"EResult.{details.result}", null, HttpStatusCode.TooManyRequests);
					} else if (details.result == EResult.LimitExceeded) {
						throw new HttpRequestException($"EResult.{details.result}", null, HttpStatusCode.RequestHeaderFieldsTooLarge);
					} else if (details.result == EResult.FileNotFound) {
						throw new InvalidOperationException("The provided file was not found. It is either not a collection, or it is hidden, unlisted, or friends-only.");
					} else if (details.result != EResult.OK) {
						throw new KeyNotFoundException($"Failed to process collection: Steam replied with EResult.{details.result}");
					} else {
						// Good to go. Ignore all of the collections first...
						GD.Print("Got collections. Trimming the ones that have already been scanned, in case of recursion...");
						foreach (long collection in collectionsToRead.Span) {
							// ^ Must get the span again since spans can't cross await boundaries (and it won't raise a compiler error)
							ignoreCollectionIDs.Add(collection);
						}

						GD.Print("Reading entries...");
						List<long> childCollectionsToRead = [];
						foreach (CollectionDetailsEntry entry in details.collectionDetails) {
							foreach (CollectionDetailsEntryChild child in entry.children) {
								if (child.fileType == EWorkshopFileType.Community) {
									itemIDsOut?.Add(child.publishedFileID);
									GD.Print($"{child.publishedFileID} is a mod.");
								} else if (child.fileType == EWorkshopFileType.Collection) {
									GD.Print($"{child.publishedFileID} is a sub-collection.");
									if (!recursive) {
										collectionIDsOut?.Add(child.publishedFileID);
									} else {
										childCollectionsToRead.Add(child.publishedFileID);
									}
								}
							}
						}
						if (!recursive || childCollectionsToRead.Count == 0) return;
						await GetAllModsInCollectionAsync(childCollectionsToRead.ToArray().AsMemory(), ignoreCollectionIDs, itemIDsOut, collectionIDsOut, recursive, cancellationToken).ConfigureAwait(false);
						return;
					}

				} catch (HttpRequestException httpError) {
					if (httpError.StatusCode == HttpStatusCode.TooManyRequests) {
						// If we get rate limited, wait a while then try again.
						GD.Print("Steam rate limited us. Waiting a few seconds and trying again...");
						await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
						continue;

					} else if (httpError.StatusCode == HttpStatusCode.RequestHeaderFieldsTooLarge) {
						// If the request is too large for whatever reason, just split it in half and try again.

						GD.Print("Steam says we requested way too much at once. Splitting this request in half...");
						int halfLength = collectionsToRead.Length >>> 1;
						if ((collectionsToRead.Length & 1) != 0) halfLength++;
						if (halfLength == 0) throw new InvalidOperationException("Somehow the request was too large but there was literally only one field. This is a very strange and nonsensical error.");

						// No "halfLengthB" because we use a range expression that goes to the end of the array, knowing the length of the second segment is not useful.
						await GetAllModsInCollectionAsync(collectionsToRead[..halfLength], ignoreCollectionIDs, itemIDsOut, collectionIDsOut, recursive, cancellationToken).ConfigureAwait(false);
						await GetAllModsInCollectionAsync(collectionsToRead[halfLength..], ignoreCollectionIDs, itemIDsOut, collectionIDsOut, recursive, cancellationToken).ConfigureAwait(false);
						return;
					}
				}
			}
			throw new InvalidOperationException("Failed to get data about collection(s) after multiple tries.");
		}

		/// <summary>
		/// Reads the HTTP Response from a call to <see cref="STEAM_API_GET_COLLECTION_DETAILS"/> returns the resulting <see cref="CollectionDetails"/> directly.
		/// </summary>
		/// <param name="response">The http response from Steam.</param>
		/// <param name="cancellationToken">Used to cancel processing.</param>
		/// <returns></returns>
		/// <exception cref="OperationCanceledException"></exception>
		private static async Task<CollectionDetails> GetCollectionDetailsFromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken) {
			string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			Variant parsed = Json.ParseString(content);
			if (parsed.VariantType == Variant.Type.Dictionary) {
				GDDictionary json = (GDDictionary)parsed;
				json = (GDDictionary)json["response"];
				return new CollectionDetails(json);
			}
			return default;
		}

		#endregion

		#region Get Data of Workshop IDs

		/// <summary>
		/// Asks Steam to provide information about every file ID provided in <paramref name="workshopIDs"/>.
		/// </summary>
		/// <param name="workshopIDs"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<PublishedFileDetailsEntry[]> GetPublishedFileDetailsAsync(long[] workshopIDs, CancellationToken cancellationToken) {
			List<PublishedFileDetailsEntry> result = new List<PublishedFileDetailsEntry>(workshopIDs.Length);
			await GetPartialPublishedFileDetailsAsync(workshopIDs, result, cancellationToken);
			return result.ToArray();
		}

		/// <summary>
		/// To be used by <see cref="GetPublishedFileDetailsAsync(long[], CancellationToken)"/>. This contains a contingency for when there's so many IDs that Steam
		/// says the request is too large.
		/// </summary>
		/// <param name="workshopIDs"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task GetPartialPublishedFileDetailsAsync(ReadOnlyMemory<long> workshopIDs, List<PublishedFileDetailsEntry> result, CancellationToken cancellationToken) {
			cancellationToken.ThrowIfCancellationRequested();

			/*
			MultipartFormDataContent requestBody = new MultipartFormDataContent();

			ReadOnlySpan<long> entries = workshopIDs.Span;
			if (entries.Length == 0) return;
			for (int i = 0; i < workshopIDs.Length; i++) {
				long collection = entries[i];
				requestBody.Add(new StringContent(collection.ToString()), $"publishedfileids[{i}]");
			}
			requestBody.Add(new StringContent(workshopIDs.Length.ToString()), "itemcount");
			*/

			ReadOnlySpan<long> entries = workshopIDs.Span;
			if (entries.Length == 0) return;
			Dictionary<string, string> kvp = [];

			int index = 0;
			bool hasMagicID = false;
			for (int i = 0; i < workshopIDs.Length; i++) {
				long item = entries[i];
				if (item == long.MinValue) {
					hasMagicID = true;
					continue;
				}
				kvp[$"publishedfileids[{index++}]"] = item.ToString();
			}
			kvp["itemcount"] = index.ToString();

			FormUrlEncodedContent requestBody = new FormUrlEncodedContent(kvp);

			if (index == 0) {
				if (hasMagicID) {
					result.Add(PublishedFileDetailsEntry.CreateVirtualTestItem());
				}
				return;
			}

			int retries = 3;
			while (retries-- > 0) {
				try {
					GD.Print("Asking Steam for information about various workshop items...");
					HttpRequestMessage request = new HttpRequestMessage {
						Method = HttpMethod.Post,
						RequestUri = new Uri(STEAM_API_GET_PUBLISHED_FILE_DETAILS),
						Content = requestBody
					};
					HttpResponseMessage message = await SBModManagerGlobals.HTTP_CLIENT.SendAsync(request, cancellationToken).ConfigureAwait(false);
					PublishedFileDetails details = await GetFileDetailsFromResponseAsync(message, cancellationToken).ConfigureAwait(false);
					if (details.result == EResult.RateLimitExceeded) {
						throw new HttpRequestException($"EResult.{details.result}", null, HttpStatusCode.TooManyRequests);
					} else if (details.result == EResult.LimitExceeded) {
						throw new HttpRequestException($"EResult.{details.result}", null, HttpStatusCode.RequestHeaderFieldsTooLarge);
					} else if (details.result != EResult.OK) {
						throw new InvalidOperationException($"Failed to process items: Steam replied with EResult.{details.result}");
					} else {
						// Good to go.
						// List<long> childCollectionsToRead = [];
						foreach (PublishedFileDetailsEntry entry in details.publishedFileDetails) {
							result.Add(entry);
						}
						if (hasMagicID) {
							result.Add(PublishedFileDetailsEntry.CreateVirtualTestItem());
						}
						return;
					}

				} catch (HttpRequestException httpError) {
					if (httpError.StatusCode == HttpStatusCode.TooManyRequests) {
						// If we get rate limited, wait a while then try again.
						GD.Print("Steam rate limited us. Waiting a few seconds and trying again...");
						await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
						continue;

					} else if (httpError.StatusCode == HttpStatusCode.RequestHeaderFieldsTooLarge) {
						// If the request is too large for whatever reason, just split it in half and try again.

						int halfLength = workshopIDs.Length >>> 1;
						if ((workshopIDs.Length & 1) != 0) halfLength++;
						if (halfLength == 0) throw new InvalidOperationException("Somehow the request was too large but there was literally only one field. This is a very strange and nonsensical error.");

						GD.Print("Steam says we requested way too much at once. Splitting this request in half...");
						// No "halfLengthB" because we use a range expression that goes to the end of the array, knowing the length of the second segment is not useful.
						await GetPartialPublishedFileDetailsAsync(workshopIDs[..halfLength], result, cancellationToken);
						await GetPartialPublishedFileDetailsAsync(workshopIDs[halfLength..], result, cancellationToken);
						return;
					}
				}
			}
			throw new InvalidOperationException("Failed to get data about items(s) after multiple tries.");
		}

		/// <summary>
		/// Reads the HTTP Response from a call to <see cref="STEAM_API_GET_PUBLISHED_FILE_DETAILS"/> returns the resulting <see cref="PublishedFileDetails"/> directly.
		/// </summary>
		/// <param name="response">The http response from Steam.</param>
		/// <param name="cancellationToken">Used to cancel processing.</param>
		/// <returns></returns>
		/// <exception cref="OperationCanceledException"></exception>
		private static async Task<PublishedFileDetails> GetFileDetailsFromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken) {
			string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			Variant parsed = Json.ParseString(content);
			if (parsed.VariantType == Variant.Type.Dictionary) {
				GDDictionary json = (GDDictionary)parsed;
				json = (GDDictionary)json["response"];
				return new PublishedFileDetails(json);
			}
			return default;
		}

		#endregion

	}
}
